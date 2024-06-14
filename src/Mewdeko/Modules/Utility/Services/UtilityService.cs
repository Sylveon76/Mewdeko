﻿using System.Text.RegularExpressions;
using Mewdeko.Modules.Utility.Common;
using VirusTotalNet;
using VirusTotalNet.Results;

namespace Mewdeko.Modules.Utility.Services;

/// <summary>
/// Provides various utility functionalities including message sniping, link previews, reaction management, and URL checking.
/// </summary>
public partial class UtilityService : INService
{
    private readonly IDataCache cache;
    private readonly DiscordShardedClient client;
    private readonly DbService db;
    private readonly GuildSettingsService guildSettings;

    /// <summary>
    /// Initializes a new instance of the <see cref="UtilityService"/> class.
    /// </summary>
    /// <param name="db">The database service.</param>
    /// <param name="cache">The data cache service.</param>
    /// <param name="guildSettings">The guild settings service.</param>
    /// <param name="eventHandler">The event handler service.</param>
    /// <param name="client">The Discord client.</param>
    public UtilityService(
        DbService db,
        IDataCache cache,
        GuildSettingsService guildSettings,
        EventHandler eventHandler,
        DiscordShardedClient client)
    {
        eventHandler.MessageDeleted += MsgStore;
        eventHandler.MessageUpdated += MsgStore2;
        eventHandler.MessageReceived += MsgReciev;
        eventHandler.MessagesBulkDeleted += BulkMsgStore;
        this.db = db;
        this.cache = cache;
        this.guildSettings = guildSettings;
        this.client = client;
    }

    /// <summary>
    /// Retrieves sniped messages for a specific guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild to retrieve sniped messages for.</param>
    /// <returns>A task that represents the asynchronous operation, containing a list of sniped messages.</returns>
    public Task<List<SnipeStore>> GetSnipes(ulong guildId) =>
        cache.GetSnipesForGuild(guildId);

    /// <summary>
    /// Checks whether link previewing is enabled for a specific guild.
    /// </summary>
    /// <param name="id">The ID of the guild to check.</param>
    /// <returns>A task that represents the asynchronous operation, containing a boolean indicating if link previewing is enabled.</returns>
    public async Task<int> GetPLinks(ulong id) => (await guildSettings.GetGuildConfig(id)).PreviewLinks;

    /// <summary>
    /// Toggles link previewing for a specific guild.
    /// </summary>
    /// <param name="guild">The guild to toggle link previewing for.</param>
    /// <param name="yesnt">A string indicating whether to enable or disable link previewing.</param>
    public async Task PreviewLinks(IGuild guild, string yesnt)
    {
        var yesno = -1;
        await using (db.GetDbContext().ConfigureAwait(false))
        {
            yesno = yesnt switch
            {
                "y" => 1,
                "n" => 0,
                _ => yesno
            };
        }

        var uow = db.GetDbContext();
        await using (uow.ConfigureAwait(false))
        {
            var gc = await uow.ForGuildId(guild.Id, set => set);
            gc.PreviewLinks = yesno;
            await guildSettings.UpdateGuildConfig(guild.Id, gc);
        }
    }

    /// <summary>
    /// Retrieves the snipe set status for a specific guild.
    /// </summary>
    /// <param name="id">The ID of the guild to check.</param>
    /// <returns>A task that represents the asynchronous operation, containing a boolean indicating if snipe set is enabled.</returns>
    public async Task<bool> GetSnipeSet(ulong id) =>
        (await guildSettings.GetGuildConfig(id)).snipeset;

    /// <summary>
    /// Sets the snipe set status for a specific guild.
    /// </summary>
    /// <param name="guild">The guild to set the snipe set status for.</param>
    /// <param name="enabled">A boolean indicating whether to enable or disable snipe set.</param>
    public async Task SnipeSet(IGuild guild, bool enabled)
    {
        await using var uow = db.GetDbContext();
        var gc = await uow.ForGuildId(guild.Id, set => set);
        gc.snipeset = enabled; // Converting bool to long
        await uow.SaveChangesAsync().ConfigureAwait(false);
        await guildSettings.UpdateGuildConfig(guild.Id, gc);
    }


    private async Task BulkMsgStore(IReadOnlyCollection<Cacheable<IMessage, ulong>> messages,
        Cacheable<IMessageChannel, ulong> channel)
    {
        if (!channel.HasValue)
            return;

        if (channel.Value is not SocketTextChannel chan)
            return;

        if (!await GetSnipeSet(chan.Guild.Id))
            return;

        if (!messages.Select(x => x.HasValue).Any())
            return;


        var msgs = messages.Where(x => x.HasValue).Select(x => new SnipeStore
        {
            GuildId = chan.Guild.Id,
            ChannelId = chan.Id,
            ReferenceMessage =
                (x.Value as IUserMessage).ReferencedMessage == null
                    ? null
                    : $"{Format.Bold((x.Value as IUserMessage).ReferencedMessage.Author.ToString())}: {(x.Value as IUserMessage).ReferencedMessage.Content.TrimTo(400)}",
            Message = x.Value.Content,
            UserId = x.Value.Author.Id,
            Edited = false,
            DateAdded = DateTime.UtcNow
        });

        if (!msgs.Any())
            return;

        var snipes = await cache.GetSnipesForGuild(chan.Guild.Id).ConfigureAwait(false) ?? new List<SnipeStore>();
        if (snipes.Count == 0)
        {
            var todelete = snipes.Where(x => DateTime.UtcNow.Subtract(x.DateAdded) >= TimeSpan.FromDays(3));
            if (todelete.Any())
                snipes.RemoveRange(todelete);
        }

        snipes.AddRange(msgs);
        await cache.AddSnipeToCache(chan.Guild.Id, snipes).ConfigureAwait(false);
    }

    private async Task MsgStore(Cacheable<IMessage, ulong> optMsg, Cacheable<IMessageChannel, ulong> ch)
    {
        if (!ch.HasValue || ch.Value is not IGuildChannel channel)
            return;

        if (!await GetSnipeSet(channel.Guild.Id)) return;

        if ((optMsg.HasValue ? optMsg.Value : null) is not IUserMessage msg) return;
        if (msg.Author is null /*for some reason*/) return;
        var snipemsg = new SnipeStore
        {
            GuildId = channel.Guild.Id,
            ChannelId = channel.Id,
            Message = msg.Content,
            ReferenceMessage =
                msg.ReferencedMessage == null
                    ? null
                    : $"{Format.Bold(msg.ReferencedMessage.Author.ToString())}: {msg.ReferencedMessage.Content.TrimTo(400)}",
            UserId = msg.Author.Id,
            Edited = false,
            DateAdded = DateTime.UtcNow
        };
        var snipes = await cache.GetSnipesForGuild(channel.Guild.Id).ConfigureAwait(false) ?? new List<SnipeStore>();
        if (snipes.Count == 0)
        {
            var todelete = snipes.Where(x => DateTime.UtcNow.Subtract(x.DateAdded) >= TimeSpan.FromDays(3));
            if (todelete.Any())
                snipes.RemoveRange(todelete);
        }

        snipes.Add(snipemsg);
        await cache.AddSnipeToCache(channel.Guild.Id, snipes).ConfigureAwait(false);
    }

    private async Task MsgStore2(Cacheable<IMessage, ulong> optMsg, SocketMessage imsg2, ISocketMessageChannel ch)
    {
        if (ch is not IGuildChannel channel) return;


        if (!await GetSnipeSet(channel.Guild.Id)) return;

        if ((optMsg.HasValue ? optMsg.Value : null) is not IUserMessage msg) return;
        var snipemsg = new SnipeStore
        {
            GuildId = channel.GuildId,
            ChannelId = channel.Id,
            Message = msg.Content,
            ReferenceMessage =
                msg.ReferencedMessage == null
                    ? null
                    : $"{Format.Bold(msg.ReferencedMessage.Author.ToString())}: {msg.ReferencedMessage.Content.TrimTo(1048)}",
            UserId = msg.Author.Id,
            Edited = true,
            DateAdded = DateTime.UtcNow
        };
        var snipes = await cache.GetSnipesForGuild(channel.Guild.Id).ConfigureAwait(false) ?? new List<SnipeStore>();
        if (snipes.Count == 0)
        {
            var todelete = snipes.Where(x => DateTime.UtcNow.Subtract(x.DateAdded) >= TimeSpan.FromDays(3));
            if (todelete.Any())
                snipes.RemoveRange(todelete);
        }

        snipes.Add(snipemsg);
        await cache.AddSnipeToCache(channel.Guild.Id, snipes).ConfigureAwait(false);
    }

    /// <summary>
    /// Checks a URL for malware and other security threats.
    /// </summary>
    /// <param name="url">The URL to check.</param>
    /// <returns>A task that represents the asynchronous operation, containing a report on the URL's security status.</returns>
    public static Task<UrlReport> UrlChecker(string url)
    {
        var vcheck = new VirusTotal("e49046afa41fdf4e8ca72ea58a5542d0b8fbf72189d54726eed300d2afe5d9a9");
        return vcheck.GetUrlReportAsync(url, true);
    }

    private async Task MsgReciev(IMessage msg)
    {
        if (msg.Channel is SocketTextChannel t)
        {
            if (msg.Author.IsBot) return;
            var gid = t.Guild;
            if (await GetPLinks(gid.Id) == 1)
            {
                var linkParser = MyRegex();
                foreach (Match m in linkParser.Matches(msg.Content))
                {
                    var e = new Uri(m.Value);
                    var en = e.Host.Split(".");
                    if (!en.Contains("discord")) continue;
                    var eb = string.Join("", e.Segments).Split("/");
                    if (!eb.Contains("channels")) continue;
                    SocketGuild guild;
                    if (gid.Id != Convert.ToUInt64(eb[2]))
                    {
                        guild = client.GetGuild(Convert.ToUInt64(eb[2]));
                        if (guild is null) return;
                    }
                    else
                    {
                        guild = gid;
                    }

                    if (guild != t.Guild)
                        return;
                    var em = await ((IGuild)guild).GetTextChannelAsync(Convert.ToUInt64(eb[3])).ConfigureAwait(false);
                    if (em == null) return;
                    var msg2 = await em.GetMessageAsync(Convert.ToUInt64(eb[4])).ConfigureAwait(false);
                    if (msg2 is null) return;
                    var en2 = new EmbedBuilder
                    {
                        Color = Mewdeko.OkColor,
                        Author = new EmbedAuthorBuilder
                        {
                            Name = msg2.Author.Username, IconUrl = msg2.Author.GetAvatarUrl(size: 2048)
                        },
                        Footer = new EmbedFooterBuilder
                        {
                            IconUrl = ((IGuild)guild).IconUrl, Text = $"{((IGuild)guild).Name}: {em.Name}"
                        }
                    };
                    if (msg2.Embeds.Count > 0)
                    {
                        en2.AddField("Embed Content:", msg2.Embeds.FirstOrDefault()?.Description);
                        if (msg2.Embeds.FirstOrDefault()!.Image != null)
                        {
                            var embedImage = msg2.Embeds.FirstOrDefault()?.Image;
                            if (embedImage != null)
                                en2.ImageUrl = embedImage?.Url;
                        }
                    }

                    if (msg2.Content.Length > 0) en2.Description = msg2.Content;

                    if (msg2.Attachments.Count > 0) en2.ImageUrl = msg2.Attachments.FirstOrDefault().Url;

                    await msg.Channel.SendMessageAsync(embed: en2.WithTimestamp(msg2.Timestamp).Build())
                        .ConfigureAwait(false);
                }
            }
        }
    }

    [GeneratedRegex(
        @"https?:\/\/(www\.)?[-a-zA-Z0-9@:%._\+~#=]{1,256}\.[a-zA-Z0-9()]{1,6}\b([-a-zA-Z0-9()@:%_\+.~#?&//=]*)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
    private static partial Regex MyRegex();
}
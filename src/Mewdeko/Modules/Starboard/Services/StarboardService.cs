using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Database.Models;
using Mewdeko.Database.DbContextStuff;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Discord;
using Discord.WebSocket;

namespace Mewdeko.Modules.Starboard.Services;

/// <summary>
///     Service responsible for managing multiple starboards in Discord servers.
/// </summary>
public class StarboardService : INService, IReadyExecutor
{
    private readonly DiscordShardedClient client;
    private readonly DbContextProvider dbProvider;

    private List<StarboardPost> starboardPosts = [];
    private List<StarboardConfig> starboardConfigs = [];

    /// <summary>
    ///     Initializes a new instance of the <see cref="StarboardService" /> class.
    /// </summary>
    /// <param name="client">The Discord socket client.</param>
    /// <param name="dbProvider">The database context provider.</param>
    /// <param name="eventHandler">The event handler.</param>
    public StarboardService(DiscordShardedClient client, DbContextProvider dbProvider,
       EventHandler eventHandler)
    {
        this.client = client;
        this.dbProvider = dbProvider;
        eventHandler.ReactionAdded += OnReactionAddedAsync;
        eventHandler.MessageDeleted += OnMessageDeletedAsync;
        eventHandler.ReactionRemoved += OnReactionRemoveAsync;
        eventHandler.ReactionsCleared += OnAllReactionsClearedAsync;
    }

    /// <inheritdoc />
    public async Task OnReadyAsync()
    {
        Log.Information($"Starting {GetType()} Cache");
        await using var dbContext = await dbProvider.GetContextAsync();

        starboardPosts = await dbContext.StarboardPosts.ToListAsync();
        starboardConfigs = await dbContext.Starboards.ToListAsync();
        Log.Information("Starboard Cache Ready");
    }

    /// <summary>
    ///     Creates a new starboard configuration for a guild.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="channelId">The ID of the starboard channel.</param>
    /// <param name="emote">The emote to use for this starboard.</param>
    /// <param name="threshold">The number of reactions required.</param>
    /// <returns>The ID of the created starboard configuration.</returns>
    public async Task<int> CreateStarboard(IGuild guild, ulong channelId, string emote, int threshold)
    {
        await using var db = await dbProvider.GetContextAsync();
        var config = new StarboardConfig
        {
            GuildId = guild.Id,
            StarboardChannelId = channelId,
            Emote = emote,
            Threshold = threshold,
            CheckedChannels = "",
            UseBlacklist = false,
            AllowBots = false,
            RemoveOnDelete = true,
            RemoveOnReactionsClear = true,
            RemoveOnBelowThreshold = true,
            RepostThreshold = 0
        };

        db.Starboards.Add(config);
        await db.SaveChangesAsync();
        starboardConfigs.Add(config);
        return config.Id;
    }

    /// <summary>
    ///     Deletes a starboard configuration.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="starboardId">The ID of the starboard configuration.</param>
    /// <returns>True if the starboard was deleted, false otherwise.</returns>
    public async Task<bool> DeleteStarboard(IGuild guild, int starboardId)
    {
        var config = starboardConfigs.FirstOrDefault(x => x.Id == starboardId && x.GuildId == guild.Id);
        if (config == null)
            return false;

        await using var db = await dbProvider.GetContextAsync();
        db.Starboards.Remove(config);
        await db.SaveChangesAsync();
        starboardConfigs.Remove(config);
        return true;
    }

    /// <summary>
    ///     Gets all starboard configurations for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>A list of starboard configurations.</returns>
    public List<StarboardConfig> GetStarboards(ulong guildId)
        => starboardConfigs.Where(x => x.GuildId == guildId).ToList();

    /// <summary>
    ///     Sets whether bots are allowed to be starred for a specific starboard.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="starboardId">The ID of the starboard configuration.</param>
    /// <param name="allowed">Whether bots are allowed.</param>
    /// <returns>True if the setting was updated, false if the starboard wasn't found.</returns>
    public async Task<bool> SetAllowBots(IGuild guild, int starboardId, bool allowed)
    {
        var config = starboardConfigs.FirstOrDefault(x => x.Id == starboardId && x.GuildId == guild.Id);
        if (config == null)
            return false;

        await using var db = await dbProvider.GetContextAsync();
        config.AllowBots = allowed;
        db.Starboards.Update(config);
        await db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    ///     Sets whether to remove starred messages when the original is deleted.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="starboardId">The ID of the starboard configuration.</param>
    /// <param name="removeOnDelete">Whether to remove on delete.</param>
    /// <returns>True if the setting was updated, false if the starboard wasn't found.</returns>
    public async Task<bool> SetRemoveOnDelete(IGuild guild, int starboardId, bool removeOnDelete)
    {
        var config = starboardConfigs.FirstOrDefault(x => x.Id == starboardId && x.GuildId == guild.Id);
        if (config == null)
            return false;

        await using var db = await dbProvider.GetContextAsync();
        config.RemoveOnDelete = removeOnDelete;
        db.Starboards.Update(config);
        await db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    ///     Sets whether to remove starred messages when reactions are cleared.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="starboardId">The ID of the starboard configuration.</param>
    /// <param name="removeOnClear">Whether to remove on clear.</param>
    /// <returns>True if the setting was updated, false if the starboard wasn't found.</returns>
    public async Task<bool> SetRemoveOnClear(IGuild guild, int starboardId, bool removeOnClear)
    {
        var config = starboardConfigs.FirstOrDefault(x => x.Id == starboardId && x.GuildId == guild.Id);
        if (config == null)
            return false;

        await using var db = await dbProvider.GetContextAsync();
        config.RemoveOnReactionsClear = removeOnClear;
        db.Starboards.Update(config);
        await db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    ///     Sets whether to remove starred messages when they fall below threshold.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="starboardId">The ID of the starboard configuration.</param>
    /// <param name="removeBelowThreshold">Whether to remove below threshold.</param>
    /// <returns>True if the setting was updated, false if the starboard wasn't found.</returns>
    public async Task<bool> SetRemoveBelowThreshold(IGuild guild, int starboardId, bool removeBelowThreshold)
    {
        var config = starboardConfigs.FirstOrDefault(x => x.Id == starboardId && x.GuildId == guild.Id);
        if (config == null)
            return false;

        await using var db = await dbProvider.GetContextAsync();
        config.RemoveOnBelowThreshold = removeBelowThreshold;
        db.Starboards.Update(config);
        await db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    ///     Sets the repost threshold for a starboard.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="starboardId">The ID of the starboard configuration.</param>
    /// <param name="threshold">The threshold value.</param>
    /// <returns>True if the setting was updated, false if the starboard wasn't found.</returns>
    public async Task<bool> SetRepostThreshold(IGuild guild, int starboardId, int threshold)
    {
        var config = starboardConfigs.FirstOrDefault(x => x.Id == starboardId && x.GuildId == guild.Id);
        if (config == null)
            return false;

        await using var db = await dbProvider.GetContextAsync();
        config.RepostThreshold = threshold;
        db.Starboards.Update(config);
        await db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    ///     Sets the star threshold for a starboard.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="starboardId">The ID of the starboard configuration.</param>
    /// <param name="threshold">The threshold value.</param>
    /// <returns>True if the setting was updated, false if the starboard wasn't found.</returns>
    public async Task<bool> SetStarThreshold(IGuild guild, int starboardId, int threshold)
    {
        var config = starboardConfigs.FirstOrDefault(x => x.Id == starboardId && x.GuildId == guild.Id);
        if (config == null)
            return false;

        await using var db = await dbProvider.GetContextAsync();
        config.Threshold = threshold;
        db.Starboards.Update(config);
        await db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    ///     Sets whether to use blacklist mode for channel checking.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="starboardId">The ID of the starboard configuration.</param>
    /// <param name="useBlacklist">Whether to use blacklist mode.</param>
    /// <returns>True if the setting was updated, false if the starboard wasn't found.</returns>
    public async Task<bool> SetUseBlacklist(IGuild guild, int starboardId, bool useBlacklist)
    {
        var config = starboardConfigs.FirstOrDefault(x => x.Id == starboardId && x.GuildId == guild.Id);
        if (config == null)
            return false;

        await using var db = await dbProvider.GetContextAsync();
        config.UseBlacklist = useBlacklist;
        db.Starboards.Update(config);
        await db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    ///     Toggles a channel in the starboard's check list.
    /// </summary>
    /// <param name="guild">The guild.</param>
    /// <param name="starboardId">The ID of the starboard configuration.</param>
    /// <param name="channelId">The channel ID to toggle.</param>
    /// <returns>A tuple containing whether the channel was added and the starboard configuration.</returns>
    public async Task<(bool WasAdded, StarboardConfig Config)> ToggleChannel(IGuild guild, int starboardId, string channelId)
    {
        var config = starboardConfigs.FirstOrDefault(x => x.Id == starboardId && x.GuildId == guild.Id);
        if (config == null)
            return (false, null);

        var channels = config.CheckedChannels.Split(" ", StringSplitOptions.RemoveEmptyEntries).ToList();

        await using var db = await dbProvider.GetContextAsync();
        if (!channels.Contains(channelId))
        {
            channels.Add(channelId);
            config.CheckedChannels = string.Join(" ", channels);
            db.Starboards.Update(config);
            await db.SaveChangesAsync();
            return (true, config);
        }

        channels.Remove(channelId);
        config.CheckedChannels = string.Join(" ", channels);
        db.Starboards.Update(config);
        await db.SaveChangesAsync();
        return (false, config);
    }


    private async Task AddStarboardPost(ulong messageId, ulong postId, int starboardId)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        var post = starboardPosts.Find(x => x.MessageId == messageId && x.StarboardConfigId == starboardId);
        if (post == null)
        {
            var toAdd = new StarboardPost
            {
                MessageId = messageId,
                PostId = postId,
                StarboardConfigId = starboardId
            };
            starboardPosts.Add(toAdd);
            dbContext.StarboardPosts.Add(toAdd);
            await dbContext.SaveChangesAsync();
            return;
        }

        if (post.PostId == postId)
            return;

        starboardPosts.Remove(post);
        post.PostId = postId;
        dbContext.StarboardPosts.Update(post);
        starboardPosts.Add(post);
        await dbContext.SaveChangesAsync();
    }

    private async Task RemoveStarboardPost(ulong messageId, int starboardId)
    {
        var toRemove = starboardPosts.Find(x => x.MessageId == messageId && x.StarboardConfigId == starboardId);
        if (toRemove == null)
            return;

        await using var dbContext = await dbProvider.GetContextAsync();
        dbContext.StarboardPosts.Remove(toRemove);
        starboardPosts.Remove(toRemove);
        await dbContext.SaveChangesAsync();
    }

    private async Task OnReactionAddedAsync(Cacheable<IUserMessage, ulong> message,
        Cacheable<IMessageChannel, ulong> channel,
        SocketReaction reaction)
    {
        if (!reaction.User.IsSpecified
            || reaction.User.Value.IsBot
            || !channel.HasValue
            || channel.Value is not ITextChannel textChannel)
            return;

        var guildStarboards = GetStarboards(textChannel.GuildId);
        if (!guildStarboards.Any())
            return;

        foreach (var starboard in guildStarboards)
        {
            await HandleReactionChange(message, channel, reaction, starboard, true);
        }
    }

   private async Task OnReactionRemoveAsync(Cacheable<IUserMessage, ulong> message,
        Cacheable<IMessageChannel, ulong> channel,
        SocketReaction reaction)
    {
        if (!reaction.User.IsSpecified
            || reaction.User.Value.IsBot
            || !channel.HasValue
            || channel.Value is not ITextChannel textChannel)
            return;

        var guildStarboards = GetStarboards(textChannel.GuildId);
        if (!guildStarboards.Any())
            return;

        foreach (var starboard in guildStarboards)
        {
            await HandleReactionChange(message, channel, reaction, starboard, false);
        }
    }

    private async Task HandleReactionChange(Cacheable<IUserMessage, ulong> message,
        Cacheable<IMessageChannel, ulong> channel,
        SocketReaction reaction,
        StarboardConfig starboard,
        bool isAdd)
    {
        var textChannel = channel.Value as ITextChannel;
        var emote = starboard.Emote.ToIEmote();

        if (emote.Name == null || !Equals(reaction.Emote, emote))
            return;

        var starboardChannel = await textChannel.Guild.GetTextChannelAsync(starboard.StarboardChannelId);
        if (starboardChannel == null)
            return;

        IUserMessage newMessage;
        if (!message.HasValue)
            newMessage = await message.GetOrDownloadAsync();
        else
            newMessage = message.Value;

        if (newMessage == null)
            return;

        var gUser = await textChannel.Guild.GetUserAsync(client.CurrentUser.Id);
        var botPerms = gUser.GetPermissions(starboardChannel);

        if (!botPerms.Has(ChannelPermission.SendMessages))
            return;

        if (starboard.UseBlacklist)
        {
            if (!starboard.CheckedChannels.IsNullOrWhiteSpace() && starboard.CheckedChannels.Split(" ").Contains(newMessage.Channel.Id.ToString()))
                return;
        }
        else
        {
            if (!starboard.CheckedChannels.IsNullOrWhiteSpace() && !starboard.CheckedChannels.Split(" ").Contains(newMessage.Channel.ToString()))
                return;
        }

        string content;
        string imageurl;
        var component = new ComponentBuilder()
            .WithButton(url: newMessage.GetJumpUrl(), style: ButtonStyle.Link, label: "Jump To Message")
            .Build();

        if (newMessage.Author.IsBot)
        {
            if (!starboard.AllowBots)
                return;

            content = newMessage.Embeds.Count > 0
                ? newMessage.Embeds.Select(x => x.Description).FirstOrDefault()
                : newMessage.Content;
            imageurl = newMessage.Attachments.Count > 0
                ? newMessage.Attachments.FirstOrDefault().ProxyUrl
                : newMessage.Embeds?.Select(x => x.Image).FirstOrDefault()?.ProxyUrl;
        }
        else
        {
            content = newMessage.Content;
            imageurl = newMessage.Attachments?.FirstOrDefault()?.ProxyUrl;
        }

        if (content is null && imageurl is null)
            return;

        var emoteCount = await newMessage.GetReactionUsersAsync(emote, int.MaxValue).FlattenAsync();
        var count = emoteCount.Where(x => !x.IsBot);
        var enumerable = count as IUser[] ?? count.ToArray();
        var maybePost = starboardPosts.Find(x => x.MessageId == newMessage.Id && x.StarboardConfigId == starboard.Id);

        if (enumerable.Length < starboard.Threshold)
        {
            if (maybePost != null && starboard.RemoveOnBelowThreshold)
            {
                await RemoveStarboardPost(newMessage.Id, starboard.Id);
                try
                {
                    var post = await starboardChannel.GetMessageAsync(maybePost.PostId);
                    if (post != null)
                        await post.DeleteAsync();
                }
                catch
                {
                    // ignored
                }
            }
            return;
        }

        if (maybePost != null)
        {
            if (starboard.RepostThreshold > 0)
            {
                var messages = await starboardChannel.GetMessagesAsync(starboard.RepostThreshold).FlattenAsync();
                var post = messages.FirstOrDefault(x => x.Id == maybePost.PostId);

                if (post != null)
                {
                    var post2 = post as IUserMessage;
                    var eb1 = new EmbedBuilder()
                        .WithOkColor()
                        .WithAuthor(newMessage.Author)
                        .WithDescription(content)
                        .WithTimestamp(newMessage.Timestamp);

                    if (imageurl is not null)
                        eb1.WithImageUrl(imageurl);

                    await post2.ModifyAsync(x =>
                    {
                        x.Content = $"{emote} **{enumerable.Length}** {textChannel.Mention}";
                        x.Components = component;
                        x.Embed = eb1.Build();
                    });
                }
                else
                {
                    var tryGetOldPost = await starboardChannel.GetMessageAsync(maybePost.PostId);
                    if (tryGetOldPost != null)
                    {
                        try
                        {
                            await tryGetOldPost.DeleteAsync();
                        }
                        catch
                        {
                            // ignored
                        }
                    }

                    var eb2 = new EmbedBuilder()
                        .WithOkColor()
                        .WithAuthor(newMessage.Author)
                        .WithDescription(content)
                        .WithTimestamp(newMessage.Timestamp);

                    if (imageurl is not null)
                        eb2.WithImageUrl(imageurl);

                    var msg1 = await starboardChannel.SendMessageAsync(
                        $"{emote} **{enumerable.Length}** {textChannel.Mention}",
                        embed: eb2.Build(),
                        components: component);

                    await AddStarboardPost(message.Id, msg1.Id, starboard.Id);
                }
            }
            else
            {
                var tryGetOldPost = await starboardChannel.GetMessageAsync(maybePost.PostId);
                if (tryGetOldPost != null)
                {
                    var toModify = tryGetOldPost as IUserMessage;
                    var eb1 = new EmbedBuilder()
                        .WithOkColor()
                        .WithAuthor(newMessage.Author)
                        .WithDescription(content)
                        .WithTimestamp(newMessage.Timestamp);

                    if (imageurl is not null)
                        eb1.WithImageUrl(imageurl);

                    await toModify.ModifyAsync(x =>
                    {
                        x.Content = $"{emote} **{enumerable.Length}** {textChannel.Mention}";
                        x.Components = component;
                        x.Embed = eb1.Build();
                    });
                }
                else
                {
                    var eb2 = new EmbedBuilder()
                        .WithOkColor()
                        .WithAuthor(newMessage.Author)
                        .WithDescription(content)
                        .WithTimestamp(newMessage.Timestamp);

                    if (imageurl is not null)
                        eb2.WithImageUrl(imageurl);

                    var msg1 = await starboardChannel.SendMessageAsync(
                        $"{emote} **{enumerable.Length}** {textChannel.Mention}",
                        embed: eb2.Build(),
                        components: component);

                    await AddStarboardPost(message.Id, msg1.Id, starboard.Id);
                }
            }
        }
        else
        {
            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithAuthor(newMessage.Author)
                .WithDescription(content)
                .WithTimestamp(newMessage.Timestamp);

            if (imageurl is not null)
                eb.WithImageUrl(imageurl);

            var msg = await starboardChannel.SendMessageAsync(
                $"{emote} **{enumerable.Length}** {textChannel.Mention}",
                embed: eb.Build(),
                components: component);

            await AddStarboardPost(message.Id, msg.Id, starboard.Id);
        }
    }

    private async Task OnMessageDeletedAsync(Cacheable<IMessage, ulong> arg1, Cacheable<IMessageChannel, ulong> arg2)
    {
        if (!arg1.HasValue || !arg2.HasValue)
            return;

        var msg = arg1.Value;
        var chan = arg2.Value;
        if (chan is not ITextChannel channel)
            return;

        var permissions = (await channel.Guild.GetUserAsync(client.CurrentUser.Id)).GetPermissions(channel);
        if (!permissions.ManageMessages)
            return;

        var posts = starboardPosts.Where(x => x.MessageId == msg.Id);
        foreach (var post in posts)
        {
            var config = starboardConfigs.FirstOrDefault(x => x.Id == post.StarboardConfigId);
            if (config?.RemoveOnDelete != true)
                continue;

            var starboardChannel = await channel.Guild.GetTextChannelAsync(config.StarboardChannelId);
            if (starboardChannel == null)
                continue;

            try
            {
                var starboardMessage = await starboardChannel.GetMessageAsync(post.PostId);
                if (starboardMessage != null)
                    await starboardMessage.DeleteAsync();
            }
            catch
            {
                // ignored
            }

            await RemoveStarboardPost(msg.Id, config.Id);
        }
    }

    private async Task OnAllReactionsClearedAsync(Cacheable<IUserMessage, ulong> arg1, Cacheable<IMessageChannel, ulong> arg2)
    {
        if (!arg2.HasValue || arg2.Value is not ITextChannel channel)
            return;

        IUserMessage msg;
        if (!arg1.HasValue)
            msg = await arg1.GetOrDownloadAsync();
        else
            msg = arg1.Value;

        if (msg == null)
            return;

        var posts = starboardPosts.Where(x => x.MessageId == msg.Id);
        foreach (var post in posts)
        {
            var config = starboardConfigs.FirstOrDefault(x => x.Id == post.StarboardConfigId);
            if (config?.RemoveOnReactionsClear != true)
                continue;

            var starboardChannel = await channel.Guild.GetTextChannelAsync(config.StarboardChannelId);
            if (starboardChannel == null)
                continue;

            try
            {
                var starboardMessage = await starboardChannel.GetMessageAsync(post.PostId);
                if (starboardMessage != null)
                    await starboardMessage.DeleteAsync();
            }
            catch
            {
                // ignored
            }

            await RemoveStarboardPost(msg.Id, config.Id);
        }
    }
}
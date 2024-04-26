﻿using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.Giveaways.Services;
using SkiaSharp;

namespace Mewdeko.Modules.Giveaways;

/// <summary>
/// Slash commands for giveaways.
/// </summary>
/// <param name="db">The database service</param>
/// <param name="interactiveService">The service used to make paginated embeds</param>
/// <param name="guildSettings">Service for getting guild configs</param>
[Group("giveaways", "Create or manage giveaways!")]
public class SlashGiveaways(DbService db, InteractiveService interactiveService, GuildSettingsService guildSettings)
    : MewdekoSlashModuleBase<GiveawayService>
{
    /// <summary>
    /// Sets the giveaway emote
    /// </summary>
    /// <param name="maybeEmote">The emote to set</param>
    [SlashCommand("emote", "Set the giveaway emote!"), SlashUserPerm(GuildPermission.ManageMessages), CheckPermissions]
    public async Task GEmote(string maybeEmote)
    {
        await DeferAsync().ConfigureAwait(false);
        var emote = maybeEmote.ToIEmote();
        if (emote.Name == null)
        {
            await ctx.Interaction.SendErrorFollowupAsync("That emote is invalid!", Config).ConfigureAwait(false);
            return;
        }

        try
        {
            var message = await ctx.Interaction.SendConfirmFollowupAsync("Checking emote...").ConfigureAwait(false);
            await message.AddReactionAsync(emote).ConfigureAwait(false);
        }
        catch
        {
            await ctx.Interaction.SendErrorFollowupAsync(
                    "I'm unable to use that emote for giveaways! Most likely because I'm not in a server with it.",
                    Config)
                .ConfigureAwait(false);
            return;
        }

        await Service.SetGiveawayEmote(ctx.Guild, emote.ToString()).ConfigureAwait(false);
        await ctx.Interaction.SendConfirmFollowupAsync(
                $"Giveaway emote set to {emote}! Just keep in mind this doesn't update until the next giveaway.")
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Sets the giveaway banner
    /// </summary>
    /// <param name="banner">The url of the banner to set</param>
    [SlashCommand("banner", "Allows you to set a banner for giveaways!"), SlashUserPerm(GuildPermission.ManageMessages)]
    public async Task GBanner(string banner)
    {
        var gc = await guildSettings.GetGuildConfig(Context.Guild.Id);
        if (!Uri.IsWellFormedUriString(banner, UriKind.Absolute) && banner != "none")
        {
            await ctx.Interaction.SendErrorAsync("That's not a valid URL!", Config).ConfigureAwait(false);
            return;
        }

        gc.GiveawayBanner = banner == "none" ? "" : banner;
        await guildSettings.UpdateGuildConfig(Context.Guild.Id, gc);
        if (banner == "none")
            await ctx.Interaction.SendConfirmAsync("Giveaway banner removed!").ConfigureAwait(false);
        else
            await ctx.Interaction.SendConfirmAsync("Giveaway banner set!").ConfigureAwait(false);
    }

    /// <summary>
    /// Sets the giveaway embed color for winning users
    /// </summary>
    /// <param name="color">The color in hex</param>
    [SlashCommand("winembedcolor", "Allows you to set the win embed color!"),
     SlashUserPerm(GuildPermission.ManageMessages)]
    public async Task GWinEmbedColor(string color)
    {
        var colorVal = StringExtensions.GetHexFromColorName(color);
        if (color.StartsWith("#"))
        {
            if (SKColor.TryParse(color, out _))
                colorVal = color;
        }

        if (colorVal is not null)
        {
            var gc = await guildSettings.GetGuildConfig(Context.Guild.Id);
            gc.GiveawayEmbedColor = colorVal;
            await guildSettings.UpdateGuildConfig(Context.Guild.Id, gc);
            await ctx.Interaction.SendConfirmAsync(
                    "Giveaway win embed color set! Just keep in mind this doesn't update until the next giveaway.")
                .ConfigureAwait(false);
        }
        else
        {
            await ctx.Interaction
                .SendErrorAsync(
                    "That's not a valid color! Please use proper hex (starts with #) or use html color names!", Config)
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Sets the giveaway embed color
    /// </summary>
    /// <param name="color">The color in hex</param>
    [SlashCommand("embedcolor", "Allows you to set the regular embed color!"),
     SlashUserPerm(GuildPermission.ManageMessages)]
    public async Task GEmbedColor(string color)
    {
        var colorVal = StringExtensions.GetHexFromColorName(color);
        if (color.StartsWith("#"))
        {
            if (SKColor.TryParse(color, out _))
                colorVal = color;
        }

        if (colorVal is not null)
        {
            var gc = await guildSettings.GetGuildConfig(Context.Guild.Id);
            gc.GiveawayEmbedColor = colorVal;
            await guildSettings.UpdateGuildConfig(Context.Guild.Id, gc);
            await ctx.Interaction.SendConfirmAsync(
                    "Giveaway embed color set! Just keep in mind this doesn't update until the next giveaway.")
                .ConfigureAwait(false);
        }
        else
        {
            await ctx.Interaction
                .SendErrorAsync(
                    "That's not a valid color! Please use proper hex (starts with #) or use html color names!", Config)
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Toggles whether winners get dmed
    /// </summary>
    [SlashCommand("dm", "Toggles whether winners get dmed!"), SlashUserPerm(GuildPermission.ManageMessages)]
    public async Task GDm()
    {
        var gc = await guildSettings.GetGuildConfig(Context.Guild.Id);
        gc.DmOnGiveawayWin = !gc.DmOnGiveawayWin;
        await guildSettings.UpdateGuildConfig(Context.Guild.Id, gc);
        await ctx.Interaction.SendConfirmAsync(
                $"Giveaway DMs set to {gc.DmOnGiveawayWin}! Just keep in mind this doesn't update until the next giveaway.")
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Rerolls a giveaway
    /// </summary>
    /// <param name="messageid">The messageid of the giveaway to reroll</param>
    [SlashCommand("reroll", "Rerolls a giveaway!"), SlashUserPerm(GuildPermission.ManageMessages), CheckPermissions]
    public async Task GReroll(ulong messageid)
    {
        await using var uow = db.GetDbContext();
        var gway = uow.Giveaways
            .GiveawaysForGuild(ctx.Guild.Id).ToList().Find(x => x.MessageId == messageid);
        if (gway is null)
        {
            await ctx.Interaction.SendErrorAsync("No Giveaway with that message ID exists! Please try again!", Config)
                .ConfigureAwait(false);
            return;
        }

        if (gway.Ended != 1)
        {
            await ctx.Interaction.SendErrorAsync("This giveaway hasn't ended yet!", Config).ConfigureAwait(false);
            return;
        }

        await Service.GiveawayTimerAction(gway).ConfigureAwait(false);
        await ctx.Interaction.SendConfirmAsync("Giveaway Rerolled!").ConfigureAwait(false);
    }

    /// <summary>
    /// View giveaway stats
    /// </summary>
    [SlashCommand("stats", "View giveaway stats!"), CheckPermissions]
    public async Task GStats()
    {
        var eb = new EmbedBuilder().WithOkColor();
        var gways = db.GetDbContext().Giveaways.GiveawaysForGuild(ctx.Guild.Id);
        if (gways.Count == 0)
        {
            await ctx.Channel.SendErrorAsync("There have been no giveaways here, so no stats!", Config)
                .ConfigureAwait(false);
        }
        else
        {
            List<ITextChannel> gchans = new();
            foreach (var i in gways)
            {
                var chan = await ctx.Guild.GetTextChannelAsync(i.ChannelId).ConfigureAwait(false);
                if (!gchans.Contains(chan))
                    gchans.Add(chan);
            }

            var amount = gways.Distinct(x => x.UserId).Count();
            eb.WithTitle("Giveaway Statistics!");
            eb.AddField("Amount of users that started giveaways", amount, true);
            eb.AddField("Total amount of giveaways", gways.Count, true);
            eb.AddField("Active Giveaways", gways.Count(x => x.Ended == 0), true);
            eb.AddField("Ended Giveaways", gways.Count(x => x.Ended == 1), true);
            eb.AddField("Giveaway Channels: Uses",
                string.Join("\n", gchans.Select(x => $"{x.Mention}: {gways.Count(s => s.ChannelId == x.Id)}")),
                true);

            await ctx.Interaction.RespondAsync(embed: eb.Build()).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Starts a giveaway faster than just .gstart
    /// </summary>
    /// <param name="chan">The channel to start the giveaway in</param>
    /// <param name="time">The amount of time the giveaway should last</param>
    /// <param name="winners">The number of winners</param>
    /// <param name="what">The item being given away</param>
    /// <param name="pingRole">The role to ping when starting the giveaway</param>
    /// <param name="attachment">The banner to use for the giveaway</param>
    /// <param name="host">The host of the giveaway</param>
    [SlashCommand("start", "Start a giveaway!"), SlashUserPerm(GuildPermission.ManageMessages), CheckPermissions]
    public async Task GStart(ITextChannel chan, TimeSpan time, int winners, string what, IRole pingRole = null,
        IAttachment attachment = null, IUser host = null)
    {
        host ??= ctx.User;
        await ctx.Interaction.DeferAsync().ConfigureAwait(false);
        var emote = (await Service.GetGiveawayEmote(ctx.Guild.Id)).ToIEmote();
        try
        {
            var message = await ctx.Interaction.SendConfirmFollowupAsync("Checking emote...").ConfigureAwait(false);
            await message.AddReactionAsync(emote).ConfigureAwait(false);
        }
        catch
        {
            await ctx.Interaction.SendErrorFollowupAsync(
                    "I'm unable to use that emote for giveaways! Most likely because I'm not in a server with it.",
                    Config)
                .ConfigureAwait(false);
            return;
        }

        var user = await ctx.Guild.GetUserAsync(ctx.Client.CurrentUser.Id).ConfigureAwait(false);
        var perms = user.GetPermissions(chan);
        if (!perms.Has(ChannelPermission.AddReactions))
        {
            await ctx.Interaction.SendErrorFollowupAsync("I cannot add reactions in that channel!", Config)
                .ConfigureAwait(false);
            return;
        }

        if (!perms.Has(ChannelPermission.UseExternalEmojis) && !ctx.Guild.Emotes.Contains(emote))
        {
            await ctx.Interaction.SendErrorFollowupAsync("I'm unable to use external emotes!", Config)
                .ConfigureAwait(false);
            return;
        }

        await Service.GiveawaysInternal(chan, time, what, winners, host.Id, ctx.Guild.Id,
            ctx.Channel as ITextChannel, ctx.Guild, banner: attachment?.Url, pingROle: pingRole).ConfigureAwait(false);
    }

    /// <summary>
    /// View current giveaways
    /// </summary>
    [SlashCommand("list", "View current giveaways!"), SlashUserPerm(GuildPermission.ManageMessages), CheckPermissions]
    public async Task GList()
    {
        await using var uow = db.GetDbContext();
        var gways = uow.Giveaways.GiveawaysForGuild(ctx.Guild.Id).Where(x => x.Ended == 0);
        if (!gways.Any())
        {
            await ctx.Channel.SendErrorAsync("No active giveaways", Config).ConfigureAwait(false);
            return;
        }

        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(gways.Count() / 5)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await interactiveService.SendPaginatorAsync(paginator, Context.Channel,
            TimeSpan.FromMinutes(60)).ConfigureAwait(false);

        async Task<PageBuilder> PageFactory(int page)
        {
            return new PageBuilder().WithOkColor().WithTitle($"{gways.Count()} Active Giveaways")
                .WithDescription(string.Join("\n\n",
                    await gways.Skip(page * 5).Take(5).Select(async x =>
                            $"{x.MessageId}\nPrize: {x.Item}\nWinners: {x.Winners}\nLink: {await GetJumpUrl(x.ChannelId, x.MessageId).ConfigureAwait(false)}")
                        .GetResults()
                        .ConfigureAwait(false)));
        }
    }

    private async Task<string> GetJumpUrl(ulong channelId, ulong messageId)
    {
        var channel = await ctx.Guild.GetTextChannelAsync(channelId).ConfigureAwait(false);
        var message = await channel.GetMessageAsync(messageId).ConfigureAwait(false);
        return message.GetJumpUrl();
    }

    /// <summary>
    /// End a giveaway
    /// </summary>
    /// <param name="messageid"></param>
    [SlashCommand("end", "End a giveaway!"), RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.ManageMessages), CheckPermissions]
    public async Task GEnd(ulong messageid)
    {
        await using var uow = db.GetDbContext();
        var gway = uow.Giveaways
            .GiveawaysForGuild(ctx.Guild.Id).ToList().Find(x => x.MessageId == messageid);
        if (gway is null)
        {
            await ctx.Channel.SendErrorAsync("No Giveaway with that message ID exists! Please try again!", Config)
                .ConfigureAwait(false);
        }

        if (gway.Ended == 1)
        {
            await ctx.Channel.SendErrorAsync(
                    $"This giveaway has already ended! Plase use `{await guildSettings.GetPrefix(ctx.Guild)}greroll {messageid}` to reroll!",
                    Config)
                .ConfigureAwait(false);
        }
        else
        {
            await Service.GiveawayTimerAction(gway).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync("Giveaway ended!").ConfigureAwait(false);
        }
    }
}
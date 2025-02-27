using Discord.Interactions;
using Mewdeko.Common.Configs;
using Mewdeko.Services.Strings;
using Microsoft.Extensions.DependencyInjection;

// ReSharper disable NotNullOrRequiredMemberIsNotInitialized

namespace Mewdeko.Common;

/// <summary>
///     Base class for slash command modules in Mewdeko.
/// </summary>
public abstract class MewdekoSlashCommandModule : InteractionModuleBase
{
    /// <summary>
    ///     The bot strings service for localization.
    /// </summary>
    public GeneratedBotStrings Strings { get; set; }

    /// <summary>
    ///     The command handler service.
    /// </summary>
    public CommandHandler? CmdHandler { get; set; }

    /// <summary>
    ///     The bot configuration.
    /// </summary>
    public BotConfig Config { get; set; }

    // ReSharper disable once InconsistentNaming
    /// <summary>
    ///     Gets the interaction context.
    /// </summary>
    protected IInteractionContext ctx
    {
        get
        {
            return Context;
        }
    }


    /// <summary>
    ///     Sends an error message based on the specified key with optional arguments.
    /// </summary>
    public Task ErrorAsync(string? text)
    {
        return !ctx.Interaction.HasResponded
            ? ctx.Interaction.SendErrorAsync(text, Config)
            : ctx.Interaction.SendErrorFollowupAsync(text, Config);
    }

    /// <summary>
    ///     Sends an error message as a reply to the user with the specified key and optional arguments.
    /// </summary>
    public Task ReplyErrorAsync(string? text)
    {
        return !ctx.Interaction.HasResponded
            ? ctx.Interaction.SendErrorAsync($"{Format.Bold(ctx.User.ToString())} {text}", Config)
            : ctx.Interaction.SendErrorFollowupAsync($"{Format.Bold(ctx.User.ToString())} {text}", Config);
    }

    /// <summary>
    ///     Sends an ephemeral follow-up error message asynchronously.
    /// </summary>
    /// <param name="message">Message to include in the error.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    public Task<IUserMessage> SendEphemeralFollowupErrorAsync(
        string message)
    {
        return ctx.Interaction.FollowupAsync(embed: new EmbedBuilder().WithErrorColor().WithDescription(message).Build(),
            ephemeral: true, components: Config.ShowInviteButton
                ? new ComponentBuilder()
                    .WithButton("Support Server", style: ButtonStyle.Link, url: "https://discord.gg/mewdeko")
                    .WithButton("Support Us!", style: ButtonStyle.Link, url: "https://ko-fi.com/mewdeko")
                    .Build()
                : null);
    }
    /// <summary>
    ///     Sends an ephemeral error message as a reply to the user with the specified key and optional arguments.
    /// </summary>
    public Task EphemeralReplyErrorAsync(string? text)
    {
        return !ctx.Interaction.HasResponded
            ? ctx.Interaction.SendEphemeralFollowupErrorAsync($"{Format.Bold(ctx.User.ToString())} {text}", Config)
            : ctx.Interaction.SendEphemeralErrorAsync($"{Format.Bold(ctx.User.ToString())} {text}", Config);
    }

    /// <summary>
    ///     Sends a confirmation message based on the specified key with optional arguments.
    /// </summary>
    public Task ConfirmAsync(string? text)
    {
        return !ctx.Interaction.HasResponded
            ? ctx.Interaction.SendConfirmAsync(text)
            : ctx.Interaction.SendConfirmFollowupAsync(text);
    }

    /// <summary>
    ///     Sends a confirmation message as a reply to the user with the specified key and optional arguments.
    /// </summary>
    public Task ReplyConfirmAsync(string? text)
    {
        return ctx.Interaction.HasResponded
            ? ctx.Interaction.SendConfirmFollowupAsync($"{Format.Bold(ctx.User.ToString())} {text}")
            : ctx.Interaction.SendConfirmAsync($"{Format.Bold(ctx.User.ToString())} {text}");
    }

    /// <summary>
    ///     Sends an ephemeral confirmation message as a reply to the user with the specified key and optional arguments.
    /// </summary>
    public Task EphemeralReplyConfirmAsync(string? text)
    {
        return !ctx.Interaction.HasResponded
            ? ctx.Interaction.SendEphemeralConfirmAsync($"{Format.Bold(ctx.User.ToString())} {text}")
            : ctx.Interaction.SendEphemeralFollowupConfirmAsync($"{Format.Bold(ctx.User.ToString())} {text}");
    }

    /// <summary>
    ///     Prompts the user to confirm an action with the specified text and user ID.
    /// </summary>
    public Task<bool> PromptUserConfirmAsync(string text, ulong uid, bool ephemeral = false, bool delete = true)
    {
        return PromptUserConfirmAsync(new EmbedBuilder().WithOkColor().WithDescription(text), uid, ephemeral, delete);
    }

    /// <summary>
    ///     Prompts the user to confirm an action with the specified embed, user ID, ephemeral status, and delete option.
    /// </summary>
    public async Task<bool> PromptUserConfirmAsync(EmbedBuilder embed, ulong userid, bool ephemeral = false,
        bool delete = true)
    {
        embed.WithOkColor();
        var buttons = new ComponentBuilder().WithButton("Yes", "yes", ButtonStyle.Success)
            .WithButton("No", "no", ButtonStyle.Danger);
        if (!ctx.Interaction.HasResponded) await ctx.Interaction.DeferAsync(ephemeral).ConfigureAwait(false);
        var msg = await ctx.Interaction
            .FollowupAsync(embed: embed.Build(), components: buttons.Build(), ephemeral: ephemeral)
            .ConfigureAwait(false);
        try
        {
            var input = await GetButtonInputAsync(msg.Channel.Id, msg.Id, userid).ConfigureAwait(false);
            return input == "Yes";
        }
        finally
        {
            if (delete)
                _ = Task.Run(() => msg.DeleteAsync());
        }
    }

    /// <summary>
    ///     Checks the hierarchy of roles between the current user and the target user.
    /// </summary>
    public async Task<bool> CheckRoleHierarchy(IGuildUser target, bool displayError = true)
    {
        var curUser = await ctx.Guild.GetCurrentUserAsync().ConfigureAwait(false);
        var ownerId = Context.Guild.OwnerId;
        var botMaxRole = curUser.GetRoles().Max(r => r.Position);
        var targetMaxRole = target.GetRoles().Max(r => r.Position);
        var modMaxRole = ((IGuildUser)ctx.User).GetRoles().Max(r => r.Position);

        var hierarchyCheck = ctx.User.Id == ownerId
            ? botMaxRole > targetMaxRole
            : botMaxRole >= targetMaxRole && modMaxRole > targetMaxRole;

        if (!hierarchyCheck && displayError)
            await ReplyErrorAsync("hierarchy").ConfigureAwait(false);

        return hierarchyCheck;
    }


    /// <summary>
    ///     Gets the user's input from a button interaction.
    /// </summary>
    /// <param name="channelId">The channel ID to bind to</param>
    /// <param name="msgId">The message ID to bind to</param>
    /// <param name="userId">The user ID to bind to</param>
    /// <param name="alreadyDeferred">Whether the interaction was already responded to.</param>
    /// <returns></returns>
    public async Task<string>? GetButtonInputAsync(ulong channelId, ulong msgId, ulong userId,
        bool alreadyDeferred = false)
    {
        var userInputTask = new TaskCompletionSource<string>();
        var dsc = CmdHandler.Services.GetRequiredService<DiscordShardedClient>();
        var handler = new EventHandler(dsc);
        try
        {
            handler.InteractionCreated += Interaction;
            if (await Task.WhenAny(userInputTask.Task, Task.Delay(30000)).ConfigureAwait(false) !=
                userInputTask.Task)
            {
                return null;
            }

            return await userInputTask.Task.ConfigureAwait(false);
        }
        finally
        {
            handler.InteractionCreated -= Interaction;
        }

        async Task Interaction(SocketInteraction arg)
        {
            if (arg is not SocketMessageComponent c) return;
            if (c.Channel.Id != channelId || c.Message.Id != msgId || c.User.Id != userId)
            {
                if (!alreadyDeferred) await c.DeferAsync();
                return;
            }

            if (c.Data.CustomId == "yes")
            {
                if (!alreadyDeferred) await c.DeferAsync();
                userInputTask.TrySetResult("Yes");
                return;
            }

            if (!alreadyDeferred) await c.DeferAsync();
            userInputTask.TrySetResult(c.Data.CustomId);
        }
    }

    /// <summary>
    ///     Gets the user's input from a message.
    /// </summary>
    /// <param name="channelId">The channel ID to bind to.</param>
    /// <param name="userId">The user ID to bind to.</param>
    /// <returns></returns>
    public async Task<string>? NextMessageAsync(ulong channelId, ulong userId)
    {
        var userInputTask = new TaskCompletionSource<string>();
        var dsc = CmdHandler.Services.GetRequiredService<DiscordShardedClient>();
        var handler = new EventHandler(dsc);
        try
        {
            handler.MessageReceived += Interaction;
            if (await Task.WhenAny(userInputTask.Task, Task.Delay(60000)).ConfigureAwait(false) !=
                userInputTask.Task)
            {
                return null;
            }

            return await userInputTask.Task.ConfigureAwait(false);
        }
        finally
        {
            dsc.MessageReceived -= Interaction;
        }

        async Task Interaction(SocketMessage arg)
        {
                if (arg.Author.Id != userId || arg.Channel.Id != channelId) return;
                userInputTask.TrySetResult(arg.Content);
                try
                {
                    await arg.DeleteAsync();
                }
                catch
                {
                    //Exclude
                }

        }
    }
}

/// <summary>
///     Base class for generic slash command modules in Mewdeko.
/// </summary>
public abstract class MewdekoSlashModuleBase<TService> : MewdekoSlashCommandModule
{
    /// <summary>
    ///     The service associated with the module.
    /// </summary>
    public TService Service { get; set; }
}

/// <summary>
///     Base class for generic slash submodule in Mewdeko.
/// </summary>
public abstract class MewdekoSlashSubmodule : MewdekoSlashCommandModule;

/// <summary>
///     Base class for generic slash submodule with a service in Mewdeko.
/// </summary>
public abstract class MewdekoSlashSubmodule<TService> : MewdekoSlashModuleBase<TService>;
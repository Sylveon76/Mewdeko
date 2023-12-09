using System.Globalization;
using Discord.Commands;
using Mewdeko.Services.strings;

namespace Mewdeko.Common;

public abstract class MewdekoModule : ModuleBase
{
    protected CultureInfo? CultureInfo { get; set; }
    public IBotStrings Strings { get; set; }
    public ILocalization Localization { get; set; }

    // ReSharper disable once InconsistentNaming
    protected ICommandContext ctx => Context;

    protected override void BeforeExecute(CommandInfo cmd) => CultureInfo = Localization.GetCultureInfo(ctx.Guild?.Id);

    protected string? GetText(string? key) => Strings.GetText(key, CultureInfo);

    protected string? GetText(string? key, params object?[] args) => Strings.GetText(key, CultureInfo, args);

    public Task<IUserMessage> ErrorLocalizedAsync(string? textKey, params object?[] args)
    {
        var text = GetText(textKey, args);
        return ctx.Channel.SendErrorAsync(text);
    }

    public Task<IUserMessage> ReplyErrorLocalizedAsync(string? textKey, params object?[] args)
    {
        var text = GetText(textKey, args);
        return ctx.Channel.SendErrorAsync($"{Format.Bold(ctx.User.ToString())} {text}");
    }

    public Task<IUserMessage> ConfirmLocalizedAsync(string? textKey, params object?[] args)
    {
        var text = GetText(textKey, args);
        return ctx.Channel.SendConfirmAsync(text);
    }

    public Task<IUserMessage> ReplyConfirmLocalizedAsync(string? textKey, params object?[] args)
    {
        var text = GetText(textKey, args);
        return ctx.Channel.SendConfirmAsync($"{Format.Bold(ctx.User.ToString())} {text}");
    }

    public Task<bool> PromptUserConfirmAsync(string message, ulong userid)
        => PromptUserConfirmAsync(new EmbedBuilder().WithOkColor().WithDescription(message), userid);

    public async Task<bool> PromptUserConfirmAsync(EmbedBuilder embed, ulong userid)
    {
        embed.WithOkColor();
        var buttons = new ComponentBuilder().WithButton("Yes", "yes", ButtonStyle.Success)
            .WithButton("No", "no", ButtonStyle.Danger);
        var msg = await ctx.Channel.SendMessageAsync(embed: embed.Build(), components: buttons.Build())
            .ConfigureAwait(false);
        try
        {
            var input = await GetButtonInputAsync(msg.Channel.Id, msg.Id, userid).ConfigureAwait(false);

            return input == "Yes";
        }
        finally
        {
            _ = Task.Run(() => msg.DeleteAsync());
        }
    }

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
            await ReplyErrorLocalizedAsync("hierarchy").ConfigureAwait(false);

        return hierarchyCheck;
    }

    public async Task<bool> PromptUserConfirmAsync(IUserMessage message, EmbedBuilder embed, ulong userid)
    {
        embed.WithOkColor();
        var buttons = new ComponentBuilder().WithButton("Yes", "yes", ButtonStyle.Success)
            .WithButton("No", "no", ButtonStyle.Danger);
        await message.ModifyAsync(x =>
        {
            x.Embed = embed.Build();
            x.Components = buttons.Build();
        }).ConfigureAwait(false);
        var input = await GetButtonInputAsync(message.Channel.Id, message.Id, userid).ConfigureAwait(false);

        return input == "Yes";
    }

    public async Task<string>? GetButtonInputAsync(ulong channelId, ulong msgId, ulong userId,
        bool alreadyDeferred = false)
    {
        var userInputTask = new TaskCompletionSource<string>();
        var dsc = (DiscordSocketClient)ctx.Client;
        try
        {
            dsc.InteractionCreated += Interaction;
            if (await Task.WhenAny(userInputTask.Task, Task.Delay(30000)).ConfigureAwait(false) !=
                userInputTask.Task)
            {
                return null;
            }

            return await userInputTask.Task.ConfigureAwait(false);
        }
        finally
        {
            dsc.InteractionCreated -= Interaction;
        }

        async Task Interaction(SocketInteraction arg)
        {
            if (arg is SocketMessageComponent c)
            {
                await Task.Run(async () =>
                {
                    if (c.Channel.Id != channelId || c.Message.Id != msgId || c.User.Id != userId)
                    {
                        if (!alreadyDeferred) await c.DeferAsync().ConfigureAwait(false);
                        return Task.CompletedTask;
                    }

                    if (c.Data.CustomId == "yes")
                    {
                        if (!alreadyDeferred) await c.DeferAsync().ConfigureAwait(false);
                        userInputTask.TrySetResult("Yes");
                        return Task.CompletedTask;
                    }

                    if (!alreadyDeferred) await c.DeferAsync().ConfigureAwait(false);
                    userInputTask.TrySetResult(c.Data.CustomId);
                    return Task.CompletedTask;
                }).ConfigureAwait(false);
            }
        }
    }

    public async Task<string>? NextMessageAsync(ulong channelId, ulong userId)
    {
        var userInputTask = new TaskCompletionSource<string>();
        var dsc = (DiscordSocketClient)ctx.Client;
        try
        {
            dsc.MessageReceived += Interaction;
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

        Task Interaction(SocketMessage arg)
        {
            return Task.Run(async () =>
            {
                if (arg.Author.Id != userId || arg.Channel.Id != channelId) return;
                userInputTask.TrySetResult(arg.Content);
                try
                {
                    await arg.DeleteAsync().ConfigureAwait(false);
                }
                catch
                {
                    //Exclude
                }
            });
        }
    }

    public async Task<SocketMessage>? NextFullMessageAsync(ulong channelId, ulong userId)
    {
        var userInputTask = new TaskCompletionSource<SocketMessage>();
        var dsc = (DiscordSocketClient)ctx.Client;
        try
        {
            dsc.MessageReceived += Interaction;
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

        Task Interaction(SocketMessage arg)
        {
            return Task.Run(async () =>
            {
                if (arg.Author.Id != userId || arg.Channel.Id != channelId) return;
                userInputTask.TrySetResult(arg);
                try
                {
                    await arg.DeleteAsync().ConfigureAwait(false);
                }
                catch
                {
                    //Exclude
                }
            });
        }
    }
}

public abstract class MewdekoModuleBase<TService> : MewdekoModule
{
    public TService Service { get; set; }
}

public abstract class MewdekoSubmodule : MewdekoModule;

public abstract class MewdekoSubmodule<TService> : MewdekoModuleBase<TService>;
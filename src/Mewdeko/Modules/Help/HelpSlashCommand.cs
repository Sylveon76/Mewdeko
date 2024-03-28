using Discord.Commands;
using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.Autocompleters;
using Mewdeko.Common.DiscordImplementations;
using Mewdeko.Common.Modals;
using Mewdeko.Modules.Help.Services;
using Mewdeko.Modules.Permissions.Services;
using Mewdeko.Services.Settings;

namespace Mewdeko.Modules.Help;

/// <summary>
/// Slash command module for help commands.
/// </summary>
/// <param name="permissionService">The server permission service</param>
/// <param name="interactivity">The service for embed pagination</param>
/// <param name="serviceProvider">Service provider</param>
/// <param name="cmds">The command service</param>
/// <param name="ch">The command handler (yes they are different now shut up)</param>
/// <param name="guildSettings">The service to retrieve guildconfigs</param>
/// <param name="config">Service to retrieve yml based configs</param>
[Discord.Interactions.Group("help", "Help Commands, what else is there to say?")]
public class HelpSlashCommand(
    GlobalPermissionService permissionService,
    InteractiveService interactivity,
    IServiceProvider serviceProvider,
    CommandService cmds,
    CommandHandler ch,
    GuildSettingsService guildSettings,
    BotConfigService config)
    : MewdekoSlashModuleBase<HelpService>
{
    private static readonly ConcurrentDictionary<ulong, ulong> HelpMessages = new();

    /// <summary>
    /// Shows all modules as well as additional information.
    /// </summary>
    [SlashCommand("help", "Shows help on how to use the bot"), CheckPermissions]
    public async Task Modules()
    {
        var embed = await Service.GetHelpEmbed(false, ctx.Guild, ctx.Channel, ctx.User);
        await RespondAsync(embed: embed.Build(), components: Service.GetHelpComponents(ctx.Guild, ctx.User).Build())
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Handles select menus for the help menu.
    /// </summary>
    /// <param name="unused">Literally unused</param>
    /// <param name="selected">The selected module</param>
    [ComponentInteraction("helpselect:*", true)]
    public async Task HelpSlash(string unused, string[] selected)
    {
        var currentmsg = new MewdekoUserMessage
        {
            Content = "help", Author = ctx.User, Channel = ctx.Channel
        };

        if (HelpMessages.TryGetValue(ctx.Channel.Id, out var msgId))
        {
            try
            {
                await ctx.Channel.DeleteMessageAsync(msgId);
                HelpMessages.TryRemove(ctx.Channel.Id, out _);
            }

            catch
            {
                // ignored
            }
        }

        var module = selected.FirstOrDefault();
        module = module?.Trim().ToUpperInvariant().Replace(" ", "");
        if (string.IsNullOrWhiteSpace(module))
        {
            await Modules().ConfigureAwait(false);
            return;
        }

        var prefix = await guildSettings.GetPrefix(ctx.Guild);
        // Find commands for that module
        // don't show commands which are blocked
        // order by name
        var commandInfos = cmds.Commands.Where(c =>
                c.Module.GetTopLevelModule().Name.ToUpperInvariant()
                    .StartsWith(module, StringComparison.InvariantCulture) &&
                !permissionService.BlockedCommands.Contains(c.Aliases[0].ToLowerInvariant()))
            .OrderBy(c => c.Aliases[0])
            .Distinct(new CommandTextEqualityComparer());
        // check preconditions for all commands, but only if it's not 'all'
        // because all will show all commands anyway, no need to check
        var succ = new HashSet<CommandInfo>((await Task.WhenAll(commandInfos.Select(async x =>
            {
                var pre = await x.CheckPreconditionsAsync(new CommandContext(ctx.Client, currentmsg), serviceProvider)
                    .ConfigureAwait(false);
                return (Cmd: x, Succ: pre.IsSuccess);
            })).ConfigureAwait(false))
            .Where(x => x.Succ)
            .Select(x => x.Cmd));

        var cmdsWithGroup = commandInfos
            .GroupBy(c => c.Module.Name.Replace("Commands", "", StringComparison.InvariantCulture))
            .OrderBy(x => x.Key == x.First().Module.Name ? int.MaxValue : x.Count());

        if (!commandInfos.Any())
        {
            await ReplyErrorLocalizedAsync("module_not_found_or_cant_exec").ConfigureAwait(false);
            return;
        }

        var i = 0;
        var groups = cmdsWithGroup.GroupBy(_ => i++ / 48).ToArray();
        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(groups.Select(x => x.Count()).FirstOrDefault() - 1)
            .WithDefaultEmotes()
            .Build();

        var msg = await interactivity.SendPaginatorAsync(paginator, ctx.Interaction as SocketInteraction,
            TimeSpan.FromMinutes(60)).ConfigureAwait(false);

        HelpMessages.TryAdd(ctx.Channel.Id, msg.Message.Id);


        async Task<PageBuilder> PageFactory(int page)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            var transformed = groups.Select(x => x.ElementAt(page)
                    .Where(commandInfo => !commandInfo.Attributes.Any(attribute => attribute is HelpDisabled)).Select(
                        commandInfo =>
                            $"{(succ.Contains(commandInfo) ? "✅" : "❌")}{prefix + commandInfo.Aliases[0]}{(commandInfo.Aliases.Skip(1).FirstOrDefault() is not null ? $"/{prefix}{commandInfo.Aliases[1]}" : "")}"))
                .FirstOrDefault();
            var last = groups.Select(x => x.Count()).FirstOrDefault();
            for (i = 0; i < last; i++)
            {
                if (i != last - 1 || (i + 1) % 1 == 0) continue;
                var grp = 0;
                var count = transformed.Count();
                transformed = transformed
                    .GroupBy(_ => grp++ % count / 2)
                    .Select(x => x.Count() == 1 ? $"{x.First()}" : string.Concat(x));
            }

            return new PageBuilder()
                .AddField(groups.Select(x => x.ElementAt(page).Key).FirstOrDefault(),
                    $"```css\n{string.Join("\n", transformed)}\n```")
                .WithDescription(
                    $"✅: You can use this command.\n❌: You cannot use this command.\n{config.Data.LoadingEmote}: If you need any help don't hesitate to join [The Support Server](https://discord.gg/mewdeko)\nDo `{prefix}h commandname` to see info on that command")
                .WithOkColor();
        }
    }

    /// <summary>
    /// Shows the invite link for the bot.
    /// </summary>
    /// <returns></returns>
    [SlashCommand("invite", "You should invite me to your server and check all my features!"), CheckPermissions]
    public Task Invite()
    {
        var eb = new EmbedBuilder()
            .AddField("Invite Link",
                "[Mewdeko](https://discord.com/oauth2/authorize?client_id=752236274261426212&scope=bot&permissions=66186303)\n[Mewdeko Nightly](https://discord.com/oauth2/authorize?client_id=964590728397344868&scope=bot&permissions=66186303)")
            .AddField("Website/Docs", "https://mewdeko.tech")
            .AddField("Support Server", config.Data.SupportServer)
            .WithOkColor();
        return ctx.Interaction.RespondAsync(embed: eb.Build());
    }

    /// <summary>
    /// ALlows you to search for a command using the autocompleter. Can also show help for the command thats chosen from autocomplete.
    /// </summary>
    /// <param name="command">The command to search for or to get help for</param>
    [SlashCommand("search", "get information on a specific command"), CheckPermissions]
    public async Task SearchCommand
    (
        [Discord.Interactions.Summary("command", "the command to get information about"),
         Autocomplete(typeof(GenericCommandAutocompleter))]
        string command
    )
    {
        var com = cmds.Commands.FirstOrDefault(x => x.Aliases.Contains(command));
        if (com == null)
        {
            await Modules().ConfigureAwait(false);
            return;
        }

        var (embed, comp) = await Service.GetCommandHelp(com, ctx.Guild, (ctx.User as IGuildUser)!);
        await RespondAsync(embed: embed.Build(), components: comp.Build()).ConfigureAwait(false);
    }

    /// <summary>
    /// Allows you to run a command from the commands help.
    /// </summary>
    /// <param name="command">The command in question</param>
    [ComponentInteraction("runcmd.*", true)]
    public async Task RunCmd(string command)
    {
        var com = cmds.Commands.FirstOrDefault(x => x.Aliases.Contains(command));
        if (com.Parameters.Count == 0)
        {
            ch.AddCommandToParseQueue(new MewdekoUserMessage
            {
                Content = await guildSettings.GetPrefix(ctx.Guild) + command, Author = ctx.User, Channel = ctx.Channel
            });
            _ = Task.Run(() => ch.ExecuteCommandsInChannelAsync(ctx.Channel.Id)).ConfigureAwait(false);
            return;
        }

        await RespondWithModalAsync<CommandModal>($"runcmdmodal.{command}").ConfigureAwait(false);
    }

    /// <summary>
    /// A modal that displays if the command has any arguments.
    /// </summary>
    /// <param name="command">The command to run</param>
    /// <param name="modal">The modal itself</param>
    [ModalInteraction("runcmdmodal.*", true)]
    public async Task RunModal(string command, CommandModal modal)
    {
        await DeferAsync().ConfigureAwait(false);
        var msg = new MewdekoUserMessage
        {
            Content = $"{await guildSettings.GetPrefix(ctx.Guild)}{command} {modal.Args}",
            Author = ctx.User,
            Channel = ctx.Channel
        };
        ch.AddCommandToParseQueue(msg);
        _ = Task.Run(() => ch.ExecuteCommandsInChannelAsync(ctx.Channel.Id)).ConfigureAwait(false);
    }

    /// <summary>
    /// Toggles module descriptions in help.
    /// </summary>
    /// <param name="sDesc">Bool thats parsed to either true or false to show the descriptions</param>
    /// <param name="sId">The server id the button is ran in</param>
    [ComponentInteraction("toggle-descriptions:*,*", true)]
    public async Task ToggleHelpDescriptions(string sDesc, string sId)
    {
        if (ctx.User.Id.ToString() != sId) return;

        await DeferAsync().ConfigureAwait(false);
        var description = bool.TryParse(sDesc, out var desc) && desc;
        var message = (ctx.Interaction as SocketMessageComponent)?.Message;
        var embed = await Service.GetHelpEmbed(description, ctx.Guild, ctx.Channel, ctx.User);

        await message.ModifyAsync(x =>
        {
            x.Embed = embed.Build();
            x.Components = Service.GetHelpComponents(ctx.Guild, ctx.User, !description).Build();
        }).ConfigureAwait(false);
    }
}
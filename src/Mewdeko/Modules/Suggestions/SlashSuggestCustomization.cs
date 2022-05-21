﻿using Discord;
using Discord.Interactions;
using Mewdeko.Common.Attributes;
using Mewdeko.Extensions;
using Mewdeko.Modules.Suggestions.Services;
using ContextType = Discord.Interactions.ContextType;

namespace Mewdeko.Modules.Suggestions;

[Discord.Interactions.Group("suggestionscustomize", "Manage suggestions!")]
public class SlashSuggestionsCustomization : MewdekoSlashModuleBase<SuggestionsService>
{
    [SlashCommand("suggestmessage", "Allows to set a custom embed when suggesting."), Discord.Interactions.RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.Administrator), CheckPermissions]
    public async Task SuggestMessage(string embed)
    {
        if (embed == "-")
        {
            await Service.SetSuggestionMessage(ctx.Guild, embed);
            await ctx.Interaction.SendConfirmAsync("Suggestions will now have the default look.");
            return;
        }

        await Service.SetSuggestionMessage(ctx.Guild, embed);
        await ctx.Interaction.SendConfirmAsync("Sucessfully updated suggestion message!");
    }

    [SlashCommand("suggestminlength", "Set the minimum suggestion length."), Discord.Interactions.RequireContext(ContextType.Guild), SlashUserPerm(GuildPermission.Administrator),
     CheckPermissions]
    public async Task MinSuggestionLength(int length)
    {
        if (length >= 2048)
        {
            await ctx.Interaction.SendErrorAsync("Can't set this value because it means users will not be able to suggest anything!");
            return;
        }

        await Service.SetMinLength(ctx.Guild, length);
        await ctx.Interaction.SendConfirmAsync($"Minimum length set to {length} characters!");
    }

    [SlashCommand("suggestmaxlength", "Set the maximum suggestion length."), Discord.Interactions.RequireContext(ContextType.Guild), SlashUserPerm(GuildPermission.Administrator),
     CheckPermissions]
    public async Task MaxSuggestionLength(int length)
    {
        if (length <= 0)
        {
            await ctx.Interaction.SendErrorAsync("Cant set this value because it means users will not be able to suggest anything!");
            return;
        }

        await Service.SetMaxLength(ctx.Guild, length);
        await ctx.Interaction.SendConfirmAsync($"Max length set to {length} characters!");
    }

    [SlashCommand("acceptmessage", "Allows to set a custom embed when a suggestion is accepted."), Discord.Interactions.RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.Administrator), CheckPermissions]
    public async Task AcceptMessage(string embed)
    {
        if (embed == "-")
        {
            await Service.SetAcceptMessage(ctx.Guild, embed);
            await ctx.Interaction.SendConfirmAsync("Accepted Suggestions will now have the default look.");
            return;
        }

        await Service.SetAcceptMessage(ctx.Guild, embed);
        await ctx.Interaction.SendConfirmAsync("Sucessfully updated accepted suggestion message!");
    }

    [SlashCommand("implementmessage", "Allows to set a custom embed when a suggestion is set implemented."), Discord.Interactions.RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.Administrator), CheckPermissions]
    public async Task ImplementMessage(string embed)
    {
        if (embed == "-")
        {
            await Service.SetImplementMessage(ctx.Guild, embed);
            await ctx.Interaction.SendConfirmAsync("Implemented Suggestions will now have the default look.");
            return;
        }

        await Service.SetImplementMessage(ctx.Guild, embed);
        await ctx.Interaction.SendConfirmAsync("Sucessfully updated implemented suggestion message!");
    }

    [SlashCommand("denymessage", "Allows to set a custom embed when a suggestion is denied."), Discord.Interactions.RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.Administrator), CheckPermissions]
    public async Task DenyMessage(string embed)
    {
        if (embed == "-")
        {
            await Service.SetDenyMessage(ctx.Guild, embed);
            await ctx.Interaction.SendConfirmAsync("Denied Suggestions will now have the default look.");
            return;
        }

        await Service.SetDenyMessage(ctx.Guild, embed);
        await ctx.Interaction.SendConfirmAsync("Sucessfully updated denied suggestion message!");
    }

    [SlashCommand("considermessage", "Allows to set a custom embed when a suggestion is considered."), Discord.Interactions.RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.Administrator), CheckPermissions]
    public async Task ConsiderMessage(string embed)
    {
        if (embed == "-")
        {
            await Service.SetConsiderMessage(ctx.Guild, embed);
            await ctx.Interaction.SendConfirmAsync("Considered Suggestions will now have the default look.");
            return;
        }

        await Service.SetConsiderMessage(ctx.Guild, embed);
        await ctx.Interaction.SendConfirmAsync("Sucessfully updated considered suggestion message!");
    }

    [SlashCommand("emotesmode", "Set whether suggestmotes are buttons or reactions"), Discord.Interactions.RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.Administrator), CheckPermissions]
    public async Task SuggestMotesMode(Suggestions.SuggestEmoteModeEnum mode)
    {
        await Service.SetEmoteMode(ctx.Guild, (int)mode);
        await ctx.Interaction.SendConfirmAsync($"Sucessfully set Emote Mode to {mode}");
    }

    [SlashCommand("buttoncolor", "Change the color of the suggestion button"), Discord.Interactions.RequireContext(ContextType.Guild), SlashUserPerm(GuildPermission.Administrator),
     CheckPermissions]
    public async Task SuggestButtonColor(Suggestions.ButtonType type)
    {
        await Service.SetSuggestButtonColor(ctx.Guild, (int)type);
        await ctx.Interaction.SendConfirmAsync($"Suggest Button Color will now be `{type}`");
        await Service.UpdateSuggestionButtonMessage(ctx.Guild, Service.GetSuggestButtonMessage(ctx.Guild));
    }

    [SlashCommand("emotecolor", "Set the color of each button on a suggestion"), Discord.Interactions.RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.Administrator), CheckPermissions]
    public async Task SuggestMoteColor([Discord.Interactions.Summary("number", "The number you want to change")] int num, Suggestions.ButtonType type)
    {
        await Service.SetButtonType(ctx.Guild, num, (int)type);
        await ctx.Interaction.SendConfirmAsync($"Suggest Button {num} will now be `{type}`");
        await Service.UpdateSuggestionButtonMessage(ctx.Guild, Service.GetSuggestButtonMessage(ctx.Guild));
    }

    [SlashCommand("acceptchannel", "Set the channel accepted suggestions get sent to."), Discord.Interactions.RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.Administrator), CheckPermissions]
    public async Task AcceptChannel(ITextChannel? channel = null)
    {
        await Service.SetAcceptChannel(ctx.Guild, channel?.Id ?? 0);
        if (channel is null)
            await ctx.Interaction.SendConfirmAsync("Accept Channel Disabled.");
        else
            await ctx.Interaction.SendConfirmAsync($"Accept channel set to {channel.Mention}");
    }

    [SlashCommand("denychannel", "Set the channel denied suggestions go to."), Discord.Interactions.RequireContext(ContextType.Guild), SlashUserPerm(GuildPermission.Administrator),
     CheckPermissions]
    public async Task DenyChannel(ITextChannel? channel = null)
    {
        await Service.SetDenyChannel(ctx.Guild, channel?.Id ?? 0);
        if (channel is null)
            await ctx.Interaction.SendConfirmAsync("Deny Channel Disabled.");
        else
            await ctx.Interaction.SendConfirmAsync($"Deny channel set to {channel.Mention}");
    }

    [SlashCommand("considerchannel", "Set the channel considered suggestions go to."), Discord.Interactions.RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.Administrator), CheckPermissions]
    public async Task ConsiderChannel(ITextChannel? channel = null)
    {
        await Service.SetConsiderChannel(ctx.Guild, channel?.Id ?? 0);
        if (channel is null)
            await ctx.Interaction.SendConfirmAsync("Consider Channel Disabled.");
        else
            await ctx.Interaction.SendConfirmAsync($"Consider channel set to {channel.Mention}");
    }

    [SlashCommand("implementchannel", "Set the channel where implemented suggestions go"), Discord.Interactions.RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.Administrator), CheckPermissions]
    public async Task ImplementChannel(ITextChannel? channel = null)
    {
        await Service.SetImplementChannel(ctx.Guild, channel?.Id ?? 0);
        if (channel is null)
            await ctx.Interaction.SendConfirmAsync("Implement Channel Disabled.");
        else
            await ctx.Interaction.SendConfirmAsync($"Implement channel set to {channel.Mention}");
    }

    [SlashCommand("threadstype", "Set the type of threads used in suggestions."), Discord.Interactions.RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.Administrator), CheckPermissions]
    public async Task SuggestThreadsType(Suggestions.SuggestThreadType type)
    {
        if (type == Suggestions.SuggestThreadType.Private && !ctx.Guild.Features.HasPrivateThreads)
        {
            await ctx.Interaction.SendErrorAsync("You do not have enough server boosts for private threads!");
            return;
        }

        await Service.SetSuggestThreadsType(ctx.Guild, (int)type);
        await ctx.Interaction.SendConfirmAsync($"Succesfully set Suggestion Threads Type to `{type}`");
    }

    [SlashCommand("archiveondeny", "Set whether threads auto archive on deny"), Discord.Interactions.RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.Administrator), CheckPermissions]
    public async Task ArchiveOnDeny()
    {
        var current = Service.GetArchiveOnDeny(ctx.Guild);
        await Service.SetArchiveOnDeny(ctx.Guild, !current);
        await ctx.Interaction.SendConfirmAsync($"Archive on deny is now set to `{!current}`");
    }

    [SlashCommand("archiveonaccept", "Set whether threads auto archive on accept"), Discord.Interactions.RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.Administrator), CheckPermissions]
    public async Task ArchiveOnAccept()
    {
        var current = Service.GetArchiveOnAccept(ctx.Guild);
        await Service.SetArchiveOnAccept(ctx.Guild, !current);
        await ctx.Interaction.SendConfirmAsync($"Archive on accept is now set to `{!current}`");
    }

    [SlashCommand("archiveonconsider", "Set whether threads auto archive on consider"), Discord.Interactions.RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.Administrator), CheckPermissions]
    public async Task ArchiveOnConsider()
    {
        var current = Service.GetArchiveOnConsider(ctx.Guild);
        await Service.SetArchiveOnConsider(ctx.Guild, !current);
        await ctx.Interaction.SendConfirmAsync($"Archive on consider is now set to `{!current}`");
    }

    [SlashCommand("archiveonimplement", "Set whether threads auto archive on implement"), Discord.Interactions.RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.Administrator), CheckPermissions]
    public async Task ArchiveOnImplement()
    {
        var current = Service.GetArchiveOnImplement(ctx.Guild);
        await Service.SetArchiveOnImplement(ctx.Guild, !current);
        await ctx.Interaction.SendConfirmAsync($"Archive on implement is now set to `{!current}`");
    }
}
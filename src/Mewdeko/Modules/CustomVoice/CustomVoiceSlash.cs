using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Database.Models;
using Mewdeko.Modules.CustomVoice.Services;
using Serilog;

namespace Mewdeko.Modules.CustomVoice;

/// <summary>
///     Interaction commands for managing custom voice channels.
/// </summary>
public class CustomVoiceSlash(DiscordShardedClient client, DbContextProvider dbContextProvider) : MewdekoSlashModuleBase<CustomVoiceService>
{

    /// <summary>
    ///     Button handler for basic voice channel operations (rename, limit, bitrate, lock/unlock)
    /// </summary>
    [ComponentInteraction("voice:*:*", true)]
    [SlashUserPerm(GuildPermission.UseApplicationCommands)]
    public async Task HandleVoiceButtonInteraction(string action, string channelId)
    {
        // Validate channel ID
        if (!ulong.TryParse(channelId, out var parsedChannelId))
        {
            await SendEphemeralFollowupErrorAsync(Strings.CustomVoiceInvalidChannel(ctx.Guild.Id));
            return;
        }

        // Check if the channel exists and is a custom voice channel
        var customChannel = await Service.GetChannelAsync(ctx.Guild.Id, parsedChannelId);
        if (customChannel == null)
        {
            await SendEphemeralFollowupErrorAsync(Strings.CustomVoiceChannelNotFound(ctx.Guild.Id));
            return;
        }

        // Get the actual Discord channel
        var channel = await ctx.Guild.GetVoiceChannelAsync(parsedChannelId);
        if (channel == null)
        {
            await SendEphemeralFollowupErrorAsync(Strings.CustomVoiceChannelNotFound(ctx.Guild.Id));
            return;
        }

        // Check if the user is authorized to control this channel
        var user = await ctx.Guild.GetUserAsync(ctx.User.Id);
        var isOwner = customChannel.OwnerId == user.Id;

        if (!isOwner)
        {
            var config = await Service.GetOrCreateConfigAsync(ctx.Guild.Id);
            var hasAdminRole = config.CustomVoiceAdminRoleId.HasValue && user.RoleIds.Contains(config.CustomVoiceAdminRoleId.Value);

            if (!hasAdminRole)
            {
                await SendEphemeralFollowupErrorAsync(Strings.CustomVoiceControlsNotOwner(ctx.Guild.Id));
                return;
            }
        }

        // Get server config to check permissions
        var guildConfig = await Service.GetOrCreateConfigAsync(ctx.Guild.Id);

        // Handle the specific action
        switch (action)
        {
            case "rename":
                if (!guildConfig.AllowNameCustomization)
                {
                    await SendEphemeralFollowupErrorAsync(Strings.CustomVoiceNameCustomizationDisabled(ctx.Guild.Id));
                    return;
                }

                await DeferAsync(ephemeral: true);

                // Create a modal for renaming
                var modal = new ModalBuilder()
                    .WithTitle(Strings.CustomVoiceControlsRenameModal(ctx.Guild.Id))
                    .WithCustomId($"voice:rename_modal:{channelId}")
                    .AddTextInput(
                        Strings.CustomVoiceControlsRenameLabel(ctx.Guild.Id),
                        "channel_name",
                        required: true,
                        placeholder: Strings.CustomVoiceControlsRenamePlaceholder(ctx.Guild.Id, channel.Name),
                        minLength: 1,
                        maxLength: 100,
                        value: channel.Name
                    );

                await ctx.Interaction.RespondWithModalAsync(modal.Build());
                break;

            case "limit":
                if (!guildConfig.AllowUserLimitCustomization)
                {
                    await SendEphemeralFollowupErrorAsync(Strings.CustomVoiceLimitCustomizationDisabled(ctx.Guild.Id));
                    return;
                }

                await DeferAsync(ephemeral: true);

                // Create a modal for setting user limit
                var limitModal = new ModalBuilder()
                    .WithTitle(Strings.CustomVoiceControlsLimitModal(ctx.Guild.Id))
                    .WithCustomId($"voice:limit_modal:{channelId}")
                    .AddTextInput(
                        Strings.CustomVoiceControlsLimitLabel(ctx.Guild.Id),
                        "user_limit",
                        required: true,
                        placeholder: Strings.CustomVoiceControlsLimitPlaceholder(ctx.Guild.Id),
                        minLength: 1,
                        maxLength: 3,
                        value: channel.UserLimit.ToString()
                    );

                await ctx.Interaction.RespondWithModalAsync(limitModal.Build());
                break;

            case "bitrate":
                if (!guildConfig.AllowBitrateCustomization)
                {
                    await SendEphemeralFollowupErrorAsync(Strings.CustomVoiceBitrateCustomizationDisabled(ctx.Guild.Id));
                    return;
                }

                await DeferAsync(ephemeral: true);

                // Create a modal for setting bitrate
                var bitrateModal = new ModalBuilder()
                    .WithTitle(Strings.CustomVoiceControlsBitrateModal(ctx.Guild.Id))
                    .WithCustomId($"voice:bitrate_modal:{channelId}")
                    .AddTextInput(
                        Strings.CustomVoiceControlsBitrateLabel(ctx.Guild.Id),
                        "bitrate",
                        required: true,
                        placeholder: Strings.CustomVoiceControlsBitratePlaceholder(ctx.Guild.Id),
                        minLength: 1,
                        maxLength: 3,
                        value: (channel.Bitrate / 1000).ToString()
                    );

                await ctx.Interaction.RespondWithModalAsync(bitrateModal.Build());
                break;

            case "lock":
                if (!guildConfig.AllowLocking)
                {
                    await SendEphemeralFollowupErrorAsync(Strings.CustomVoiceLockingDisabled(ctx.Guild.Id));
                    return;
                }

                await DeferAsync(ephemeral: true);
                if (customChannel.IsLocked)
                {
                    await ctx.Interaction.SendEphemeralFollowupConfirmAsync(Strings.CustomVoiceAlreadyLocked(ctx.Guild.Id));
                    return;
                }

                if (await Service.UpdateVoiceChannelAsync(ctx.Guild.Id, parsedChannelId, isLocked: true))
                {
                    await ctx.Interaction.SendEphemeralFollowupConfirmAsync(Strings.CustomVoiceLocked(ctx.Guild.Id));
                    await UpdateVoiceControlsMessage(customChannel);
                }
                else
                {
                    await SendEphemeralFollowupErrorAsync(Strings.CustomVoiceLockError(ctx.Guild.Id));
                }
                break;

            case "unlock":
                if (!guildConfig.AllowLocking)
                {
                    await SendEphemeralFollowupErrorAsync(Strings.CustomVoiceLockingDisabled(ctx.Guild.Id));
                    return;
                }

                await DeferAsync(ephemeral: true);
                if (!customChannel.IsLocked)
                {
                    await ctx.Interaction.SendEphemeralFollowupConfirmAsync(Strings.CustomVoiceAlreadyUnlocked(ctx.Guild.Id));
                    return;
                }

                if (await Service.UpdateVoiceChannelAsync(ctx.Guild.Id, parsedChannelId, isLocked: false))
                {
                    await ctx.Interaction.SendEphemeralFollowupConfirmAsync(Strings.CustomVoiceUnlocked(ctx.Guild.Id));
                    await UpdateVoiceControlsMessage(customChannel);
                }
                else
                {
                    await SendEphemeralFollowupErrorAsync(Strings.CustomVoiceUnlockError(ctx.Guild.Id));
                }
                break;

            case "transfer":
                await DeferAsync(ephemeral: true);

                // Get users who can become owners (users in the voice channel except the current owner)
                var usersInChannel = (await channel.GetUsersAsync().FlattenAsync()).Where(u => u.Id != customChannel.OwnerId).ToList();
                usersInChannel = usersInChannel.Where(x => x.VoiceChannel.Id == parsedChannelId).ToList();

                if (usersInChannel.Count == 0)
                {
                    await SendEphemeralFollowupErrorAsync(Strings.CustomVoiceTransferNoUsers(ctx.Guild.Id));
                    return;
                }

                // Create a select menu for transferring ownership
                var selectMenu = new SelectMenuBuilder()
                    .WithCustomId($"voice:transfer_user:{channelId}")
                    .WithPlaceholder(Strings.CustomVoiceControlsTransferPlaceholder(ctx.Guild.Id))
                    .WithMinValues(1)
                    .WithMaxValues(1);

                foreach (var potentialOwner in usersInChannel)
                {
                    selectMenu.AddOption(
                        potentialOwner.Username,
                        potentialOwner.Id.ToString(),
                        Strings.CustomVoiceControlsTransferUserOption(ctx.Guild.Id, potentialOwner.Username),
                        new Emoji("👑")
                    );
                }

                var components = new ComponentBuilder()
                    .WithSelectMenu(selectMenu)
                    .Build();

                await ctx.Interaction.FollowupAsync(
                    Strings.CustomVoiceControlsTransferPrompt(ctx.Guild.Id),
                    components: components,
                    ephemeral: true
                );
                break;

            default:
                // Handle the keepalive action which includes a parameter
                if (action.StartsWith("keepalive"))
                {
                    await DeferAsync(ephemeral: true);
                    var inter = ctx.Interaction.Data as IComponentInteractionData;
                    // Parse the additional parameter from the custom ID
                    // The format is "keepalive:{channelId}:{newKeepAliveValue}"
                    var parts = inter.CustomId.Split(':');
                    if (parts.Length >= 4 && bool.TryParse(parts[3], out var keepAlive))
                    {

                        if (customChannel.KeepAlive == keepAlive)
                        {
                            var stateText = keepAlive
                                ? Strings.CustomVoiceAlreadyKeepAlive(ctx.Guild.Id)
                                : Strings.CustomVoiceAlreadyNotKeepAlive(ctx.Guild.Id);

                            await ctx.Interaction.SendEphemeralFollowupConfirmAsync(stateText);
                            return;
                        }

                        // Update the keep-alive status
                        await using var db = await dbContextProvider.GetContextAsync();
                        customChannel.KeepAlive = keepAlive;
                        db.CustomVoiceChannels.Update(customChannel);
                        await db.SaveChangesAsync();

                        var actionText = keepAlive
                            ? Strings.CustomVoiceKeptAlive(ctx.Guild.Id)
                            : Strings.CustomVoiceNotKeptAlive(ctx.Guild.Id);

                        await ctx.Interaction.SendEphemeralFollowupConfirmAsync(actionText);
                        await UpdateVoiceControlsMessage(customChannel);
                    }
                    else
                    {
                        await SendEphemeralFollowupErrorAsync(Strings.CustomVoiceInvalidAction(ctx.Guild.Id));
                    }
                }
                else
                {
                    await SendEphemeralFollowupErrorAsync(Strings.CustomVoiceInvalidAction(ctx.Guild.Id));
                }
                break;
        }
    }

    /// <summary>
    ///     Modal handler for channel rename
    /// </summary>
    [ModalInteraction("voice:rename_modal:*", true)]
    [SlashUserPerm(GuildPermission.UseApplicationCommands)]
    public async Task HandleRenameModal(string channelId, ModalRenameChannel modal)
    {
        await DeferAsync(ephemeral: true);

        // Validate channel ID
        if (!ulong.TryParse(channelId, out var parsedChannelId))
        {
            await SendEphemeralFollowupErrorAsync(Strings.CustomVoiceInvalidChannel(ctx.Guild.Id));
            return;
        }

        // Validate user permissions
        var customChannel = await Service.GetChannelAsync(ctx.Guild.Id, parsedChannelId);
        if (customChannel == null)
        {
            await SendEphemeralFollowupErrorAsync(Strings.CustomVoiceChannelNotFound(ctx.Guild.Id));
            return;
        }

        var user = await ctx.Guild.GetUserAsync(ctx.User.Id);
        var isOwner = customChannel.OwnerId == user.Id;

        if (!isOwner)
        {
            var config = await Service.GetOrCreateConfigAsync(ctx.Guild.Id);
            var hasAdminRole = config.CustomVoiceAdminRoleId.HasValue && user.RoleIds.Contains(config.CustomVoiceAdminRoleId.Value);

            if (!hasAdminRole)
            {
                await SendEphemeralFollowupErrorAsync(Strings.CustomVoiceControlsNotOwner(ctx.Guild.Id));
                return;
            }
        }

        // Validate and update the channel name
        var guildConfig = await Service.GetOrCreateConfigAsync(ctx.Guild.Id);
        if (!guildConfig.AllowNameCustomization)
        {
            await SendEphemeralFollowupErrorAsync(Strings.CustomVoiceNameCustomizationDisabled(ctx.Guild.Id));
            return;
        }

        if (string.IsNullOrWhiteSpace(modal.ChannelName))
        {
            await SendEphemeralFollowupErrorAsync(Strings.CustomVoiceNameRequired(ctx.Guild.Id));
            return;
        }

        if (await Service.UpdateVoiceChannelAsync(ctx.Guild.Id, parsedChannelId, name: modal.ChannelName))
        {
            await ctx.Interaction.SendEphemeralFollowupConfirmAsync(Strings.CustomVoiceRenamed(ctx.Guild.Id, modal.ChannelName));
            await UpdateVoiceControlsMessage(customChannel);
        }
        else
        {
            await SendEphemeralFollowupErrorAsync(Strings.CustomVoiceRenameError(ctx.Guild.Id));
        }
    }

    /// <summary>
    ///     Modal handler for user limit
    /// </summary>
    [ModalInteraction("voice:limit_modal:*", true)]
    [SlashUserPerm(GuildPermission.UseApplicationCommands)]
    public async Task HandleLimitModal(string channelId, ModalUserLimit modal)
    {
        await DeferAsync(ephemeral: true);

        // Validate channel ID
        if (!ulong.TryParse(channelId, out var parsedChannelId))
        {
            await SendEphemeralFollowupErrorAsync(Strings.CustomVoiceInvalidChannel(ctx.Guild.Id));
            return;
        }

        // Validate user permissions
        var customChannel = await Service.GetChannelAsync(ctx.Guild.Id, parsedChannelId);
        if (customChannel == null)
        {
            await SendEphemeralFollowupErrorAsync(Strings.CustomVoiceChannelNotFound(ctx.Guild.Id));
            return;
        }

        var user = await ctx.Guild.GetUserAsync(ctx.User.Id);
        var isOwner = customChannel.OwnerId == user.Id;

        if (!isOwner)
        {
            var config = await Service.GetOrCreateConfigAsync(ctx.Guild.Id);
            var hasAdminRole = config.CustomVoiceAdminRoleId.HasValue && user.RoleIds.Contains(config.CustomVoiceAdminRoleId.Value);

            if (!hasAdminRole)
            {
                await SendEphemeralFollowupErrorAsync(Strings.CustomVoiceControlsNotOwner(ctx.Guild.Id));
                return;
            }
        }

        // Validate and update the user limit
        var guildConfig = await Service.GetOrCreateConfigAsync(ctx.Guild.Id);
        if (!guildConfig.AllowUserLimitCustomization)
        {
            await SendEphemeralFollowupErrorAsync(Strings.CustomVoiceLimitCustomizationDisabled(ctx.Guild.Id));
            return;
        }

        if (!int.TryParse(modal.UserLimit, out var limit) || limit < 0)
        {
            await SendEphemeralFollowupErrorAsync(Strings.CustomVoiceLimitNegative(ctx.Guild.Id));
            return;
        }

        if (guildConfig.MaxUserLimit > 0 && limit > guildConfig.MaxUserLimit)
        {
            await SendEphemeralFollowupErrorAsync(Strings.CustomVoiceLimitTooHigh(ctx.Guild.Id, guildConfig.MaxUserLimit));
            return;
        }

        if (await Service.UpdateVoiceChannelAsync(ctx.Guild.Id, parsedChannelId, userLimit: limit))
        {
            var limitText = limit == 0 ? Strings.CustomVoiceConfigUnlimited(ctx.Guild.Id) : limit.ToString();
            await ctx.Interaction.SendEphemeralFollowupConfirmAsync(Strings.CustomVoiceLimitSet(ctx.Guild.Id, limitText));
            await UpdateVoiceControlsMessage(customChannel);
        }
        else
        {
            await SendEphemeralFollowupErrorAsync(Strings.CustomVoiceLimitError(ctx.Guild.Id));
        }
    }

    /// <summary>
    ///     Modal handler for bitrate
    /// </summary>
    [ModalInteraction("voice:bitrate_modal:*", true)]
    [SlashUserPerm(GuildPermission.UseApplicationCommands)]
    public async Task HandleBitrateModal(string channelId, ModalBitrate modal)
    {
        await DeferAsync(ephemeral: true);

        // Validate channel ID
        if (!ulong.TryParse(channelId, out var parsedChannelId))
        {
            await SendEphemeralFollowupErrorAsync(Strings.CustomVoiceInvalidChannel(ctx.Guild.Id));
            return;
        }

        // Validate user permissions
        var customChannel = await Service.GetChannelAsync(ctx.Guild.Id, parsedChannelId);
        if (customChannel == null)
        {
            await SendEphemeralFollowupErrorAsync(Strings.CustomVoiceChannelNotFound(ctx.Guild.Id));
            return;
        }

        var user = await ctx.Guild.GetUserAsync(ctx.User.Id);
        var isOwner = customChannel.OwnerId == user.Id;

        if (!isOwner)
        {
            var config = await Service.GetOrCreateConfigAsync(ctx.Guild.Id);
            var hasAdminRole = config.CustomVoiceAdminRoleId.HasValue && user.RoleIds.Contains(config.CustomVoiceAdminRoleId.Value);

            if (!hasAdminRole)
            {
                await SendEphemeralFollowupErrorAsync(Strings.CustomVoiceControlsNotOwner(ctx.Guild.Id));
                return;
            }
        }

        // Validate and update the bitrate
        var guildConfig = await Service.GetOrCreateConfigAsync(ctx.Guild.Id);
        if (!guildConfig.AllowBitrateCustomization)
        {
            await SendEphemeralFollowupErrorAsync(Strings.CustomVoiceBitrateCustomizationDisabled(ctx.Guild.Id));
            return;
        }

        if (!int.TryParse(modal.Bitrate, out var bitrate) || bitrate <= 0)
        {
            await SendEphemeralFollowupErrorAsync(Strings.CustomVoiceBitrateNegative(ctx.Guild.Id));
            return;
        }

        if (guildConfig.MaxBitrate > 0 && bitrate > guildConfig.MaxBitrate)
        {
            await SendEphemeralFollowupErrorAsync(Strings.CustomVoiceBitrateTooHigh(ctx.Guild.Id, guildConfig.MaxBitrate));
            return;
        }

        if (await Service.UpdateVoiceChannelAsync(ctx.Guild.Id, parsedChannelId, bitrate: bitrate))
        {
            await ctx.Interaction.SendEphemeralFollowupConfirmAsync(Strings.CustomVoiceBitrateSet(ctx.Guild.Id, bitrate));
            await UpdateVoiceControlsMessage(customChannel);
        }
        else
        {
            await SendEphemeralFollowupErrorAsync(Strings.CustomVoiceBitrateError(ctx.Guild.Id));
        }
    }

    /// <summary>
    ///     Handler for ownership transfer selection
    /// </summary>
    [ComponentInteraction("voice:transfer_user:*", true)]
    [SlashUserPerm(GuildPermission.UseApplicationCommands)]
    public async Task HandleTransferUser(string channelId, string[] selectedUsers)
    {
        await DeferAsync(ephemeral: true);

        if (selectedUsers.Length != 1)
        {
            await SendEphemeralFollowupErrorAsync(Strings.CustomVoiceTransferSelectOne(ctx.Guild.Id));
            return;
        }

        // Validate channel ID
        if (!ulong.TryParse(channelId, out var parsedChannelId))
        {
            await SendEphemeralFollowupErrorAsync(Strings.CustomVoiceInvalidChannel(ctx.Guild.Id));
            return;
        }

        // Validate user permissions
        var customChannel = await Service.GetChannelAsync(ctx.Guild.Id, parsedChannelId);
        if (customChannel == null)
        {
            await SendEphemeralFollowupErrorAsync(Strings.CustomVoiceChannelNotFound(ctx.Guild.Id));
            return;
        }

        var user = await ctx.Guild.GetUserAsync(ctx.User.Id);
        var isOwner = customChannel.OwnerId == user.Id;

        if (!isOwner)
        {
            var config = await Service.GetOrCreateConfigAsync(ctx.Guild.Id);
            var hasAdminRole = config.CustomVoiceAdminRoleId.HasValue && user.RoleIds.Contains(config.CustomVoiceAdminRoleId.Value);

            if (!hasAdminRole)
            {
                await SendEphemeralFollowupErrorAsync(Strings.CustomVoiceControlsNotOwner(ctx.Guild.Id));
                return;
            }
        }

        // Get the selected user
        if (!ulong.TryParse(selectedUsers[0], out var newOwnerId))
        {
            await SendEphemeralFollowupErrorAsync(Strings.CustomVoiceInvalidUser(ctx.Guild.Id));
            return;
        }

        var newOwner = await ctx.Guild.GetUserAsync(newOwnerId);
        if (newOwner == null)
        {
            await SendEphemeralFollowupErrorAsync(Strings.CustomVoiceUserNotFound(ctx.Guild.Id));
            return;
        }

        // Make sure the new owner is in the voice channel
        var channel = await ctx.Guild.GetVoiceChannelAsync(parsedChannelId);
        if (channel == null || (await channel.GetUsersAsync().FlattenAsync()).All(u => u.Id != newOwnerId))
        {
            await SendEphemeralFollowupErrorAsync(Strings.CustomVoiceTransferUserNotPresent(ctx.Guild.Id));
            return;
        }

        // Transfer the ownership
        if (await Service.TransferOwnershipAsync(ctx.Guild.Id, parsedChannelId, newOwnerId))
        {
            await ctx.Interaction.SendEphemeralFollowupConfirmAsync(Strings.CustomVoiceTransferSuccess(ctx.Guild.Id, newOwner.Mention));
            await UpdateVoiceControlsMessage(customChannel);
        }
        else
        {
            await SendEphemeralFollowupErrorAsync(Strings.CustomVoiceTransferError(ctx.Guild.Id, newOwner.Mention));
        }
    }

    /// <summary>
    ///     Handler for user management dropdown
    /// </summary>
    [ComponentInteraction("voice:usermenu:*", true)]
    [SlashUserPerm(GuildPermission.UseApplicationCommands)]
    public async Task HandleUserMenu(string channelId, string[] selectedOptions)
    {
        await DeferAsync(ephemeral: true);

        if (selectedOptions.Length != 1)
        {
            await SendEphemeralFollowupErrorAsync(Strings.CustomVoiceUserManagementSelectOne(ctx.Guild.Id));
            return;
        }

        // Validate channel ID
        if (!ulong.TryParse(channelId, out var parsedChannelId))
        {
            await SendEphemeralFollowupErrorAsync(Strings.CustomVoiceInvalidChannel(ctx.Guild.Id));
            return;
        }

        // Validate user permissions
        var customChannel = await Service.GetChannelAsync(ctx.Guild.Id, parsedChannelId);
        if (customChannel == null)
        {
            await SendEphemeralFollowupErrorAsync(Strings.CustomVoiceChannelNotFound(ctx.Guild.Id));
            return;
        }

        var user = await ctx.Guild.GetUserAsync(ctx.User.Id);
        var isOwner = customChannel.OwnerId == user.Id;

        if (!isOwner)
        {
            var config = await Service.GetOrCreateConfigAsync(ctx.Guild.Id);
            var hasAdminRole = config.CustomVoiceAdminRoleId.HasValue && user.RoleIds.Contains(config.CustomVoiceAdminRoleId.Value);

            if (!hasAdminRole)
            {
                await SendEphemeralFollowupErrorAsync(Strings.CustomVoiceControlsNotOwner(ctx.Guild.Id));
                return;
            }
        }

        // Check if user management is allowed
        var guildConfig = await Service.GetOrCreateConfigAsync(ctx.Guild.Id);
        if (!guildConfig.AllowUserManagement)
        {
            await SendEphemeralFollowupErrorAsync(Strings.CustomVoiceUserManagementDisabled(ctx.Guild.Id));
            return;
        }

        // Parse the selected option
        var option = selectedOptions[0];
        var parts = option.Split(':');
        if (parts.Length != 2)
        {
            await SendEphemeralFollowupErrorAsync(Strings.CustomVoiceInvalidAction(ctx.Guild.Id));
            return;
        }

        var action = parts[0];
        if (!ulong.TryParse(parts[1], out var targetUserId))
        {
            await SendEphemeralFollowupErrorAsync(Strings.CustomVoiceInvalidUser(ctx.Guild.Id));
            return;
        }

        var targetUser = await ctx.Guild.GetUserAsync(targetUserId);
        if (targetUser == null)
        {
            await SendEphemeralFollowupErrorAsync(Strings.CustomVoiceUserNotFound(ctx.Guild.Id));
            return;
        }

        // Execute the requested action
        switch (action)
        {
            case "kick":
                // Make sure the user is in the voice channel
                var channel = await ctx.Guild.GetVoiceChannelAsync(parsedChannelId);
                if (channel == null || (await channel.GetUsersAsync().FlattenAsync()).All(u => u.Id != targetUserId))
                {
                    await SendEphemeralFollowupErrorAsync(Strings.CustomVoiceUserNotInChannel(ctx.Guild.Id, targetUser.Username));
                    return;
                }

                // Can't kick the owner
                if (customChannel.OwnerId == targetUserId)
                {
                    await SendEphemeralFollowupErrorAsync(Strings.CustomVoiceCantKickOwner(ctx.Guild.Id));
                    return;
                }

                // Kick the user
                try
                {
                    await targetUser.ModifyAsync(props => props.Channel = null);
                    await ctx.Interaction.SendEphemeralFollowupConfirmAsync(Strings.CustomVoiceUserKicked(ctx.Guild.Id, targetUser.Mention));
                    await UpdateVoiceControlsMessage(customChannel);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error kicking user {UserId} from voice channel {ChannelId}", targetUserId, parsedChannelId);
                    await SendEphemeralFollowupErrorAsync(Strings.CustomVoiceUserKickError(ctx.Guild.Id, targetUser.Mention));
                }
                break;

            case "allow":
                if (await Service.AllowUserAsync(ctx.Guild.Id, parsedChannelId, targetUserId))
                {
                    await ctx.Interaction.SendEphemeralFollowupConfirmAsync(Strings.CustomVoiceUserAllowed(ctx.Guild.Id, targetUser.Mention));
                    await UpdateVoiceControlsMessage(customChannel);
                }
                else
                {
                    await SendEphemeralFollowupErrorAsync(Strings.CustomVoiceUserAllowError(ctx.Guild.Id, targetUser.Mention));
                }
                break;

            case "deny":
                if (customChannel.OwnerId == targetUserId)
                {
                    await SendEphemeralFollowupErrorAsync(Strings.CustomVoiceCantDenyOwner(ctx.Guild.Id));
                    return;
                }

                if (await Service.DenyUserAsync(ctx.Guild.Id, parsedChannelId, targetUserId))
                {
                    await ctx.Interaction.SendEphemeralFollowupConfirmAsync(Strings.CustomVoiceUserDenied(ctx.Guild.Id, targetUser.Mention));
                    await UpdateVoiceControlsMessage(customChannel);
                }
                else
                {
                    await SendEphemeralFollowupErrorAsync(Strings.CustomVoiceUserDenyError(ctx.Guild.Id, targetUser.Mention));
                }
                break;

            case "unallow":
                // Parse allowed users from JSON
                var allowedUsers = new List<ulong>();
                if (!string.IsNullOrEmpty(customChannel.AllowedUsersJson))
                {
                    try
                    {
                        allowedUsers = JsonSerializer.Deserialize<List<ulong>>(customChannel.AllowedUsersJson) ?? new List<ulong>();
                    }
                    catch { }
                }

                // Remove user from allowed list
                if (allowedUsers.Remove(targetUserId))
                {
                    // Update the allowed users list
                    await using var db = await dbContextProvider.GetContextAsync();
                    customChannel.AllowedUsersJson = JsonSerializer.Serialize(allowedUsers);
                    db.CustomVoiceChannels.Update(customChannel);
                    await db.SaveChangesAsync();

                    // Update permissions if the channel is locked
                    if (customChannel.IsLocked)
                    {
                        var voiceChannel = await ctx.Guild.GetVoiceChannelAsync(parsedChannelId);
                        // Remove the allow permission
                        var overwrite = voiceChannel?.PermissionOverwrites
                            .FirstOrDefault(o => o.TargetId == targetUserId && o.TargetType == PermissionTarget.User);

                        if (overwrite?.ToString() != null)
                        {
                            await voiceChannel.RemovePermissionOverwriteAsync(targetUser);
                        }
                    }

                    await ctx.Interaction.SendEphemeralFollowupConfirmAsync(Strings.CustomVoiceUserRemovedFromAllowed(ctx.Guild.Id, targetUser.Mention));
                    await UpdateVoiceControlsMessage(customChannel);
                }
                else
                {
                    await SendEphemeralFollowupErrorAsync(Strings.CustomVoiceUserNotInAllowList(ctx.Guild.Id, targetUser.Mention));
                }
                break;

            case "undeny":
                // Parse denied users from JSON
                var deniedUsers = new List<ulong>();
                if (!string.IsNullOrEmpty(customChannel.DeniedUsersJson))
                {
                    try
                    {
                        deniedUsers = JsonSerializer.Deserialize<List<ulong>>(customChannel.DeniedUsersJson) ?? new List<ulong>();
                    }
                    catch { }
                }

                // Remove user from denied list
                if (deniedUsers.Remove(targetUserId))
                {
                    // Update the denied users list
                    await using var db = await dbContextProvider.GetContextAsync();
                    customChannel.DeniedUsersJson = JsonSerializer.Serialize(deniedUsers);
                    db.CustomVoiceChannels.Update(customChannel);
                    await db.SaveChangesAsync();

                    // Update permissions
                    var voiceChannel = await ctx.Guild.GetVoiceChannelAsync(parsedChannelId);
                    // Remove the deny permission
                    var overwrite = voiceChannel?.PermissionOverwrites
                        .FirstOrDefault(o => o.TargetId == targetUserId && o.TargetType == PermissionTarget.User);

                    if (overwrite?.ToString() != null)
                    {
                        await voiceChannel.RemovePermissionOverwriteAsync(targetUser);
                    }

                    await ctx.Interaction.SendEphemeralFollowupConfirmAsync(Strings.CustomVoiceUserRemovedFromDenied(ctx.Guild.Id, targetUser.Mention));
                    await UpdateVoiceControlsMessage(customChannel);
                }
                else
                {
                    await SendEphemeralFollowupErrorAsync(Strings.CustomVoiceUserNotInDenyList(ctx.Guild.Id, targetUser.Mention));
                }
                break;

            default:
                await SendEphemeralFollowupErrorAsync(Strings.CustomVoiceInvalidAction(ctx.Guild.Id));
                break;
        }
    }

    /// <summary>
    ///     Updates the voice controls message after changes
    /// </summary>
    private async Task UpdateVoiceControlsMessage(CustomVoiceChannel customChannel)
    {
        try
        {
            var inter = ctx.Interaction as IComponentInteraction;
            // Get the message from the same channel the interaction was triggered in
            if (inter.Message == null)
                return;

            if (await ctx.Channel.GetMessageAsync(inter.Message.Id) is not IUserMessage message)
                return;

            // Get the updated channel data
            var channel = await ctx.Guild.GetVoiceChannelAsync(customChannel.ChannelId);
            if (channel == null)
                return;

            // Get the updated custom channel data
            var updatedCustomChannel = await Service.GetChannelAsync(ctx.Guild.Id, customChannel.ChannelId);
            if (updatedCustomChannel == null)
                return;

            // Get server config
            var guildConfig = await Service.GetOrCreateConfigAsync(ctx.Guild.Id);

            // Rebuild the embed
            var embed = new EmbedBuilder()
                .WithTitle(Strings.CustomVoiceControlsTitle(ctx.Guild.Id, channel.Name))
                .WithOkColor()
                .WithDescription(Strings.CustomVoiceControlsDesc(ctx.Guild.Id))
                .AddField(Strings.CustomVoiceControlsOwner(ctx.Guild.Id), MentionUtils.MentionUser(updatedCustomChannel.OwnerId), true)
                .AddField(Strings.CustomVoiceControlsCreated(ctx.Guild.Id),
                    Strings.CustomVoiceControlsTimeAgo(ctx.Guild.Id, (DateTime.UtcNow - updatedCustomChannel.CreatedAt).TotalHours.ToString("F1")), true)
                .AddField(Strings.CustomVoiceControlsUserLimit(ctx.Guild.Id),
                    channel.UserLimit == 0 ? Strings.CustomVoiceConfigUnlimited(ctx.Guild.Id) : channel.UserLimit.ToString(), true)
                .AddField(Strings.CustomVoiceControlsBitrate(ctx.Guild.Id), $"{channel.Bitrate / 1000} kbps", true)
                .AddField(Strings.CustomVoiceControlsLocked(ctx.Guild.Id),
                    updatedCustomChannel.IsLocked ? Strings.CustomVoiceConfigYes(ctx.Guild.Id) : Strings.CustomVoiceConfigNo(ctx.Guild.Id), true)
                .AddField(Strings.CustomVoiceControlsKeepAlive(ctx.Guild.Id),
                    updatedCustomChannel.KeepAlive ? Strings.CustomVoiceConfigYes(ctx.Guild.Id) : Strings.CustomVoiceConfigNo(ctx.Guild.Id), true)
                .AddField(Strings.CustomVoiceControlsUsers(ctx.Guild.Id),
                    (await channel.GetUsersAsync().FlattenAsync()).Count() == 0 ?
                        Strings.CustomVoiceControlsNoUsers(ctx.Guild.Id) :
                        string.Join(", ", (await channel.GetUsersAsync().FlattenAsync()).Select(u => u.Mention)));

            // Rebuild the components
            var components = new ComponentBuilder();

            // Row 1: Basic controls
            components.WithButton(
                customId: $"voice:rename:{channel.Id}",
                label: Strings.CustomVoiceControlsRenameButton(ctx.Guild.Id),
                style: ButtonStyle.Primary,
                disabled: !guildConfig.AllowNameCustomization,
                row: 0
            );

            components.WithButton(
                customId: $"voice:limit:{channel.Id}",
                label: Strings.CustomVoiceControlsLimitButton(ctx.Guild.Id),
                style: ButtonStyle.Primary,
                disabled: !guildConfig.AllowUserLimitCustomization,
                row: 0
            );

            components.WithButton(
                customId: $"voice:bitrate:{channel.Id}",
                label: Strings.CustomVoiceControlsBitrateButton(ctx.Guild.Id),
                style: ButtonStyle.Primary,
                disabled: !guildConfig.AllowBitrateCustomization,
                row: 0
            );

            // Row 2: Lock/Keep Alive toggles
            components.WithButton(
                customId: $"voice:{(updatedCustomChannel.IsLocked ? "unlock" : "lock")}:{channel.Id}",
                label: updatedCustomChannel.IsLocked ?
                    Strings.CustomVoiceControlsUnlockButton(ctx.Guild.Id) :
                    Strings.CustomVoiceControlsLockButton(ctx.Guild.Id),
                style: updatedCustomChannel.IsLocked ? ButtonStyle.Success : ButtonStyle.Danger,
                disabled: !guildConfig.AllowLocking,
                row: 1
            );

            components.WithButton(
                customId: $"voice:keepalive:{channel.Id}:{!updatedCustomChannel.KeepAlive}",
                label: updatedCustomChannel.KeepAlive ?
                    Strings.CustomVoiceControlsDisableKeepAliveButton(ctx.Guild.Id) :
                    Strings.CustomVoiceControlsEnableKeepAliveButton(ctx.Guild.Id),
                style: updatedCustomChannel.KeepAlive ? ButtonStyle.Danger : ButtonStyle.Success,
                row: 1
            );

            components.WithButton(
                customId: $"voice:transfer:{channel.Id}",
                label: Strings.CustomVoiceControlsTransferButton(ctx.Guild.Id),
                style: ButtonStyle.Secondary,
                row: 1
            );

            // Row 3: User management dropdown
            if (guildConfig.AllowUserManagement && ((await channel.GetUsersAsync().FlattenAsync()).Count() > 1 || updatedCustomChannel.IsLocked))
            {
                var userSelect = new SelectMenuBuilder()
                    .WithPlaceholder(Strings.CustomVoiceControlsManageUsersPlaceholder(ctx.Guild.Id))
                    .WithCustomId($"voice:usermenu:{channel.Id}")
                    .WithMinValues(1)
                    .WithMaxValues(1);

                // Add options for all users except the channel owner
                foreach (var channelUser in (await channel.GetUsersAsync().FlattenAsync()).Where(u => u.Id != updatedCustomChannel.OwnerId))
                {
                    userSelect.AddOption(
                        Strings.CustomVoiceControlsKickOption(ctx.Guild.Id, channelUser.Username),
                        $"kick:{channelUser.Id}",
                        Strings.CustomVoiceControlsKickDesc(ctx.Guild.Id),
                        new Emoji("👢")
                    );
                }

                // Add options for all users in the guild
                foreach (var guildUser in await ctx.Guild.GetUsersAsync())
                {
                    // Skip users who are in the voice channel to avoid duplicate entries
                    if ((await channel.GetUsersAsync().FlattenAsync()).Any(u => u.Id == guildUser.Id) || guildUser.Id == client.CurrentUser.Id)
                        continue;

                    // Only add a reasonable number of users to avoid hitting Discord's limits
                    if (userSelect.Options.Count >= 20)
                        break;

                    // Check if the user is allowed or denied
                    var isAllowed = false;
                    var isDenied = false;

                    // Parse allowed/denied users from JSON
                    if (!string.IsNullOrEmpty(updatedCustomChannel.AllowedUsersJson))
                    {
                        try
                        {
                            var allowedUsers = JsonSerializer.Deserialize<List<ulong>>(updatedCustomChannel.AllowedUsersJson);
                            isAllowed = allowedUsers?.Contains(guildUser.Id) == true;
                        }
                        catch { }
                    }

                    if (!string.IsNullOrEmpty(updatedCustomChannel.DeniedUsersJson))
                    {
                        try
                        {
                            var deniedUsers = JsonSerializer.Deserialize<List<ulong>>(updatedCustomChannel.DeniedUsersJson);
                            isDenied = deniedUsers?.Contains(guildUser.Id) == true;
                        }
                        catch { }
                    }

                    // Add allow/deny options
                    if (isAllowed)
                    {
                        userSelect.AddOption(
                            Strings.CustomVoiceControlsRemoveAllowOption(ctx.Guild.Id, guildUser.Username),
                            $"unallow:{guildUser.Id}",
                            Strings.CustomVoiceControlsRemoveAllowDesc(ctx.Guild.Id),
                            new Emoji("❌")
                        );
                    }
                    else if (isDenied)
                    {
                        userSelect.AddOption(
                            Strings.CustomVoiceControlsRemoveDenyOption(ctx.Guild.Id, guildUser.Username),
                            $"undeny:{guildUser.Id}",
                            Strings.CustomVoiceControlsRemoveDenyDesc(ctx.Guild.Id),
                            new Emoji("✅")
                        );
                    }
                    else
                    {
                        userSelect.AddOption(
                            Strings.CustomVoiceControlsAllowOption(ctx.Guild.Id, guildUser.Username),
                            $"allow:{guildUser.Id}",
                            Strings.CustomVoiceControlsAllowDesc(ctx.Guild.Id),
                            new Emoji("✅")
                        );

                        userSelect.AddOption(
                            Strings.CustomVoiceControlsDenyOption(ctx.Guild.Id, guildUser.Username),
                            $"deny:{guildUser.Id}",
                            Strings.CustomVoiceControlsDenyDesc(ctx.Guild.Id),
                            new Emoji("❌")
                        );
                    }
                }

                // Only add the select menu if there are options
                if (userSelect.Options.Count > 0)
                {
                    components.WithSelectMenu(userSelect, 2);
                }
            }

            // Update the message
            await message.ModifyAsync(props =>
            {
                props.Embed = embed.Build();
                props.Components = components.Build();
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error updating voice controls message for channel {ChannelId}", customChannel.ChannelId);
        }
    }
}

/// <summary>
///     Modal class for handling channel rename operations in Discord interactions.
///     Used to capture user input when renaming a custom voice channel.
/// </summary>
public class ModalRenameChannel
{
    /// <summary>
    ///     Gets or sets the new name for the voice channel.
    ///     This value is submitted by the user through the Discord modal interface.
    /// </summary>
    [InputLabel("Channel Name")]
    [ModalTextInput("channel_name")]
    public string ChannelName { get; set; }
}

/// <summary>
///     Modal class for handling user limit modifications in Discord interactions.
///     Used to capture user input when changing the maximum number of users allowed in a voice channel.
/// </summary>
public class ModalUserLimit
{
    /// <summary>
    ///     Gets or sets the new user limit for the voice channel.
    ///     This is a string value that will be parsed to an integer, with 0 representing unlimited users.
    ///     This value is submitted by the user through the Discord modal interface.
    /// </summary>
    [InputLabel("User Limit")]
    [ModalTextInput("user_limit")]
    public string UserLimit { get; set; }
}

/// <summary>
///     Modal class for handling bitrate modifications in Discord interactions.
///     Used to capture user input when changing the audio quality of a voice channel.
/// </summary>
public class ModalBitrate
{
    /// <summary>
    ///     Gets or sets the new bitrate for the voice channel in kbps.
    ///     This is a string value that will be parsed to an integer.
    ///     This value is submitted by the user through the Discord modal interface.
    /// </summary>
    [InputLabel("Bitrate")]
    [ModalTextInput("bitrate")]
    public string Bitrate { get; set; }
}
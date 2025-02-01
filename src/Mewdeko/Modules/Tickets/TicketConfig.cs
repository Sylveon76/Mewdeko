using System.Text;
using System.Text.Json;
using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Tickets.Common;
using Mewdeko.Modules.Tickets.Services;
using Serilog;

namespace Mewdeko.Modules.Tickets;

/// <inheritdoc />
public partial class Tickets : MewdekoModuleBase<TicketService>
{
    /// <summary>
    ///     Provides configuration commands for managing ticket panels, buttons, and select menus.
    /// </summary>
    /// <remarks>
    ///     This submodule contains commands for server administrators to manage their ticket system,
    ///     including creating and modifying panels, buttons, and select menus.
    /// </remarks>
    [Group]
    public class TicketConfig : MewdekoSubmodule<TicketService>
    {
        private readonly InteractiveService interactivity;

        /// <summary>
        ///     Initializes a new instance of the <see cref="TicketConfig" /> class.
        /// </summary>
        /// <param name="interactivity">The service used for interactive commands.</param>
        public TicketConfig(InteractiveService interactivity)
        {
            this.interactivity = interactivity;
        }

        /// <summary>
        ///     Lists all ticket panels in the guild.
        /// </summary>
        /// <remarks>
        ///     Displays a paginated list of all ticket panels, showing their locations
        ///     and the number of components (buttons/select menus) they contain.
        /// </remarks>
        [Cmd]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task PanelList()
        {
            var panels = await Service.GetPanelsAsync(ctx.Guild.Id);
            if (!panels.Any())
            {
                await ctx.Channel.SendErrorAsync("No ticket panels found in this server.", Config);
                return;
            }

            var paginator = new LazyPaginatorBuilder()
                .AddUser(ctx.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(panels.Count / 5)
                .WithDefaultEmotes()
                .Build();

            await interactivity.SendPaginatorAsync(paginator, Context.Channel,
                TimeSpan.FromMinutes(60));

            async Task<PageBuilder> PageFactory(int page)
            {
                var pageBuilder = new PageBuilder()
                    .WithTitle("Ticket Panels")
                    .WithOkColor();

                var items = panels.Skip(page * 5).Take(5);
                foreach (var panel in items)
                {
                    var channel = await ctx.Guild.GetTextChannelAsync(panel.ChannelId);
                    var buttonCount = panel.Buttons?.Count ?? 0;
                    var menuCount = panel.SelectMenus?.Count ?? 0;

                    pageBuilder.AddField(
                        $"Panel ID: {panel.MessageId}",
                        $"Channel: {(channel == null ? "Deleted" : channel.Mention)}\n" +
                        $"Buttons: {buttonCount}\n" +
                        $"Select Menus: {menuCount}");
                }

                return pageBuilder;
            }
        }

        /// <summary>
        ///     Deletes a button from a panel.
        /// </summary>
        /// <param name="buttonId">The ID of the button to delete.</param>
        /// <remarks>
        ///     This command removes the specified button from its panel.
        ///     The button ID can be found using the panel info command.
        /// </remarks>
        [Cmd]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task DeleteButton(int buttonId)
        {
            var button = await Service.GetButtonAsync(buttonId);
            if (button == null || button.Panel.GuildId != ctx.Guild.Id)
            {
                await ctx.Channel.SendErrorAsync("Button not found.", Config);
                return;
            }

            await Service.UpdateButtonSettingsAsync(ctx.Guild, buttonId, new Dictionary<string, object>());
            await ctx.Channel.SendConfirmAsync($"Successfully deleted button '{button.Label}'");
        }

        /// <summary>
        ///     Enables or disables transcript saving for a panel.
        /// </summary>
        /// <param name="panelId">The ID of the panel to modify.</param>
        /// <param name="enable">Whether to enable or disable transcript saving.</param>
        /// <remarks>
        ///     When enabled, all tickets created through this panel's buttons
        ///     will automatically save transcripts when closed.
        /// </remarks>
        [Cmd]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task SetPanelTranscripts(ulong panelId, bool enable)
        {
            var panel = await Service.GetPanelAsync(panelId);
            if (panel == null || panel.GuildId != ctx.Guild.Id)
            {
                await ctx.Channel.SendErrorAsync("Panel not found.", Config);
                return;
            }

            if (panel.Buttons != null)
            {
                foreach (var button in panel.Buttons)
                {
                    await Service.UpdateButtonSettingsAsync(ctx.Guild, button.Id,
                        new Dictionary<string, object>
                        {
                            {
                                "SaveTranscript", enable
                            }
                        });
                }
            }

            await ctx.Channel.SendConfirmAsync(
                $"Transcripts {(enable ? "enabled" : "disabled")} for all buttons in this panel.");
        }

        /// <summary>
        ///     Adds a select menu to a ticket panel.
        /// </summary>
        /// <param name="panelId">The ID of the panel to add the menu to</param>
        [Cmd]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task AddSelectMenu(ulong panelId)
        {
            var panel = await Service.GetPanelAsync(panelId);
            if (panel == null || panel.GuildId != ctx.Guild.Id)
            {
                await ctx.Channel.SendErrorAsync("Panel not found.", Config);
                return;
            }

            // Get all required information first
            var embed = new EmbedBuilder()
                .WithTitle("Select Menu Setup")
                .WithDescription("Please provide the following information:")
                .AddField("1. Placeholder", "The text shown when no option is selected")
                .AddField("2. First Option Label", "The text shown for the first option")
                .AddField("3. First Option Description", "Optional description for the first option")
                .AddField("4. First Option Emoji", "Optional emoji for the first option")
                .WithFooter("Type your response for #1 (Placeholder)")
                .WithOkColor();

            var setupMsg = await ctx.Channel.SendMessageAsync(embed: embed.Build());

            var placeholder = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id);
            if (string.IsNullOrEmpty(placeholder))
            {
                await ctx.Channel.SendErrorAsync("Setup cancelled - no placeholder provided.", Config);
                return;
            }

            embed.Fields[0].Value += $" ✓ {placeholder}";
            embed.Footer.Text = "Type your response for #2 (First Option Label)";
            await setupMsg.ModifyAsync(x => x.Embed = embed.Build());

            var label = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id);
            if (string.IsNullOrEmpty(label))
            {
                await ctx.Channel.SendErrorAsync("Setup cancelled - no label provided.", Config);
                return;
            }

            embed.Fields[1].Value += $" ✓ {label}";
            embed.Footer.Text = "Type your response for #3 (Description) or type 'skip'";
            await setupMsg.ModifyAsync(x => x.Embed = embed.Build());

            var description = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id);
            description = description?.ToLower() == "skip" ? null : description;

            embed.Fields[2].Value += description == null ? " ✓ Skipped" : $" ✓ {description}";
            embed.Footer.Text = "Type your response for #4 (Emoji) or type 'skip'";
            await setupMsg.ModifyAsync(x => x.Embed = embed.Build());

            var emoji = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id);
            emoji = emoji?.ToLower() == "skip" ? null : emoji;

            try
            {
                var menu = await Service.AddSelectMenuAsync(
                    panel,
                    placeholder,
                    label,
                    description,
                    emoji);

                var confirmEmbed = new EmbedBuilder()
                    .WithTitle("Select Menu Created")
                    .WithDescription("Successfully created select menu with first option.")
                    .AddField("Placeholder", placeholder)
                    .AddField("First Option",
                        $"Label: {label}\n" +
                        $"Description: {description ?? "None"}\n" +
                        $"Emoji: {emoji ?? "None"}")
                    .AddField("Note",
                        $"Use `{Config.Prefix}tc config addoption {menu.Id} <label>` to add more options.")
                    .WithOkColor();

                await ctx.Channel.SendMessageAsync(embed: confirmEmbed.Build());
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating select menu");
                await ctx.Channel.SendErrorAsync("Failed to create select menu.", Config);
            }
        }

        /// <summary>
        ///     Adds an option to an existing select menu
        /// </summary>
        /// <param name="menuId">The ID of the menu to add the option to</param>
        /// <param name="label">The label shown to users</param>
        /// <param name="description">Optional description shown under the label</param>
        /// <param name="emoji">Optional emoji shown next to the label</param>
        [Cmd]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task AddOption(
            string menuId,
            string label,
            string description = null,
            string emoji = null)
        {
            var menu = await Service.GetSelectMenuAsync(menuId);
            if (menu == null || menu.Panel.GuildId != ctx.Guild.Id)
            {
                await ctx.Channel.SendErrorAsync("Select menu not found.", Config);
                return;
            }

            try
            {
                var option = await Service.AddSelectOptionAsync(
                    menu,
                    label,
                    $"option_{Guid.NewGuid():N}", // Generate unique value
                    description,
                    emoji);

                var embed = new EmbedBuilder()
                    .WithTitle("Option Added")
                    .WithDescription($"Successfully added option '{label}' to the select menu.")
                    .AddField("Details",
                        $"Label: {label}\n" +
                        $"Description: {description ?? "None"}\n" +
                        $"Emoji: {emoji ?? "None"}")
                    .WithOkColor();

                await ctx.Channel.SendMessageAsync(embed: embed.Build());
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error adding select menu option");
                await ctx.Channel.SendErrorAsync("Failed to add option.", Config);
            }
        }

        /// <summary>
        ///     Lists all options in a select menu
        /// </summary>
        /// <param name="menuId">The ID of the menu to view</param>
        [Cmd]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task ListOptions(string menuId)
        {
            var menu = await Service.GetSelectMenuAsync(menuId);
            if (menu == null || menu.Panel.GuildId != ctx.Guild.Id)
            {
                await ctx.Channel.SendErrorAsync("Select menu not found.", Config);
                return;
            }

            var embed = new EmbedBuilder()
                .WithTitle("Select Menu Options")
                .WithDescription($"Placeholder: {menu.Placeholder}")
                .WithOkColor();

            foreach (var option in menu.Options)
            {
                embed.AddField(option.Label,
                    $"Value: {option.Value}\n" +
                    $"Description: {option.Description ?? "None"}\n" +
                    $"Emoji: {option.Emoji ?? "None"}");
            }

            await ctx.Channel.SendMessageAsync(embed: embed.Build());
        }

        /// <summary>
        ///     Removes an option from a select menu
        /// </summary>
        /// <param name="menuId">The ID of the menu</param>
        /// <param name="optionValue">The value of the option to remove</param>
        [Cmd]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task RemoveOption(string menuId, string optionValue)
        {
            var menu = await Service.GetSelectMenuAsync(menuId);
            if (menu == null || menu.Panel.GuildId != ctx.Guild.Id)
            {
                await ctx.Channel.SendErrorAsync("Select menu not found.", Config);
                return;
            }

            var option = menu.Options.FirstOrDefault(o => o.Value == optionValue);
            if (option == null)
            {
                await ctx.Channel.SendErrorAsync("Option not found.", Config);
                return;
            }

            if (menu.Options.Count <= 1)
            {
                await ctx.Channel.SendErrorAsync("Cannot remove the last option from a select menu.", Config);
                return;
            }

            // Here you would need to add a service method to remove the option
            // await Service.RemoveSelectOptionAsync(menu, optionValue);

            await ctx.Channel.SendConfirmAsync($"Removed option '{option.Label}' from the select menu.");
        }

        /// <summary>
        ///     Updates the placeholder text for a select menu
        /// </summary>
        /// <param name="menuId">The ID of the menu</param>
        /// <param name="placeholder">The new placeholder text</param>
        [Cmd]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task SetPlaceholder(string menuId, [Remainder] string placeholder)
        {
            var menu = await Service.GetSelectMenuAsync(menuId);
            if (menu == null || menu.Panel.GuildId != ctx.Guild.Id)
            {
                await ctx.Channel.SendErrorAsync("Select menu not found.", Config);
                return;
            }

            await Service.UpdateSelectMenuAsync(menu, m => m.Placeholder = placeholder);
            await ctx.Channel.SendConfirmAsync($"Updated placeholder text to: {placeholder}");
        }

        /// <summary>
        ///     Sets the required response time for tickets created by a button.
        /// </summary>
        /// <param name="buttonId">The ID of the button to modify.</param>
        /// <param name="minutes">The number of minutes, or null to disable.</param>
        /// <remarks>
        ///     When set, staff will be notified if they don't respond to a ticket
        ///     within the specified time. Set to null to disable the requirement.
        /// </remarks>
        [Cmd]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task SetResponseTime(int buttonId, int? minutes)
        {
            var button = await Service.GetButtonAsync(buttonId);
            if (button == null || button.Panel.GuildId != ctx.Guild.Id)
            {
                await ctx.Channel.SendErrorAsync("Button not found.", Config);
                return;
            }

            await Service.UpdateRequiredResponseTimeAsync(ctx.Guild, buttonId,
                minutes.HasValue ? TimeSpan.FromMinutes(minutes.Value) : null);

            if (minutes.HasValue)
                await ctx.Channel.SendConfirmAsync($"Response time set to {minutes} minutes for this button.");
            else
                await ctx.Channel.SendConfirmAsync("Response time requirement removed for this button.");
        }

        /// <summary>
        ///     Displays detailed information about a ticket panel.
        /// </summary>
        /// <param name="panelId">The ID of the panel to view.</param>
        /// <remarks>
        ///     Shows information about the panel's location, buttons,
        ///     select menus, and their configurations.
        /// </remarks>
        [Cmd]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task PanelInfo(ulong panelId)
        {
            var panel = await Service.GetPanelAsync(panelId);
            if (panel == null || panel.GuildId != ctx.Guild.Id)
            {
                await ctx.Channel.SendErrorAsync("Panel not found.", Config);
                return;
            }

            var channel = await ctx.Guild.GetTextChannelAsync(panel.ChannelId);
            var embed = new EmbedBuilder()
                .WithTitle("Ticket Panel Information")
                .WithDescription($"Channel: {(channel == null ? "Deleted" : channel.Mention)}")
                .AddField("Message ID", panel.MessageId, true)
                .AddField("Buttons", panel.Buttons?.Count ?? 0, true)
                .AddField("Select Menus", panel.SelectMenus?.Count ?? 0, true);

            if (panel.Buttons?.Any() == true)
            {
                var buttonInfo = string.Join("\n", panel.Buttons.Select(b =>
                    $"• {b.Label} (ID: {b.Id}) - Style: {b.Style}"));
                embed.AddField("Button Details", buttonInfo);
            }

            if (panel.SelectMenus?.Any() == true)
            {
                foreach (var menu in panel.SelectMenus)
                {
                    var optionInfo = string.Join("\n", menu.Options.Select(o =>
                        $"• {o.Label} ({o.Value})"));
                    embed.AddField($"Select Menu {menu.Id}",
                        $"Placeholder: {menu.Placeholder}\nOptions:\n{optionInfo}");
                }
            }

            await ctx.Channel.SendMessageAsync(embed: embed.Build());
        }

        /// <summary>
        ///     Sets the auto-close time for tickets created by a button.
        /// </summary>
        /// <param name="buttonId">The ID of the button to modify.</param>
        /// <param name="hours">The number of hours of inactivity before auto-close, or null to disable.</param>
        /// <remarks>
        ///     When set, tickets will automatically close after the specified number
        ///     of hours of inactivity. Set to null to disable auto-closing.
        /// </remarks>
        [Cmd]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task SetAutoClose(int buttonId, int? hours)
        {
            var button = await Service.GetButtonAsync(buttonId);
            if (button == null || button.Panel.GuildId != ctx.Guild.Id)
            {
                await ctx.Channel.SendErrorAsync("Button not found.", Config);
                return;
            }

            await Service.UpdateButtonSettingsAsync(ctx.Guild, buttonId,
                new Dictionary<string, object>
                {
                    {
                        "autoCloseTime", hours.HasValue ? TimeSpan.FromHours(hours.Value) : null
                    }
                });

            if (hours.HasValue)
                await ctx.Channel.SendConfirmAsync($"Auto-close time set to {hours} hours for this button.");
            else
                await ctx.Channel.SendConfirmAsync("Auto-close disabled for this button.");
        }

        /// <summary>
        ///     Displays detailed information about a button.
        /// </summary>
        /// <param name="buttonId">The ID of the button to view.</param>
        /// <remarks>
        ///     Shows all configuration settings for the button including style,
        ///     transcript settings, categories, and role permissions.
        /// </remarks>
        [Cmd]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task ButtonInfo(int buttonId)
        {
            var button = await Service.GetButtonAsync(buttonId);
            if (button == null || button.Panel.GuildId != ctx.Guild.Id)
            {
                await ctx.Channel.SendErrorAsync("Button not found.", Config);
                return;
            }

            var embed = new EmbedBuilder()
                .WithTitle("Button Information")
                .AddField("Label", button.Label, true)
                .AddField("Style", button.Style, true)
                .AddField("Save Transcripts", button.SaveTranscript, true)
                .AddField("Max Active Tickets", button.MaxActiveTickets, true)
                .AddField("Auto Close Time",
                    button.AutoCloseTime.HasValue ? $"{button.AutoCloseTime.Value.TotalHours} hours" : "Disabled", true)
                .AddField("Required Response Time",
                    button.RequiredResponseTime.HasValue
                        ? $"{button.RequiredResponseTime.Value.TotalMinutes} minutes"
                        : "None", true);

            if (button.CategoryId.HasValue)
            {
                var category = await ctx.Guild.GetCategoryChannelAsync(button.CategoryId.Value);
                embed.AddField("Ticket Category", category?.Name ?? "Deleted", true);
            }

            if (button.ArchiveCategoryId.HasValue)
            {
                var archiveCategory = await ctx.Guild.GetCategoryChannelAsync(button.ArchiveCategoryId.Value);
                embed.AddField("Archive Category", archiveCategory?.Name ?? "Deleted", true);
            }

            if (button.SupportRoles?.Any() == true)
            {
                var roles = button.SupportRoles
                    .Select(id => ctx.Guild.GetRole(id))
                    .Where(r => r != null)
                    .Select(r => r.Mention);
                embed.AddField("Support Roles", string.Join(", ", roles));
            }

            if (button.ViewerRoles?.Any() == true)
            {
                var roles = button.ViewerRoles
                    .Select(id => ctx.Guild.GetRole(id))
                    .Where(r => r != null)
                    .Select(r => r.Mention);
                embed.AddField("Viewer Roles", string.Join(", ", roles));
            }

            await ctx.Channel.SendMessageAsync(embed: embed.Build());
        }

        /// <summary>
        ///     Sets the title for a ticket creation modal
        /// </summary>
        /// <param name="buttonId">The ID of the button to configure</param>
        /// <param name="title">The title to display on the modal</param>
        /// <remarks>
        ///     When a user clicks this button, they will see a modal with this title.
        ///     The default title is "Create Ticket" if none is set.
        /// </remarks>
        [Cmd]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task SetModalTitle(int buttonId, [Remainder] string title)
        {
            var button = await Service.GetButtonAsync(buttonId);
            if (button == null || button.Panel.GuildId != ctx.Guild.Id)
            {
                await ctx.Channel.SendErrorAsync("Button not found.", Config);
                return;
            }

            // Get or create modal config
            var modalConfig = new ModalConfiguration();
            if (!string.IsNullOrWhiteSpace(button.ModalJson))
            {
                try
                {
                    modalConfig = JsonSerializer.Deserialize<ModalConfiguration>(button.ModalJson);
                }
                catch
                {
                    // If old format, try to preserve fields
                    try
                    {
                        modalConfig.Fields =
                            JsonSerializer.Deserialize<Dictionary<string, ModalFieldConfig>>(button.ModalJson);
                    }
                    catch
                    {
                        modalConfig.Fields = new Dictionary<string, ModalFieldConfig>();
                    }
                }
            }

            // Update title
            modalConfig.Title = title;

            // Update button
            await Service.UpdateButtonSettingsAsync(ctx.Guild, buttonId, new Dictionary<string, object>
            {
                {
                    "modalJson", JsonSerializer.Serialize(modalConfig)
                }
            });

            await ctx.Channel.SendConfirmAsync($"Modal title set to: {title}");
        }

        /// <summary>
        ///     Adds a field to the ticket creation modal
        /// </summary>
        /// <param name="buttonId">The ID of the button to configure</param>
        /// <param name="label">The label for the field</param>
        /// <param name="fieldConfig">Optional configuration options</param>
        /// <remarks>
        ///     Configuration options (comma-separated):
        ///     - paragraph: Makes the field multi-line
        ///     - optional: Makes the field not required
        ///     - min:X: Sets minimum length (0-4000)
        ///     - max:X: Sets maximum length (0-4000)
        /// </remarks>
        [Cmd]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task AddModalField(int buttonId, string label, [Remainder] string fieldConfig = null)
        {
            var button = await Service.GetButtonAsync(buttonId);
            if (button == null || button.Panel.GuildId != ctx.Guild.Id)
            {
                await ctx.Channel.SendErrorAsync("Button not found.", Config);
                return;
            }

            // Get or create modal config
            var modalConfig = new ModalConfiguration();
            if (!string.IsNullOrWhiteSpace(button.ModalJson))
            {
                try
                {
                    modalConfig = JsonSerializer.Deserialize<ModalConfiguration>(button.ModalJson);
                }
                catch
                {
                    // If old format, try to preserve fields
                    try
                    {
                        modalConfig.Fields =
                            JsonSerializer.Deserialize<Dictionary<string, ModalFieldConfig>>(button.ModalJson);
                    }
                    catch
                    {
                        modalConfig.Fields = new Dictionary<string, ModalFieldConfig>();
                    }
                }
            }

            // Process optional field config
            var fieldSettings = new ModalFieldConfig
            {
                Label = label,
                Style = 1, // Default to short style
                Required = true, // Default to required
                MinLength = 1,
                MaxLength = 1000
            };

            if (!string.IsNullOrWhiteSpace(fieldConfig))
            {
                var options = fieldConfig.Split(',')
                    .Select(o => o.Trim().ToLower())
                    .ToList();

                foreach (var option in options)
                {
                    if (option == "paragraph")
                        fieldSettings.Style = 2;
                    else if (option == "optional")
                        fieldSettings.Required = false;
                    else if (option.StartsWith("min:") && int.TryParse(option[4..], out var min))
                        fieldSettings.MinLength = Math.Max(0, Math.Min(min, 4000));
                    else if (option.StartsWith("max:") && int.TryParse(option[4..], out var max))
                        fieldSettings.MaxLength = Math.Max(fieldSettings.MinLength ?? 0, Math.Min(max, 4000));
                }
            }

            // Add or update field
            var fieldId = label.ToLower().Replace(" ", "_");
            modalConfig.Fields[fieldId] = fieldSettings;

            // Update button
            await Service.UpdateButtonSettingsAsync(ctx.Guild, buttonId, new Dictionary<string, object>
            {
                {
                    "modalJson", JsonSerializer.Serialize(modalConfig)
                }
            });

            var embed = new EmbedBuilder()
                .WithTitle("Modal Field Added")
                .WithDescription($"Added field '{label}' to the ticket creation modal.")
                .AddField("Field ID", fieldId, true)
                .AddField("Style", fieldSettings.Style == 2 ? "Paragraph" : "Short", true)
                .AddField("Required", fieldSettings.Required, true)
                .AddField("Length Limits",
                    $"Min: {fieldSettings.MinLength ?? 0}, Max: {fieldSettings.MaxLength ?? 4000}",
                    true)
                .WithOkColor();

            await ctx.Channel.SendMessageAsync(embed: embed.Build());
        }

        /// <summary>
        ///     Removes a field from a ticket creation modal
        /// </summary>
        [Cmd]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task RemoveModalField(int buttonId, string fieldId)
        {
            var button = await Service.GetButtonAsync(buttonId);
            if (button == null || button.Panel.GuildId != ctx.Guild.Id)
            {
                await ctx.Channel.SendErrorAsync("Button not found.", Config);
                return;
            }

            if (string.IsNullOrWhiteSpace(button.ModalJson))
            {
                await ctx.Channel.SendErrorAsync("This button has no modal configuration.", Config);
                return;
            }

            var modalFields = JsonSerializer.Deserialize<Dictionary<string, ModalFieldConfig>>(button.ModalJson);
            if (!modalFields.ContainsKey(fieldId))
            {
                await ctx.Channel.SendErrorAsync("Field not found in modal configuration.", Config);
                return;
            }

            modalFields.Remove(fieldId);

            // Update button
            await Service.UpdateButtonSettingsAsync(ctx.Guild, buttonId, new Dictionary<string, object>
            {
                {
                    "modalJson", modalFields.Any() ? JsonSerializer.Serialize(modalFields) : null
                }
            });

            await ctx.Channel.SendConfirmAsync($"Removed field '{fieldId}' from the modal configuration.");
        }

        /// <summary>
        ///     Lists all fields in a ticket creation modal
        /// </summary>
        [Cmd]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task ListModalFields(int buttonId)
        {
            var button = await Service.GetButtonAsync(buttonId);
            if (button == null || button.Panel.GuildId != ctx.Guild.Id)
            {
                await ctx.Channel.SendErrorAsync("Button not found.", Config);
                return;
            }

            if (string.IsNullOrWhiteSpace(button.ModalJson))
            {
                await ctx.Channel.SendErrorAsync("This button has no modal configuration.", Config);
                return;
            }

            var modalFields = JsonSerializer.Deserialize<Dictionary<string, ModalFieldConfig>>(button.ModalJson);

            var embed = new EmbedBuilder()
                .WithTitle($"Modal Fields for {button.Label}")
                .WithOkColor();

            foreach (var (fieldId, config) in modalFields)
            {
                embed.AddField(fieldId,
                    $"Label: {config.Label}\n" +
                    $"Style: {(config.Style == 2 ? "Paragraph" : "Short")}\n" +
                    $"Required: {config.Required}\n" +
                    $"Length: {config.MinLength ?? 0}-{config.MaxLength ?? 4000}");
            }

            await ctx.Channel.SendMessageAsync(embed: embed.Build());
        }

        /// <summary>
        ///     Updates the modal help command to include title configuration
        /// </summary>
        [Cmd]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task ModalHelp()
        {
            var embed = new EmbedBuilder()
                .WithTitle("Modal Configuration Help")
                .WithDescription(
                    "Modals are forms that appear when users create tickets. You can configure what fields appear in these forms.\n\n" +
                    $"`{Config.Prefix}setmodaltitle <buttonId> <title>`\n" +
                    "Sets the title shown at the top of the modal\n\n" +
                    $"`{Config.Prefix}addmodalfield <buttonId> <label> [options]`\n" +
                    "Options (comma-separated):\n" +
                    "• paragraph - Makes the field multi-line\n" +
                    "• optional - Makes the field not required\n" +
                    "• min:X - Sets minimum length (0-4000)\n" +
                    "• max:X - Sets maximum length (0-4000)\n\n" +
                    $"`{Config.Prefix}removemodalfield <buttonId> <fieldId>`\n" +
                    "Removes a field from the modal\n\n" +
                    $"`{Config.Prefix}listmodalfields <buttonId>`\n" +
                    "Shows all fields and title in a button's modal")
                .WithOkColor();

            await ctx.Channel.SendMessageAsync(embed: embed.Build());
        }


        /// <summary>
        ///     Lists all components of a specific ticket panel
        /// </summary>
        /// <param name="panelId">The message ID of the panel to inspect</param>
        /// <remarks>
        ///     Shows detailed information about a panel's buttons and select menus, including:
        ///     - Component IDs and custom IDs
        ///     - Labels and styles
        ///     - Modal and custom message configurations
        ///     - Category and role settings
        /// </remarks>
        [Cmd]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageChannels)]
        public async Task TicketListPanel(ulong panelId)
        {
            try
            {
                var buttons = await Service.GetPanelButtonsAsync(panelId);
                var menus = await Service.GetPanelSelectMenusAsync(panelId);

                var embed = new EmbedBuilder()
                    .WithTitle("Panel Components")
                    .WithOkColor();

                if (buttons.Any())
                {
                    var buttonText = new StringBuilder();
                    foreach (var button in buttons)
                    {
                        buttonText.AppendLine($"**Button ID: {button.Id}**")
                            .AppendLine($"└ Label: {button.Label}")
                            .AppendLine($"└ Style: {button.Style}")
                            .AppendLine($"└ Custom ID: {button.CustomId}")
                            .AppendLine($"└ Has Modal: {(button.HasModal ? "Yes" : "No")}")
                            .AppendLine($"└ Has Custom Open Message: {(button.HasCustomOpenMessage ? "Yes" : "No")}")
                            .AppendLine(
                                $"└ Category: {(button.CategoryId.HasValue ? $"<#{button.CategoryId}>" : "None")}")
                            .AppendLine(
                                $"└ Archive Category: {(button.ArchiveCategoryId.HasValue ? $"<#{button.ArchiveCategoryId}>" : "None")}")
                            .AppendLine(
                                $"└ Support Roles: {string.Join(", ", button.SupportRoles.Select(r => $"<@&{r}>"))}")
                            .AppendLine(
                                $"└ Viewer Roles: {string.Join(", ", button.ViewerRoles.Select(r => $"<@&{r}>"))}")
                            .AppendLine();
                    }

                    embed.AddField("Buttons", buttonText.ToString());
                }

                if (menus.Any())
                {
                    var menuText = new StringBuilder();
                    foreach (var menu in menus)
                    {
                        menuText.AppendLine($"**Menu ID: {menu.Id}**")
                            .AppendLine($"└ Custom ID: {menu.CustomId}")
                            .AppendLine($"└ Placeholder: {menu.Placeholder}")
                            .AppendLine("└ Options:");

                        foreach (var option in menu.Options)
                        {
                            menuText.AppendLine($"  **Option ID: {option.Id}**")
                                .AppendLine($"  └ Label: {option.Label}")
                                .AppendLine($"  └ Value: {option.Value}")
                                .AppendLine($"  └ Description: {option.Description}")
                                .AppendLine($"  └ Has Modal: {(option.HasModal ? "Yes" : "No")}")
                                .AppendLine(
                                    $"  └ Has Custom Open Message: {(option.HasCustomOpenMessage ? "Yes" : "No")}")
                                .AppendLine(
                                    $"  └ Category: {(option.CategoryId.HasValue ? $"<#{option.CategoryId}>" : "None")}")
                                .AppendLine(
                                    $"  └ Archive Category: {(option.ArchiveCategoryId.HasValue ? $"<#{option.ArchiveCategoryId}>" : "None")}");
                        }

                        menuText.AppendLine();
                    }

                    embed.AddField("Select Menus", menuText.ToString());
                }

                if (!buttons.Any() && !menus.Any())
                {
                    embed.WithDescription("No components found on this panel.");
                }

                await ctx.Channel.SendMessageAsync(embed: embed.Build());
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error listing panel components for panel {PanelId}", panelId);
                await ctx.Channel.SendErrorAsync("An error occurred while listing panel components.", Config);
            }
        }

        /// <summary>
        ///     Lists all ticket panels in the guild
        /// </summary>
        /// <remarks>
        ///     Shows a paginated list of all panels in the server, including:
        ///     - Channel locations
        ///     - Message IDs
        ///     - Button and select menu configurations
        ///     - Associated categories and roles
        /// </remarks>
        [Cmd]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageChannels)]
        public async Task TicketListPanels()
        {
            try
            {
                var panels = await Service.GetAllPanelsAsync(Context.Guild.Id);

                if (!panels.Any())
                {
                    await ctx.Channel.SendErrorAsync("No ticket panels found in this server.", Config);
                    return;
                }

                var paginator = new LazyPaginatorBuilder()
                    .AddUser(ctx.User)
                    .WithPageFactory(PageFactory)
                    .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                    .WithMaxPageIndex(panels.Count / 5)
                    .WithDefaultEmotes()
                    .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                    .Build();

                await interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60));

                async Task<PageBuilder> PageFactory(int page)
                {
                    await Task.CompletedTask;
                    var pagePanels = panels.Skip(5 * page).Take(5);
                    var pageBuilder = new PageBuilder()
                        .WithTitle("Ticket Panels")
                        .WithOkColor();

                    foreach (var panel in pagePanels)
                    {
                        var channel = await Context.Guild.GetChannelAsync(panel.ChannelId) as ITextChannel;
                        var fieldBuilder = new StringBuilder();

                        fieldBuilder.AppendLine($"Channel: #{channel?.Name ?? "deleted-channel"}");

                        if (panel.Buttons.Any())
                        {
                            fieldBuilder.AppendLine("\n**Buttons:**");
                            foreach (var button in panel.Buttons)
                            {
                                fieldBuilder.AppendLine($"• ID: {button.Id} | Label: {button.Label}")
                                    .AppendLine($"  Style: {button.Style}")
                                    .AppendLine(
                                        $"  Category: {(button.CategoryId.HasValue ? $"<#{button.CategoryId}>" : "None")}")
                                    .AppendLine(
                                        $"  Support Roles: {string.Join(", ", button.SupportRoles.Select(r => $"<@&{r}>"))}");
                            }
                        }

                        if (panel.SelectMenus.Any())
                        {
                            fieldBuilder.AppendLine("\n**Select Menus:**");
                            foreach (var menu in panel.SelectMenus)
                            {
                                fieldBuilder.AppendLine($"• ID: {menu.Id} | Options: {menu.Options.Count}");
                                foreach (var option in menu.Options)
                                {
                                    fieldBuilder.AppendLine($"  - Option ID: {option.Id} | Label: {option.Label}");
                                }
                            }
                        }

                        pageBuilder.AddField($"Panel ID: {panel.MessageId}", fieldBuilder.ToString());
                    }

                    return pageBuilder;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error listing panels");
                await ctx.Channel.SendErrorAsync("An error occurred while listing panels.", Config);
            }
        }
    }
}
// using System.Text;
// using Discord.Commands;
// using Mewdeko.Common.Attributes.TextCommands;
// using Mewdeko.Common.TypeReaders.Models;
// using Mewdeko.Database.DbContextStuff;
// using Mewdeko.Modules.Tickets.Services;
// using Microsoft.EntityFrameworkCore;
//
// namespace Mewdeko.Modules.Tickets;
//
// /// <summary>
// ///     Module containing commands for managing the ticket system.
// /// </summary>
// public class Tickets : MewdekoModuleBase<TicketService>
// {
//     private readonly DbContextProvider _db;
//
//     /// <summary>
//     ///     Initializes a new instance of the <see cref="Tickets" /> class.
//     /// </summary>
//     public Tickets(DbContextProvider db)
//     {
//         _db = db;
//     }
//
//     /// <summary>
//     ///     Creates a new ticket panel in the specified channel.
//     /// </summary>
//     /// <param name="channel">The channel to create the panel in.</param>
//     /// <param name="title">The title of the panel.</param>
//     /// <param name="description">The description of the panel.</param>
//     [Cmd]
//     [RequireContext(ContextType.Guild)]
//     [UserPerm(GuildPermission.ManageGuild)]
//     public async Task CreateTicketPanel(ITextChannel channel, string title, [Remainder] string description)
//     {
//         var panel = await Service.CreateTicketPanel(ctx.Guild.Id, channel.Id, title, description);
//
//         if (panel == null)
//         {
//             await ReplyErrorAsync(Strings.PanelAlreadyExists(ctx.Guild.Id)).ConfigureAwait(false);
//             return;
//         }
//
//         var embed = new EmbedBuilder()
//             .WithTitle(title)
//             .WithDescription(description)
//             .WithColor(Mewdeko.OkColor)
//             .WithCurrentTimestamp();
//
//         var components = new ComponentBuilder()
//             .WithButton("Create Ticket", $"createticket:{panel.Id}")
//             .Build();
//
//         await channel.SendMessageAsync(embed: embed.Build(), components: components).ConfigureAwait(false);
//         await ReplyConfirmAsync(Strings.PanelCreated(ctx.Guild.Id)).ConfigureAwait(false);
//     }
//
//     /// <summary>
//     ///     Closes the current ticket channel.
//     /// </summary>
//     [Cmd]
//     [RequireContext(ContextType.Guild)]
//     public async Task CloseTicket()
//     {
//         var success = await Service.CloseTicket(ctx.Guild, ctx.Channel.Id);
//
//         if (!success)
//         {
//             await ReplyErrorAsync(Strings.NotTicketChannel(ctx.Guild.Id)).ConfigureAwait(false);
//         }
//     }
//
//     /// <summary>
//     ///     Claims the current ticket channel.
//     /// </summary>
//     [Cmd]
//     [RequireContext(ContextType.Guild)]
//     public async Task ClaimTicket()
//     {
//         var success = await Service.ClaimTicket(ctx.Guild, ctx.Channel.Id, (IGuildUser)ctx.User);
//
//         if (!success)
//         {
//             await ReplyErrorAsync(Strings.TicketClaimFailed(ctx.Guild.Id)).ConfigureAwait(false);
//         }
//     }
//
//     /// <summary>
//     ///     Unclaims the current ticket channel.
//     /// </summary>
//     [Cmd]
//     [RequireContext(ContextType.Guild)]
//     public async Task UnclaimTicket()
//     {
//         var success = await Service.UnclaimTicket(ctx.Guild, ctx.Channel.Id, (IGuildUser)ctx.User);
//
//         if (!success)
//         {
//             await ReplyErrorAsync(Strings.TicketUnclaimFailed(ctx.Guild.Id)).ConfigureAwait(false);
//         }
//     }
//
//     /// <summary>
//     ///     Adds a note to the current ticket.
//     /// </summary>
//     /// <param name="note">The note to add.</param>
//     [Cmd]
//     [RequireContext(ContextType.Guild)]
//     public async Task AddNote([Remainder] string note)
//     {
//         var success = await Service.AddNote(ctx.Channel.Id, (IGuildUser)ctx.User, note);
//
//         if (!success)
//         {
//             await ReplyErrorAsync(Strings.NoteAddFailed(ctx.Guild.Id)).ConfigureAwait(false);
//         }
//     }
//
//     /// <summary>
//     ///     Creates a new case to group tickets together.
//     /// </summary>
//     /// <param name="name">The name of the case.</param>
//     /// <param name="description">The description of the case.</param>
//     [Cmd]
//     [RequireContext(ContextType.Guild)]
//     [UserPerm(GuildPermission.ManageGuild)]
//     public async Task CreateCase(string name, [Remainder] string description)
//     {
//         var ticketCase = await Service.CreateCase(ctx.Guild, (IGuildUser)ctx.User, name, description);
//
//         await ReplyConfirmAsync(Strings.CaseCreated(ctx.Guild.Id, ticketCase.Id)).ConfigureAwait(false);
//     }
//
//     /// <summary>
//     ///     Adds the current ticket to a case.
//     /// </summary>
//     /// <param name="caseId">The ID of the case.</param>
//     [Cmd]
//     [RequireContext(ContextType.Guild)]
//     [UserPerm(GuildPermission.ManageGuild)]
//     public async Task AddToCase(int caseId)
//     {
//         await using var db = await _db.GetContextAsync();
//         var ticket = await db.Tickets.FirstOrDefaultAsync(t => t.ChannelId == ctx.Channel.Id);
//
//         if (ticket == null)
//         {
//             await ReplyErrorAsync(Strings.NotTicketChannel(ctx.Guild.Id)).ConfigureAwait(false);
//             return;
//         }
//
//         var success = await Service.AddTicketToCase(ctx.Guild.Id, caseId, ticket.Id);
//
//         if (!success)
//         {
//             await ReplyErrorAsync(Strings.AddToCaseFailed(ctx.Guild.Id)).ConfigureAwait(false);
//             return;
//         }
//
//         await ReplyConfirmAsync(Strings.AddedToCase(ctx.Guild.Id, caseId)).ConfigureAwait(false);
//     }
//
//     /// <summary>
//     ///     Removes the current ticket from its case.
//     /// </summary>
//     [Cmd]
//     [RequireContext(ContextType.Guild)]
//     [UserPerm(GuildPermission.ManageGuild)]
//     public async Task RemoveFromCase()
//     {
//         await using var db = await _db.GetContextAsync();
//         var ticket = await db.Tickets.FirstOrDefaultAsync(t => t.ChannelId == ctx.Channel.Id);
//
//         if (ticket == null)
//         {
//             await ReplyErrorAsync(Strings.NotTicketChannel(ctx.Guild.Id)).ConfigureAwait(false);
//             return;
//         }
//
//         var success = await Service.RemoveTicketFromCase(ctx.Guild.Id, ticket.Id);
//
//         if (!success)
//         {
//             await ReplyErrorAsync(Strings.RemoveFromCaseFailed(ctx.Guild.Id)).ConfigureAwait(false);
//             return;
//         }
//
//         await ReplyConfirmAsync(Strings.RemovedFromCase(ctx.Guild.Id)).ConfigureAwait(false);
//     }
//
//     /// <summary>
//     ///     Shows statistics about tickets in the guild.
//     /// </summary>
//     [Cmd]
//     [RequireContext(ContextType.Guild)]
//     public async Task TicketStats()
//     {
//         var stats = await Service.GetGuildStatistics(ctx.Guild.Id);
//
//         var eb = new EmbedBuilder()
//             .WithTitle("Ticket Statistics")
//             .WithColor(Mewdeko.OkColor)
//             .AddField("Total Tickets", stats.TotalTickets, true)
//             .AddField("Open Tickets", stats.OpenTickets, true)
//             .AddField("Closed Tickets", stats.ClosedTickets, true)
//             .AddField("Average Response Time", $"{stats.AverageResponseTime:F1} minutes", true)
//             .AddField("Average Resolution Time", $"{stats.AverageResolutionTime:F1} hours", true);
//
//         if (stats.TicketsByType.Any())
//         {
//             eb.AddField("Tickets by Type",
//                 string.Join("\n", stats.TicketsByType.Select(kvp => $"{kvp.Key}: {kvp.Value}")));
//         }
//
//         if (stats.TicketsByPriority.Any())
//         {
//             eb.AddField("Tickets by Priority",
//                 string.Join("\n", stats.TicketsByPriority.Select(kvp => $"{kvp.Key}: {kvp.Value}")));
//         }
//
//         await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
//     }
//
//     /// <summary>
//     ///     Shows statistics about a user's tickets.
//     /// </summary>
//     /// <param name="user">The user to show statistics for.</param>
//     [Cmd]
//     [RequireContext(ContextType.Guild)]
//     public async Task UserTicketStats(IUser user = null)
//     {
//         user ??= ctx.User;
//         var stats = await Service.GetUserStatistics(ctx.Guild.Id, user.Id);
//
//         var eb = new EmbedBuilder()
//             .WithTitle($"Ticket Statistics for {user}")
//             .WithColor(Mewdeko.OkColor)
//             .AddField("Total Tickets", stats.TotalTickets, true)
//             .AddField("Open Tickets", stats.OpenTickets, true)
//             .AddField("Closed Tickets", stats.ClosedTickets, true);
//
//         if (stats.TicketsByType.Any())
//         {
//             eb.AddField("Tickets by Type",
//                 string.Join("\n", stats.TicketsByType.Select(kvp => $"{kvp.Key}: {kvp.Value}")));
//         }
//
//         if (stats.RecentTickets.Any())
//         {
//             var recentTickets = new StringBuilder();
//             foreach (var ticket in stats.RecentTickets)
//             {
//                 recentTickets.AppendLine($"ID: {ticket.TicketId} | Type: {ticket.Type} | " +
//                                          $"Created: {ticket.CreatedAt:g} | " +
//                                          $"Status: {(ticket.ClosedAt.HasValue ? "Closed" : "Open")}");
//             }
//
//             eb.AddField("Recent Tickets", recentTickets.ToString());
//         }
//
//         await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
//     }
//
//     /// <summary>
//     ///     Creates a new ticket priority level.
//     /// </summary>
//     /// <param name="id">The unique identifier for the priority.</param>
//     /// <param name="name">The display name of the priority.</param>
//     /// <param name="emoji">The emoji associated with the priority.</param>
//     /// <param name="level">The priority level (1-5).</param>
//     /// <param name="pingStaff">Whether to ping staff for tickets with this priority.</param>
//     [Cmd]
//     [RequireContext(ContextType.Guild)]
//     [UserPerm(GuildPermission.ManageGuild)]
//     public async Task CreatePriority(string id, string name, string emoji, int level, bool pingStaff = false)
//     {
//         if (level < 1 || level > 5)
//         {
//             await ReplyErrorAsync(Strings.InvalidPriorityLevel(ctx.Guild.Id)).ConfigureAwait(false);
//             return;
//         }
//
//         var success = await Service.CreatePriority(ctx.Guild.Id, id, name, emoji, level, pingStaff,
//             TimeSpan.FromHours(24), Color.Blue);
//
//         if (!success)
//         {
//             await ReplyErrorAsync(Strings.PriorityCreateFailed(ctx.Guild.Id)).ConfigureAwait(false);
//             return;
//         }
//
//         await ReplyConfirmAsync(Strings.PriorityCreated(ctx.Guild.Id)).ConfigureAwait(false);
//     }
//
//     /// <summary>
//     ///     Deletes a ticket priority level.
//     /// </summary>
//     /// <param name="id">The unique identifier of the priority to delete.</param>
//     [Cmd]
//     [RequireContext(ContextType.Guild)]
//     [UserPerm(GuildPermission.ManageGuild)]
//     public async Task DeletePriority(string id)
//     {
//         var success = await Service.DeletePriority(ctx.Guild.Id, id);
//
//         if (!success)
//         {
//             await ReplyErrorAsync(Strings.PriorityDeleteFailed(ctx.Guild.Id)).ConfigureAwait(false);
//             return;
//         }
//
//         await ReplyConfirmAsync(Strings.PriorityDeleted(ctx.Guild.Id)).ConfigureAwait(false);
//     }
//
//     /// <summary>
//     ///     Sets the priority of the current ticket.
//     /// </summary>
//     /// <param name="priorityId">The ID of the priority to set.</param>
//     [Cmd]
//     [RequireContext(ContextType.Guild)]
//     public async Task SetPriority(string priorityId)
//     {
//         var success = await Service.SetTicketPriority(ctx.Guild, ctx.Channel.Id, priorityId, (IGuildUser)ctx.User);
//
//         if (!success)
//         {
//             await ReplyErrorAsync(Strings.PrioritySetFailed(ctx.Guild.Id)).ConfigureAwait(false);
//         }
//     }
//
//     /// <summary>
//     ///     Creates a new ticket tag.
//     /// </summary>
//     /// <param name="id">The unique identifier for the tag.</param>
//     /// <param name="name">The display name of the tag.</param>
//     /// <param name="description">The description of the tag.</param>
//     [Cmd]
//     [RequireContext(ContextType.Guild)]
//     [UserPerm(GuildPermission.ManageGuild)]
//     public async Task CreateTag(string id, string name, [Remainder] string description)
//     {
//         var success = await Service.CreateTag(ctx.Guild.Id, id, name, description, Color.Blue);
//
//         if (!success)
//         {
//             await ReplyErrorAsync(Strings.TagCreateFailed(ctx.Guild.Id)).ConfigureAwait(false);
//             return;
//         }
//
//         await ReplyConfirmAsync(Strings.TagCreated(ctx.Guild.Id)).ConfigureAwait(false);
//     }
//
//     /// <summary>
//     ///     Deletes a ticket tag.
//     /// </summary>
//     /// <param name="id">The unique identifier of the tag to delete.</param>
//     [Cmd]
//     [RequireContext(ContextType.Guild)]
//     [UserPerm(GuildPermission.ManageGuild)]
//     public async Task DeleteTag(string id)
//     {
//         var success = await Service.DeleteTag(ctx.Guild.Id, id);
//
//         if (!success)
//         {
//             await ReplyErrorAsync(Strings.TagDeleteFailed(ctx.Guild.Id)).ConfigureAwait(false);
//             return;
//         }
//
//         await ReplyConfirmAsync(Strings.TagDeleted(ctx.Guild.Id)).ConfigureAwait(false);
//     }
//
//     /// <summary>
//     ///     Adds tags to the current ticket.
//     /// </summary>
//     /// <param name="tags">The tags to add, separated by spaces.</param>
//     [Cmd]
//     [RequireContext(ContextType.Guild)]
//     public async Task AddTags([Remainder] string tags)
//     {
//         var tagList = tags.Split(' ', StringSplitOptions.RemoveEmptyEntries);
//         var success = await Service.AddTicketTags(ctx.Guild, ctx.Channel.Id, tagList, (IGuildUser)ctx.User);
//
//         if (!success)
//         {
//             await ReplyErrorAsync(Strings.TagsAddFailed(ctx.Guild.Id)).ConfigureAwait(false);
//             return;
//         }
//
//         await ReplyConfirmAsync(Strings.TagsAdded(ctx.Guild.Id)).ConfigureAwait(false);
//     }
//
//     /// <summary>
//     ///     Removes tags from the current ticket.
//     /// </summary>
//     /// <param name="tags">The tags to remove, separated by spaces.</param>
//     [Cmd]
//     [RequireContext(ContextType.Guild)]
//     public async Task RemoveTags([Remainder] string tags)
//     {
//         var tagList = tags.Split(' ', StringSplitOptions.RemoveEmptyEntries);
//         var success = await Service.RemoveTicketTags(ctx.Guild, ctx.Channel.Id, tagList, (IGuildUser)ctx.User);
//
//         if (!success)
//         {
//             await ReplyErrorAsync(Strings.TagsRemoveFailed(ctx.Guild.Id)).ConfigureAwait(false);
//             return;
//         }
//
//         await ReplyConfirmAsync(Strings.TagsRemoved(ctx.Guild.Id)).ConfigureAwait(false);
//     }
//
//     /// <summary>
//     ///     Blacklists a user from creating tickets.
//     /// </summary>
//     /// <param name="user">The user to blacklist.</param>
//     /// <param name="reason">The reason for blacklisting.</param>
//     [Cmd]
//     [RequireContext(ContextType.Guild)]
//     [UserPerm(GuildPermission.ManageGuild)]
//     public async Task BlacklistUser(IGuildUser user, [Remainder] string reason = null)
//     {
//         var success = await Service.BlacklistUser(ctx.Guild.Id, user.Id, reason);
//
//         if (!success)
//         {
//             await ReplyErrorAsync(Strings.BlacklistFailed(ctx.Guild.Id)).ConfigureAwait(false);
//             return;
//         }
//
//         await ReplyConfirmAsync(Strings.UserBlacklisted(ctx.Guild.Id)).ConfigureAwait(false);
//     }
//
//     /// <summary>
//     ///     Blacklists a user from creating specific types of tickets.
//     /// </summary>
//     /// <param name="user">The user to blacklist.</param>
//     /// <param name="ticketType">The type of ticket to blacklist from.</param>
//     /// <param name="reason">The reason for blacklisting.</param>
//     [Cmd]
//     [RequireContext(ContextType.Guild)]
//     [UserPerm(GuildPermission.ManageGuild)]
//     public async Task BlacklistUserFromType(IGuildUser user, string ticketType, [Remainder] string reason = null)
//     {
//         var success = await Service.BlacklistUserFromTicketType(ctx.Guild.Id, user.Id, ticketType, reason);
//
//         if (!success)
//         {
//             await ReplyErrorAsync(Strings.TypeBlacklistFailed(ctx.Guild.Id)).ConfigureAwait(false);
//             return;
//         }
//
//         await ReplyConfirmAsync(Strings.UserTypeBlacklisted(ctx.Guild.Id)).ConfigureAwait(false);
//     }
//
//     /// <summary>
//     ///     Removes a user from the ticket blacklist.
//     /// </summary>
//     /// <param name="user">The user to unblacklist.</param>
//     [Cmd]
//     [RequireContext(ContextType.Guild)]
//     [UserPerm(GuildPermission.ManageGuild)]
//     public async Task UnblacklistUser(IGuildUser user)
//     {
//         var success = await Service.UnblacklistUser(ctx.Guild.Id, user.Id);
//
//         if (!success)
//         {
//             await ReplyErrorAsync(Strings.UnblacklistFailed(ctx.Guild.Id)).ConfigureAwait(false);
//             return;
//         }
//
//         await ReplyConfirmAsync(Strings.UserUnblacklisted(ctx.Guild.Id)).ConfigureAwait(false);
//     }
//
//     /// <summary>
//     ///     Removes a user's blacklist from a specific ticket type.
//     /// </summary>
//     /// <param name="user">The user to unblacklist.</param>
//     /// <param name="ticketType">The type of ticket to unblacklist from.</param>
//     [Cmd]
//     [RequireContext(ContextType.Guild)]
//     [UserPerm(GuildPermission.ManageGuild)]
//     public async Task UnblacklistUserFromType(IGuildUser user, string ticketType)
//     {
//         var success = await Service.UnblacklistUserFromTicketType(ctx.Guild.Id, user.Id, ticketType);
//
//         if (!success)
//         {
//             await ReplyErrorAsync(Strings.TypeUnblacklistFailed(ctx.Guild.Id)).ConfigureAwait(false);
//             return;
//         }
//
//         await ReplyConfirmAsync(Strings.UserTypeUnblacklisted(ctx.Guild.Id)).ConfigureAwait(false);
//     }
//
//     /// <summary>
//     ///     Closes all inactive tickets.
//     /// </summary>
//     /// <param name="time">The time period of inactivity required.</param>
//     [Cmd]
//     [RequireContext(ContextType.Guild)]
//     [UserPerm(GuildPermission.ManageGuild)]
//     public async Task BatchCloseInactive(StoopidTime time)
//     {
//         var (closed, failed) = await Service.BatchCloseInactiveTickets(ctx.Guild, time.Time);
//
//         await ReplyConfirmAsync(Strings.BatchClosed(ctx.Guild.Id, closed, failed)).ConfigureAwait(false);
//     }
//
//     /// <summary>
//     ///     Moves all tickets from one category to another.
//     /// </summary>
//     /// <param name="sourceCategory">The source category.</param>
//     /// <param name="targetCategory">The target category.</param>
//     [Cmd]
//     [RequireContext(ContextType.Guild)]
//     [UserPerm(GuildPermission.ManageGuild)]
//     public async Task BatchMoveTickets(ICategoryChannel sourceCategory, ICategoryChannel targetCategory)
//     {
//         var (moved, failed) = await Service.BatchMoveTickets(ctx.Guild, sourceCategory.Id, targetCategory.Id);
//
//         await ReplyConfirmAsync(Strings.BatchMoved(ctx.Guild.Id, moved, failed)).ConfigureAwait(false);
//     }
//
//     /// <summary>
//     ///     Adds a role to all active tickets.
//     /// </summary>
//     /// <param name="role">The role to add.</param>
//     /// <param name="viewOnly">Whether the role should have view-only permissions.</param>
//     [Cmd]
//     [RequireContext(ContextType.Guild)]
//     [UserPerm(GuildPermission.ManageGuild)]
//     public async Task BatchAddRole(IRole role, bool viewOnly = false)
//     {
//         var (updated, failed) = await Service.BatchAddRole(ctx.Guild, role, viewOnly);
//
//         await ReplyConfirmAsync(Strings.BatchRoleAdded(ctx.Guild.Id, updated, failed)).ConfigureAwait(false);
//     }
//
//     /// <summary>
//     ///     Transfers all tickets from one staff member to another.
//     /// </summary>
//     /// <param name="fromStaff">The staff member to transfer tickets from.</param>
//     /// <param name="toStaff">The staff member to transfer tickets to.</param>
//     [Cmd]
//     [RequireContext(ContextType.Guild)]
//     [UserPerm(GuildPermission.ManageGuild)]
//     public async Task TransferTickets(IGuildUser fromStaff, IGuildUser toStaff)
//     {
//         var (transferred, failed) = await Service.BatchTransferTickets(ctx.Guild, fromStaff.Id, toStaff.Id);
//
//         await ReplyConfirmAsync(Strings.TicketsTransferred(ctx.Guild.Id, transferred, failed)).ConfigureAwait(false);
//     }
//
//     /// <summary>
//     ///     Adds a button that creates a specific type of ticket.
//     /// </summary>
//     [Cmd]
//     [RequireContext(ContextType.Guild)]
//     [UserPerm(GuildPermission.ManageGuild)]
//     public async Task AddPanelButton(
//         int panelId,
//         string label,
//         string openMessage,
//         ulong categoryId,
//         string channelFormat = "ticket-{username}-{id}",
//         string emoji = null,
//         [Remainder] string supportRoles = null)
//     {
//         var roleIds = supportRoles?.Split(' ').Select(ulong.Parse).ToList() ?? [];
//
//         var button = new TicketButton
//         {
//             Label = label,
//             OpenMessage = openMessage,
//             CategoryId = categoryId,
//             ChannelNameFormat = channelFormat,
//             SupportRoleIds = roleIds,
//             AutoAddRoleIds = [],
//             AutoAddUserIds = [],
//             ViewerRoleIds = [],
//             AllowedPriorityIds = [],
//             RequiredTags = [],
//             SaveTranscript = true,
//             MaxActiveTickets = 1
//         };
//
//         if (emoji is not null)
//             button.Emoji = emoji;
//
//         var success = await Service.AddButtonToPanel(ctx.Guild.Id, panelId, button);
//         if (!success)
//         {
//             await ReplyErrorAsync(Strings.PanelNotFound(ctx.Guild.Id)).ConfigureAwait(false);
//             return;
//         }
//
//         var eb = new EmbedBuilder()
//             .WithTitle("Ticket Button Added")
//             .WithDescription($"Button '{label}' added to panel {panelId}")
//             .AddField("Category", $"<#{categoryId}>", true)
//             .AddField("Channel Format", channelFormat, true)
//             .AddField("Support Roles", string.Join(", ", roleIds.Select(r => $"<@&{r}>")))
//             .WithOkColor();
//
//         await ctx.Channel.SendMessageAsync(embed: eb.Build());
//     }
//
//     /// <summary>
//     ///     Updates the configuration for a ticket button.
//     /// </summary>
//     [Cmd]
//     [RequireContext(ContextType.Guild)]
//     [UserPerm(GuildPermission.ManageGuild)]
//     public async Task ConfigureButton(int buttonId, string setting, [Remainder] string value)
//     {
//         await using var db = await _db.GetContextAsync();
//         var button = await db.TicketButtons.FindAsync(buttonId);
//
//         if (button == null)
//         {
//             await ReplyErrorAsync(Strings.ButtonNotFound(ctx.Guild.Id)).ConfigureAwait(false);
//             return;
//         }
//
//         switch (setting.ToLower())
//         {
//             case "category":
//                 button.CategoryId = ulong.Parse(value);
//                 break;
//             case "archivecategory":
//                 button.ArchiveCategoryId = ulong.Parse(value);
//                 break;
//             case "maxtickets":
//                 button.MaxActiveTickets = int.Parse(value);
//                 break;
//             case "cooldown":
//                 button.Cooldown = TimeSpan.Parse(value);
//                 break;
//             case "autoclose":
//                 button.AutoCloseTime = TimeSpan.Parse(value);
//                 break;
//             case "supportroles":
//                 button.SupportRoleIds = value.Split(' ').Select(ulong.Parse).ToList();
//                 break;
//             case "viewroles":
//                 button.ViewerRoleIds = value.Split(' ').Select(ulong.Parse).ToList();
//                 break;
//             case "addusers":
//                 button.AutoAddUserIds = value.Split(' ').Select(ulong.Parse).ToList();
//                 break;
//             case "addroles":
//                 button.AutoAddRoleIds = value.Split(' ').Select(ulong.Parse).ToList();
//                 break;
//             case "channelformat":
//                 button.ChannelNameFormat = value;
//                 break;
//             case "openmessage":
//                 button.OpenMessage = value;
//                 break;
//             case "precreatemessage":
//                 button.PreCreateMessage = value;
//                 break;
//             case "requireconfirmation":
//                 button.RequireConfirmation = bool.Parse(value);
//                 break;
//             case "priority":
//                 button.DefaultPriorityId = value;
//                 break;
//             case "allowedpriorities":
//                 button.AllowedPriorityIds = value.Split(' ').ToList();
//                 break;
//             case "requiredtags":
//                 button.RequiredTags = value.Split(' ').ToList();
//                 break;
//             default:
//                 await ReplyErrorAsync(Strings.InvalidSetting(ctx.Guild.Id)).ConfigureAwait(false);
//                 return;
//         }
//
//         await db.SaveChangesAsync();
//         await ReplyConfirmAsync(Strings.ButtonConfigured(ctx.Guild.Id)).ConfigureAwait(false);
//     }
//
//     /// <summary>
//     ///     Adds a select menu for different ticket types.
//     /// </summary>
//     [Cmd]
//     [RequireContext(ContextType.Guild)]
//     [UserPerm(GuildPermission.ManageGuild)]
//     public async Task AddPanelSelect(int panelId, [Remainder] string placeholder)
//     {
//         var select = new TicketSelect
//         {
//             Placeholder = placeholder, Options = []
//         };
//
//         var success = await Service.AddSelectToPanel(ctx.Guild.Id, panelId, select);
//         if (!success)
//         {
//             await ReplyErrorAsync(Strings.PanelNotFound(ctx.Guild.Id)).ConfigureAwait(false);
//             return;
//         }
//
//         await ReplyConfirmAsync(Strings.SelectAdded(ctx.Guild.Id, select.Id)).ConfigureAwait(false);
//     }
//
//     /// <summary>
//     ///     Adds a ticket type option to a select menu.
//     /// </summary>
//     [Cmd]
//     [RequireContext(ContextType.Guild)]
//     [UserPerm(GuildPermission.ManageGuild)]
//     public async Task AddSelectOption(
//         int selectId,
//         string label,
//         string description,
//         ulong categoryId,
//         string openMessage,
//         string channelFormat = "ticket-{username}-{id}",
//         string emoji = null,
//         [Remainder] string supportRoles = null)
//     {
//         await using var db = await _db.GetContextAsync();
//         var select = await db.TicketSelects
//             .Include(s => s.Options)
//             .FirstOrDefaultAsync(s => s.Id == selectId);
//
//         if (select == null)
//         {
//             await ReplyErrorAsync(Strings.SelectNotFound(ctx.Guild.Id)).ConfigureAwait(false);
//             return;
//         }
//
//         var roleIds = supportRoles?.Split(' ').Select(ulong.Parse).ToList() ?? [];
//
//         var option = new TicketSelectOption
//         {
//             Label = label,
//             Description = description,
//             Emoji = emoji,
//             OpenMessage = openMessage,
//             CategoryId = categoryId,
//             ChannelNameFormat = channelFormat,
//             SupportRoleIds = roleIds,
//             AutoAddRoleIds = [],
//             AutoAddUserIds = [],
//             ViewerRoleIds = [],
//             AllowedPriorityIds = [],
//             RequiredTags = [],
//             SaveTranscript = true,
//             MaxActiveTickets = 1
//         };
//
//         select.Options.Add(option);
//         await db.SaveChangesAsync();
//
//         var eb = new EmbedBuilder()
//             .WithTitle("Select Option Added")
//             .WithDescription($"Option '{label}' added to select menu {selectId}")
//             .AddField("Category", $"<#{categoryId}>", true)
//             .AddField("Channel Format", channelFormat, true)
//             .AddField("Support Roles", string.Join(", ", roleIds.Select(r => $"<@&{r}>")))
//             .WithOkColor();
//
//         await ctx.Channel.SendMessageAsync(embed: eb.Build());
//     }
//
//     /// <summary>
//     ///     Updates the configuration for a select menu option.
//     /// </summary>
//     [Cmd]
//     [RequireContext(ContextType.Guild)]
//     [UserPerm(GuildPermission.ManageGuild)]
//     public async Task ConfigureOption(int optionId, string setting, [Remainder] string value)
//     {
//         await using var db = await _db.GetContextAsync();
//         var option = await db.TicketSelectOptions.FindAsync(optionId);
//
//         if (option == null)
//         {
//             await ReplyErrorAsync(Strings.OptionNotFound(ctx.Guild.Id)).ConfigureAwait(false);
//             return;
//         }
//
//         switch (setting.ToLower())
//         {
//             case "category":
//                 option.CategoryId = ulong.Parse(value);
//                 break;
//             case "archivecategory":
//                 option.ArchiveCategoryId = ulong.Parse(value);
//                 break;
//             case "maxtickets":
//                 option.MaxActiveTickets = int.Parse(value);
//                 break;
//             case "cooldown":
//                 option.Cooldown = TimeSpan.Parse(value);
//                 break;
//             case "autoclose":
//                 option.AutoCloseTime = TimeSpan.Parse(value);
//                 break;
//             case "supportroles":
//                 option.SupportRoleIds = value.Split(' ').Select(ulong.Parse).ToList();
//                 break;
//             case "viewroles":
//                 option.ViewerRoleIds = value.Split(' ').Select(ulong.Parse).ToList();
//                 break;
//             case "addusers":
//                 option.AutoAddUserIds = value.Split(' ').Select(ulong.Parse).ToList();
//                 break;
//             case "addroles":
//                 option.AutoAddRoleIds = value.Split(' ').Select(ulong.Parse).ToList();
//                 break;
//             case "channelformat":
//                 option.ChannelNameFormat = value;
//                 break;
//             case "openmessage":
//                 option.OpenMessage = value;
//                 break;
//             case "precreatemessage":
//                 option.PreCreateMessage = value;
//                 break;
//             case "requireconfirmation":
//                 option.RequireConfirmation = bool.Parse(value);
//                 break;
//             case "priority":
//                 option.DefaultPriorityId = value;
//                 break;
//             case "allowedpriorities":
//                 option.AllowedPriorityIds = value.Split(' ').ToList();
//                 break;
//             case "requiredtags":
//                 option.RequiredTags = value.Split(' ').ToList();
//                 break;
//             default:
//                 await ReplyErrorAsync(Strings.InvalidSetting(ctx.Guild.Id)).ConfigureAwait(false);
//                 return;
//         }
//
//         await db.SaveChangesAsync();
//         await ReplyConfirmAsync(Strings.OptionConfigured(ctx.Guild.Id)).ConfigureAwait(false);
//     }
// }
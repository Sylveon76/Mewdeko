﻿// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Style", "IDE1006:Naming Styles")]
[assembly: SuppressMessage("Usage", "CA2211:Non-constant fields should not be visible")]
[assembly: SuppressMessage("Design", "CA1069:Enums values should not be duplicated")]
[assembly: SuppressMessage("Performance", "CA1806:Do not ignore method results", Justification = "<Pending>", Scope = "member", Target = "~M:Mewdeko.Modules.Administration.Services.LogCommandService.#ctor(Discord.WebSocket.DiscordSocketClient,Mewdeko.Services.strings.IBotStrings,Mewdeko.Services.DbService,Mewdeko.Modules.Moderation.Services.MuteService,Mewdeko.Modules.Administration.Services.ProtectionService,Mewdeko.Modules.Administration.Services.GuildTimezoneService,Microsoft.Extensions.Caching.Memory.IMemoryCache)")]
[assembly: SuppressMessage("Performance", "CA1806:Do not ignore method results", Justification = "<Pending>", Scope = "member", Target = "~M:Mewdeko.Modules.Administration.Services.VcRoleService.#ctor(Discord.WebSocket.DiscordSocketClient,Mewdeko.Services.Mewdeko,Mewdeko.Services.DbService)")]
[assembly: SuppressMessage("Performance", "CA1806:Do not ignore method results", Justification = "<Pending>", Scope = "member", Target = "~M:Mewdeko.Modules.Afk.Afk.AfkRemove(Discord.IGuildUser[])~System.Threading.Tasks.Task")]
[assembly: SuppressMessage("Performance", "CA1806:Do not ignore method results", Justification = "<Pending>", Scope = "member", Target = "~M:Mewdeko.Modules.Afk.Afk.CustomAfkMessage(System.String)~System.Threading.Tasks.Task")]
[assembly: SuppressMessage("Performance", "CA1806:Do not ignore method results", Justification = "<Pending>", Scope = "member", Target = "~M:Mewdeko.Modules.Gambling.Services.GamblingService.#ctor(Mewdeko.Services.DbService,Mewdeko.Services.Mewdeko,Mewdeko.Services.ICurrencyService,Discord.WebSocket.DiscordSocketClient,Mewdeko.Services.IDataCache,Mewdeko.Modules.Gambling.Services.GamblingConfigService)")]
[assembly: SuppressMessage("Performance", "CA1806:Do not ignore method results", Justification = "<Pending>", Scope = "member", Target = "~M:Mewdeko.Modules.Moderation.Services.UserPunishService.GetBanUserDmEmbed(Discord.WebSocket.DiscordSocketClient,Discord.WebSocket.SocketGuild,Discord.IGuildUser,Discord.IGuildUser,System.String,System.String,System.Nullable{System.TimeSpan})~Mewdeko.Common.CrEmbed")]
[assembly: SuppressMessage("Performance", "CA1806:Do not ignore method results", Justification = "<Pending>", Scope = "member", Target = "~M:Mewdeko.Modules.Moderation.Services.UserPunishService2.#ctor(Mewdeko.Modules.Moderation.Services.MuteService,Mewdeko.Services.DbService,Mewdeko.Services.Mewdeko)")]
[assembly: SuppressMessage("Performance", "CA1806:Do not ignore method results", Justification = "<Pending>", Scope = "member", Target = "~M:Mewdeko.Modules.Permissions.Services.FilterService.FilterWords(Discord.IGuild,Discord.IUserMessage)~System.Threading.Tasks.Task{System.Boolean}")]
[assembly: SuppressMessage("Performance", "CA1806:Do not ignore method results", Justification = "<Pending>", Scope = "member", Target = "~M:Mewdeko.Modules.Server_Management.ServerManagement.ChannelCommands.Webhook(Discord.ITextChannel,System.String,System.String)~System.Threading.Tasks.Task")]
[assembly: SuppressMessage("Performance", "CA1806:Do not ignore method results", Justification = "<Pending>", Scope = "member", Target = "~M:Mewdeko.Modules.Server_Management.ServerManagement.ChannelCommands.Webhook(Discord.ITextChannel,System.String,System.String,System.String)~System.Threading.Tasks.Task")]
[assembly: SuppressMessage("Performance", "CA1806:Do not ignore method results", Justification = "<Pending>", Scope = "member", Target = "~M:Mewdeko.Modules.Suggestions.SuggestionsCustomization.AcceptMessage(System.String)~System.Threading.Tasks.Task")]
[assembly: SuppressMessage("Performance", "CA1806:Do not ignore method results", Justification = "<Pending>", Scope = "member", Target = "~M:Mewdeko.Modules.Suggestions.SuggestionsCustomization.ConsiderMessage(System.String)~System.Threading.Tasks.Task")]
[assembly: SuppressMessage("Performance", "CA1806:Do not ignore method results", Justification = "<Pending>", Scope = "member", Target = "~M:Mewdeko.Modules.Suggestions.SuggestionsCustomization.DenyMessage(System.String)~System.Threading.Tasks.Task")]
[assembly: SuppressMessage("Performance", "CA1806:Do not ignore method results", Justification = "<Pending>", Scope = "member", Target = "~M:Mewdeko.Modules.Suggestions.SuggestionsCustomization.ImplementMessage(System.String)~System.Threading.Tasks.Task")]
[assembly: SuppressMessage("Performance", "CA1806:Do not ignore method results", Justification = "<Pending>", Scope = "member", Target = "~M:Mewdeko.Modules.Suggestions.SuggestionsCustomization.SuggestMessage(System.String)~System.Threading.Tasks.Task")]
[assembly: SuppressMessage("Performance", "CA1806:Do not ignore method results", Justification = "<Pending>", Scope = "member", Target = "~M:Mewdeko.Modules.Utility.Services.UtilityService.MsgReciev2(Discord.WebSocket.SocketMessage)~System.Threading.Tasks.Task")]
[assembly: SuppressMessage("Performance", "CA1806:Do not ignore method results", Justification = "<Pending>", Scope = "member", Target = "~M:Mewdeko.Modules.Utility.Utility.RemindCommands.RemindDelete(System.Int32)~System.Threading.Tasks.Task")]
[assembly: SuppressMessage("Reliability", "CA2012:Use ValueTasks correctly", Justification = "<Pending>", Scope = "member", Target = "~M:Mewdeko.Modules.CustomReactions.Services.CustomReactionsService.OnGcrEdited(Mewdeko.Services.Database.Models.CustomReaction)~System.Threading.Tasks.ValueTask")]
[assembly: SuppressMessage("Reliability", "CA2012:Use ValueTasks correctly", Justification = "<Pending>", Scope = "member", Target = "~M:Mewdeko.Modules.Music.Services.MusicService.TrackStarted(Victoria.EventArgs.TrackStartEventArgs)~System.Threading.Tasks.Task")]

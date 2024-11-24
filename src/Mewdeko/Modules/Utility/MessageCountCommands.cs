using System.IO;
using System.Text;
using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Utility.Services;
using SkiaSharp;

namespace Mewdeko.Modules.Utility;

public partial class Utility
{
    /// <summary>
    ///     Commands for message counts
    /// </summary>
    public class MessageCountCommands(GuildSettingsService guildSettingsService) : MewdekoSubmodule<MessageCountService>
    {
        /// <summary>
        /// </summary>
        public enum GraphType
        {
            /// <summary>
            /// </summary>
            Days,

            /// <summary>
            /// </summary>
            Hours
        }

        /// <summary>
        ///     Retrieves message statistics for a specific user.
        /// </summary>
        /// <param name="user">The user to get message statistics for. If null, uses the command invoker.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task UserMessages(IUser? user = null)
        {
            user ??= ctx.User;
            var (cnt, enabled) = await Service.GetAllCountsForEntity(MessageCountService.CountQueryType.User, user.Id,
                ctx.Guild.Id);

            if (!enabled)
            {
                await ReplyErrorAsync(Strings.MessageCountDisabled(ctx.Guild.Id));
                return;
            }

            var mostActive = cnt.MaxBy(x => x.Count);
            var leastActive = cnt.MinBy(x => x.Count);

            var eb = new EmbedBuilder()
                .WithTitle(Strings.UserMessageCountTitle(ctx.Guild.Id, user))
                .WithDescription(Strings.UserMessageCountDescription(ctx.Guild.Id,
                    cnt.SumUlong(x => x.Count),
                    mostActive.ChannelId, mostActive.Count,
                    leastActive.ChannelId, leastActive.Count))
                .WithOkColor();

            await ctx.Channel.SendMessageAsync(embed: eb.Build());
        }

        /// <summary>
        ///     Retrieves message statistics for a specific channel.
        /// </summary>
        /// <param name="channel">The channel to get message statistics for. If null, uses the current channel.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task ChannelMessages(IGuildChannel? channel = null)
        {
            channel ??= ctx.Channel as IGuildChannel;
            var (cnt, enabled) = await Service.GetAllCountsForEntity(MessageCountService.CountQueryType.Channel,
                channel.Id,
                ctx.Guild.Id);

            if (!enabled)
            {
                await ReplyErrorAsync(Strings.MessageCountDisabled(ctx.Guild.Id));
                return;
            }

            var mostActive = cnt.MaxBy(x => x.Count);
            var leastActive = cnt.MinBy(x => x.Count);

            var eb = new EmbedBuilder()
                .WithTitle(Strings.ChannelMessageCountTitle(ctx.Guild.Id, channel.Name))
                .WithDescription(Strings.ChannelMessageCountDescription(ctx.Guild.Id,
                    cnt.SumUlong(x => x.Count),
                    mostActive.UserId, mostActive.Count,
                    leastActive.UserId, leastActive.Count))
                .WithOkColor();

            await ctx.Channel.SendMessageAsync(embed: eb.Build());
        }

        /// <summary>
        ///     Retrieves message statistics for the entire server.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task ServerMessages()
        {
            var (cnt, enabled) = await Service.GetAllCountsForEntity(MessageCountService.CountQueryType.Guild,
                ctx.Guild.Id,
                ctx.Guild.Id);

            if (!enabled)
            {
                await ReplyErrorAsync(Strings.MessageCountDisabled(ctx.Guild.Id));
                return;
            }

            var userGroups = cnt.GroupBy(x => x.UserId)
                .Select(g => new
                {
                    UserId = g.Key, Count = g.SumUlong(x => x.Count)
                })
                .ToList();

            var channelGroups = cnt.GroupBy(x => x.ChannelId)
                .Select(g => new
                {
                    ChannelId = g.Key, Count = g.SumUlong(x => x.Count)
                })
                .ToList();

            var mostActiveUser = userGroups.MaxBy(x => x.Count);
            var leastActiveUser = userGroups.MinBy(x => x.Count);
            var mostActiveChannel = channelGroups.MaxBy(x => x.Count);
            var leastActiveChannel = channelGroups.MinBy(x => x.Count);

            var totalMessages = channelGroups.SumUlong(x => x.Count);

            var eb = new EmbedBuilder()
                .WithTitle(Strings.ServerMessageStatsTitle(ctx.Guild.Id, ctx.Guild.Name))
                .WithDescription(Strings.ServerMessageStatsDescription(ctx.Guild.Id,
                    totalMessages,
                    mostActiveUser.UserId, mostActiveUser.Count,
                    leastActiveUser.UserId, leastActiveUser.Count,
                    mostActiveChannel.ChannelId, mostActiveChannel.Count,
                    leastActiveChannel.ChannelId, leastActiveChannel.Count))
                .WithOkColor();

            await ctx.Channel.SendMessageAsync(embed: eb.Build());
        }

        /// <summary>
        ///     Displays a leaderboard of the top 10 users by message count.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task TopUsers()
        {
            var (cnt, enabled) = await Service.GetAllCountsForEntity(MessageCountService.CountQueryType.Guild,
                ctx.Guild.Id,
                ctx.Guild.Id);

            if (!enabled)
            {
                await ReplyErrorAsync(Strings.MessageCountDisabled(ctx.Guild.Id));
                return;
            }

            var userGroups = cnt.GroupBy(x => x.UserId)
                .Select(g => new
                {
                    UserId = g.Key, Count = g.SumUlong(x => x.Count)
                })
                .OrderByDescending(x => x.Count)
                .Take(10)
                .ToList();

            var totalMessages = cnt.SumUlong(x => x.Count);

            var eb = new EmbedBuilder()
                .WithTitle(Strings.TopUsersTitle(ctx.Guild.Id, ctx.Guild.Name))
                .WithOkColor();

            var description = new StringBuilder();
            for (var i = 0; i < userGroups.Count; i++)
            {
                var user = userGroups[i];
                var userMention = MentionUtils.MentionUser(user.UserId);
                var percentage = (user.Count * 100.0 / totalMessages).ToString("F2");
                description.AppendLine(Strings.TopUsersEntry(ctx.Guild.Id, i + 1, userMention, user.Count, percentage));
            }

            eb.WithDescription(description.ToString());

            await ctx.Channel.SendMessageAsync(embed: eb.Build());
        }

        /// <summary>
        ///     Sets the minimum length for a message to count
        /// </summary>
        /// <param name="minLength"></param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task MinMessageCountLength(int minLength = 0)
        {
            var config = await guildSettingsService.GetGuildConfig(ctx.Guild.Id);

            switch (minLength)
            {
                case > 4098:
                    await ReplyErrorAsync(Strings.MaxCountReached(ctx.Guild.Id));
                    return;
                case 0:
                    await ReplyConfirmAsync(Strings.CurrentMinMessageSetting(ctx.Guild.Id, config.MinMessageLength));
                    break;
                default:
                    config.MinMessageLength = minLength;
                    await guildSettingsService.UpdateGuildConfig(ctx.Guild.Id, config);
                    await ReplyConfirmAsync(Strings.MinMessageLengthSet(ctx.Guild.Id, minLength));
                    break;
            }
        }

        /// <summary>
        ///     Displays a graph of the busiest hours and days in the server.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task ActivityGraph(GraphType graphType = GraphType.Days)
        {
            switch (graphType)
            {
                case GraphType.Days:
                    await GenerateBusiestDaysGraph();
                    break;
                case GraphType.Hours:
                    await GenerateBusiestHoursGraph();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(graphType), graphType, null);
            }
        }


        /// <summary>
        ///     Toggles message counting in the server
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task ToggleMessageCount()
        {
            var toggled = await Service.ToggleGuildMessageCount(ctx.Guild.Id);

            if (toggled)
                await ReplyConfirmAsync(Strings.MessageCountingEnabled(ctx.Guild.Id));
            else
                await ReplyConfirmAsync(Strings.MessageCountingDisabled(ctx.Guild.Id));
        }

        private async Task GenerateBusiestDaysGraph()
        {
            var busiestDays = await Service.GetBusiestDays(ctx.Guild.Id);

            if (busiestDays == null || busiestDays.Count() < 7)
            {
                await ReplyErrorAsync(Strings.InsufficientDayData(ctx.Guild.Id, busiestDays?.Count() ?? 0));
                return;
            }

            using var graphImage = GenerateDaysGraph(busiestDays);
            using var ms = new MemoryStream();
            graphImage.Encode(SKEncodedImageFormat.Png, 100).SaveTo(ms);
            ms.Position = 0;

            await ctx.Channel.SendFileAsync(ms, "busiest_days.png",
                Strings.BusiestDaysGraphTitle(ctx.Guild.Id, ctx.Guild.Name));
        }

        private async Task GenerateBusiestHoursGraph()
        {
            var busiestHours = await Service.GetBusiestHours(ctx.Guild.Id);

            if (busiestHours == null || busiestHours.Count() < 24)
            {
                await ReplyErrorAsync(Strings.InsufficientHourData(ctx.Guild.Id, busiestHours?.Count() ?? 0));
                return;
            }

            var userTimezone = await PromptForTimezone();
            if (userTimezone == null)
            {
                await ReplyErrorAsync(Strings.TimezoneNotSelected(ctx.Guild.Id));
                return;
            }

            var adjustedHours = AdjustHoursToTimezone(busiestHours, userTimezone);
            using var graphImage = GenerateHoursGraph(adjustedHours);
            using var ms = new MemoryStream();
            graphImage.Encode(SKEncodedImageFormat.Png, 100).SaveTo(ms);
            ms.Position = 0;

            await ctx.Channel.SendFileAsync(ms, "busiest_hours.png",
                Strings.BusiestHoursGraphTitle(ctx.Guild.Id, ctx.Guild.Name, userTimezone.Id));
        }

        private async Task<TimeZoneInfo?> PromptForTimezone()
        {
            var commonTimeZones = new List<(string Id, string DisplayName)>
            {
                ("Etc/UTC", "UTC (Coordinated Universal Time)"),
                ("America/New_York", "Eastern Time (ET)"),
                ("America/Chicago", "Central Time (CT)"),
                ("America/Denver", "Mountain Time (MT)"),
                ("America/Los_Angeles", "Pacific Time (PT)"),
                ("Europe/London", "British Time (BT)"),
                ("Europe/Berlin", "Central European Time (CET)"),
                ("Asia/Tokyo", "Japan Standard Time (JST)"),
                ("Australia/Sydney", "Australian Eastern Standard Time (AEST)")
            };

            var eb = new EmbedBuilder()
                .WithTitle(Strings.TimezoneSelectTitle(ctx.Guild.Id))
                .WithDescription(Strings.TimezoneSelectDescription(ctx.Guild.Id,
                    string.Join("\n", commonTimeZones.Select((tz, i) => $"{i + 1}. {tz.DisplayName}"))))
                .WithFooter(Strings.TimezoneSelectFooter(ctx.Guild.Id))
                .WithOkColor();

            await ctx.Channel.SendMessageAsync(embed: eb.Build());

            while (true)
            {
                var response = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id);
                if (string.IsNullOrEmpty(response))
                    return null;

                response = response.Trim().ToLowerInvariant();

                if (response == Strings.TimezoneCancelKeyword(ctx.Guild.Id))
                    return null;

                if (response == Strings.TimezoneListKeyword(ctx.Guild.Id))
                {
                    var allTimeZones = TimeZoneInfo.GetSystemTimeZones()
                        .OrderBy(tz => tz.BaseUtcOffset)
                        .ThenBy(tz => tz.DisplayName);

                    var tzList = string.Join("\n", allTimeZones.Select(tz => $"{tz.Id}: {tz.DisplayName}"));
                    await ctx.Channel.SendMessageAsync(Strings.TimezoneFullList(ctx.Guild.Id, tzList));
                    continue;
                }

                if (int.TryParse(response, out var index) && index > 0 && index <= commonTimeZones.Count)
                {
                    return TimeZoneInfo.FindSystemTimeZoneById(commonTimeZones[index - 1].Id);
                }

                var matchingTimeZones = TimeZoneInfo.GetSystemTimeZones()
                    .Where(tz =>
                        tz.Id.Contains(response, StringComparison.InvariantCultureIgnoreCase) ||
                        tz.DisplayName.Contains(response, StringComparison.InvariantCultureIgnoreCase))
                    .ToList();

                switch (matchingTimeZones.Count)
                {
                    case 1:
                        return matchingTimeZones[0];
                    case > 1:
                    {
                        var matchEmbed = new EmbedBuilder()
                            .WithTitle(Strings.TimezoneMultipleMatchesTitle(ctx.Guild.Id))
                            .WithDescription(Strings.TimezoneMultipleMatchesDescription(ctx.Guild.Id,
                                string.Join("\n", matchingTimeZones.Select(tz => $"{tz.Id}: {tz.DisplayName}"))))
                            .WithColor(Color.Orange);
                        await ctx.Channel.SendMessageAsync(embed: matchEmbed.Build());
                        break;
                    }
                    default:
                        await ctx.Channel.SendMessageAsync(Strings.TimezoneNoMatch(ctx.Guild.Id));
                        break;
                }
            }
        }

        /// <summary>
        ///     Resets message counts for a user, channel, or both, with confirmation.
        /// </summary>
        /// <param name="user">Optional: The user to reset message counts for.</param>
        /// <param name="channel">Optional: The channel to reset message counts in.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        public async Task ResetMessageCounts(IUser? user = null, ITextChannel? channel = null)
        {
            var confirmMessage = (user, channel) switch
            {
                (null, null) => Strings.ConfirmResetMessageCountGuild(ctx.Guild.Id),
                (not null, null) => Strings.ConfirmResetMessageCountUser(ctx.Guild.Id, user.Mention),
                (null, not null) => Strings.ConfirmResetMessageCountChannel(ctx.Guild.Id, channel.Mention),
                (not null, not null) => Strings.ConfirmResetMessageCountUserChannel(ctx.Guild.Id, user.Mention, channel.Mention)
            };

            if (!await PromptUserConfirmAsync(confirmMessage, ctx.User.Id))
                return;

            var result = await Service.ResetCount(ctx.Guild.Id, user?.Id ?? 0, channel?.Id ?? 0);

            var responseMessage = (user, channel, result) switch
            {
                (null, null, true) => Strings.ResetMessageCountSuccessGuild(ctx.Guild.Id),
                (not null, null, true) => Strings.ResetMessageCountSuccessUser(ctx.Guild.Id, user.Mention),
                (null, not null, true) => Strings.ResetMessageCountSuccessChannel(ctx.Guild.Id, channel.Mention),
                (not null, not null, true) => Strings.ResetMessageCountSuccessUserChannel(ctx.Guild.Id, user.Mention, channel.Mention),
                (null, null, false) => Strings.ResetMessageCountFailGuild(ctx.Guild.Id),
                (not null, null, false) => Strings.ResetMessageCountFailUser(ctx.Guild.Id, user.Mention),
                (null, not null, false) => Strings.ResetMessageCountFailChannel(ctx.Guild.Id, channel.Mention),
                (not null, not null, false) => Strings.ResetMessageCountFailUserChannel(ctx.Guild.Id, user.Mention, channel.Mention)
            };

            await (result ? ctx.Channel.SendConfirmAsync(responseMessage) : ctx.Channel.SendErrorAsync(responseMessage, Config));
        }

        /// <summary>
        ///     Overload for <see cref="ResetMessageCounts(Discord.IUser?,Discord.ITextChannel?)" />
        /// </summary>
        /// <param name="channel">Optional: The channel to reset message counts in.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageMessages)]
        public async Task ResetMessageCounts(ITextChannel? channel)
        {
            await ResetMessageCounts(null, channel);
        }


        private IEnumerable<(int Hour, int Count)> AdjustHoursToTimezone(IEnumerable<(int Hour, int Count)> utcHours,
            TimeZoneInfo timezone)
        {
            return utcHours.Select(h =>
            {
                var utcTime = DateTime.UtcNow.Date.AddHours(h.Hour);
                var localTime = TimeZoneInfo.ConvertTimeFromUtc(utcTime, timezone);
                return (localTime.Hour, h.Count);
            }).OrderBy(h => h.Hour);
        }

        private SKImage GenerateDaysGraph(IEnumerable<(DayOfWeek Day, int Count)> busiestDays)
        {
            const int width = 800;
            const int height = 600;
            using var surface = SKSurface.Create(new SKImageInfo(width, height));
            var canvas = surface.Canvas;

            canvas.Clear(new SKColor(30, 30, 30));

            using var paint = new SKPaint
            {
                Color = SKColors.White, IsAntialias = true
            };
            using var font = new SKFont(SKTypeface.Default);

            var data = busiestDays.Select(d => (d.Day.ToString().Substring(0, 3), d.Count)).ToList();
            DrawBarGraph(canvas, data, "Busiest Days of the Week", 0, 0, width, height, paint, font);

            return surface.Snapshot();
        }

        private SKImage GenerateHoursGraph(IEnumerable<(int Hour, int Count)> busiestHours)
        {
            const int width = 800;
            const int height = 600;
            using var surface = SKSurface.Create(new SKImageInfo(width, height));
            var canvas = surface.Canvas;

            canvas.Clear(new SKColor(30, 30, 30));

            using var paint = new SKPaint
            {
                Color = SKColors.White, IsAntialias = true
            };
            using var font = new SKFont(SKTypeface.Default);

            var data = busiestHours.Select(h => (h.Hour.ToString("D2") + ":00", h.Count)).ToList();
            DrawBarGraph(canvas, data, "Busiest Hours of the Day", 0, 0, width, height, paint, font);

            return surface.Snapshot();
        }

        private void DrawBarGraph(SKCanvas canvas, List<(string Label, int Value)> data, string title,
            float x, float y, float width, float height, SKPaint paint, SKFont font)
        {
            // Draw title
            using (var titleFont = new SKFont(font.Typeface, font.Size * 1.5f))
            {
                canvas.DrawText(title, x + width / 2, y + 40, SKTextAlign.Center, titleFont, paint);
            }

            var barWidth = (width - 100) / data.Count;
            float spacing = 5;
            float maxValue = data.Max(d => d.Value);

            for (var i = 0; i < data.Count; i++)
            {
                var barHeight = data[i].Value / maxValue * (height - 120);
                var barX = x + 50 + i * (barWidth + spacing);
                var barY = y + height - 60 - barHeight;

                // Draw bar
                using (var barPaint = new SKPaint
                       {
                           Color = new SKColor(66, 135, 245)
                       })
                {
                    canvas.DrawRect(barX, barY, barWidth, barHeight, barPaint);
                }

                // Draw value on top of the bar
                canvas.DrawText(data[i].Value.ToString(), barX + barWidth / 2, barY - 5, SKTextAlign.Center, font,
                    paint);

                // Draw label below the bar
                canvas.DrawText(data[i].Label, barX + barWidth / 2, y + height - 25, SKTextAlign.Center, font, paint);
            }

            // Draw axes
            canvas.DrawLine(x + 45, y + 60, x + 45, y + height - 40, paint);
            canvas.DrawLine(x + 45, y + height - 40, x + width - 5, y + height - 40, paint);
        }
    }
}
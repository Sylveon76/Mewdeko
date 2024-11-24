using System.Collections.Immutable;
using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Games.Common.Acrophobia;
using Mewdeko.Modules.Games.Services;

namespace Mewdeko.Modules.Games;

/// <summary>
///     A module containing various games.
/// </summary>
public partial class Games
{
    /// <summary>
    ///     A module containing Acrophobia commands.
    /// </summary>
    [Group]
    public class AcropobiaCommands(EventHandler handler) : MewdekoSubmodule<GamesService>
    {
        /// <summary>
        ///     Command for starting an Acrophobia game.
        /// </summary>
        /// <param name="args">Arguments passed to the command.</param>
        /// <example>.acro</example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [MewdekoOptions(typeof(AcrophobiaGame.Options))]
        public async Task Acrophobia(params string[] args)
        {
            var (options, _) = OptionsParser.ParseFrom(new AcrophobiaGame.Options(), args);
            var channel = (ITextChannel)ctx.Channel;

            var game = new AcrophobiaGame(options);
            if (Service.AcrophobiaGames.TryAdd(channel.Id, game))
            {
                try
                {
                    game.OnStarted += Game_OnStarted;
                    game.OnEnded += Game_OnEnded;
                    game.OnVotingStarted += Game_OnVotingStarted;
                    game.OnUserVoted += Game_OnUserVoted;
                    handler.MessageReceived += ClientMessageReceived;
                    await game.Run().ConfigureAwait(false);
                }
                finally
                {
                    handler.MessageReceived -= ClientMessageReceived;
                    Service.AcrophobiaGames.TryRemove(channel.Id, out game);
                    game.Dispose();
                }
            }
            else
            {
                await ReplyErrorAsync(Strings.AcroRunning(ctx.Guild.Id)).ConfigureAwait(false);
            }

            async Task ClientMessageReceived(SocketMessage msg)
            {
                if (msg.Channel.Id != ctx.Channel.Id)
                    return;

                try
                {
                    var success = await game.UserInput(msg.Author.Id, msg.Author.ToString(), msg.Content)
                        .ConfigureAwait(false);
                    if (success)
                        await msg.DeleteAsync().ConfigureAwait(false);
                }
                catch
                {
                    // ignored
                }
            }
        }

        /// <summary>
        ///     Event handler for when an Acrophobia game is started.
        /// </summary>
        /// <param name="game">The Acrophobia game that has started.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private Task Game_OnStarted(AcrophobiaGame game)
        {
            var embed = new EmbedBuilder().WithOkColor()
                .WithTitle(Strings.Acrophobia(ctx.Guild.Id))
                .WithDescription(Strings.AcroStarted(ctx.Guild.Id, Format.Bold(string.Join(".", game.StartingLetters))))
                .WithFooter(efb => efb.WithText(Strings.AcroStartedFooter(ctx.Guild.Id, game.Opts.SubmissionTime)));

            return ctx.Channel.EmbedAsync(embed);
        }

        /// <summary>
        ///     Event handler for when a user votes in an Acrophobia game.
        /// </summary>
        /// <param name="user">The user who voted.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private Task Game_OnUserVoted(string user)
        {
            return ctx.Channel.SendConfirmAsync(
                Strings.Acrophobia(ctx.Guild.Id),
                Strings.AcroVoteCast(ctx.Guild.Id, Format.Bold(user)));
        }

        /// <summary>
        ///     Event handler for when voting starts in an Acrophobia game.
        /// </summary>
        /// <param name="game">The Acrophobia game in which voting started.</param>
        /// <param name="submissions">The submissions made by the players.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task Game_OnVotingStarted(AcrophobiaGame game,
            ImmutableArray<KeyValuePair<AcrophobiaUser, int>> submissions)
        {
            switch (submissions.Length)
            {
                case 0:
                    await ctx.Channel.SendErrorAsync(Strings.Acrophobia(ctx.Guild.Id), Strings.AcroEndedNoSub(ctx.Guild.Id))
                        .ConfigureAwait(false);
                    return;
                case 1:
                    await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                            .WithDescription(
                                Strings.AcroWinnerOnly(ctx.Guild.Id,
                                    Format.Bold(submissions.First().Key.UserName)))
                            .WithFooter(efb => efb.WithText(submissions.First().Key.Input)))
                        .ConfigureAwait(false);
                    return;
            }

            var i = 0;
            var embed = new EmbedBuilder()
                .WithOkColor()
                .WithTitle($"{Strings.Acrophobia(ctx.Guild.Id)} - {Strings.SubmissionsClosed(ctx.Guild.Id)}")
                .WithDescription(Strings.AcroNymWas(ctx.Guild.Id,
                    $"{Format.Bold(string.Join(".", game.StartingLetters))}\n--\n{submissions.Aggregate("", (agg, cur) => $"{agg}`{++i}.` **{cur.Key.Input}**\n")}\n--"))
                .WithFooter(efb => efb.WithText(Strings.AcroVote(ctx.Guild.Id)));

            await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        /// <summary>
        ///     Event handler for when an Acrophobia game ends.
        /// </summary>
        /// <param name="game">The Acrophobia game that has ended.</param>
        /// <param name="votes">The votes received by the players.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task Game_OnEnded(AcrophobiaGame game,
            ImmutableArray<KeyValuePair<AcrophobiaUser, int>> votes)
        {
            if (!votes.Any() || votes.All(x => x.Value == 0))
            {
                await ctx.Channel.SendErrorAsync(Strings.Acrophobia(ctx.Guild.Id), Strings.AcroNoVotesCast(ctx.Guild.Id))
                    .ConfigureAwait(false);
                return;
            }

            var table = votes.OrderByDescending(v => v.Value);
            var winner = table.First();
            var embed = new EmbedBuilder().WithOkColor()
                .WithTitle(Strings.Acrophobia(ctx.Guild.Id))
                .WithDescription(Strings.AcroWinner(ctx.Guild.Id, Format.Bold(winner.Key.UserName),
                    Format.Bold(winner.Value.ToString())))
                .WithFooter(efb => efb.WithText(winner.Key.Input));

            await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }
    }
}
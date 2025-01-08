using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Games.Common.Hangman;
using Mewdeko.Modules.Games.Common.Hangman.Exceptions;
using Mewdeko.Modules.Games.Services;

namespace Mewdeko.Modules.Games;

public partial class Games
{
    /// <summary>
    ///     A module containing Hangman commands.
    /// </summary>
    /// <param name="guildSettings">The guild settings service</param>
    [Group]
    public class HangmanCommands(GuildSettingsService guildSettings, EventHandler handler)
        : MewdekoSubmodule<GamesService>
    {
        /// <summary>
        ///     Lists the available hangman types in the current guild.
        /// </summary>
        /// <example>.hangmanlist</example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Hangmanlist()
        {
            await ctx.Channel
                .SendConfirmAsync(
                    $"{Format.Code(Strings.HangmanTypes(ctx.Guild.Id, await guildSettings.GetPrefix(ctx.Guild)))}\n{string.Join("\n", Service.TermPool.Data.Keys)}")
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Starts a hangman game with the specified type.
        /// </summary>
        /// <param name="type">The type of hangman game to start.</param>
        /// <example>.hangman countries</example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Hangman([Remainder] string type = "random")
        {
            Hangman hm;
            try
            {
                hm = new Hangman(type, Service.TermPool);
            }
            catch (TermNotFoundException)
            {
                return;
            }

            if (!Service.HangmanGames.TryAdd(ctx.Channel.Id, hm))
            {
                hm.Dispose();
                await ReplyErrorAsync(Strings.HangmanRunning(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            hm.OnGameEnded += Hm_OnGameEnded;
            hm.OnGuessFailed += Hm_OnGuessFailed;
            hm.OnGuessSucceeded += Hm_OnGuessSucceeded;
            hm.OnLetterAlreadyUsed += Hm_OnLetterAlreadyUsed;
            handler.MessageReceived += ClientMessageReceived;

            try
            {
                await ctx.Channel.SendConfirmAsync($"{Strings.HangmanGameStarted(ctx.Guild.Id)} ({hm.TermType})",
                        $"{hm.ScrambledWord}\n{hm.GetHangman()}")
                    .ConfigureAwait(false);
            }
            catch
            {
                // ignored
            }

            await hm.EndedTask.ConfigureAwait(false);

            handler.MessageReceived -= ClientMessageReceived;
            Service.HangmanGames.TryRemove(ctx.Channel.Id, out _);
            hm.Dispose();
            return;

            async Task ClientMessageReceived(SocketMessage msg)
            {
                    if (ctx.Channel.Id == msg.Channel.Id && !msg.Author.IsBot)
                        await hm.Input(msg.Author.Id, msg.Author.ToString(), msg.Content);
            }
        }

        /// <summary>
        ///     Handles the event when the hangman game ends.
        /// </summary>
        /// <param name="game">The hangman game that ended.</param>
        /// <param name="winner">The winner of the hangman game. Null if no winner.</param>
        private Task Hm_OnGameEnded(Hangman game, string? winner)
        {
            if (winner == null)
            {
                var loseEmbed = new EmbedBuilder().WithTitle($"Hangman Game ({game.TermType}) - Ended")
                    .WithDescription(Format.Bold("You lose."))
                    .AddField(efb => efb.WithName("It was").WithValue(game.Term.GetWord()))
                    .WithFooter(efb => efb.WithText(string.Join(" ", game.PreviousGuesses)))
                    .WithErrorColor();

                if (Uri.IsWellFormedUriString(game.Term.ImageUrl, UriKind.Absolute))
                    loseEmbed.WithImageUrl(game.Term.ImageUrl);

                return ctx.Channel.EmbedAsync(loseEmbed);
            }

            var winEmbed = new EmbedBuilder().WithTitle($"Hangman Game ({game.TermType}) - Ended")
                .WithDescription(Format.Bold($"{winner} Won."))
                .AddField(efb => efb.WithName("It was").WithValue(game.Term.GetWord()))
                .WithFooter(efb => efb.WithText(string.Join(" ", game.PreviousGuesses)))
                .WithOkColor();

            if (Uri.IsWellFormedUriString(game.Term.ImageUrl, UriKind.Absolute))
                winEmbed.WithImageUrl(game.Term.ImageUrl);

            return ctx.Channel.EmbedAsync(winEmbed);
        }

        /// <summary>
        ///     Handles the event when a letter is already used in the hangman game.
        /// </summary>
        /// <param name="game">The hangman game.</param>
        /// <param name="user">The user who attempted to guess the letter.</param>
        /// <param name="guess">The letter that was guessed.</param>
        private Task Hm_OnLetterAlreadyUsed(Hangman game, string user, char guess)
        {
            return ctx.Channel.SendErrorAsync($"Hangman Game ({game.TermType})",
                $"{user} Letter `{guess}` has already been used. You can guess again in 3 seconds.\n{game.ScrambledWord}\n{game.GetHangman()}",
                footer: string.Join(" ", game.PreviousGuesses));
        }

        /// <summary>
        ///     Handles the event when a guess in the hangman game is successful.
        /// </summary>
        /// <param name="game">The hangman game.</param>
        /// <param name="user">The user who made the successful guess.</param>
        /// <param name="guess">The letter that was guessed.</param>
        private Task Hm_OnGuessSucceeded(Hangman game, string user, char guess)
        {
            return ctx.Channel.SendConfirmAsync($"Hangman Game ({game.TermType})",
                $"{user} guessed a letter `{guess}`!\n{game.ScrambledWord}\n{game.GetHangman()}",
                footer: string.Join(" ", game.PreviousGuesses));
        }

        /// <summary>
        ///     Handles the event when a guess in the hangman game fails.
        /// </summary>
        /// <param name="game">The hangman game.</param>
        /// <param name="user">The user who made the unsuccessful guess.</param>
        /// <param name="guess">The letter that was guessed.</param>
        private Task Hm_OnGuessFailed(Hangman game, string user, char guess)
        {
            return ctx.Channel.SendErrorAsync($"Hangman Game ({game.TermType})",
                $"{user} Letter `{guess}` does not exist. You can guess again in 3 seconds.\n{game.ScrambledWord}\n{game.GetHangman()}",
                footer: string.Join(" ", game.PreviousGuesses));
        }

        /// <summary>
        ///     Stops the currently running hangman game in the current channel.
        /// </summary>
        /// <example>.hangmanstop</example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task HangmanStop()
        {
            if (Service.HangmanGames.TryRemove(ctx.Channel.Id, out var removed))
            {
                await removed.Stop().ConfigureAwait(false);
                await ReplyConfirmAsync(Strings.HangmanStopped(ctx.Guild.Id)).ConfigureAwait(false);
            }
        }
    }
}
using Discord.Interactions;
using Mewdeko.Common.Configs;

namespace Mewdeko.Modules.Games.Services;

/// <summary>
///     Handles interaction with poll buttons for voting.
/// </summary>
public class PollButtons(PollService pollService, BotConfig config) : MewdekoSlashCommandModule
{
    /// <summary>
    ///     Handles interaction with poll buttons for voting.
    /// </summary>
    /// <param name="num">The number representing the option selected.</param>
    [ComponentInteraction("pollbutton:*")]
    public async Task Pollbutton(string num)
    {
        var (allowed, type) = await pollService.TryVote(ctx.Guild, int.Parse(num) - 1, ctx.User);
        switch (type)
        {
            case PollType.PollEnded:
                await ctx.Interaction.SendEphemeralErrorAsync(Strings.PollEnded(ctx.Guild.Id), config);
                break;
            case PollType.SingleAnswer:
                if (!allowed)
                    await ctx.Interaction.SendEphemeralErrorAsync(Strings.PollVoteNoChange(ctx.Guild.Id), config
                    );
                else
                    await ctx.Interaction.SendEphemeralConfirmAsync(Strings.PollVoted(ctx.Guild.Id));
                break;
            case PollType.AllowChange:
                if (!allowed)
                    await ctx.Interaction.SendEphemeralErrorAsync(Strings.PollVoteExists(ctx.Guild.Id), config);
                else
                    await ctx.Interaction.SendEphemeralConfirmAsync(Strings.PollVoteChanged(ctx.Guild.Id));
                break;
            case PollType.MultiAnswer:
                if (!allowed)
                    await ctx.Interaction.SendEphemeralErrorAsync(Strings.PollVoteRemoved(ctx.Guild.Id), config);
                else
                    await ctx.Interaction.SendEphemeralConfirmAsync(Strings.PollVoteAdded(ctx.Guild.Id));
                break;
        }
    }
}
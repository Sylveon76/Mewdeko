﻿#nullable enable

using Lavalink4NET.Artwork;
using Lavalink4NET.Events;
using Lavalink4NET.Player;
using Mewdeko.Common.Configs;

namespace Mewdeko.Modules.Music.Services;

/// <summary>
/// Music player service.
/// </summary>
/// <param name="client">The Discord client</param>
/// <param name="musicService">The music service</param>
/// <param name="config">The bot configuration service</param>
public class MusicPlayer(
    DiscordSocketClient client,
    MusicService musicService,
    BotConfig config)
    : LavalinkPlayer
{
    /// <summary>
    /// Handles the track started event.
    /// </summary>
    /// <param name="args">The event arguments</param>
    public override async Task OnTrackStartedAsync(TrackStartedEventArgs args)
    {
        var queue = musicService.GetQueue(args.Player.GuildId);
        var track = queue.Find(x => x.Identifier == args.Player.CurrentTrack.Identifier);
        LavalinkTrack? nextTrack = null;
        try
        {
            nextTrack = queue.ElementAt(queue.IndexOf(track) + 1);
        }
        catch
        {
            //ignored
        }

        var resultMusicChannelId =
            (await musicService.GetSettingsInternalAsync(args.Player.GuildId).ConfigureAwait(false)).MusicChannelId;
        var autoPlay = (await musicService.GetSettingsInternalAsync(args.Player.GuildId)).AutoPlay;
        if (resultMusicChannelId != null)
        {
            if (client.GetChannel(
                    resultMusicChannelId.Value) is SocketTextChannel channel)
            {
                if (track.Uri != null)
                {
                    using var artworkService = new ArtworkService();
                    var artWork = await artworkService.ResolveAsync(track).ConfigureAwait(false);
                    var eb = new EmbedBuilder()
                        .WithOkColor()
                        .WithDescription($"Now playing {track.Title} by {track.Author}")
                        .WithTitle($"Track #{queue.IndexOf(track) + 1}")
                        .WithFooter(await musicService.GetPrettyInfo(args.Player, client.GetGuild(args.Player.GuildId))
                            .ConfigureAwait(false))
                        .WithThumbnailUrl(artWork.OriginalString);
                    if (nextTrack is not null) eb.AddField("Up Next", $"{nextTrack.Title} by {nextTrack.Author}");
                    if (nextTrack is null && autoPlay > 0)
                    {
                        await musicService.AutoPlay(args.Player.GuildId);
                    }

                    await channel.SendMessageAsync(embed: eb.Build(),
                        components: config.ShowInviteButton
                            ? new ComponentBuilder()
                                .WithButton(label: "Support Server", style: ButtonStyle.Link,
                                    url: "https://discord.gg/mewdeko")
                                .WithButton(label: "Support Us!", style: ButtonStyle.Link,
                                    url: "https://ko-fi.com/mewdeko")
                                .Build()
                            : null);
                }
            }
        }
    }

    /// <summary>
    /// Handles the track end event.
    /// </summary>
    /// <param name="args">The event arguments</param>
    public override async Task OnTrackEndAsync(TrackEndEventArgs args)
    {
        var queue = musicService.GetQueue(args.Player.GuildId);
        if (queue.Count > 0)
        {
            var gid = args.Player.GuildId;
            var msettings = await musicService.GetSettingsInternalAsync(gid).ConfigureAwait(false);
            if (client.GetChannel(msettings.MusicChannelId.Value) is not ITextChannel channel)
                return;
            if (args.Reason is TrackEndReason.Stopped or TrackEndReason.CleanUp or TrackEndReason.Replaced) return;
            var currentTrack = queue.Find(x => args.Player.CurrentTrack.Identifier == x.Identifier);
            if (msettings.PlayerRepeat == PlayerRepeatType.Track)
            {
                await args.Player.PlayAsync(currentTrack).ConfigureAwait(false);
                return;
            }

            var nextTrack = queue.ElementAt(queue.IndexOf(currentTrack) + 1);
            if (nextTrack.Uri is null && channel != null)
            {
                if (msettings.PlayerRepeat == PlayerRepeatType.Queue)
                {
                    await args.Player.PlayAsync(musicService.GetQueue(gid).FirstOrDefault()).ConfigureAwait(false);
                    return;
                }

                var eb1 = new EmbedBuilder()
                    .WithOkColor()
                    .WithDescription("I have reached the end of the queue!");
                await channel.SendMessageAsync(embed: eb1.Build(),
                    components: config.ShowInviteButton
                        ? new ComponentBuilder()
                            .WithButton(label: "Support Server", style: ButtonStyle.Link,
                                url: "https://discord.gg/mewdeko")
                            .WithButton(label: "Support Us!", style: ButtonStyle.Link, url: "https://ko-fi.com/mewdeko")
                            .Build()
                        : null);
                if ((await musicService.GetSettingsInternalAsync(args.Player.GuildId).ConfigureAwait(false))
                    .AutoDisconnect is
                    AutoDisconnect.Either or AutoDisconnect.Queue)
                {
                    await args.Player.StopAsync(true).ConfigureAwait(false);
                    return;
                }
            }

            await args.Player.PlayAsync(nextTrack).ConfigureAwait(false);
        }
    }
}
﻿using System.Text;
using System.Text.Json;
using System.Threading;
using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Lavalink4NET;
using Lavalink4NET.DiscordNet;
using Lavalink4NET.Players;
using Lavalink4NET.Rest.Entities.Tracks;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Music.Common;
using Mewdeko.Modules.Music.CustomPlayer;
using Serilog;
using Swan;

namespace Mewdeko.Modules.Music;

/// <summary>
///     A module containing music commands.
/// </summary>
public partial class Music(
    IAudioService service,
    IDataCache cache,
    InteractiveService interactiveService,
    GuildSettingsService guildSettingsService) : MewdekoModule
{
    /// <summary>
    ///     Retrieves the music player an attempts to join the voice channel.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task Join()
    {
        var (player, result) = await GetPlayerAsync();
        if (string.IsNullOrWhiteSpace(result))
            await ReplyConfirmAsync(Strings.MusicJoinSuccess(ctx.Guild.Id, player.VoiceChannelId)).ConfigureAwait(false);
        else
        {
            var eb = new EmbedBuilder()
                .WithErrorColor()
                .WithTitle(Strings.MusicPlayerError(ctx.Guild.Id))
                .WithDescription(result);

            await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Disconnects the bot from the voice channel.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task Leave()
    {
        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            var eb = new EmbedBuilder()
                .WithErrorColor()
                .WithTitle(Strings.MusicPlayerError(ctx.Guild.Id))
                .WithDescription(result);

            await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            return;
        }

        await cache.SetMusicQueue(ctx.Guild.Id, []).ConfigureAwait(false);
        await cache.SetCurrentTrack(ctx.Guild.Id, null);

        await player.DisconnectAsync().ConfigureAwait(false);
        await ReplyConfirmAsync(Strings.MusicDisconnect(ctx.Guild.Id)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Clears the music queue.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task ClearQueue()
    {
        var (player, result) = await GetPlayerAsync(false);

        if (result is not null)
        {
            var eb = new EmbedBuilder()
                .WithErrorColor()
                .WithTitle(Strings.MusicPlayerError(ctx.Guild.Id))
                .WithDescription(result);

            await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            return;
        }

        await cache.SetMusicQueue(ctx.Guild.Id, []).ConfigureAwait(false);
        await ReplyConfirmAsync(Strings.MusicQueueCleared(ctx.Guild.Id)).ConfigureAwait(false);
        await player.StopAsync();
        await cache.SetCurrentTrack(ctx.Guild.Id, null);
    }

    /// <summary>
    ///     Plays a specified track in the current voice channel.
    /// </summary>
    /// <param name="queueNumber">The queue number to play.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task Play([Remainder] int queueNumber)
    {
        var (player, result) = await GetPlayerAsync(false);

        if (result is not null)
        {
            var eb = new EmbedBuilder()
                .WithErrorColor()
                .WithTitle(Strings.MusicPlayerError(ctx.Guild.Id))
                .WithDescription(result);

            await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            return;
        }

        var queue = await cache.GetMusicQueue(ctx.Guild.Id);
        if (queue.Count == 0)
        {
            await ReplyErrorAsync(Strings.MusicQueueEmpty(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var actualNumber = queueNumber - 1;
        if (queueNumber < 1 || queueNumber > queue.Count)
        {
            await ReplyErrorAsync(Strings.MusicQueueInvalidIndex(ctx.Guild.Id, queue.Count)).ConfigureAwait(false);
            return;
        }

        var trackToPlay = queue.FirstOrDefault(x => x.Index == queueNumber);
        await player.StopAsync();
        await player.PlayAsync(trackToPlay.Track).ConfigureAwait(false);
        await cache.SetCurrentTrack(ctx.Guild.Id, trackToPlay);
    }

    /// <summary>
    ///     Plays a track in the current voice channel.
    /// </summary>
    /// <param name="query">The query to search for.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task Play([Remainder] string query)
    {
        var (player, result) = await GetPlayerAsync();

        if (result is not null)
        {
            var eb = new EmbedBuilder()
                .WithErrorColor()
                .WithTitle(Strings.MusicPlayerError(ctx.Guild.Id))
                .WithDescription(result);

            await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            return;
        }

        await player.SetVolumeAsync(await player.GetVolume() / 100f).ConfigureAwait(false);

        var queue = await cache.GetMusicQueue(ctx.Guild.Id);
        if (Uri.TryCreate(query, UriKind.Absolute, out var uri))
        {
            TrackLoadOptions options;
            if (query.Contains("music.youtube"))
            {
                options = new TrackLoadOptions
                {
                    SearchMode = TrackSearchMode.YouTubeMusic
                };
            }
            else if (query.Contains("youtube.com") || query.Contains("youtu.be"))
            {
                options = new TrackLoadOptions
                {
                    SearchMode = TrackSearchMode.YouTube
                };
            }
            else if (query.Contains("open.spotify") || query.Contains("spotify.com"))
            {
                options = new TrackLoadOptions
                {
                    SearchMode = TrackSearchMode.Spotify
                };
            }
            else if (query.Contains("soundcloud.com"))
            {
                options = new TrackLoadOptions
                {
                    SearchMode = TrackSearchMode.SoundCloud
                };
            }
            else
            {
                options = new TrackLoadOptions
                {
                    SearchMode = TrackSearchMode.None
                };
            }

            var trackResults = await service.Tracks.LoadTracksAsync(query, options);
            if (!trackResults.IsSuccess)
            {
                await ReplyErrorAsync(Strings.MusicSearchFail(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (trackResults.Tracks.Length > 1)
            {
                var startIndex = queue.Count + 1;
                queue.AddRange(trackResults.Tracks.Select(track =>
                    new MewdekoTrack(startIndex++, track, new PartialUser
                    {
                        Id = ctx.User.Id, Username = ctx.User.Username, AvatarUrl = ctx.User.GetAvatarUrl()
                    })));
                await cache.SetMusicQueue(ctx.Guild.Id, queue);

                var eb = new EmbedBuilder()
                    .WithDescription(
                        $"Added {trackResults.Tracks.Length} tracks to the queue from {trackResults.Playlist.Name}")
                    .WithThumbnailUrl(trackResults.Tracks[0].ArtworkUri?.ToString())
                    .WithOkColor()
                    .Build();

                await ctx.Channel.SendMessageAsync(embed: eb).ConfigureAwait(false);
            }
            else
            {
                queue.Add(new MewdekoTrack(queue.Count + 1, trackResults.Tracks[0], new PartialUser
                {
                    Id = ctx.User.Id, Username = ctx.User.Username, AvatarUrl = ctx.User.GetAvatarUrl()
                }));
                await cache.SetMusicQueue(ctx.Guild.Id, queue);
            }

            if (player.CurrentItem is null)
            {
                await player.PlayAsync(trackResults.Tracks[0]).ConfigureAwait(false);
                await cache.SetCurrentTrack(ctx.Guild.Id, queue[0]);
            }
        }
        else
        {
            var tracks = await service.Tracks.LoadTracksAsync(query, TrackSearchMode.YouTube);

            if (!tracks.IsSuccess)
            {
                await ReplyErrorAsync(Strings.MusicNoTracks(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var trackList = tracks.Tracks.Take(25).ToList();
            var selectMenu = new SelectMenuBuilder()
                .WithCustomId($"track_select:{ctx.User.Id}")
                .WithPlaceholder(Strings.MusicSelectTracks(ctx.Guild.Id))
                .WithMaxValues(trackList.Count)
                .WithMinValues(1);

            foreach (var track in trackList)
            {
                var index = trackList.IndexOf(track);
                selectMenu.AddOption(track.Title.Truncate(100), $"track_{index}");
            }

            var eb = new EmbedBuilder()
                .WithDescription(Strings.MusicSelectTracksEmbed(ctx.Guild.Id))
                .WithOkColor()
                .Build();

            var components = new ComponentBuilder().WithSelectMenu(selectMenu).Build();

            var message = await ctx.Channel.SendMessageAsync(embed: eb, components: components);

            await cache.Redis.GetDatabase().StringSetAsync($"{ctx.User.Id}_{message.Id}_tracks",
                JsonSerializer.Serialize(trackList), TimeSpan.FromMinutes(5));
        }
    }

    /// <summary>
    ///     Pauses or unpauses the player based on the current state.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task Pause()
    {
        var (player, result) = await GetPlayerAsync();

        if (result is not null)
        {
            var eb = new EmbedBuilder()
                .WithErrorColor()
                .WithTitle(Strings.MusicPlayerError(ctx.Guild.Id))
                .WithDescription(result);

            await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            return;
        }

        if (player.State == PlayerState.Paused)
        {
            await player.ResumeAsync();
            await ReplyConfirmAsync(Strings.MusicResume(ctx.Guild.Id)).ConfigureAwait(false);
        }
        else
        {
            await player.PauseAsync();
            await ReplyConfirmAsync(Strings.MusicPause(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }


    /// <summary>
    ///     Gets the now playing track, if any.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task NowPlaying()
    {
        try
        {
            var (player, result) = await GetPlayerAsync(false);

            if (result is not null)
            {
                var eb = new EmbedBuilder()
                    .WithErrorColor()
                    .WithTitle(Strings.MusicPlayerError(ctx.Guild.Id))
                    .WithDescription(result);

                await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
                return;
            }

            var queue = await cache.GetMusicQueue(ctx.Guild.Id);

            if (queue.Count == 0)
            {
                await ReplyErrorAsync(Strings.MusicQueueEmpty(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }


            var embed = await player.PrettyNowPlayingAsync(queue);
            await ctx.Channel.SendMessageAsync(embed: embed).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Log.Error("Failed to get now playing track: {Message}", e.Message);
        }
    }

    /// <summary>
    ///     Removes the selected track from the queue. If the selected track is the current track, it will be skipped. If next
    ///     track is not available, the player will stop.
    /// </summary>
    /// <param name="queueNumber">The queue number to remove.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task SongRemove(int queueNumber)
    {
        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            var eb = new EmbedBuilder()
                .WithErrorColor()
                .WithTitle(Strings.MusicPlayerError(ctx.Guild.Id))
                .WithDescription(result);

            await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            return;
        }

        var queue = await cache.GetMusicQueue(ctx.Guild.Id);
        var currentTrack = await cache.GetCurrentTrack(ctx.Guild.Id);
        var nextTrack = queue.FirstOrDefault(x => x.Index == currentTrack.Index + 1);
        if (queue.Count == 0)
        {
            await ReplyErrorAsync(Strings.MusicQueueEmpty(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (queueNumber < 1 || queueNumber > queue.Count)
        {
            await ReplyErrorAsync(Strings.MusicQueueInvalidIndex(ctx.Guild.Id, queue.Count)).ConfigureAwait(false);
            return;
        }

        if (nextTrack is not null)
        {
            await player.StopAsync();
            await player.PlayAsync(nextTrack.Track);
            await cache.SetCurrentTrack(ctx.Guild.Id, nextTrack);
        }
        else
        {
            await player.StopAsync();
            await cache.SetCurrentTrack(ctx.Guild.Id, null);
        }

        queue.Remove(currentTrack);
        await cache.SetMusicQueue(ctx.Guild.Id, queue);

        if (player.State == PlayerState.Playing)
        {
            await ReplyConfirmAsync(Strings.MusicSongRemoved(ctx.Guild.Id)).ConfigureAwait(false);
        }
        else
        {
            await ReplyConfirmAsync(Strings.MusicSongRemovedStop(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Moves a song in the queue to a new position.
    /// </summary>
    /// <param name="from">The current position of the song.</param>
    /// <param name="to">The new position of the song.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task MoveSong(int from, int to)
    {
        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            var eb = new EmbedBuilder()
                .WithErrorColor()
                .WithTitle(Strings.MusicPlayerError(ctx.Guild.Id))
                .WithDescription(result);

            await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            return;
        }

        var queue = await cache.GetMusicQueue(ctx.Guild.Id);
        if (queue.Count == 0)
        {
            await ReplyErrorAsync(Strings.MusicQueueEmpty(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (from < 1 || from > queue.Count || to < 1 || to > queue.Count + 1)
        {
            await ReplyErrorAsync(Strings.MusicQueueInvalidIndex(ctx.Guild.Id, queue.Count)).ConfigureAwait(false);
            return;
        }

        var track = queue.FirstOrDefault(x => x.Index == from);
        var replace = queue.FirstOrDefault(x => x.Index == to);
        var currentSong = await cache.GetCurrentTrack(ctx.Guild.Id);

        queue[queue.IndexOf(track)].Index = to;

        if (currentSong is not null && currentSong.Index == from)
        {
            track.Index = to;
            await cache.SetCurrentTrack(ctx.Guild.Id, track);
        }

        if (replace is not null)
        {
            queue[queue.IndexOf(replace)].Index = from;
        }

        try
        {
            await cache.SetMusicQueue(ctx.Guild.Id, queue);
            await ReplyConfirmAsync(Strings.MusicSongMoved(ctx.Guild.Id, track.Track.Title, to)).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to move song.");
        }
    }

    /// <summary>
    ///     Sets the players volume
    /// </summary>
    /// <param name="volume">The volume to set</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task Volume(int volume)
    {
        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            var eb = new EmbedBuilder()
                .WithErrorColor()
                .WithTitle(Strings.MusicPlayerError(ctx.Guild.Id))
                .WithDescription(result);

            await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            return;
        }

        if (volume is < 0 or > 100)
        {
            await ReplyErrorAsync(Strings.MusicVolumeInvalid(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await player.SetVolumeAsync(volume / 100f).ConfigureAwait(false);
        await player.SetGuildVolumeAsync(volume).ConfigureAwait(false);
        await ReplyConfirmAsync(Strings.MusicVolumeSet(ctx.Guild.Id, volume)).ConfigureAwait(false);
    }

    /// <summary>
    /// Seeks to a specific position in the current track.
    /// </summary>
    /// <param name="timeSpan">Time to seek to in format mm:ss</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task Seek([Remainder] string timeSpan)
    {
        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            var eb = new EmbedBuilder()
                .WithErrorColor()
                .WithTitle(Strings.MusicPlayerError(ctx.Guild.Id))
                .WithDescription(result);

            await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            return;
        }

        if (player.CurrentItem is null)
        {
            await ReplyErrorAsync(Strings.MusicNoCurrentTrack(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (!TimeSpan.TryParseExact(timeSpan, "mm\\:ss", null, out var position))
        {
            await ReplyErrorAsync(Strings.MusicInvalidTimeFormat(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (position > player.CurrentItem.Track.Duration)
        {
            await ReplyErrorAsync(Strings.MusicSeekOutOfRange(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await player.SeekAsync(position).ConfigureAwait(false);
        await ReplyConfirmAsync(Strings.MusicSeekedTo(ctx.Guild.Id, position.ToString(@"mm\:ss"))).ConfigureAwait(false);
    }

    /// <summary>
    /// Initiates or participates in a vote to skip the current track.
    /// </summary>
    /// <remarks>
    /// Requires 70% of users in the voice channel to vote for skipping.
    /// Users with specific roles can be configured to skip without voting.
    /// </remarks>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task VoteSkip()
    {
        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            var eb = new EmbedBuilder()
                .WithErrorColor()
                .WithTitle(Strings.MusicPlayerError(ctx.Guild.Id))
                .WithDescription(result);

            await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            return;
        }

        if (player.CurrentItem is null)
        {
            await ReplyErrorAsync(Strings.MusicNoCurrentTrack(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        // Check if user has DJ role for instant skip
        if (await HasDjRole(ctx.Guild, ctx.User as IGuildUser))
        {
            await player.SeekAsync(player.CurrentItem.Track.Duration).ConfigureAwait(false);
            await ReplyConfirmAsync(Strings.MusicSkipDj(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var voiceChannel = (ctx.User as IGuildUser)?.VoiceChannel;
        if (voiceChannel == null)
        {
            await ReplyErrorAsync(Strings.MusicNotInChannel(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var votes = await cache.GetVoteSkip(ctx.Guild.Id) ?? new HashSet<ulong>();
        if (votes.Add(ctx.User.Id))
            await cache.SetVoteSkip(ctx.Guild.Id, votes);

        var usersInVoice = (await voiceChannel.GetUsersAsync().FlattenAsync()).Count(x => !x.IsBot);
        var votesNeeded = (int)Math.Ceiling(usersInVoice * 0.7);

        if (votes.Count >= votesNeeded)
        {
            await player.SeekAsync(player.CurrentItem.Track.Duration).ConfigureAwait(false);
            await cache.SetVoteSkip(ctx.Guild.Id, null);
            await ReplyConfirmAsync(Strings.MusicSkipVoteSuccess(ctx.Guild.Id)).ConfigureAwait(false);
        }
        else
        {
            await ReplyConfirmAsync(
                Strings.MusicSkipVoteCount(ctx.Guild.Id, votes.Count, votesNeeded)
            ).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Sets the DJ role for music commands that require elevated permissions.
    /// </summary>
    /// <param name="role">The role to set as DJ. If null, removes the DJ role.</param>
    [Cmd]
    [Aliases]
    [RequireUserPermission(GuildPermission.ManageGuild)]
    [RequireContext(ContextType.Guild)]
    public async Task SetDjRole(IRole role = null)
    {
        var settings = await cache.GetMusicPlayerSettings(ctx.Guild.Id)
                       ?? new MusicPlayerSettings { GuildId = ctx.Guild.Id };

        settings.DjRoleId = role?.Id;
        await cache.SetMusicPlayerSettings(ctx.Guild.Id, settings);

        if (role == null)
            await ReplyConfirmAsync(Strings.MusicDjRoleRemoved(ctx.Guild.Id)).ConfigureAwait(false);
        else
            await ReplyConfirmAsync(Strings.MusicDjRoleSet(ctx.Guild.Id, role.Name)).ConfigureAwait(false);
    }

    private async Task<bool> HasDjRole(IGuild guild, IGuildUser user)
    {
        if (user.GuildPermissions.Administrator) return true;

        var settings = await cache.GetMusicPlayerSettings(guild.Id);
        if (settings?.DjRoleId == null) return false;

        return user.RoleIds.Contains(settings.DjRoleId.Value);
    }

    /// <summary>
    /// Saves the current queue as a named playlist.
    /// </summary>
    /// <param name="name">The name to save the playlist as.</param>
    /// <remarks>
    /// Saves all tracks currently in the queue to a persistent playlist that can be loaded later.
    /// Playlists are saved per guild.
    /// </remarks>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task SavePlaylist([Remainder] string name)
    {
        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            var eb = new EmbedBuilder()
                .WithErrorColor()
                .WithTitle(Strings.MusicPlayerError(ctx.Guild.Id))
                .WithDescription(result);

            await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            return;
        }

        var queue = await cache.GetMusicQueue(ctx.Guild.Id);
        if (queue.Count == 0)
        {
            await ReplyErrorAsync(Strings.MusicQueueEmpty(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var playlist = new MusicPlaylist()
        {
            Name = name,
            AuthorId = ctx.User.Id,
            Tracks = queue.Select(x => new MusicPlaylistTrack()
            {
                Title = x.Track.Title,
                Uri = x.Track.Uri.ToString(),
                Duration = x.Track.Duration
            }).ToList()
        };

        await cache.SavePlaylist(ctx.Guild.Id, playlist).ConfigureAwait(false);
        await ReplyConfirmAsync(Strings.MusicPlaylistSaved(ctx.Guild.Id, name, queue.Count)).ConfigureAwait(false);
    }

    /// <summary>
    /// Loads a previously saved playlist into the queue.
    /// </summary>
    /// <param name="name">The name of the playlist to load.</param>
    /// <param name="clear">Whether to clear the current queue before loading. Defaults to false.</param>
    /// <remarks>
    /// Loads all tracks from a saved playlist into the current queue.
    /// Can optionally clear the current queue first.
    /// </remarks>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task LoadPlaylist(string name, bool clear = false)
    {
        var (player, result) = await GetPlayerAsync();
        if (result is not null)
        {
            var eb = new EmbedBuilder()
                .WithErrorColor()
                .WithTitle(Strings.MusicPlayerError(ctx.Guild.Id))
                .WithDescription(result);

            await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            return;
        }

        var playlist = await cache.GetPlaylist(ctx.Guild.Id, name);
        if (playlist == null)
        {
            await ReplyErrorAsync(Strings.MusicPlaylistNotFound(ctx.Guild.Id, name)).ConfigureAwait(false);
            return;
        }

        var queue = clear ? new List<MewdekoTrack>() : await cache.GetMusicQueue(ctx.Guild.Id);
        var startIndex = queue.Count + 1;

        foreach (var savedTrack in playlist.Tracks)
        {
            var trackResult = await service.Tracks.LoadTrackAsync(savedTrack.Uri, TrackSearchMode.YouTube);
            if (trackResult is null) continue;

            queue.Add(new MewdekoTrack(startIndex++, trackResult, new PartialUser
            {
                Id = ctx.User.Id,
                Username = ctx.User.Username,
                AvatarUrl = ctx.User.GetAvatarUrl()
            }));
        }

        await cache.SetMusicQueue(ctx.Guild.Id, queue);

        if (player.CurrentItem is null && queue.Count > 0)
        {
            await player.PlayAsync(queue[0].Track).ConfigureAwait(false);
            await cache.SetCurrentTrack(ctx.Guild.Id, queue[0]);
        }

        await ReplyConfirmAsync(
            Strings.MusicPlaylistLoaded(ctx.Guild.Id, name, playlist.Tracks.Count, playlist.AuthorId)
        ).ConfigureAwait(false);
    }

    /// <summary>
    /// Lists all saved playlists for the guild.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task Playlists()
    {
        var playlists = await cache.GetPlaylists(ctx.Guild.Id);
        if (!playlists.Any())
        {
            await ReplyErrorAsync(Strings.MusicNoPlaylists(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var sb = new StringBuilder();
        foreach (var playlist in playlists)
        {
            var user = await ctx.Guild.GetUserAsync(playlist.AuthorId);
            sb.AppendLine(Strings.MusicPlaylistEntry(
                ctx.Guild.Id,
                playlist.Name,
                playlist.Tracks.Count,
                user?.Username ?? "Unknown"
            ));
        }

        var eb = new EmbedBuilder()
            .WithTitle(Strings.MusicPlaylistsTitle(ctx.Guild.Id))
            .WithDescription(sb.ToString())
            .WithOkColor()
            .Build();

        await ctx.Channel.SendMessageAsync(embed: eb).ConfigureAwait(false);
    }

    /// <summary>
    /// Removes a saved playlist.
    /// </summary>
    /// <param name="name">The name of the playlist to remove.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task DeletePlaylist([Remainder] string name)
    {
        var success = await cache.DeletePlaylist(ctx.Guild.Id, name);
        if (success)
            await ReplyConfirmAsync(Strings.MusicPlaylistDeleted(ctx.Guild.Id, name)).ConfigureAwait(false);
        else
            await ReplyErrorAsync(Strings.MusicPlaylistNotFound(ctx.Guild.Id, name)).ConfigureAwait(false);
    }

    /// <summary>
    /// Searches for tracks without automatically playing them.
    /// </summary>
    /// <param name="query">The search query</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task Search([Remainder] string query)
    {
        var tracks = await service.Tracks.LoadTracksAsync(query, TrackSearchMode.YouTube);

        if (!tracks.IsSuccess)
        {
            await ReplyErrorAsync(Strings.MusicNoTracks(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var trackList = tracks.Tracks.Take(10).ToList();
        var sb = new StringBuilder();

        for (int i = 0; i < trackList.Count; i++)
        {
            var track = trackList[i];
            sb.AppendLine($"`{i + 1}.` [{track.Title}]({track.Uri}) `{track.Duration}`");
        }

        var eb = new EmbedBuilder()
            .WithTitle(Strings.MusicSearchResults(ctx.Guild.Id))
            .WithDescription(sb.ToString())
            .WithFooter(Strings.MusicSearchUsePlay(ctx.Guild.Id))
            .WithOkColor()
            .Build();

        await ctx.Channel.SendMessageAsync(embed: eb);
    }

    /// <summary>
    /// Shuffles the current music queue.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task Shuffle()
    {
        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            var eb = new EmbedBuilder()
                .WithErrorColor()
                .WithTitle(Strings.MusicPlayerError(ctx.Guild.Id))
                .WithDescription(result);

            await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            return;
        }

        var queue = await cache.GetMusicQueue(ctx.Guild.Id);
        if (queue.Count <= 1)
        {
            await ReplyErrorAsync(Strings.MusicQueueTooShort(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var currentTrack = await cache.GetCurrentTrack(ctx.Guild.Id);
        var remainingTracks = queue.Where(x => x.Index != currentTrack.Index).ToList();

        // Fisher-Yates shuffle
        var rng = new Random();
        int n = remainingTracks.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            (remainingTracks[k], remainingTracks[n]) = (remainingTracks[n], remainingTracks[k]);
        }

        // Reassign indices
        for (int i = 0; i < remainingTracks.Count; i++)
        {
            remainingTracks[i].Index = i + 1;
        }

        var newQueue = new List<MewdekoTrack> { currentTrack };
        newQueue.AddRange(remainingTracks);

        await cache.SetMusicQueue(ctx.Guild.Id, newQueue);
        await ReplyConfirmAsync(Strings.MusicQueueShuffled(ctx.Guild.Id)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Skips to the next track.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task Skip()
    {
        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            var eb = new EmbedBuilder()
                .WithErrorColor()
                .WithTitle(Strings.MusicPlayerError(ctx.Guild.Id))
                .WithDescription(result);

            await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            return;
        }

        if (player.CurrentItem is null)
        {
            await ReplyErrorAsync(Strings.MusicNoCurrentTrack(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await player.SeekAsync(player.CurrentItem.Track.Duration).ConfigureAwait(false);
    }

    /// <summary>
    ///     The music queue.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task Queue()
    {
        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            var eb = new EmbedBuilder()
                .WithErrorColor()
                .WithTitle(Strings.MusicPlayerError(ctx.Guild.Id))
                .WithDescription(result);

            await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            return;
        }

        var queue = await cache.GetMusicQueue(ctx.Guild.Id);
        if (queue.Count == 0)
        {
            await ReplyErrorAsync(Strings.MusicQueueEmpty(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var currentTrack = await cache.GetCurrentTrack(ctx.Guild.Id);

        var paginator = new LazyPaginatorBuilder().AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex((queue.Count - 1) / 10)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await interactiveService.SendPaginatorAsync(paginator, ctx.Channel, TimeSpan.FromMinutes(5));

        async Task<PageBuilder> PageFactory(int index)
        {
            await Task.CompletedTask;
            var tracks = queue.OrderBy(x => x.Index).Skip(index * 10).Take(10).ToList();
            var sb = new StringBuilder();
            foreach (var track in tracks)
            {
                if (currentTrack.Index == track.Index)
                    sb.AppendLine(
                        $":loud_sound: **{track.Index}. [{track.Track.Title}]({track.Track.Uri})**" +
                        $"\n`{track.Track.Duration} {track.Requester.Username} {track.Track.Provider}`");
                else
                    sb.AppendLine($"{track.Index}. [{track.Track.Title}]({track.Track.Uri})" +
                                  $"\n`{track.Track.Duration} {track.Requester.Username} {track.Track.Provider}`");
            }

            return new PageBuilder()
                .WithTitle($"Queue - {queue.Count} tracks")
                .WithDescription(sb.ToString())
                .WithOkColor();
        }
    }

    /// <summary>
    ///     Sets the autoplay amount in the guild. Uses spotify api so client secret and id must be valid.
    /// </summary>
    /// <param name="amount">The amount of tracks to autoplay. Max of 5</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task AutoPlay(int amount)
    {
        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            var eb = new EmbedBuilder()
                .WithErrorColor()
                .WithTitle(Strings.MusicPlayerError(ctx.Guild.Id))
                .WithDescription(result);

            await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            return;
        }

        if (amount is < 0 or > 5)
        {
            await ReplyErrorAsync(Strings.AutoplayDisabled(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await player.SetAutoPlay(amount).ConfigureAwait(false);
        if (amount == 0)
        {
            await ReplyConfirmAsync(Strings.AutoplayDisabled(ctx.Guild.Id)).ConfigureAwait(false);
        }
        else
        {
            await ReplyConfirmAsync(Strings.MusicAutoplaySet(ctx.Guild.Id, amount)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Gets the guilds current settings for music.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task MusicSettings()
    {
        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            var eb = new EmbedBuilder()
                .WithErrorColor()
                .WithTitle(Strings.MusicPlayerError(ctx.Guild.Id))
                .WithDescription(result);

            await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            return;
        }

        var volume = await player.GetVolume();
        var autoplay = await player.GetAutoPlay();
        var repeat = await player.GetRepeatType();
        var musicChannel = await player.GetMusicChannel();

        var toSend = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.MusicSettings(ctx.Guild.Id))
            .WithDescription(
                $"{(autoplay == 0 ? Strings.MusicsettingsAutoplayDisabled(ctx.Guild.Id) : Strings.MusicsettingsAutoplay(ctx.Guild.Id, autoplay))}\n" +
                $"{Strings.MusicsettingsVolume(ctx.Guild.Id, volume)}\n" +
                $"{Strings.MusicsettingsRepeat(ctx.Guild.Id, repeat)}\n" +
                $"{(musicChannel == null ? Strings.UnsetMusicChannel(ctx.Guild.Id) : Strings.MusicsettingsChannel(ctx.Guild.Id, musicChannel.Id))}");

        await ctx.Channel.SendMessageAsync(embed: toSend.Build()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets the channel where music events will be sent.
    /// </summary>
    /// <param name="channel">The channel where music events will be sent.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task SetMusicChannel(IMessageChannel channel = null)
    {
        var channelToUse = channel ?? ctx.Channel;
        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            var eb = new EmbedBuilder()
                .WithErrorColor()
                .WithTitle(Strings.MusicPlayerError(ctx.Guild.Id))
                .WithDescription(result);

            await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            return;
        }

        await player.SetMusicChannelAsync(channelToUse.Id).ConfigureAwait(false);
        await ReplyConfirmAsync(Strings.MusicChannelSet(ctx.Guild.Id, channelToUse.Id)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets if the bot should loop and how.
    /// </summary>
    /// <param name="repeatType">The repeat type.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task Loop(PlayerRepeatType repeatType)
    {
        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            var eb = new EmbedBuilder()
                .WithErrorColor()
                .WithTitle(Strings.MusicPlayerError(ctx.Guild.Id))
                .WithDescription(result);

            await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            return;
        }

        await player.SetRepeatTypeAsync(repeatType).ConfigureAwait(false);
        await ReplyConfirmAsync(Strings.MusicRepeatType(ctx.Guild.Id, repeatType)).ConfigureAwait(false);
    }


    private async ValueTask<(MewdekoPlayer, string?)> GetPlayerAsync(bool connectToVoiceChannel = true)
    {
        try
        {
            var channelBehavior = connectToVoiceChannel
                ? PlayerChannelBehavior.Join
                : PlayerChannelBehavior.None;

            var retrieveOptions = new PlayerRetrieveOptions(channelBehavior);

            var options = new MewdekoPlayerOptions
            {
                Channel = ctx.Channel as ITextChannel
            };

            var result = await service.Players
                .RetrieveAsync<MewdekoPlayer, MewdekoPlayerOptions>(Context, CreatePlayerAsync, options,
                    retrieveOptions)
                .ConfigureAwait(false);

            await result.Player.SetVolumeAsync(await result.Player.GetVolume() / 100f).ConfigureAwait(false);

            if (result.IsSuccess) return (result.Player, null);
            var errorMessage = result.Status switch
            {
                PlayerRetrieveStatus.UserNotInVoiceChannel => Strings.MusicNotInChannel(ctx.Guild.Id),
                PlayerRetrieveStatus.BotNotConnected => Strings.MusicBotNotConnect(ctx.Guild.Id,
                    await guildSettingsService.GetPrefix(ctx.Guild)),
                PlayerRetrieveStatus.VoiceChannelMismatch => Strings.MusicVoiceChannelMismatch(ctx.Guild.Id),
                PlayerRetrieveStatus.Success => null,
                PlayerRetrieveStatus.UserInSameVoiceChannel => null,
                PlayerRetrieveStatus.PreconditionFailed => null,
                _ => throw new ArgumentOutOfRangeException()
            };
            return (null, errorMessage);
        }
        catch (TimeoutException)
        {
            return (null, Strings.MusicLavalinkDisconnected(ctx.Guild.Id));
        }
    }

    private static ValueTask<MewdekoPlayer> CreatePlayerAsync(
        IPlayerProperties<MewdekoPlayer, MewdekoPlayerOptions> properties,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(properties);

        return ValueTask.FromResult(new MewdekoPlayer(properties));
    }
}
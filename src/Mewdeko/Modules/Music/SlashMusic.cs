using System.Text;
using System.Text.Json;
using System.Threading;
using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Lavalink4NET;
using Lavalink4NET.DiscordNet;
using Lavalink4NET.Players;
using Lavalink4NET.Rest.Entities.Tracks;
using Lavalink4NET.Tracks;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.Music.Common;
using Mewdeko.Modules.Music.CustomPlayer;
using Serilog;
using Swan;

namespace Mewdeko.Modules.Music;

/// <summary>
///     Slash commands module containing music commands.
/// </summary>
[Group("music", "Music commands")]
public class SlashMusic(
    IAudioService service,
    IDataCache cache,
    InteractiveService interactiveService,
    GuildSettingsService guildSettingsService) : MewdekoSlashCommandModule
{
    /// <summary>
    ///     Joins the voice channel.
    /// </summary>
    [SlashCommand("join", "Joins your voice channel")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
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

            await Context.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Disconnects the bot from the voice channel.
    /// </summary>
    [SlashCommand("leave", "Disconnects the bot from the voice channel")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Leave()
    {
        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            var eb = new EmbedBuilder()
                .WithErrorColor()
                .WithTitle(Strings.MusicPlayerError(ctx.Guild.Id))
                .WithDescription(result);

            await Context.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            return;
        }

        await cache.SetMusicQueue(Context.Guild.Id, new List<MewdekoTrack>()).ConfigureAwait(false);
        await cache.SetCurrentTrack(Context.Guild.Id, null);

        await player.DisconnectAsync().ConfigureAwait(false);
        await ReplyConfirmAsync(Strings.MusicDisconnect(ctx.Guild.Id)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Clears the music queue.
    /// </summary>
    [SlashCommand("clearqueue", "Clears the music queue")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task ClearQueue()
    {
        var (player, result) = await GetPlayerAsync(false);

        if (result is not null)
        {
            var eb = new EmbedBuilder()
                .WithErrorColor()
                .WithTitle(Strings.MusicPlayerError(ctx.Guild.Id))
                .WithDescription(result);

            await Context.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            return;
        }

        await cache.SetMusicQueue(Context.Guild.Id, new List<MewdekoTrack>()).ConfigureAwait(false);
        await ReplyConfirmAsync(Strings.MusicQueueCleared(ctx.Guild.Id)).ConfigureAwait(false);
        await player.StopAsync();
        await cache.SetCurrentTrack(Context.Guild.Id, null);
    }

    /// <summary>
    ///     Plays a specified track in the current voice channel.
    /// </summary>
    /// <param name="queueNumber">The queue number to play.</param>
    [SlashCommand("playnumber", "Plays a song from the queue by its number")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task PlayNumber(int queueNumber)
    {
        var (player, result) = await GetPlayerAsync(false);

        if (result is not null)
        {
            var eb = new EmbedBuilder()
                .WithErrorColor()
                .WithTitle(Strings.MusicPlayerError(ctx.Guild.Id))
                .WithDescription(result);

            await Context.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            return;
        }

        var queue = await cache.GetMusicQueue(Context.Guild.Id);
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

        var trackToPlay = queue.FirstOrDefault(x => x.Index == queueNumber);
        await player.StopAsync();
        await player.PlayAsync(trackToPlay.Track).ConfigureAwait(false);
        await cache.SetCurrentTrack(Context.Guild.Id, trackToPlay);
    }

    /// <summary>
    ///     Plays a track in the current voice channel.
    /// </summary>
    /// <param name="query">The query to search for.</param>
    [SlashCommand("play", "Plays a song from a query or URL")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Play(string query)
    {
        var (player, result) = await GetPlayerAsync();

        if (result is not null)
        {
            var eb = new EmbedBuilder()
                .WithErrorColor()
                .WithTitle(Strings.MusicPlayerError(ctx.Guild.Id))
                .WithDescription(result);

            await Context.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            return;
        }

        await player.SetVolumeAsync(await player.GetVolume() / 100f).ConfigureAwait(false);

        var queue = await cache.GetMusicQueue(Context.Guild.Id);
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
                        Id = Context.User.Id, Username = Context.User.Username, AvatarUrl = Context.User.GetAvatarUrl()
                    })));
                await cache.SetMusicQueue(Context.Guild.Id, queue);

                var eb = new EmbedBuilder()
                    .WithDescription(
                        $"Added {trackResults.Tracks.Length} tracks to the queue from {trackResults.Playlist.Name}")
                    .WithThumbnailUrl(trackResults.Tracks[0].ArtworkUri?.ToString())
                    .WithOkColor()
                    .Build();

                await Context.Channel.SendMessageAsync(embed: eb).ConfigureAwait(false);
            }
            else
            {
                queue.Add(new MewdekoTrack(queue.Count + 1, trackResults.Tracks[0], new PartialUser
                {
                    Id = Context.User.Id, Username = Context.User.Username, AvatarUrl = Context.User.GetAvatarUrl()
                }));
                await cache.SetMusicQueue(Context.Guild.Id, queue);
            }

            if (player.CurrentItem is null)
            {
                await player.PlayAsync(trackResults.Tracks[0]).ConfigureAwait(false);
                await cache.SetCurrentTrack(Context.Guild.Id, queue[0]);
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
                .WithCustomId($"track_select:{Context.User.Id}")
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

            var message = await Context.Channel.SendMessageAsync(embed: eb, components: components);

            await cache.Redis.GetDatabase().StringSetAsync($"{Context.User.Id}_{message.Id}_tracks",
                JsonSerializer.Serialize(trackList), TimeSpan.FromMinutes(5));
        }
    }

    /// <summary>
    ///     Pauses or resumes the player based on the current state.
    /// </summary>
    [SlashCommand("pause", "Pauses or resumes the player")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Pause()
    {
        var (player, result) = await GetPlayerAsync();

        if (result is not null)
        {
            var eb = new EmbedBuilder()
                .WithErrorColor()
                .WithTitle(Strings.MusicPlayerError(ctx.Guild.Id))
                .WithDescription(result);

            await Context.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
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
    ///     Displays the currently playing track.
    /// </summary>
    [SlashCommand("nowplaying", "Displays the currently playing track")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
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

                await Context.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
                return;
            }

            var queue = await cache.GetMusicQueue(Context.Guild.Id);

            if (queue.Count == 0)
            {
                await ReplyErrorAsync(Strings.MusicQueueEmpty(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var embed = await player.PrettyNowPlayingAsync(queue);
            await Context.Channel.SendMessageAsync(embed: embed).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Log.Error("Failed to get now playing track: {Message}", e.Message);
        }
    }

    /// <summary>
    ///     Removes a song from the queue by its number.
    /// </summary>
    /// <param name="queueNumber">The queue number to remove.</param>
    [SlashCommand("removesong", "Removes a song from the queue by its number")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task SongRemove(int queueNumber)
    {
        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            var eb = new EmbedBuilder()
                .WithErrorColor()
                .WithTitle(Strings.MusicPlayerError(ctx.Guild.Id))
                .WithDescription(result);

            await Context.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            return;
        }

        var queue = await cache.GetMusicQueue(Context.Guild.Id);
        var currentTrack = await cache.GetCurrentTrack(Context.Guild.Id);
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

        var trackToRemove = queue.FirstOrDefault(x => x.Index == queueNumber);
        if (trackToRemove == null)
        {
            await ReplyErrorAsync(Strings.MusicQueueInvalidIndex(ctx.Guild.Id, queue.Count)).ConfigureAwait(false);
            return;
        }

        queue.Remove(trackToRemove);

        if (currentTrack.Index == trackToRemove.Index)
        {
            if (nextTrack != null)
            {
                await player.StopAsync();
                await player.PlayAsync(nextTrack.Track);
                await cache.SetCurrentTrack(Context.Guild.Id, nextTrack);
            }
            else
            {
                await player.StopAsync();
                await cache.SetCurrentTrack(Context.Guild.Id, null);
            }
        }

        await cache.SetMusicQueue(Context.Guild.Id, queue);

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
    [SlashCommand("movesong", "Moves a song in the queue to a new position")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task MoveSong(int from, int to)
    {
        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            var eb = new EmbedBuilder()
                .WithErrorColor()
                .WithTitle(Strings.MusicPlayerError(ctx.Guild.Id))
                .WithDescription(result);

            await Context.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            return;
        }

        var queue = await cache.GetMusicQueue(Context.Guild.Id);
        if (queue.Count == 0)
        {
            await ReplyErrorAsync(Strings.MusicQueueEmpty(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (from < 1 || from > queue.Count || to < 1 || to > queue.Count)
        {
            await ReplyErrorAsync(Strings.MusicQueueInvalidIndex(ctx.Guild.Id, queue.Count)).ConfigureAwait(false);
            return;
        }

        var track = queue.FirstOrDefault(x => x.Index == from);
        queue.Remove(track);
        queue.Insert(to - 1, track);

        for (int i = 0; i < queue.Count; i++)
        {
            queue[i].Index = i + 1;
        }

        var currentSong = await cache.GetCurrentTrack(Context.Guild.Id);
        if (currentSong.Index == from)
        {
            await cache.SetCurrentTrack(Context.Guild.Id, track);
        }

        await cache.SetMusicQueue(Context.Guild.Id, queue);
        await ReplyConfirmAsync(Strings.MusicSongMoved(ctx.Guild.Id, track.Track.Title, to)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets the player's volume.
    /// </summary>
    /// <param name="volume">The volume to set (0-100).</param>
    [SlashCommand("volume", "Sets the player's volume")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Volume(int volume)
    {
        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            var eb = new EmbedBuilder()
                .WithErrorColor()
                .WithTitle(Strings.MusicPlayerError(ctx.Guild.Id))
                .WithDescription(result);

            await Context.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
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
    ///     Skips the current track.
    /// </summary>
    [SlashCommand("skip", "Skips the current track")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Skip()
    {
        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            var eb = new EmbedBuilder()
                .WithErrorColor()
                .WithTitle(Strings.MusicPlayerError(ctx.Guild.Id))
                .WithDescription(result);

            await Context.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            return;
        }

        if (player.CurrentItem is null)
        {
            await ReplyErrorAsync(Strings.MusicNoCurrentTrack(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await player.SeekAsync(player.CurrentItem.Track.Duration).ConfigureAwait(false);
        await ReplyConfirmAsync(Strings.SkippedTo(ctx.Guild.Id, player.CurrentTrack.Author, player.CurrentTrack.Title)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Displays the current music queue.
    /// </summary>
    [SlashCommand("queue", "Displays the current music queue")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Queue()
    {
        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            var eb = new EmbedBuilder()
                .WithErrorColor()
                .WithTitle(Strings.MusicPlayerError(ctx.Guild.Id))
                .WithDescription(result);

            await Context.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            return;
        }

        var queue = await cache.GetMusicQueue(Context.Guild.Id);
        if (queue.Count == 0)
        {
            await ReplyErrorAsync(Strings.MusicQueueEmpty(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var currentTrack = await cache.GetCurrentTrack(Context.Guild.Id);

        var paginator = new LazyPaginatorBuilder()
            .AddUser(Context.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex((queue.Count - 1) / 10)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await interactiveService.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(5));

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
    ///     Sets the autoplay amount in the guild.
    /// </summary>
    /// <param name="amount">The amount of tracks to autoplay (max 5).</param>
    [SlashCommand("autoplay", "Sets the autoplay amount")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task AutoPlay(int amount)
    {
        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            var eb = new EmbedBuilder()
                .WithErrorColor()
                .WithTitle(Strings.MusicPlayerError(ctx.Guild.Id))
                .WithDescription(result);

            await Context.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            return;
        }

        if (amount is < 0 or > 5)
        {
            await ReplyErrorAsync(Strings.MusicAutoPlayInvalid(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await player.SetAutoPlay(amount).ConfigureAwait(false);
        if (amount == 0)
        {
            await ReplyConfirmAsync(Strings.MusicAutoPlayDisabled(ctx.Guild.Id)).ConfigureAwait(false);
        }
        else
        {
            await ReplyConfirmAsync(Strings.MusicAutoplaySet(ctx.Guild.Id, amount)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Displays the guild's current music settings.
    /// </summary>
    [SlashCommand("musicsettings", "Displays the current music settings")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task MusicSettings()
    {
        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            var eb = new EmbedBuilder()
                .WithErrorColor()
                .WithTitle(Strings.MusicPlayerError(ctx.Guild.Id))
                .WithDescription(result);

            await Context.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
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

        await Context.Channel.SendMessageAsync(embed: toSend.Build()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets the channel where music events will be sent.
    /// </summary>
    /// <param name="channel">The channel where music events will be sent.</param>
    [SlashCommand("setmusicchannel", "Sets the channel where music events will be sent")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task SetMusicChannel(ITextChannel channel = null)
    {
        var channelToUse = channel ?? Context.Channel;
        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            var eb = new EmbedBuilder()
                .WithErrorColor()
                .WithTitle(Strings.MusicPlayerError(ctx.Guild.Id))
                .WithDescription(result);

            await Context.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            return;
        }

        await player.SetMusicChannelAsync(channelToUse.Id).ConfigureAwait(false);
        await ReplyConfirmAsync(Strings.MusicChannelSet(ctx.Guild.Id, channelToUse.Id)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets the loop mode.
    /// </summary>
    /// <param name="repeatType">The repeat type.</param>
    [SlashCommand("loop", "Sets the loop mode")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Loop(PlayerRepeatType repeatType)
    {
        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            var eb = new EmbedBuilder()
                .WithErrorColor()
                .WithTitle(Strings.MusicPlayerError(ctx.Guild.Id))
                .WithDescription(result);

            await Context.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            return;
        }

        await player.SetRepeatTypeAsync(repeatType).ConfigureAwait(false);
        await ReplyConfirmAsync(Strings.MusicRepeatType(ctx.Guild.Id, repeatType)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Handling track selection for the play command select menu.
    /// </summary>
    /// <param name="userId">The original user who summoned the select menu</param>
    /// <param name="selectedValue">The selected track.</param>
    [ComponentInteraction("track_select:*", true)]
    [CheckPermissions]
    public async Task TrackSelect(ulong userId, string[] selectedValue)
    {
        await DeferAsync();

        if (ctx.User.Id != userId) return;

        var componentInteraction = ctx.Interaction as IComponentInteraction;

        var (player, result) = await GetPlayerAsync(false);

        var tracks = await cache.Redis.GetDatabase()
            .StringGetAsync($"{ctx.User.Id}_{componentInteraction.Message.Id}_tracks");

        var trackList = JsonSerializer.Deserialize<List<LavalinkTrack>>(tracks);

        var queue = await cache.GetMusicQueue(ctx.Guild.Id);

        var selectedTracks = selectedValue.Select(i => trackList[Convert.ToInt32(i.Split("_")[1])]).ToList();

        var startIndex = queue.Count + 1;
        queue.AddRange(
            selectedTracks.Select(track => new MewdekoTrack(startIndex++, track, new PartialUser
            {
                Id = ctx.User.Id, Username = ctx.User.Username, AvatarUrl = ctx.User.GetAvatarUrl()
            })));

        if (selectedTracks.Count == 1)
        {
            var eb = new EmbedBuilder()
                .WithAuthor(Strings.MusicAdded(ctx.Guild.Id))
                .WithDescription($"[{selectedTracks[0].Title}]({selectedTracks[0].Uri}) by {selectedTracks[0].Author}")
                .WithImageUrl(selectedTracks[0].ArtworkUri.ToString())
                .WithOkColor();

            await FollowupAsync(embed: eb.Build());
        }
        else
        {
            var paginator = new LazyPaginatorBuilder().AddUser(ctx.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(queue.Count / 10)
                .WithDefaultEmotes()
                .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                .Build();

            await interactiveService.SendPaginatorAsync(paginator, ctx.Interaction, TimeSpan.FromMinutes(5),
                InteractionResponseType.DeferredChannelMessageWithSource);

            async Task<PageBuilder> PageFactory(int index)
            {
                await Task.CompletedTask;
                var tracks = queue.Skip(index * 10).Take(10).ToList();
                var sb = new StringBuilder();
                foreach (var track in tracks)
                {
                    sb.AppendLine($"{track.Index}. [{track.Track.Title}]({track.Track.Uri})");
                }

                return new PageBuilder()
                    .WithTitle($"Queue - {queue.Count} tracks")
                    .WithDescription(sb.ToString())
                    .WithOkColor();
            }
        }

        if (player.CurrentItem == null)
        {
            await cache.SetCurrentTrack(ctx.Guild.Id,
                new MewdekoTrack(1, selectedTracks[0], new PartialUser
                {
                    Id = ctx.User.Id, Username = ctx.User.Username, AvatarUrl = ctx.User.GetAvatarUrl()
                }));
            await player.PlayAsync(selectedTracks[0]);
        }

        await cache.SetMusicQueue(ctx.Guild.Id, queue);
        await cache.Redis.GetDatabase().KeyDeleteAsync($"{ctx.User.Id}_{componentInteraction.Message.Id}_tracks");
        await ctx.Channel.DeleteMessageAsync(componentInteraction.Message.Id);
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
                Channel = Context.Channel as ITextChannel
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
                    await guildSettingsService.GetPrefix(Context.Guild)),
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

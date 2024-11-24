using Lavalink4NET;
using Lavalink4NET.Filters;
using Lavalink4NET.Players;
using Lavalink4NET.Rest.Entities.Tracks;
using Mewdeko.Modules.Music.Common;
using Mewdeko.Modules.Music.CustomPlayer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Controller for managing music playback and settings
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class MusicController : Controller
{
    private readonly IAudioService audioService;
    private readonly IDataCache cache;
    private readonly DiscordShardedClient client;

    /// <summary>
    ///     Controller for managing music playback and settings
    /// </summary>
    /// <param name="audioService"></param>
    /// <param name="cache"></param>
    /// <param name="client"></param>
    /// <param name="db"></param>
    public MusicController(
        IAudioService audioService,
        IDataCache cache,
        DiscordShardedClient client)
    {
        this.audioService = audioService;
        this.cache = cache;
        this.client = client;
    }

    /// <summary>
    ///     Gets the current player state and track information
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetPlayerStatus(ulong guildId, [FromQuery] ulong userId)
    {
        var player = await audioService.Players.GetPlayerAsync<MewdekoPlayer>(guildId);
        if (player == null)
            return NotFound("No active player found");

        var guild = client.GetGuild(guildId);
        var user = guild?.GetUser(userId);
        var botVoiceChannel = player.VoiceChannelId;
        var isInVoiceChannel = user?.VoiceChannel?.Id == botVoiceChannel;

        var currentTrack = await cache.GetCurrentTrack(guildId);
        var queue = await cache.GetMusicQueue(guildId);
        var settings = await cache.GetMusicPlayerSettings(guildId);

        return Ok(new
        {
            CurrentTrack = currentTrack,
            Queue = queue,
            player.State,
            player.Volume,
            player.Position,
            RepeatMode = settings.PlayerRepeat,
            Filters = new
            {
                BassBoost = player.Filters.Equalizer != null,
                Nightcore = player.Filters.Timescale?.Speed > 1.0f,
                Vaporwave = player.Filters.Timescale?.Speed < 1.0f,
                Karaoke = player.Filters.Karaoke != null,
                Tremolo = player.Filters.Tremolo != null,
                Vibrato = player.Filters.Vibrato != null,
                Rotation = player.Filters.Rotation != null,
                Distortion = player.Filters.Distortion != null,
                ChannelMix = player.Filters.ChannelMix != null
            },
            IsInVoiceChannel = isInVoiceChannel
        });
    }

    /// <summary>
    ///     Plays or enqueues a track
    /// </summary>
    [HttpPost("play")]
    public async Task<IActionResult> Play(ulong guildId, [FromBody] PlayRequest request)
    {
        var guild = client.GetGuild(guildId);
        if (guild == null)
            return NotFound("Guild not found");

        var player = await audioService.Players.GetPlayerAsync<MewdekoPlayer>(guildId);
        if (player == null)
            return NotFound("No active player found");

        var searchMode = request.Url.Contains("spotify") ? TrackSearchMode.Spotify :
            request.Url.Contains("youtube") ? TrackSearchMode.YouTube : TrackSearchMode.None;

        var trackResult = await audioService.Tracks.LoadTrackAsync(request.Url, new TrackLoadOptions
        {
            SearchMode = searchMode
        });
        if (trackResult is null)
            return BadRequest("Failed to load track");

        var queue = await cache.GetMusicQueue(guildId);
        queue.Add(new MewdekoTrack(queue.Count + 1, trackResult, request.Requester));
        await cache.SetMusicQueue(guildId, queue);

        if (player.State != PlayerState.Playing)
        {
            await player.PlayAsync(trackResult);
            await cache.SetCurrentTrack(guildId, queue[0]);
        }

        return Ok(new
        {
            Track = trackResult, Position = queue.Count
        });
    }

    /// <summary>
    ///     Pauses or resumes playback
    /// </summary>
    [HttpPost("pause")]
    public async Task<IActionResult> PauseResume(ulong guildId)
    {
        var player = await audioService.Players.GetPlayerAsync<MewdekoPlayer>(guildId);
        if (player == null)
            return NotFound("No active player found");

        if (player.State == PlayerState.Playing)
            await player.PauseAsync();
        else
            await player.ResumeAsync();

        return Ok(new
        {
            player.State
        });
    }

    /// <summary>
    ///     Gets the current queue
    /// </summary>
    [HttpGet("queue")]
    public async Task<IActionResult> GetQueue(ulong guildId)
    {
        var queue = await cache.GetMusicQueue(guildId);
        return Ok(queue);
    }

    /// <summary>
    ///     Clears the queue
    /// </summary>
    [HttpDelete("queue")]
    public async Task<IActionResult> ClearQueue(ulong guildId)
    {
        await cache.SetMusicQueue(guildId, new List<MewdekoTrack>());
        return Ok();
    }

    /// <summary>
    ///     Removes a track from the queue
    /// </summary>
    [HttpDelete("queue/{index}")]
    public async Task<IActionResult> RemoveTrack(ulong guildId, int index)
    {
        var queue = await cache.GetMusicQueue(guildId);
        var track = queue.FirstOrDefault(x => x.Index == index);
        if (track == null)
            return NotFound("Track not found");

        queue.Remove(track);
        await cache.SetMusicQueue(guildId, queue);
        return Ok();
    }

    /// <summary>
    ///     Sets the volume
    /// </summary>
    [HttpPost("volume/{volume}")]
    public async Task<IActionResult> SetVolume(ulong guildId, int volume)
    {
        if (volume is < 0 or > 100)
            return BadRequest("Volume must be between 0 and 100");

        var player = await audioService.Players.GetPlayerAsync<MewdekoPlayer>(guildId);
        if (player == null)
            return NotFound("No active player found");

        await player.SetVolumeAsync(volume / 100f);
        await player.SetGuildVolumeAsync(volume);
        return Ok(new
        {
            Volume = volume
        });
    }

    /// <summary>
    ///     Gets or sets player settings
    /// </summary>
    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings(ulong guildId)
    {
        var settings = await cache.GetMusicPlayerSettings(guildId);
        return Ok(settings);
    }

    /// <summary>
    ///     Updates player settings
    /// </summary>
    [HttpPost("settings")]
    public async Task<IActionResult> UpdateSettings(ulong guildId, [FromBody] MusicPlayerSettings settings)
    {
        var player = await audioService.Players.GetPlayerAsync<MewdekoPlayer>(guildId);
        await player.SetMusicSettings(guildId, settings);
        return Ok(settings);
    }

    /// <summary>
    ///     Gets or sets filters
    /// </summary>
    [HttpPost("filter/{filterName}")]
    public async Task<IActionResult> ToggleFilter(ulong guildId, string filterName, [FromBody] bool enable)
    {
        var player = await audioService.Players.GetPlayerAsync<MewdekoPlayer>(guildId);
        if (player == null)
            return NotFound("No active player found");

        switch (filterName.ToLower())
        {
            case "bass":
                player.Filters.Equalizer = enable
                    ? new EqualizerFilterOptions(new Equalizer
                    {
                        [0] = 0.6f, [1] = 0.67f, [2] = 0.67f, [3] = 0.4f
                    })
                    : null;
                break;
            case "nightcore":
                player.Filters.Timescale = enable
                    ? new TimescaleFilterOptions
                    {
                        Speed = 1.2f, Pitch = 1.2f, Rate = 1.0f
                    }
                    : null;
                break;
            // Add other filter cases...
            default:
                return BadRequest("Unknown filter");
        }

        await player.Filters.CommitAsync();
        return Ok(new
        {
            Filter = filterName, Enabled = enable
        });
    }

    /// <summary>
    ///     Gets user playlists
    /// </summary>
    [HttpGet("playlists/{userId}")]
    public async Task<IActionResult> GetUserPlaylists(ulong userId)
    {
        var playlists = await cache.GetPlaylists(userId);
        return Ok(playlists);
    }

    /// <summary>
    ///     Updates auto disconnect settings
    /// </summary>
    [HttpPost("settings/autodisconnect")]
    public async Task<IActionResult> UpdateAutoDisconnect(ulong guildId, [FromBody] AutoDisconnect setting)
    {
        var settings = await cache.GetMusicPlayerSettings(guildId);
        settings.AutoDisconnect = setting;
        await cache.SetMusicPlayerSettings(guildId, settings);
        return Ok(setting);
    }

    /// <summary>
    ///     Updates vote skip settings
    /// </summary>
    [HttpPost("settings/voteskip")]
    public async Task<IActionResult> UpdateVoteSkip(ulong guildId, [FromBody] VoteSkipSettings voteSkip)
    {
        var settings = await cache.GetMusicPlayerSettings(guildId);
        settings.VoteSkipEnabled = voteSkip.Enabled;
        settings.VoteSkipThreshold = Math.Clamp(voteSkip.Threshold, 1, 100);
        await cache.SetMusicPlayerSettings(guildId, settings);
        return Ok(new
        {
            settings.VoteSkipEnabled, settings.VoteSkipThreshold
        });
    }

    /// <summary>
    ///     Updates music channel settings
    /// </summary>
    [HttpPost("settings/channel")]
    public async Task<IActionResult> UpdateMusicChannel(ulong guildId, [FromBody] ulong? channelId)
    {
        var settings = await cache.GetMusicPlayerSettings(guildId);
        settings.MusicChannelId = channelId;
        await cache.SetMusicPlayerSettings(guildId, settings);
        return Ok(new
        {
            MusicChannelId = channelId
        });
    }

    private PlayerRepeatType ConvertRepeatType(PlayerRepeatType type)
    {
        return type switch
        {
            PlayerRepeatType.None or PlayerRepeatType.Off => PlayerRepeatType.None,
            PlayerRepeatType.Track or PlayerRepeatType.Song => PlayerRepeatType.Track,
            PlayerRepeatType.Queue or PlayerRepeatType.All => PlayerRepeatType.Queue,
            _ => PlayerRepeatType.None
        };
    }

    /// <summary>
    ///     Vote skip settings model
    /// </summary>
    public class VoteSkipSettings
    {
        /// <summary>
        ///     Whether vote skip is enabled
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        ///     Vote skip threshold percentage (1-100)
        /// </summary>
        public int Threshold { get; set; }
    }

    /// <summary>
    ///     A song request
    /// </summary>
    public class PlayRequest
    {
        /// <summary>
        ///     The requested url
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        ///     Who requested
        /// </summary>
        public PartialUser Requester { get; set; }
    }
}
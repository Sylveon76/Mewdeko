namespace Mewdeko.Database.Models;

/// <summary>
/// Represents a saved music playlist in the database.
/// </summary>
public class MusicPlaylist : DbEntity
{

    /// <summary>
    /// The name of the playlist.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// The ID of the guild this playlist belongs to.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// The ID of the user who created this playlist.
    /// </summary>
    public ulong AuthorId { get; set; }

    /// <summary>
    /// The tracks in this playlist.
    /// </summary>
    public List<MusicPlaylistTrack> Tracks { get; set; } = new();
}

/// <summary>
/// Represents a track within a saved playlist.
/// </summary>
public class MusicPlaylistTrack
{
    /// <summary>
    /// The unique identifier for the playlist track.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The playlist this track belongs to.
    /// </summary>
    public int PlaylistId { get; set; }

    /// <summary>
    /// Navigation property for the playlist.
    /// </summary>
    public MusicPlaylist Playlist { get; set; }

    /// <summary>
    /// The title of the track.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// The URI of the track.
    /// </summary>
    public string Uri { get; set; }

    /// <summary>
    /// The duration of the track.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// The position of this track in the playlist.
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// When the track was added to the playlist.
    /// </summary>
    public DateTime DateAdded { get; set; }
}
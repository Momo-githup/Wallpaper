using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace WallpaperStudio.App.Models;

[Table("community_wallpapers")]
public sealed class CommunityWallpaperRecord : BaseModel
{
    [PrimaryKey("id", false)]
    public long Id { get; set; }

    [Column("owner_id")]
    public string OwnerId { get; set; } = string.Empty;

    [Column("title")]
    public string Title { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

    [Column("storage_path")]
    public string StoragePath { get; set; } = string.Empty;

    [Column("thumbnail_path")]
    public string ThumbnailPath { get; set; } = string.Empty;

    [Column("width")]
    public int Width { get; set; }

    [Column("height")]
    public int Height { get; set; }

    [Column("file_size")]
    public long FileSize { get; set; }

    [Column("mime_type")]
    public string MimeType { get; set; } = string.Empty;

    [Column("status")]
    public string Status { get; set; } = "published";

    [Column("download_count", ignoreOnInsert: true)]
    public long DownloadCount { get; set; }

    [Column("created_at", ignoreOnInsert: true)]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at", ignoreOnInsert: true)]
    public DateTime UpdatedAt { get; set; }
}

[Table("profiles")]
public sealed class CommunityProfileRecord : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = string.Empty;

    [Column("display_name")]
    public string DisplayName { get; set; } = string.Empty;
}

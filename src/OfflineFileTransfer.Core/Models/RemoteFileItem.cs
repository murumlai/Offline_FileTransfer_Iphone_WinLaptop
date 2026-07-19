namespace OfflineFileTransfer.Core.Models;

/// <summary>
/// A downloadable file on the iPhone, normalized across providers.
/// </summary>
public sealed record RemoteFileItem : RemoteItem
{
    /// <summary>Lowercase extension including the leading dot, e.g. ".heic". Empty when none.</summary>
    public string Extension { get; init; } = string.Empty;

    /// <summary>Best-known broad category for filtering.</summary>
    public FileTypeCategory Category { get; init; } = FileTypeCategory.Other;

    /// <summary>Size in bytes, or null when the provider cannot report it.</summary>
    public long? SizeBytes { get; init; }

    /// <summary>Last-modified timestamp when the provider reports it.</summary>
    public DateTimeOffset? ModifiedUtc { get; init; }

    /// <summary>MIME/content type when known.</summary>
    public string? ContentType { get; init; }

    public override bool IsFolder => false;
}

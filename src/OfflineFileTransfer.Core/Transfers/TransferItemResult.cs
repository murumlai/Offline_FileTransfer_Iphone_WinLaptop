using OfflineFileTransfer.Core.Models;

namespace OfflineFileTransfer.Core.Transfers;

/// <summary>
/// Outcome of transferring a single file.
/// </summary>
public enum TransferItemStatus
{
    Copied = 0,
    Skipped = 1,
    Overwritten = 2,
    Renamed = 3,
    Failed = 4,
    Canceled = 5,
}

/// <summary>
/// Result for one file within a transfer.
/// </summary>
public sealed record TransferItemResult
{
    public required RemoteFileItem Item { get; init; }
    public required TransferItemStatus Status { get; init; }

    /// <summary>Local path written to, when the file was copied/overwritten/renamed.</summary>
    public string? LocalPath { get; init; }

    /// <summary>Bytes written for this file.</summary>
    public long BytesCopied { get; init; }

    /// <summary>Error message when <see cref="Status"/> is <see cref="TransferItemStatus.Failed"/>.</summary>
    public string? Error { get; init; }

    public bool IsSuccess =>
        Status is TransferItemStatus.Copied
            or TransferItemStatus.Overwritten
            or TransferItemStatus.Renamed
            or TransferItemStatus.Skipped;
}

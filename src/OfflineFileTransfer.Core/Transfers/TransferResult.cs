namespace OfflineFileTransfer.Core.Transfers;

/// <summary>
/// Aggregate result of a transfer request.
/// </summary>
public sealed class TransferResult
{
    public TransferResult(IReadOnlyList<TransferItemResult> items, bool wasCanceled)
    {
        Items = items ?? throw new ArgumentNullException(nameof(items));
        WasCanceled = wasCanceled;
    }

    public IReadOnlyList<TransferItemResult> Items { get; }

    public bool WasCanceled { get; }

    public int CopiedCount => Items.Count(i =>
        i.Status is TransferItemStatus.Copied
            or TransferItemStatus.Overwritten
            or TransferItemStatus.Renamed);

    public int SkippedCount => Items.Count(i => i.Status == TransferItemStatus.Skipped);

    public int FailedCount => Items.Count(i => i.Status == TransferItemStatus.Failed);

    public long TotalBytesCopied => Items.Sum(i => i.BytesCopied);

    public bool HasFailures => FailedCount > 0;
}

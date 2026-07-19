using OfflineFileTransfer.Core.Transfers;

namespace OfflineFileTransfer.Core.Providers;

/// <summary>
/// Orchestrates copying selected remote files to a local destination.
/// </summary>
public interface ITransferService
{
    /// <summary>
    /// Executes a transfer, reporting progress and honoring cancellation.
    /// Individual file failures are captured in the result rather than thrown.
    /// </summary>
    Task<TransferResult> TransferAsync(
        TransferRequest request,
        IProgress<TransferProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

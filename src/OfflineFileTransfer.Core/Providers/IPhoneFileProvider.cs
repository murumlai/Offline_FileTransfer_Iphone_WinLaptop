using OfflineFileTransfer.Core.Models;

namespace OfflineFileTransfer.Core.Providers;

/// <summary>
/// Abstraction over a single iPhone storage surface reachable over USB.
/// Implementations must isolate their own failures so one unavailable provider
/// does not break the others.
/// </summary>
public interface IPhoneFileProvider
{
    /// <summary>Which storage surface this provider exposes.</summary>
    FileSourceKind SourceKind { get; }

    /// <summary>Short, user-facing display name (e.g. "Camera Roll").</summary>
    string DisplayName { get; }

    /// <summary>
    /// Checks whether this provider can currently serve requests for the given device.
    /// Should not throw; return an unavailable result with a reason instead.
    /// </summary>
    Task<ProviderAvailability> CheckAvailabilityAsync(
        DeviceInfo device,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Enumerates the immediate children of a folder. Pass an empty/null path for the source root.
    /// Yields incrementally so large libraries do not block the UI.
    /// </summary>
    IAsyncEnumerable<RemoteItem> EnumerateAsync(
        string? folderPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a readable stream for the given file. Caller disposes the stream.
    /// </summary>
    Task<Stream> OpenReadAsync(
        RemoteFileItem file,
        CancellationToken cancellationToken = default);
}

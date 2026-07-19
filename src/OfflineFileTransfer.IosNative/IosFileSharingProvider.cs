using OfflineFileTransfer.Core.Models;
using OfflineFileTransfer.Core.Providers;

namespace OfflineFileTransfer.IosNative;

/// <summary>
/// Optional provider for iOS app document containers and AFC media folders via a
/// libimobiledevice-compatible native bridge. The native bridge is not bundled yet, so this
/// provider reports itself unavailable and never breaks the camera-roll path.
/// Enable by wiring a real bridge and flipping <see cref="BridgeAvailable"/>.
/// </summary>
public sealed class IosFileSharingProvider : IPhoneFileProvider
{
    /// <summary>
    /// True only when a working native bridge is present. Kept false until the bridge ships.
    /// </summary>
    public static bool BridgeAvailable => false;

    public FileSourceKind SourceKind => FileSourceKind.AppFileSharing;

    public string DisplayName => "App File Sharing";

    public Task<ProviderAvailability> CheckAvailabilityAsync(
        DeviceInfo device, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ProviderAvailability.Unavailable(
            "App File Sharing needs the optional native iOS bridge, which is not enabled in this build."));
    }

    public async IAsyncEnumerable<RemoteItem> EnumerateAsync(
        string? folderPath,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        yield break;
    }

    public Task<Stream> OpenReadAsync(RemoteFileItem file, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("The native iOS bridge is not enabled in this build.");
    }
}

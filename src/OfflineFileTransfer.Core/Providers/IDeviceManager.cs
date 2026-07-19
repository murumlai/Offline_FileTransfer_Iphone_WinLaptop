using OfflineFileTransfer.Core.Models;

namespace OfflineFileTransfer.Core.Providers;

/// <summary>
/// Discovers connected iPhones over USB.
/// </summary>
public interface IDeviceManager
{
    /// <summary>
    /// Returns the currently connected devices. Should not throw; return an empty list on failure.
    /// </summary>
    Task<IReadOnlyList<DeviceInfo>> GetConnectedDevicesAsync(
        CancellationToken cancellationToken = default);
}

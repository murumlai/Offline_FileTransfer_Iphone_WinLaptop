using System.Runtime.Versioning;
using OfflineFileTransfer.Core.Models;
using OfflineFileTransfer.Core.Providers;
using OfflineFileTransfer.WindowsDevices.Shell;

namespace OfflineFileTransfer.WindowsDevices;

/// <summary>
/// Discovers Apple iPhones exposed as portable devices in the Windows "This PC" shell namespace.
/// Detection is best-effort and never throws; failures produce an empty list.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WpdDeviceManager : IDeviceManager, IDisposable
{
    private static readonly string[] AppleNameHints =
    {
        "iPhone", "Apple iPhone", "Apple Mobile Device",
    };

    private readonly StaTaskRunner _runner = new();

    public Task<IReadOnlyList<DeviceInfo>> GetConnectedDevicesAsync(
        CancellationToken cancellationToken = default)
    {
        return _runner.RunAsync<IReadOnlyList<DeviceInfo>>(() =>
        {
            var devices = new List<DeviceInfo>();
            dynamic? shell = null;
            dynamic? thisPc = null;
            try
            {
                shell = ShellCom.CreateShell();
                thisPc = ShellCom.GetThisPc(shell);
                var appleSupport = AppleSupportDetector.IsInstalled();

                dynamic items = thisPc.Items();
                int count = items.Count;
                for (var i = 0; i < count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    dynamic entry = items.Item(i);
                    string name = entry.Name;

                    if (LooksLikeIPhone(name))
                    {
                        var trust = DetermineTrustState(entry);
                        devices.Add(new DeviceInfo
                        {
                            Id = SafePath(entry) ?? name,
                            Name = name,
                            Model = name,
                            IsConnected = true,
                            TrustState = trust,
                            AppleSupportInstalled = appleSupport,
                        });
                    }

                    ShellCom.Release(entry);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Detection failure -> report no devices rather than surfacing an error.
            }
            finally
            {
                ShellCom.Release(thisPc);
                ShellCom.Release(shell);
            }

            return devices;
        });
    }

    private static bool LooksLikeIPhone(string name) =>
        !string.IsNullOrWhiteSpace(name) &&
        AppleNameHints.Any(h => name.Contains(h, StringComparison.OrdinalIgnoreCase));

    private static DeviceTrustState DetermineTrustState(dynamic deviceEntry)
    {
        // A trusted, unlocked iPhone exposes an enumerable storage folder ("Internal Storage")
        // that itself contains entries (DCIM, etc.). A locked or permission-denied device shows
        // the storage node but it stays EMPTY until the phone is unlocked and access is allowed.
        try
        {
            dynamic folder = deviceEntry.GetFolder;
            if (folder is null)
            {
                return DeviceTrustState.Untrusted;
            }

            dynamic children = folder.Items();
            int count = children.Count;
            if (count == 0)
            {
                ShellCom.Release(children);
                ShellCom.Release(folder);
                return DeviceTrustState.Untrusted;
            }

            // Peek one level deeper: if any storage folder has content, the phone is unlocked.
            var hasContent = false;
            for (var i = 0; i < count && !hasContent; i++)
            {
                dynamic storage = children.Item(i);
                try
                {
                    if (storage.IsFolder)
                    {
                        dynamic storageFolder = storage.GetFolder;
                        dynamic grandChildren = storageFolder.Items();
                        hasContent = grandChildren.Count > 0;
                        ShellCom.Release(grandChildren);
                        ShellCom.Release(storageFolder);
                    }
                }
                catch
                {
                    // ignore and continue probing other storage nodes
                }
                finally
                {
                    ShellCom.Release(storage);
                }
            }

            ShellCom.Release(children);
            ShellCom.Release(folder);

            return hasContent ? DeviceTrustState.Trusted : DeviceTrustState.Locked;
        }
        catch
        {
            return DeviceTrustState.Untrusted;
        }
    }

    private static string? SafePath(dynamic entry)
    {
        try
        {
            return entry.Path as string;
        }
        catch
        {
            return null;
        }
    }

    public void Dispose() => _runner.Dispose();
}

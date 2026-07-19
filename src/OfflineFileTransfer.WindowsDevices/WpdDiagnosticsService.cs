using System.Runtime.Versioning;
using OfflineFileTransfer.Core.Diagnostics;
using OfflineFileTransfer.Core.Models;
using OfflineFileTransfer.Core.Providers;

namespace OfflineFileTransfer.WindowsDevices;

/// <summary>
/// Runs the feasibility spike (Phase 2) against the current environment using the
/// Windows shell / portable-device surface. Probes are ordered from most to least likely
/// to be reachable and each check degrades gracefully.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WpdDiagnosticsService : IDiagnosticsService
{
    private readonly IDeviceManager _deviceManager;

    public WpdDiagnosticsService(IDeviceManager? deviceManager = null)
    {
        _deviceManager = deviceManager ?? new WpdDeviceManager();
    }

    public async Task<DiagnosticsReport> RunAsync(CancellationToken cancellationToken = default)
    {
        var checks = new List<DiagnosticCheck>();

        var devices = await _deviceManager.GetConnectedDevicesAsync(cancellationToken).ConfigureAwait(false);
        var device = devices.FirstOrDefault();

        checks.Add(DeviceConnectedCheck(device));
        checks.Add(TrustCheck(device));
        checks.Add(AppleSupportCheck(device));

        var cameraRoll = await CameraRollCheck(device, cancellationToken).ConfigureAwait(false);
        checks.Add(cameraRoll.check);

        checks.Add(AfcCheck());
        checks.Add(AppFileSharingCheck());
        checks.Add(DownloadsCheck(cameraRoll.hadDownloadsFolder));

        return new DiagnosticsReport(checks);
    }

    private static DiagnosticCheck DeviceConnectedCheck(DeviceInfo? device) => new()
    {
        Id = DiagnosticCheckId.DeviceConnected,
        Title = "iPhone connected over USB",
        Status = device is { IsConnected: true } ? DiagnosticStatus.Pass : DiagnosticStatus.Fail,
        Detail = device is { IsConnected: true }
            ? $"Detected: {device.Name}"
            : "No iPhone detected. Connect the phone with a USB cable.",
    };

    private static DiagnosticCheck TrustCheck(DeviceInfo? device)
    {
        var status = device?.TrustState switch
        {
            DeviceTrustState.Trusted => DiagnosticStatus.Pass,
            DeviceTrustState.Locked => DiagnosticStatus.Warning,
            DeviceTrustState.Untrusted => DiagnosticStatus.Warning,
            _ => DiagnosticStatus.Unknown,
        };

        var detail = device?.TrustState switch
        {
            DeviceTrustState.Trusted => "Phone is unlocked and trusted.",
            DeviceTrustState.Locked => "Phone appears locked. Unlock it to browse storage.",
            DeviceTrustState.Untrusted => "Tap Trust/Allow on the phone when prompted.",
            _ => "Trust state could not be determined.",
        };

        return new DiagnosticCheck
        {
            Id = DiagnosticCheckId.DeviceTrust,
            Title = "Phone unlocked and trusted",
            Status = status,
            Detail = detail,
        };
    }

    private static DiagnosticCheck AppleSupportCheck(DeviceInfo? device)
    {
        var installed = device?.AppleSupportInstalled ?? AppleSupportDetector.IsInstalled();
        return new DiagnosticCheck
        {
            Id = DiagnosticCheckId.AppleSupportInstalled,
            Title = "Apple Devices / Mobile Device support installed",
            Status = installed ? DiagnosticStatus.Pass : DiagnosticStatus.Warning,
            Detail = installed
                ? "Apple mobile device support detected."
                : "Install the Apple Devices app (or iTunes) so Windows can talk to the iPhone.",
        };
    }

    private async Task<(DiagnosticCheck check, bool hadDownloadsFolder)> CameraRollCheck(
        DeviceInfo? device, CancellationToken cancellationToken)
    {
        if (device is null || !device.IsConnected)
        {
            return (new DiagnosticCheck
            {
                Id = DiagnosticCheckId.WpdCameraRollEnumerable,
                Title = "Camera Roll (DCIM) enumerable",
                Status = DiagnosticStatus.Fail,
                Detail = "No device to enumerate.",
            }, false);
        }

        using var provider = new WpdMediaProvider(device.Name);
        var availability = await provider.CheckAvailabilityAsync(device, cancellationToken).ConfigureAwait(false);
        if (!availability.IsAvailable)
        {
            return (new DiagnosticCheck
            {
                Id = DiagnosticCheckId.WpdCameraRollEnumerable,
                Title = "Camera Roll (DCIM) enumerable",
                Status = DiagnosticStatus.Fail,
                Detail = availability.Reason,
            }, false);
        }

        var count = 0;
        var sawDownloads = false;
        try
        {
            await foreach (var item in provider.EnumerateAsync(null, cancellationToken).ConfigureAwait(false))
            {
                count++;
                if (item.IsFolder && item.Name.Contains("Download", StringComparison.OrdinalIgnoreCase))
                {
                    sawDownloads = true;
                }

                if (count >= 50)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return (new DiagnosticCheck
            {
                Id = DiagnosticCheckId.WpdCameraRollEnumerable,
                Title = "Camera Roll (DCIM) enumerable",
                Status = DiagnosticStatus.Fail,
                Detail = ex.Message,
            }, false);
        }

        return (new DiagnosticCheck
        {
            Id = DiagnosticCheckId.WpdCameraRollEnumerable,
            Title = "Camera Roll (DCIM) enumerable",
            Status = count > 0 ? DiagnosticStatus.Pass : DiagnosticStatus.Warning,
            Detail = count > 0
                ? $"Enumerated {count}+ top-level storage entries."
                : "Storage node is present but empty.",
        }, sawDownloads);
    }

    private static DiagnosticCheck AfcCheck() => new()
    {
        Id = DiagnosticCheckId.AfcMediaDomainAccessible,
        Title = "AFC media-domain access",
        Status = DiagnosticStatus.Unknown,
        Detail = "AFC access requires the native iOS bridge, which is an optional provider not yet enabled.",
    };

    private static DiagnosticCheck AppFileSharingCheck() => new()
    {
        Id = DiagnosticCheckId.AppFileSharingEnumerable,
        Title = "App File Sharing containers enumerable",
        Status = DiagnosticStatus.Unknown,
        Detail = "App document containers require the native iOS bridge (HouseArrest), not yet enabled.",
    };

    private static DiagnosticCheck DownloadsCheck(bool sawDownloadsFolder) => new()
    {
        Id = DiagnosticCheckId.DownloadsVisible,
        Title = "Files app Downloads folder visible",
        Status = sawDownloadsFolder ? DiagnosticStatus.Pass : DiagnosticStatus.Fail,
        Detail = sawDownloadsFolder
            ? "A Downloads folder was visible via the portable-device surface."
            : "Downloads is not reachable over USB without an iOS app. Treated as unsupported.",
    };
}

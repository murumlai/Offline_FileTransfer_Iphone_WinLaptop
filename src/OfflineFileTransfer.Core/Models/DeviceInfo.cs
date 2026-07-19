namespace OfflineFileTransfer.Core.Models;

/// <summary>
/// Describes a connected iPhone and its readiness for browsing/transfer.
/// </summary>
public sealed record DeviceInfo
{
    /// <summary>Stable identifier for the device (WPD device id or UDID when known).</summary>
    public required string Id { get; init; }

    /// <summary>Human-friendly name, e.g. the phone's display name.</summary>
    public required string Name { get; init; }

    /// <summary>Marketing/model description when available, e.g. "iPhone 16".</summary>
    public string? Model { get; init; }

    /// <summary>True when the device is currently connected over USB.</summary>
    public bool IsConnected { get; init; }

    /// <summary>Lock/trust state as observed from Windows.</summary>
    public DeviceTrustState TrustState { get; init; } = DeviceTrustState.Unknown;

    /// <summary>True when Apple Devices / Apple Mobile Device support is detected on Windows.</summary>
    public bool AppleSupportInstalled { get; init; }

    /// <summary>True once the device is ready to browse (connected, unlocked, trusted).</summary>
    public bool IsReady => IsConnected && TrustState == DeviceTrustState.Trusted;
}

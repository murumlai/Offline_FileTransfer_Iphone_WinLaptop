namespace OfflineFileTransfer.Core.Diagnostics;

/// <summary>
/// Result of a single feasibility check.
/// </summary>
public enum DiagnosticStatus
{
    /// <summary>The check has not been run.</summary>
    NotRun = 0,

    /// <summary>The capability is present/reachable.</summary>
    Pass = 1,

    /// <summary>The capability is reachable but with caveats.</summary>
    Warning = 2,

    /// <summary>The capability is not reachable.</summary>
    Fail = 3,

    /// <summary>The capability could not be determined.</summary>
    Unknown = 4,
}

/// <summary>
/// Identifies a specific feasibility probe.
/// </summary>
public enum DiagnosticCheckId
{
    DeviceConnected = 0,
    DeviceTrust = 1,
    AppleSupportInstalled = 2,
    WpdCameraRollEnumerable = 3,
    AfcMediaDomainAccessible = 4,
    AppFileSharingEnumerable = 5,
    DownloadsVisible = 6,
}

/// <summary>
/// A single diagnostic result with a human-readable detail.
/// </summary>
public sealed record DiagnosticCheck
{
    public required DiagnosticCheckId Id { get; init; }
    public required string Title { get; init; }
    public required DiagnosticStatus Status { get; init; }
    public string? Detail { get; init; }
}

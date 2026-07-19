namespace OfflineFileTransfer.Core.Diagnostics;

/// <summary>
/// Aggregated feasibility report produced by the diagnostics spike.
/// </summary>
public sealed class DiagnosticsReport
{
    public DiagnosticsReport(IReadOnlyList<DiagnosticCheck> checks)
    {
        Checks = checks ?? throw new ArgumentNullException(nameof(checks));
        GeneratedUtc = DateTimeOffset.UtcNow;
    }

    public IReadOnlyList<DiagnosticCheck> Checks { get; }

    public DateTimeOffset GeneratedUtc { get; }

    /// <summary>
    /// Returns the status for a specific check, or <see cref="DiagnosticStatus.NotRun"/> when absent.
    /// </summary>
    public DiagnosticStatus StatusOf(DiagnosticCheckId id) =>
        Checks.FirstOrDefault(c => c.Id == id)?.Status ?? DiagnosticStatus.NotRun;

    /// <summary>True when the Downloads folder was proven visible/readable.</summary>
    public bool IsDownloadsSupported =>
        StatusOf(DiagnosticCheckId.DownloadsVisible) == DiagnosticStatus.Pass;

    /// <summary>True when camera-roll media enumeration succeeded (the MVP baseline).</summary>
    public bool IsCameraRollSupported =>
        StatusOf(DiagnosticCheckId.WpdCameraRollEnumerable) == DiagnosticStatus.Pass;

    /// <summary>True when app file-sharing containers are enumerable.</summary>
    public bool IsAppFileSharingSupported =>
        StatusOf(DiagnosticCheckId.AppFileSharingEnumerable) == DiagnosticStatus.Pass;
}

/// <summary>
/// Runs the feasibility spike against the current environment.
/// </summary>
public interface IDiagnosticsService
{
    Task<DiagnosticsReport> RunAsync(CancellationToken cancellationToken = default);
}

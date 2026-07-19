namespace OfflineFileTransfer.Core.Providers;

/// <summary>
/// Whether a provider is usable right now, and why not when it is not.
/// </summary>
public sealed record ProviderAvailability
{
    public required bool IsAvailable { get; init; }

    /// <summary>User-facing reason when unavailable (e.g. "Phone is locked").</summary>
    public string? Reason { get; init; }

    public static ProviderAvailability Available() => new() { IsAvailable = true };

    public static ProviderAvailability Unavailable(string reason) =>
        new() { IsAvailable = false, Reason = reason };
}

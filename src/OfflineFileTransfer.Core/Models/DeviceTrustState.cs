namespace OfflineFileTransfer.Core.Models;

/// <summary>
/// Trust/lock state of a connected iPhone as seen from Windows.
/// </summary>
public enum DeviceTrustState
{
    Unknown = 0,

    /// <summary>Device is connected but locked; storage is not enumerable.</summary>
    Locked = 1,

    /// <summary>Device is unlocked but the Trust/Allow prompt has not been accepted.</summary>
    Untrusted = 2,

    /// <summary>Device is unlocked and trusted; storage may be enumerable.</summary>
    Trusted = 3,
}

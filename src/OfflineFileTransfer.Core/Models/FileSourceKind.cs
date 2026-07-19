namespace OfflineFileTransfer.Core.Models;

/// <summary>
/// Identifies which iPhone storage surface a file or folder originates from.
/// </summary>
public enum FileSourceKind
{
    Unknown = 0,

    /// <summary>Camera-roll photos and videos exposed over WPD/PTP.</summary>
    CameraRoll = 1,

    /// <summary>App document containers exposed through iOS File Sharing / HouseArrest.</summary>
    AppFileSharing = 2,

    /// <summary>The Files app Downloads folder, only when proven reachable over USB.</summary>
    Downloads = 3,
}

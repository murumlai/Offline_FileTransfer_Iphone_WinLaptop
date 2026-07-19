using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace OfflineFileTransfer.WindowsDevices.Shell;

/// <summary>
/// Thin late-bound wrapper over the Windows "Shell.Application" COM automation object.
/// Uses <c>dynamic</c> so no interop assembly or extra NuGet package is required.
/// All members must be called on the STA thread owned by <see cref="StaTaskRunner"/>.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class ShellCom
{
    // Shell special folder constant for "This PC" / My Computer.
    private const int SsfDrives = 0x11;

    public static dynamic CreateShell()
    {
        var type = Type.GetTypeFromProgID("Shell.Application")
            ?? throw new PlatformNotSupportedException("Shell.Application COM object is unavailable.");
        return Activator.CreateInstance(type)
            ?? throw new InvalidOperationException("Failed to create Shell.Application.");
    }

    public static dynamic GetThisPc(dynamic shell) => shell.NameSpace(SsfDrives);

    /// <summary>
    /// Releases a COM object obtained via automation, ignoring errors.
    /// </summary>
    public static void Release(object? comObject)
    {
        if (comObject is not null && Marshal.IsComObject(comObject))
        {
            try
            {
                Marshal.ReleaseComObject(comObject);
            }
            catch
            {
                // best effort
            }
        }
    }
}

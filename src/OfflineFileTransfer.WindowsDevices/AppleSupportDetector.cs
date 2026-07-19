using System.Runtime.Versioning;
using Microsoft.Win32;

namespace OfflineFileTransfer.WindowsDevices;

/// <summary>
/// Detects whether Apple Devices / Apple Mobile Device support is installed on Windows,
/// which is required for the iPhone to expose storage over USB.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class AppleSupportDetector
{
    public static bool IsInstalled()
    {
        try
        {
            // Apple Mobile Device Support / Apple Devices register a service and install directory.
            if (ServiceExists("Apple Mobile Device Service") ||
                ServiceExists("AppleMobileDeviceService"))
            {
                return true;
            }

            using var key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Apple Inc.\Apple Mobile Device Support");
            return key is not null;
        }
        catch
        {
            return false;
        }
    }

    private static bool ServiceExists(string serviceName)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                $@"SYSTEM\CurrentControlSet\Services\{serviceName}");
            return key is not null;
        }
        catch
        {
            return false;
        }
    }
}

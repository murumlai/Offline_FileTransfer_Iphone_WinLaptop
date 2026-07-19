using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.Networking.Connectivity;
using Windows.Networking.NetworkOperators;

namespace OfflineFileTransfer.App.Services;

/// <summary>
/// Wraps the WinRT <see cref="NetworkOperatorTetheringManager"/> to read, configure,
/// start, and stop the Windows Mobile Hotspot.
/// </summary>
public sealed class MobileHotspotService
{
    /// <summary>Returns the current hotspot SSID and passphrase, or null if unavailable.</summary>
    public HotspotConfiguration? GetConfiguration()
    {
        var manager = CreateManager();
        if (manager is null) return null;
        var config = manager.GetCurrentAccessPointConfiguration();
        return new HotspotConfiguration(config.Ssid, config.Passphrase);
    }

    /// <summary>
    /// Applies a new SSID and passphrase. Returns an error message on failure, or null on success.
    /// </summary>
    public async Task<string?> ConfigureAsync(string ssid, string passphrase)
    {
        var manager = CreateManager();
        if (manager is null)
            return "No network adapter available to configure the hotspot.";
        try
        {
            var config = new NetworkOperatorTetheringAccessPointConfiguration
            {
                Ssid = ssid,
                Passphrase = passphrase
            };
            await manager.ConfigureAccessPointAsync(config).AsTask().ConfigureAwait(false);
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    /// <summary>Starts the hotspot. Returns an error message on failure, or null on success.</summary>
    public async Task<string?> StartAsync()
    {
        var manager = CreateManager();
        if (manager is null) return "No network adapter available.";
        if (manager.TetheringOperationalState == TetheringOperationalState.On)
            return null;
        try
        {
            var result = await manager.StartTetheringAsync().AsTask().ConfigureAwait(false);
            return result.Status == TetheringOperationStatus.Success
                ? null
                : result.AdditionalErrorMessage;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    /// <summary>Stops the hotspot. Returns an error message on failure, or null on success.</summary>
    public async Task<string?> StopAsync()
    {
        var manager = CreateManager();
        if (manager is null) return null;
        if (manager.TetheringOperationalState != TetheringOperationalState.On)
            return null;
        try
        {
            var result = await manager.StopTetheringAsync().AsTask().ConfigureAwait(false);
            return result.Status == TetheringOperationStatus.Success
                ? null
                : result.AdditionalErrorMessage;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    /// <summary>Returns the current tethering state, or null if a manager could not be created.</summary>
    public TetheringOperationalState? GetState()
    {
        try { return CreateManager()?.TetheringOperationalState; }
        catch { return null; }
    }

    private static NetworkOperatorTetheringManager? CreateManager()
    {
        try
        {
            // Prefer the active internet profile; fall back to any available adapter profile.
            var profile = NetworkInformation.GetInternetConnectionProfile()
                ?? NetworkInformation.GetConnectionProfiles()
                    .FirstOrDefault(p => p.NetworkAdapter is not null);
            if (profile is null) return null;
            return NetworkOperatorTetheringManager.CreateFromConnectionProfile(profile);
        }
        catch
        {
            return null;
        }
    }
}

public sealed record HotspotConfiguration(string Ssid, string Passphrase);

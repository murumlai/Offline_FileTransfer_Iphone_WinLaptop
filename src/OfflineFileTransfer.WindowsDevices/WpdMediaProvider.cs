using System.Runtime.Versioning;
using OfflineFileTransfer.Core.Files;
using OfflineFileTransfer.Core.Models;
using OfflineFileTransfer.Core.Providers;
using OfflineFileTransfer.WindowsDevices.Shell;

namespace OfflineFileTransfer.WindowsDevices;

/// <summary>
/// Enumerates and downloads camera-roll media from an iPhone exposed as a portable device
/// in the Windows shell namespace. Reads are performed by copying the shell item into a
/// temporary folder (portable-device automation does not expose direct streams) and then
/// streaming the local copy.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WpdMediaProvider : IPhoneFileProvider, IDisposable
{
    private const int CopyTimeoutMs = 120_000;
    private const int CopyPollMs = 100;

    private readonly StaTaskRunner _runner = new();
    private readonly string _deviceName;

    public WpdMediaProvider(string deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            throw new ArgumentException("Device name is required.", nameof(deviceName));
        }

        _deviceName = deviceName;
    }

    public FileSourceKind SourceKind => FileSourceKind.CameraRoll;

    public string DisplayName => "Camera Roll";

    public Task<ProviderAvailability> CheckAvailabilityAsync(
        DeviceInfo device, CancellationToken cancellationToken = default)
    {
        return _runner.RunAsync(() =>
        {
            dynamic? shell = null;
            dynamic? storageRoot = null;
            try
            {
                shell = ShellCom.CreateShell();
                storageRoot = GetStorageRootFolder(shell);
                if (storageRoot is null)
                {
                    return ProviderAvailability.Unavailable(
                        "iPhone not found in This PC. Connect via USB, unlock, and tap Trust.");
                }

                dynamic items = storageRoot.Items();
                int count = items.Count;
                ShellCom.Release(items);

                return count > 0
                    ? ProviderAvailability.Available()
                    : ProviderAvailability.Unavailable(
                        "iPhone is connected but its storage is empty/locked. Unlock the phone, keep it unlocked, tap Allow/Trust to grant access to photos, then press Refresh.");
            }
            catch (Exception ex)
            {
                return ProviderAvailability.Unavailable($"Camera Roll unavailable: {ex.Message}");
            }
            finally
            {
                ShellCom.Release(storageRoot);
                ShellCom.Release(shell);
            }
        });
    }

    public async IAsyncEnumerable<RemoteItem> EnumerateAsync(
        string? folderPath,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var normalized = PathUtilities.NormalizeRemotePath(folderPath);
        var batch = await _runner.RunAsync(() => EnumerateFolder(normalized, cancellationToken))
            .ConfigureAwait(false);

        foreach (var item in batch)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return item;
        }
    }

    private List<RemoteItem> EnumerateFolder(string normalizedPath, CancellationToken cancellationToken)
    {
        var results = new List<RemoteItem>();
        dynamic? shell = null;
        dynamic? folder = null;
        try
        {
            shell = ShellCom.CreateShell();
            folder = ResolveFolderByPath(shell, normalizedPath);
            if (folder is null)
            {
                return results;
            }

            dynamic items = folder.Items();
            int count = items.Count;
            for (var i = 0; i < count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                dynamic entry = items.Item(i);
                try
                {
                    string name = entry.Name;
                    var childPath = PathUtilities.CombineRemote(normalizedPath, name);
                    bool isFolder = entry.IsFolder;

                    if (isFolder)
                    {
                        results.Add(new RemoteFolderItem
                        {
                            Id = childPath,
                            Name = name,
                            RemotePath = childPath,
                            Source = SourceKind,
                        });
                    }
                    else
                    {
                        results.Add(new RemoteFileItem
                        {
                            Id = childPath,
                            Name = name,
                            RemotePath = childPath,
                            Source = SourceKind,
                            Extension = FileTypeClassifier.GetExtension(name),
                            Category = FileTypeClassifier.Classify(name),
                            SizeBytes = ReadSize(folder, entry),
                            ModifiedUtc = ReadModified(entry),
                        });
                    }
                }
                finally
                {
                    ShellCom.Release(entry);
                }
            }

            ShellCom.Release(items);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Return whatever was gathered; provider failures must stay isolated.
        }
        finally
        {
            ShellCom.Release(folder);
            ShellCom.Release(shell);
        }

        return results;
    }

    public Task<Stream> OpenReadAsync(RemoteFileItem file, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);

        return _runner.RunAsync(() =>
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "OfflineFileTransfer", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            dynamic? shell = null;
            dynamic? parentFolder = null;
            dynamic? destFolder = null;
            try
            {
                shell = ShellCom.CreateShell();
                var parentPath = ParentOf(file.RemotePath);
                parentFolder = ResolveFolderByPath(shell, parentPath)
                    ?? throw new FileNotFoundException($"Source folder not found: {parentPath}");

                dynamic sourceItem = FindChild(parentFolder, file.Name)
                    ?? throw new FileNotFoundException($"File not found on device: {file.Name}");

                destFolder = shell.NameSpace(tempDir);
                // 4 = no progress UI, 16 = yes-to-all. CopyHere is asynchronous.
                destFolder.CopyHere(sourceItem, 4 | 16);
                ShellCom.Release(sourceItem);

                var localPath = Path.Combine(tempDir, file.Name);
                WaitForCopy(localPath, cancellationToken);

                return (Stream)new TempFileStream(localPath, tempDir);
            }
            catch
            {
                TryDeleteDir(tempDir);
                throw;
            }
            finally
            {
                ShellCom.Release(destFolder);
                ShellCom.Release(parentFolder);
                ShellCom.Release(shell);
            }
        });
    }

    private static void WaitForCopy(string localPath, CancellationToken cancellationToken)
    {
        var deadline = Environment.TickCount64 + CopyTimeoutMs;
        long lastSize = -1;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (File.Exists(localPath))
            {
                var size = new FileInfo(localPath).Length;
                // Consider the copy complete once the size stops growing and the file is unlocked.
                if (size == lastSize && size >= 0 && IsFileReady(localPath))
                {
                    return;
                }

                lastSize = size;
            }

            if (Environment.TickCount64 > deadline)
            {
                throw new TimeoutException("Timed out copying file from device.");
            }

            Thread.Sleep(CopyPollMs);
        }
    }

    private static bool IsFileReady(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private dynamic? ResolveDeviceFolder(dynamic shell)
    {
        dynamic? thisPc = null;
        try
        {
            thisPc = ShellCom.GetThisPc(shell);
            dynamic items = thisPc.Items();
            int count = items.Count;
            for (var i = 0; i < count; i++)
            {
                dynamic entry = items.Item(i);
                string name = entry.Name;
                if (string.Equals(name, _deviceName, StringComparison.OrdinalIgnoreCase) ||
                    name.Contains(_deviceName, StringComparison.OrdinalIgnoreCase))
                {
                    dynamic folder = entry.GetFolder;
                    ShellCom.Release(entry);
                    ShellCom.Release(items);
                    return folder;
                }

                ShellCom.Release(entry);
            }

            ShellCom.Release(items);
            return null;
        }
        finally
        {
            ShellCom.Release(thisPc);
        }
    }

    private dynamic? ResolveFolderByPath(dynamic shell, string normalizedPath)
    {
        dynamic? current = GetStorageRootFolder(shell);
        if (current is null || string.IsNullOrEmpty(normalizedPath))
        {
            return current;
        }

        foreach (var segment in normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            dynamic? child = FindChild(current, segment);
            ShellCom.Release(current);
            if (child is null)
            {
                return null;
            }

            dynamic childFolder = child.GetFolder;
            ShellCom.Release(child);
            current = childFolder;
        }

        return current;
    }

    /// <summary>
    /// Returns the folder to browse from. iPhones expose a single storage node
    /// ("Internal Storage") under the device; descend into it so the user sees the
    /// media folders directly instead of an extra wrapper level.
    /// </summary>
    private dynamic? GetStorageRootFolder(dynamic shell)
    {
        dynamic? device = ResolveDeviceFolder(shell);
        if (device is null)
        {
            return null;
        }

        try
        {
            dynamic items = device.Items();
            int count = items.Count;
            if (count == 1)
            {
                dynamic only = items.Item(0);
                if (only.IsFolder)
                {
                    dynamic storage = only.GetFolder;
                    ShellCom.Release(only);
                    ShellCom.Release(items);
                    ShellCom.Release(device);
                    return storage;
                }

                ShellCom.Release(only);
            }

            ShellCom.Release(items);
            return device;
        }
        catch
        {
            return device;
        }
    }

    private static dynamic? FindChild(dynamic folder, string name)
    {
        dynamic items = folder.Items();
        int count = items.Count;
        for (var i = 0; i < count; i++)
        {
            dynamic entry = items.Item(i);
            string entryName = entry.Name;
            if (string.Equals(entryName, name, StringComparison.OrdinalIgnoreCase))
            {
                ShellCom.Release(items);
                return entry;
            }

            ShellCom.Release(entry);
        }

        ShellCom.Release(items);
        return null;
    }

    private static long? ReadSize(dynamic folder, dynamic entry)
    {
        try
        {
            // Column 1 is Size in the shell details; portable devices may return a formatted string.
            string detail = folder.GetDetailsOf(entry, 1);
            return ShellSize.Parse(detail);
        }
        catch
        {
            return null;
        }
    }

    private static DateTimeOffset? ReadModified(dynamic entry)
    {
        try
        {
            object value = entry.ModifyDate;
            if (value is DateTime dt)
            {
                return new DateTimeOffset(dt);
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static string ParentOf(string remotePath)
    {
        var normalized = PathUtilities.NormalizeRemotePath(remotePath);
        var idx = normalized.LastIndexOf('/');
        return idx < 0 ? string.Empty : normalized[..idx];
    }

    private static void TryDeleteDir(string dir)
    {
        try
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
        catch
        {
            // best effort
        }
    }

    public void Dispose() => _runner.Dispose();
}

using System.Text;
using OfflineFileTransfer.Core.IO;
using OfflineFileTransfer.Core.Models;
using OfflineFileTransfer.Core.Providers;

namespace OfflineFileTransfer.Core.Tests.Fakes;

/// <summary>
/// In-memory <see cref="IFileSystem"/> for transfer tests. Case-insensitive paths.
/// </summary>
public sealed class InMemoryFileSystem : IFileSystem
{
    public Dictionary<string, byte[]> Files { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> Directories { get; } = new(StringComparer.OrdinalIgnoreCase);

    public bool FileExists(string path) => Files.ContainsKey(path);

    public bool DirectoryExists(string path) => Directories.Contains(path);

    public void CreateDirectory(string path) => Directories.Add(path);

    public Stream OpenWrite(string path) => new CapturingStream(this, path);

    public void DeleteFile(string path) => Files.Remove(path);

    private sealed class CapturingStream : MemoryStream
    {
        private readonly InMemoryFileSystem _fs;
        private readonly string _path;

        public CapturingStream(InMemoryFileSystem fs, string path)
        {
            _fs = fs;
            _path = path;
            _fs.Files[path] = Array.Empty<byte>();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _fs.Files[_path] = ToArray();
            }

            base.Dispose(disposing);
        }
    }
}

/// <summary>
/// Fake provider that serves file content from an in-memory map keyed by remote path.
/// </summary>
public sealed class FakeProvider : IPhoneFileProvider
{
    private readonly Dictionary<string, byte[]> _content;

    public FakeProvider(FileSourceKind kind, Dictionary<string, byte[]> content)
    {
        SourceKind = kind;
        _content = content;
    }

    public FileSourceKind SourceKind { get; }

    public string DisplayName => SourceKind.ToString();

    public bool ThrowOnOpen { get; set; }

    public Task<ProviderAvailability> CheckAvailabilityAsync(
        DeviceInfo device, CancellationToken cancellationToken = default) =>
        Task.FromResult(ProviderAvailability.Available());

    public async IAsyncEnumerable<RemoteItem> EnumerateAsync(
        string? folderPath,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var key in _content.Keys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new RemoteFileItem
            {
                Id = key,
                Name = Path.GetFileName(key),
                RemotePath = key,
                Source = SourceKind,
            };
        }

        await Task.CompletedTask;
    }

    public Task<Stream> OpenReadAsync(RemoteFileItem file, CancellationToken cancellationToken = default)
    {
        if (ThrowOnOpen)
        {
            throw new IOException("Simulated device read failure.");
        }

        if (!_content.TryGetValue(file.RemotePath, out var bytes))
        {
            throw new FileNotFoundException(file.RemotePath);
        }

        return Task.FromResult<Stream>(new MemoryStream(bytes, writable: false));
    }

    public static byte[] Text(string s) => Encoding.UTF8.GetBytes(s);
}

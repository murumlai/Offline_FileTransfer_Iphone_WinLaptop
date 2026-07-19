namespace OfflineFileTransfer.WindowsDevices;

/// <summary>
/// A read stream over a temporary local copy that deletes the backing file and its
/// containing temp folder when disposed.
/// </summary>
internal sealed class TempFileStream : FileStream
{
    private readonly string _tempDirectory;
    private bool _cleaned;

    public TempFileStream(string path, string tempDirectory)
        : base(path, FileMode.Open, FileAccess.Read, FileShare.Read)
    {
        _tempDirectory = tempDirectory;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Cleanup();
    }

    private void Cleanup()
    {
        if (_cleaned)
        {
            return;
        }

        _cleaned = true;
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
        catch
        {
            // best effort
        }
    }
}

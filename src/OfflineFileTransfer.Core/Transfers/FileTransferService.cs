using OfflineFileTransfer.Core.Files;
using OfflineFileTransfer.Core.IO;
using OfflineFileTransfer.Core.Models;
using OfflineFileTransfer.Core.Providers;

namespace OfflineFileTransfer.Core.Transfers;

/// <summary>
/// Copies remote files to a local destination, applying duplicate policy, preserving structure
/// when requested, reporting progress, and isolating per-file failures.
/// </summary>
public sealed class FileTransferService : ITransferService
{
    private const int BufferSize = 81920;

    private readonly Func<FileSourceKind, IPhoneFileProvider?> _providerResolver;
    private readonly IFileSystem _fileSystem;

    public FileTransferService(
        Func<FileSourceKind, IPhoneFileProvider?> providerResolver,
        IFileSystem? fileSystem = null)
    {
        _providerResolver = providerResolver ?? throw new ArgumentNullException(nameof(providerResolver));
        _fileSystem = fileSystem ?? new LocalFileSystem();
    }

    public async Task<TransferResult> TransferAsync(
        TransferRequest request,
        IProgress<TransferProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var results = new List<TransferItemResult>(request.Items.Count);
        long totalBytes = 0;
        var canceled = false;

        for (var index = 0; index < request.Items.Count; index++)
        {
            var item = request.Items[index];

            if (cancellationToken.IsCancellationRequested)
            {
                canceled = true;
                results.Add(new TransferItemResult
                {
                    Item = item,
                    Status = TransferItemStatus.Canceled,
                });
                continue;
            }

            TransferItemResult itemResult;
            try
            {
                itemResult = await CopyOneAsync(request, item, index, progress, totalBytes, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                canceled = true;
                itemResult = new TransferItemResult
                {
                    Item = item,
                    Status = TransferItemStatus.Canceled,
                };
            }
            catch (Exception ex)
            {
                itemResult = new TransferItemResult
                {
                    Item = item,
                    Status = TransferItemStatus.Failed,
                    Error = ex.Message,
                };
            }

            totalBytes += itemResult.BytesCopied;
            results.Add(itemResult);

            progress?.Report(new TransferProgress
            {
                FilesCompleted = index + 1,
                FilesTotal = request.Items.Count,
                CurrentFileName = item.Name,
                TotalBytesCopied = totalBytes,
            });

            if (canceled)
            {
                break;
            }
        }

        return new TransferResult(results, canceled);
    }

    private async Task<TransferItemResult> CopyOneAsync(
        TransferRequest request,
        RemoteFileItem item,
        int index,
        IProgress<TransferProgress>? progress,
        long totalBytesSoFar,
        CancellationToken cancellationToken)
    {
        var provider = _providerResolver(item.Source)
            ?? throw new InvalidOperationException(
                $"No provider is registered for source '{item.Source}'.");

        var desiredPath = PathUtilities.BuildLocalDestinationPath(
            request.DestinationFolder, item.RemotePath, request.PreserveStructure);

        var directory = Path.GetDirectoryName(desiredPath);
        if (!string.IsNullOrEmpty(directory) && !_fileSystem.DirectoryExists(directory))
        {
            _fileSystem.CreateDirectory(directory);
        }

        var status = TransferItemStatus.Copied;
        var targetPath = desiredPath;

        if (_fileSystem.FileExists(desiredPath))
        {
            switch (request.DuplicatePolicy)
            {
                case DuplicatePolicy.Skip:
                    return new TransferItemResult
                    {
                        Item = item,
                        Status = TransferItemStatus.Skipped,
                        LocalPath = desiredPath,
                    };

                case DuplicatePolicy.Overwrite:
                    status = TransferItemStatus.Overwritten;
                    break;

                case DuplicatePolicy.AutoRename:
                    targetPath = PathUtilities.ResolveUniquePath(desiredPath, _fileSystem.FileExists);
                    status = TransferItemStatus.Renamed;
                    break;
            }
        }

        long bytesCopied = 0;
        try
        {
            await using var source = await provider.OpenReadAsync(item, cancellationToken)
                .ConfigureAwait(false);
            await using var destination = _fileSystem.OpenWrite(targetPath);

            var buffer = new byte[BufferSize];
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken)
                    .ConfigureAwait(false);
                bytesCopied += read;

                progress?.Report(new TransferProgress
                {
                    FilesCompleted = index,
                    FilesTotal = request.Items.Count,
                    CurrentFileName = item.Name,
                    CurrentFileBytesCopied = bytesCopied,
                    CurrentFileBytesTotal = item.SizeBytes,
                    TotalBytesCopied = totalBytesSoFar + bytesCopied,
                });
            }
        }
        catch
        {
            // Clean up a partial file so a failed/canceled copy does not leave corrupt output.
            TryDeletePartial(targetPath, status);
            throw;
        }

        return new TransferItemResult
        {
            Item = item,
            Status = status,
            LocalPath = targetPath,
            BytesCopied = bytesCopied,
        };
    }

    private void TryDeletePartial(string path, TransferItemStatus status)
    {
        // Never delete a pre-existing file that we intended to overwrite in-place only after success.
        if (status == TransferItemStatus.Overwritten)
        {
            return;
        }

        try
        {
            _fileSystem.DeleteFile(path);
        }
        catch
        {
            // Best effort; ignore cleanup failures.
        }
    }
}

using System.Text;
using OfflineFileTransfer.Core.Models;
using OfflineFileTransfer.Core.Providers;
using OfflineFileTransfer.Core.Tests.Fakes;
using OfflineFileTransfer.Core.Transfers;

namespace OfflineFileTransfer.Core.Tests;

public class FileTransferServiceTests
{
    private const string Dest = @"C:\dest";

    private static (FileTransferService service, InMemoryFileSystem fs, FakeProvider provider) CreateSut(
        Dictionary<string, byte[]> content)
    {
        var provider = new FakeProvider(FileSourceKind.CameraRoll, content);
        var fs = new InMemoryFileSystem();
        var service = new FileTransferService(
            kind => kind == FileSourceKind.CameraRoll ? provider : null,
            fs);
        return (service, fs, provider);
    }

    private static RemoteFileItem Item(string remotePath, long? size = null) => new()
    {
        Id = remotePath,
        Name = Path.GetFileName(remotePath),
        RemotePath = remotePath,
        Source = FileSourceKind.CameraRoll,
        SizeBytes = size,
    };

    [Fact]
    public async Task Transfer_CopiesFilesToFlatDestination()
    {
        var content = new Dictionary<string, byte[]>
        {
            ["CameraRoll/a.txt"] = FakeProvider.Text("hello"),
            ["CameraRoll/b.txt"] = FakeProvider.Text("world"),
        };
        var (service, fs, _) = CreateSut(content);

        var request = new TransferRequest(
            new[] { Item("CameraRoll/a.txt"), Item("CameraRoll/b.txt") }, Dest);

        var result = await service.TransferAsync(request);

        Assert.Equal(2, result.CopiedCount);
        Assert.False(result.HasFailures);
        Assert.Equal("hello", Encoding.UTF8.GetString(fs.Files[Path.Combine(Dest, "a.txt")]));
        Assert.Equal("world", Encoding.UTF8.GetString(fs.Files[Path.Combine(Dest, "b.txt")]));
    }

    [Fact]
    public async Task Transfer_PreserveStructure_MirrorsFolders()
    {
        var content = new Dictionary<string, byte[]>
        {
            ["CameraRoll/sub/a.txt"] = FakeProvider.Text("x"),
        };
        var (service, fs, _) = CreateSut(content);

        var request = new TransferRequest(
            new[] { Item("CameraRoll/sub/a.txt") }, Dest, preserveStructure: true);

        await service.TransferAsync(request);

        Assert.True(fs.Files.ContainsKey(Path.Combine(Dest, "CameraRoll", "sub", "a.txt")));
    }

    [Fact]
    public async Task Transfer_DuplicateSkip_LeavesExistingFile()
    {
        var content = new Dictionary<string, byte[]> { ["CameraRoll/a.txt"] = FakeProvider.Text("new") };
        var (service, fs, _) = CreateSut(content);
        fs.Files[Path.Combine(Dest, "a.txt")] = FakeProvider.Text("old");

        var request = new TransferRequest(
            new[] { Item("CameraRoll/a.txt") }, Dest, duplicatePolicy: DuplicatePolicy.Skip);

        var result = await service.TransferAsync(request);

        Assert.Equal(1, result.SkippedCount);
        Assert.Equal("old", Encoding.UTF8.GetString(fs.Files[Path.Combine(Dest, "a.txt")]));
    }

    [Fact]
    public async Task Transfer_DuplicateOverwrite_ReplacesFile()
    {
        var content = new Dictionary<string, byte[]> { ["CameraRoll/a.txt"] = FakeProvider.Text("new") };
        var (service, fs, _) = CreateSut(content);
        fs.Files[Path.Combine(Dest, "a.txt")] = FakeProvider.Text("old");

        var request = new TransferRequest(
            new[] { Item("CameraRoll/a.txt") }, Dest, duplicatePolicy: DuplicatePolicy.Overwrite);

        var result = await service.TransferAsync(request);

        Assert.Equal(1, result.CopiedCount);
        Assert.Equal("new", Encoding.UTF8.GetString(fs.Files[Path.Combine(Dest, "a.txt")]));
    }

    [Fact]
    public async Task Transfer_DuplicateAutoRename_WritesNewName()
    {
        var content = new Dictionary<string, byte[]> { ["CameraRoll/a.txt"] = FakeProvider.Text("new") };
        var (service, fs, _) = CreateSut(content);
        fs.Files[Path.Combine(Dest, "a.txt")] = FakeProvider.Text("old");

        var request = new TransferRequest(
            new[] { Item("CameraRoll/a.txt") }, Dest, duplicatePolicy: DuplicatePolicy.AutoRename);

        var result = await service.TransferAsync(request);

        Assert.Equal(1, result.CopiedCount);
        Assert.Equal("old", Encoding.UTF8.GetString(fs.Files[Path.Combine(Dest, "a.txt")]));
        Assert.Equal("new", Encoding.UTF8.GetString(fs.Files[Path.Combine(Dest, "a (1).txt")]));
    }

    [Fact]
    public async Task Transfer_ProviderFailure_IsCapturedNotThrown()
    {
        var content = new Dictionary<string, byte[]> { ["CameraRoll/a.txt"] = FakeProvider.Text("x") };
        var (service, _, provider) = CreateSut(content);
        provider.ThrowOnOpen = true;

        var request = new TransferRequest(new[] { Item("CameraRoll/a.txt") }, Dest);
        var result = await service.TransferAsync(request);

        Assert.True(result.HasFailures);
        Assert.Equal(TransferItemStatus.Failed, result.Items[0].Status);
        Assert.Contains("Simulated", result.Items[0].Error);
    }

    [Fact]
    public async Task Transfer_ReportsProgressPerFile()
    {
        var content = new Dictionary<string, byte[]>
        {
            ["CameraRoll/a.txt"] = FakeProvider.Text("aa"),
            ["CameraRoll/b.txt"] = FakeProvider.Text("bb"),
        };
        var (service, _, _) = CreateSut(content);

        var reports = new List<TransferProgress>();
        var progress = new Progress<TransferProgress>(reports.Add);

        var request = new TransferRequest(
            new[] { Item("CameraRoll/a.txt"), Item("CameraRoll/b.txt") }, Dest);

        await service.TransferAsync(request, progress);

        // Progress is delivered via the synchronization context; give it a moment to flush.
        await Task.Yield();
        Assert.NotEmpty(reports);
    }

    [Fact]
    public async Task Transfer_CanceledBeforeStart_MarksItemsCanceled()
    {
        var content = new Dictionary<string, byte[]> { ["CameraRoll/a.txt"] = FakeProvider.Text("x") };
        var (service, _, _) = CreateSut(content);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var request = new TransferRequest(new[] { Item("CameraRoll/a.txt") }, Dest);
        var result = await service.TransferAsync(request, null, cts.Token);

        Assert.True(result.WasCanceled);
        Assert.Equal(TransferItemStatus.Canceled, result.Items[0].Status);
    }

    [Fact]
    public async Task Transfer_MissingProvider_RecordsFailure()
    {
        var content = new Dictionary<string, byte[]> { ["Downloads/a.txt"] = FakeProvider.Text("x") };
        var provider = new FakeProvider(FileSourceKind.CameraRoll, content);
        var fs = new InMemoryFileSystem();
        var service = new FileTransferService(_ => null, fs);

        var item = new RemoteFileItem
        {
            Id = "Downloads/a.txt",
            Name = "a.txt",
            RemotePath = "Downloads/a.txt",
            Source = FileSourceKind.Downloads,
        };
        var request = new TransferRequest(new[] { item }, Dest);

        var result = await service.TransferAsync(request);

        Assert.True(result.HasFailures);
        Assert.Contains("provider", result.Items[0].Error, StringComparison.OrdinalIgnoreCase);
    }
}

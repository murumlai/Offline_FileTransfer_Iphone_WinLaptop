using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OfflineFileTransfer.Core.Files;

namespace OfflineFileTransfer.App.Services;

public sealed class HotspotUploadServer : IAsyncDisposable
{
    private WebApplication? _app;

    public bool IsRunning => _app is not null;

    public event EventHandler<HotspotUploadReceivedEventArgs>? FileReceived;

    public async Task<HotspotUploadSession> StartAsync(
        string destinationFolder,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(destinationFolder))
        {
            throw new ArgumentException("Destination folder is required.", nameof(destinationFolder));
        }

        await StopAsync(CancellationToken.None).ConfigureAwait(false);

        Directory.CreateDirectory(destinationFolder);
        var port = FindAvailablePort();
        var token = CreateSessionToken();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = Array.Empty<string>(),
            ContentRootPath = AppContext.BaseDirectory,
        });
        builder.Logging.ClearProviders();
        builder.Services.Configure<FormOptions>(options =>
        {
            options.MultipartBodyLengthLimit = long.MaxValue;
            options.ValueLengthLimit = int.MaxValue;
            options.MultipartHeadersLengthLimit = int.MaxValue;
        });
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Limits.MaxRequestBodySize = null;
            options.ListenAnyIP(port);
        });

        var app = builder.Build();
        app.MapGet("/", () => Results.Content(BuildMissingTokenPage(), "text/html"));
        app.MapGet($"/{token}", () => Results.Content(BuildUploadPage(token), "text/html"));
        app.MapPost($"/upload/{token}", async (HttpRequest request, CancellationToken ct) =>
        {
            if (!request.HasFormContentType)
            {
                return Results.BadRequest("Expected a multipart form upload.");
            }

            var form = await request.ReadFormAsync(ct).ConfigureAwait(false);
            if (form.Files.Count == 0)
            {
                return Results.BadRequest("Choose at least one file to upload.");
            }

            var savedFiles = new List<HotspotUploadReceivedFile>();
            foreach (var file in form.Files)
            {
                if (file.Length == 0)
                {
                    continue;
                }

                var originalName = string.IsNullOrWhiteSpace(file.FileName)
                    ? "upload"
                    : Path.GetFileName(file.FileName);
                var safeName = PathUtilities.SanitizeFileName(originalName);
                var targetPath = PathUtilities.ResolveUniquePath(
                    Path.Combine(destinationFolder, safeName),
                    File.Exists);

                try
                {
                    await using var source = file.OpenReadStream();
                    await using var destination = File.Create(targetPath);
                    await source.CopyToAsync(destination, ct).ConfigureAwait(false);
                }
                catch
                {
                    TryDeletePartial(targetPath);
                    throw;
                }

                var savedName = Path.GetFileName(targetPath);
                var receivedFile = new HotspotUploadReceivedFile(
                    savedName,
                    targetPath,
                    file.Length,
                    DateTimeOffset.Now);
                savedFiles.Add(receivedFile);
                FileReceived?.Invoke(this, new HotspotUploadReceivedEventArgs(receivedFile));
            }

            if (savedFiles.Count == 0)
            {
                return Results.BadRequest("The selected files were empty.");
            }

            return Results.Content(BuildSuccessPage(token, savedFiles), "text/html");
        });

        await app.StartAsync(cancellationToken).ConfigureAwait(false);
        _app = app;

        return new HotspotUploadSession(port, token, BuildUploadUrls(port, token));
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        var app = _app;
        if (app is null)
        {
            return;
        }

        _app = null;
        await app.StopAsync(cancellationToken).ConfigureAwait(false);
        await app.DisposeAsync().ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
    }

    private static int FindAvailablePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static string CreateSessionToken()
    {
        Span<byte> bytes = stackalloc byte[12];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static IReadOnlyList<string> BuildUploadUrls(int port, string token)
    {
        var candidates = NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up)
            .Where(n => n.NetworkInterfaceType is not NetworkInterfaceType.Loopback and not NetworkInterfaceType.Tunnel)
            .SelectMany(n => n.GetIPProperties().UnicastAddresses
                .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a.Address))
                .Select(a => new UploadUrlCandidate(
                    $"http://{a.Address}:{port}/{token}",
                    ScoreUploadAddress(n, a))))
            .GroupBy(candidate => candidate.Url, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(candidate => candidate.Score).First())
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Url, StringComparer.OrdinalIgnoreCase)
            .Select(candidate => candidate.Url)
            .ToList();

        if (candidates.Count == 0)
        {
            candidates.Add($"http://localhost:{port}/{token}");
        }

        return candidates;
    }

    private static int ScoreUploadAddress(NetworkInterface networkInterface, UnicastIPAddressInformation address)
    {
        var score = 0;

        if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
        {
            score += 100;
        }

        if (!IsIPv4LinkLocal(address.Address))
        {
            score += 40;
        }

        if (IsPrivateIPv4(address.Address))
        {
            score += 20;
        }

        if (address.PrefixLength is > 0 and < 32)
        {
            score += 10;
        }

        return score;
    }

    private static bool IsIPv4LinkLocal(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes[0] == 169 && bytes[1] == 254;
    }

    private static bool IsPrivateIPv4(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes[0] == 10
            || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
            || (bytes[0] == 192 && bytes[1] == 168);
    }

    private sealed record UploadUrlCandidate(string Url, int Score);

    private static void TryDeletePartial(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            // Best effort cleanup only.
        }
    }

    private static string BuildMissingTokenPage() =>
        """
        <!doctype html>
        <html lang="en">
        <head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Offline File Transfer</title></head>
        <body style="font-family:-apple-system,BlinkMacSystemFont,Segoe UI,sans-serif;margin:2rem;line-height:1.4">
        <h1>Offline File Transfer</h1>
        <p>Open the full upload URL shown in the Windows app.</p>
        </body>
        </html>
        """;

    private static string BuildUploadPage(string token) =>
        $$"""
        <!doctype html>
        <html lang="en">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width,initial-scale=1">
        <title>Upload to Windows</title>
        <style>
        body{font-family:-apple-system,BlinkMacSystemFont,Segoe UI,sans-serif;margin:0;background:#f7f5ef;color:#1f2933}
        main{max-width:42rem;margin:0 auto;padding:2rem 1.25rem}
        .panel{background:#fff;border:1px solid #ddd7c8;border-radius:8px;padding:1.25rem;box-shadow:0 10px 28px rgba(31,41,51,.08)}
        h1{font-size:1.6rem;margin:.2rem 0 1rem}
        input,button{font:inherit}
        input[type=file]{display:block;width:100%;margin:1rem 0;padding:.8rem;border:1px dashed #9aa5b1;border-radius:6px;background:#fbfaf7}
        button{width:100%;border:0;border-radius:6px;background:#2563eb;color:#fff;padding:.9rem 1rem;font-weight:700}
        p{color:#52606d}
        </style>
        </head>
        <body>
        <main>
        <div class="panel">
        <h1>Upload to Windows</h1>
        <p>Choose one or more files from this iPhone. Keep this page open until the upload completes.</p>
        <form action="/upload/{{token}}" method="post" enctype="multipart/form-data">
        <input type="file" name="files" multiple>
        <button type="submit">Upload files</button>
        </form>
        </div>
        </main>
        </body>
        </html>
        """;

    private static string BuildSuccessPage(string token, IReadOnlyList<HotspotUploadReceivedFile> files)
    {
        var items = string.Join(Environment.NewLine, files.Select(file =>
            $"<li>{WebUtility.HtmlEncode(file.FileName)} ({file.SizeBytes:N0} bytes)</li>"));

        return $$"""
        <!doctype html>
        <html lang="en">
        <head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Upload complete</title></head>
        <body style="font-family:-apple-system,BlinkMacSystemFont,Segoe UI,sans-serif;margin:2rem;line-height:1.4;background:#f7f5ef;color:#1f2933">
        <h1>Upload complete</h1>
        <p>Saved to the Windows laptop:</p>
        <ul>{{items}}</ul>
        <p><a href="/{{token}}">Upload more files</a></p>
        </body>
        </html>
        """;
    }
}

public sealed record HotspotUploadSession(
    int Port,
    string Token,
    IReadOnlyList<string> UploadUrls)
{
    public string PrimaryUrl => UploadUrls.FirstOrDefault() ?? $"http://localhost:{Port}/{Token}";
}

public sealed record HotspotUploadReceivedFile(
    string FileName,
    string LocalPath,
    long SizeBytes,
    DateTimeOffset ReceivedAt);

public sealed class HotspotUploadReceivedEventArgs : EventArgs
{
    public HotspotUploadReceivedEventArgs(HotspotUploadReceivedFile file)
    {
        File = file;
    }

    public HotspotUploadReceivedFile File { get; }
}
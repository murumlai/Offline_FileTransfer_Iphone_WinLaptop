using System.Collections.Concurrent;
using OfflineFileTransfer.Core.Models;
using OfflineFileTransfer.Core.Providers;

namespace OfflineFileTransfer.App.Services;

/// <summary>
/// Holds the currently active providers keyed by source kind so the transfer service
/// can resolve the same live provider instances used for browsing.
/// </summary>
public sealed class ProviderRegistry
{
    private readonly ConcurrentDictionary<FileSourceKind, IPhoneFileProvider> _providers = new();

    public void Set(IEnumerable<IPhoneFileProvider> providers)
    {
        _providers.Clear();
        foreach (var provider in providers)
        {
            _providers[provider.SourceKind] = provider;
        }
    }

    public IPhoneFileProvider? Resolve(FileSourceKind kind) =>
        _providers.TryGetValue(kind, out var provider) ? provider : null;
}

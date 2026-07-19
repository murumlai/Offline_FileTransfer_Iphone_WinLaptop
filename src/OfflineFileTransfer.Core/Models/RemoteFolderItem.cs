namespace OfflineFileTransfer.Core.Models;

/// <summary>
/// A browsable folder on the iPhone, normalized across providers.
/// </summary>
public sealed record RemoteFolderItem : RemoteItem
{
    /// <summary>True when the folder is known to have no children.</summary>
    public bool IsEmpty { get; init; }

    public override bool IsFolder => true;
}

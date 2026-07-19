using OfflineFileTransfer.Core.Files;
using OfflineFileTransfer.Core.Models;

namespace OfflineFileTransfer.Core.Filtering;

/// <summary>
/// Declarative filter over <see cref="RemoteFileItem"/>. All configured conditions are ANDed together.
/// An unset condition (null/empty) does not restrict results.
/// </summary>
public sealed class FileFilter
{
    /// <summary>Restrict to these categories. Empty means all categories are allowed.</summary>
    public ISet<FileTypeCategory> Categories { get; } =
        new HashSet<FileTypeCategory>();

    /// <summary>
    /// Restrict to these extensions (with or without a leading dot, case-insensitive).
    /// Empty means all extensions are allowed.
    /// </summary>
    public ISet<string> Extensions { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Minimum size in bytes, inclusive. Null means no lower bound.</summary>
    public long? MinSizeBytes { get; set; }

    /// <summary>Maximum size in bytes, inclusive. Null means no upper bound.</summary>
    public long? MaxSizeBytes { get; set; }

    /// <summary>Case-insensitive substring the file name must contain. Null/empty means no search.</summary>
    public string? SearchText { get; set; }

    /// <summary>Restrict to these sources. Empty means all sources are allowed.</summary>
    public ISet<FileSourceKind> Sources { get; } =
        new HashSet<FileSourceKind>();

    /// <summary>True when no condition is set and the filter accepts everything.</summary>
    public bool IsEmpty =>
        Categories.Count == 0 &&
        Extensions.Count == 0 &&
        MinSizeBytes is null &&
        MaxSizeBytes is null &&
        string.IsNullOrWhiteSpace(SearchText) &&
        Sources.Count == 0;

    /// <summary>
    /// Normalizes an extension to a leading-dot, lowercase form (".jpg").
    /// </summary>
    public static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return string.Empty;
        }

        var trimmed = extension.Trim().ToLowerInvariant();
        return trimmed.StartsWith('.') ? trimmed : "." + trimmed;
    }

    /// <summary>
    /// Adds an extension to the filter, normalizing its form.
    /// </summary>
    public void AddExtension(string extension)
    {
        var normalized = NormalizeExtension(extension);
        if (normalized.Length > 0)
        {
            Extensions.Add(normalized);
        }
    }

    /// <summary>
    /// Returns true when the item satisfies every configured condition.
    /// </summary>
    public bool Matches(RemoteFileItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (Sources.Count > 0 && !Sources.Contains(item.Source))
        {
            return false;
        }

        if (Categories.Count > 0 && !Categories.Contains(item.Category))
        {
            return false;
        }

        if (Extensions.Count > 0)
        {
            var ext = string.IsNullOrEmpty(item.Extension)
                ? FileTypeClassifier.GetExtension(item.Name)
                : item.Extension;
            if (!Extensions.Contains(FileFilter.NormalizeExtension(ext)))
            {
                return false;
            }
        }

        if (MinSizeBytes is { } min)
        {
            if (item.SizeBytes is not { } size || size < min)
            {
                return false;
            }
        }

        if (MaxSizeBytes is { } max)
        {
            if (item.SizeBytes is not { } size || size > max)
            {
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            if (item.Name.IndexOf(SearchText.Trim(), StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Applies the filter to a sequence, preserving order.
    /// </summary>
    public IEnumerable<RemoteFileItem> Apply(IEnumerable<RemoteFileItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        return items.Where(Matches);
    }
}

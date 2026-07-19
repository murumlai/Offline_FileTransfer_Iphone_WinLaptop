using OfflineFileTransfer.Core.Filtering;
using OfflineFileTransfer.Core.Models;

namespace OfflineFileTransfer.Core.Tests;

public class FileFilterTests
{
    private static RemoteFileItem File(
        string name,
        FileTypeCategory category = FileTypeCategory.Other,
        long? size = null,
        FileSourceKind source = FileSourceKind.CameraRoll)
    {
        var ext = System.IO.Path.GetExtension(name);
        return new RemoteFileItem
        {
            Id = name,
            Name = name,
            RemotePath = name,
            Extension = string.IsNullOrEmpty(ext) ? string.Empty : ext.ToLowerInvariant(),
            Category = category,
            SizeBytes = size,
            Source = source,
        };
    }

    [Fact]
    public void EmptyFilter_MatchesEverything()
    {
        var filter = new FileFilter();
        Assert.True(filter.IsEmpty);
        Assert.True(filter.Matches(File("anything.bin")));
    }

    [Fact]
    public void CategoryFilter_RestrictsToSelectedCategories()
    {
        var filter = new FileFilter();
        filter.Categories.Add(FileTypeCategory.Image);

        Assert.True(filter.Matches(File("a.jpg", FileTypeCategory.Image)));
        Assert.False(filter.Matches(File("a.mp4", FileTypeCategory.Video)));
    }

    [Fact]
    public void ExtensionFilter_IsCaseInsensitiveAndDotOptional()
    {
        var filter = new FileFilter();
        filter.AddExtension("JPG");
        filter.AddExtension(".png");

        Assert.True(filter.Matches(File("a.jpg")));
        Assert.True(filter.Matches(File("b.PNG")));
        Assert.False(filter.Matches(File("c.gif")));
    }

    [Fact]
    public void SizeFilter_AppliesInclusiveBounds()
    {
        var filter = new FileFilter
        {
            MinSizeBytes = 100,
            MaxSizeBytes = 200,
        };

        Assert.True(filter.Matches(File("min.bin", size: 100)));
        Assert.True(filter.Matches(File("max.bin", size: 200)));
        Assert.False(filter.Matches(File("under.bin", size: 99)));
        Assert.False(filter.Matches(File("over.bin", size: 201)));
    }

    [Fact]
    public void SizeFilter_ExcludesItemsWithUnknownSize()
    {
        var filter = new FileFilter { MinSizeBytes = 1 };
        Assert.False(filter.Matches(File("unknown.bin", size: null)));
    }

    [Fact]
    public void SearchText_MatchesSubstringCaseInsensitive()
    {
        var filter = new FileFilter { SearchText = "vacation" };

        Assert.True(filter.Matches(File("Summer_Vacation_01.jpg")));
        Assert.False(filter.Matches(File("IMG_1234.jpg")));
    }

    [Fact]
    public void SourceFilter_RestrictsToSelectedSources()
    {
        var filter = new FileFilter();
        filter.Sources.Add(FileSourceKind.Downloads);

        Assert.True(filter.Matches(File("a.pdf", source: FileSourceKind.Downloads)));
        Assert.False(filter.Matches(File("b.jpg", source: FileSourceKind.CameraRoll)));
    }

    [Fact]
    public void Apply_PreservesOrderAndFilters()
    {
        var filter = new FileFilter();
        filter.Categories.Add(FileTypeCategory.Image);

        var items = new[]
        {
            File("1.jpg", FileTypeCategory.Image),
            File("2.mp4", FileTypeCategory.Video),
            File("3.png", FileTypeCategory.Image),
        };

        var result = filter.Apply(items).Select(i => i.Name).ToArray();
        Assert.Equal(new[] { "1.jpg", "3.png" }, result);
    }

    [Fact]
    public void MultipleConditions_AreAnded()
    {
        var filter = new FileFilter { MinSizeBytes = 50, SearchText = "img" };
        filter.Categories.Add(FileTypeCategory.Image);

        Assert.True(filter.Matches(File("IMG_1.jpg", FileTypeCategory.Image, size: 100)));
        Assert.False(filter.Matches(File("IMG_1.jpg", FileTypeCategory.Image, size: 10)));
        Assert.False(filter.Matches(File("pic.jpg", FileTypeCategory.Image, size: 100)));
    }
}

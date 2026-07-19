using OfflineFileTransfer.Core.Files;

namespace OfflineFileTransfer.Core.Tests;

public class PathUtilitiesTests
{
    [Theory]
    [InlineData("a\\b\\c", "a/b/c")]
    [InlineData("/a//b/", "a/b")]
    [InlineData("  a / b ", "a/b")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void NormalizeRemotePath_Normalizes(string? input, string expected)
    {
        Assert.Equal(expected, PathUtilities.NormalizeRemotePath(input));
    }

    [Fact]
    public void CombineRemote_JoinsAndCleans()
    {
        Assert.Equal("CameraRoll/sub/IMG.HEIC",
            PathUtilities.CombineRemote("CameraRoll/", "/sub", "IMG.HEIC"));
    }

    [Fact]
    public void SanitizeFileName_ReplacesInvalidChars()
    {
        var result = PathUtilities.SanitizeFileName("a:b*c?.txt");
        Assert.DoesNotContain(':', result);
        Assert.DoesNotContain('*', result);
        Assert.DoesNotContain('?', result);
    }

    [Fact]
    public void BuildLocalDestinationPath_FlatUsesFileNameOnly()
    {
        var path = PathUtilities.BuildLocalDestinationPath(
            @"C:\dest", "CameraRoll/sub/IMG.HEIC", preserveStructure: false);

        Assert.Equal(Path.Combine(@"C:\dest", "IMG.HEIC"), path);
    }

    [Fact]
    public void BuildLocalDestinationPath_PreserveMirrorsStructure()
    {
        var path = PathUtilities.BuildLocalDestinationPath(
            @"C:\dest", "CameraRoll/sub/IMG.HEIC", preserveStructure: true);

        Assert.Equal(Path.Combine(@"C:\dest", "CameraRoll", "sub", "IMG.HEIC"), path);
    }

    [Fact]
    public void BuildLocalDestinationPath_StripsTraversalSegments()
    {
        var path = PathUtilities.BuildLocalDestinationPath(
            @"C:\dest", "../../etc/passwd", preserveStructure: true);

        Assert.StartsWith(@"C:\dest", path);
        Assert.DoesNotContain("..", path);
    }

    [Fact]
    public void ResolveUniquePath_ReturnsOriginalWhenNoCollision()
    {
        var path = PathUtilities.ResolveUniquePath(@"C:\dest\a.jpg", _ => false);
        Assert.Equal(@"C:\dest\a.jpg", path);
    }

    [Fact]
    public void ResolveUniquePath_AppendsCounterOnCollision()
    {
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            @"C:\dest\a.jpg",
            @"C:\dest\a (1).jpg",
        };

        var path = PathUtilities.ResolveUniquePath(@"C:\dest\a.jpg", taken.Contains);
        Assert.Equal(@"C:\dest\a (2).jpg", path);
    }
}

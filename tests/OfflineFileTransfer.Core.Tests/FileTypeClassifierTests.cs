using OfflineFileTransfer.Core.Files;
using OfflineFileTransfer.Core.Models;

namespace OfflineFileTransfer.Core.Tests;

public class FileTypeClassifierTests
{
    [Theory]
    [InlineData("IMG_0001.HEIC", FileTypeCategory.Image)]
    [InlineData("photo.jpg", FileTypeCategory.Image)]
    [InlineData("clip.MOV", FileTypeCategory.Video)]
    [InlineData("movie.mp4", FileTypeCategory.Video)]
    [InlineData("song.m4a", FileTypeCategory.Audio)]
    [InlineData("report.pdf", FileTypeCategory.Document)]
    [InlineData("data.csv", FileTypeCategory.Document)]
    [InlineData("bundle.zip", FileTypeCategory.Archive)]
    [InlineData("unknown.xyz", FileTypeCategory.Other)]
    [InlineData("noextension", FileTypeCategory.Other)]
    public void Classify_ReturnsExpectedCategory(string fileName, FileTypeCategory expected)
    {
        Assert.Equal(expected, FileTypeClassifier.Classify(fileName));
    }

    [Theory]
    [InlineData("photo.JPG", ".jpg")]
    [InlineData("archive.tar.gz", ".gz")]
    [InlineData("noext", "")]
    [InlineData("", "")]
    public void GetExtension_NormalizesToLowercase(string fileName, string expected)
    {
        Assert.Equal(expected, FileTypeClassifier.GetExtension(fileName));
    }
}

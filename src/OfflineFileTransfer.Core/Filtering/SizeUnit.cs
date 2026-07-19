namespace OfflineFileTransfer.Core.Filtering;

/// <summary>
/// Units used when expressing size thresholds.
/// </summary>
public enum SizeUnit
{
    Bytes = 0,
    KB = 1,
    MB = 2,
    GB = 3,
}

public static class SizeUnitExtensions
{
    /// <summary>
    /// Converts a value in the given unit to bytes.
    /// </summary>
    public static long ToBytes(this SizeUnit unit, double value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Size cannot be negative.");
        }

        var multiplier = unit switch
        {
            SizeUnit.Bytes => 1L,
            SizeUnit.KB => 1024L,
            SizeUnit.MB => 1024L * 1024L,
            SizeUnit.GB => 1024L * 1024L * 1024L,
            _ => 1L,
        };

        return checked((long)(value * multiplier));
    }
}

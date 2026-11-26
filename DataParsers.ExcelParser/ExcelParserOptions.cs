using System.Globalization;

namespace DataParsers.ExcelParser;

public sealed class ExcelParserOptions
{
    /// <summary>Zero-based worksheet index.</summary>
    public int SheetIndex { get; init; }

    /// <summary>One-based header row number.</summary>
    public int HeaderRowNumber { get; init; } = 1;

    /// <summary>Culture used for text cells. Native Excel numbers and dates are culture independent.</summary>
    public CultureInfo Culture { get; init; } = CultureInfo.InvariantCulture;
}

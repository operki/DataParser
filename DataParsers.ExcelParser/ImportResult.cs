namespace DataParsers.ExcelParser;

/// <summary>Row numbers are one-based. Column is the source header, or empty for worksheet errors.</summary>
public sealed record ImportError(int RowNumber, string Column, string Message);

public sealed record ImportResult<T>(IReadOnlyList<T> Records, IReadOnlyList<ImportError> Errors)
{
    public bool IsSuccess => Errors.Count == 0;
}

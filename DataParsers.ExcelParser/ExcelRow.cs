using System.Globalization;
using ExcelDataReader;

namespace DataParsers.ExcelParser;

/// <summary>Required typed values for the current row. Use only inside the row mapper.</summary>
public sealed class ExcelRow
{
    private readonly object?[] values;
    private readonly IReadOnlyDictionary<string, (int Index, string Header)> columns;
    private readonly CultureInfo culture;
    private readonly List<ImportError> errors;

    internal ExcelRow(int rowNumber, object?[] values, IReadOnlyDictionary<string, (int Index, string Header)> columns,
        CultureInfo culture, List<ImportError> errors)
    {
        RowNumber = rowNumber;
        this.values = values;
        this.columns = columns;
        this.culture = culture;
        this.errors = errors;
    }

    public int RowNumber { get; }

    public string GetString(string column)
        => Read(column, value => Convert.ToString(value, culture)!.Trim(), "text");

    public int GetInt32(string column)
        => Read(column, value => value switch
        {
            int number => number,
            double number when double.IsFinite(number) && number == Math.Truncate(number)
                && number >= int.MinValue && number <= int.MaxValue => (int)number,
            string text when int.TryParse(text, NumberStyles.Integer, culture, out var number) => number,
            _ => throw new FormatException()
        }, "an integer");

    public decimal GetDecimal(string column)
        => Read(column, value => value switch
        {
            int number => number,
            double number => Convert.ToDecimal(number),
            string text when decimal.TryParse(text, NumberStyles.Float, culture, out var number) => number,
            _ => throw new FormatException()
        }, "a decimal number");

    public DateTime GetDateTime(string column)
        => Read(column, value => value switch
        {
            DateTime date => date,
            string text when DateTime.TryParse(text, culture, DateTimeStyles.None, out var date) => date,
            _ => throw new FormatException()
        }, "a date");

    public bool GetBoolean(string column)
        => Read(column, value => value switch
        {
            bool flag => flag,
            string text when bool.TryParse(text, out var flag) => flag,
            string text when text.Equals("yes", StringComparison.OrdinalIgnoreCase) => true,
            string text when text.Equals("no", StringComparison.OrdinalIgnoreCase) => false,
            _ => throw new FormatException()
        }, "a boolean (true/false or yes/no)");

    private T Read<T>(string column, Func<object, T> convert, string expected)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(column);
        if (!columns.TryGetValue(ExcelParser.Normalize(column), out var mapping))
            throw new ArgumentException($"Column '{column}' is not mapped.", nameof(column));

        try
        {
            var value = mapping.Index < values.Length ? values[mapping.Index] : null;
            if (value is CellError cellError)
            {
                errors.Add(new ImportError(RowNumber, mapping.Header, $"Excel cell error: {cellError}."));
                return default!;
            }
            if (value is string cellText)
                value = cellText.Trim();
            if (value is null || value is string text && string.IsNullOrWhiteSpace(text))
            {
                errors.Add(new ImportError(RowNumber, mapping.Header, "A value is required."));
                return default!;
            }

            return convert(value);
        }
        catch (Exception error) when (error is FormatException or OverflowException)
        {
            errors.Add(new ImportError(RowNumber, mapping.Header, $"Expected {expected}."));
            return default!;
        }
    }
}

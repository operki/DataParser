using System.Text;
using ExcelDataReader;

namespace DataParsers.ExcelParser;

public static class ExcelParser
{
    /// <summary>Reads a table from the current stream position, leaving the caller's stream open.</summary>
    public static ImportResult<T> Read<T>(Stream input, IReadOnlyDictionary<string, string> columns,
        Func<ExcelRow, T> map, ExcelParserOptions? options = null) where T : notnull
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(map);
        options ??= new ExcelParserOptions();
        ArgumentOutOfRangeException.ThrowIfNegative(options.SheetIndex);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.HeaderRowNumber, 1);
        ArgumentNullException.ThrowIfNull(options.Culture);
        if (columns.Count == 0)
            throw new ArgumentException("At least one column mapping is required.", nameof(columns));

        var schema = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, header) in columns)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ArgumentException.ThrowIfNullOrWhiteSpace(header);
            if (!schema.TryAdd(Normalize(key), Normalize(header)))
                throw new ArgumentException($"Duplicate mapping key '{key}'.", nameof(columns));
        }

        using var buffer = new MemoryStream();
        input.CopyTo(buffer);
        buffer.Position = 0;
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using var reader = ExcelReaderFactory.CreateReader(buffer);
        var records = new List<T>();
        var errors = new List<ImportError>();

        for (var index = 0; index < options.SheetIndex; index++)
        {
            if (!reader.NextResult())
            {
                errors.Add(new ImportError(options.HeaderRowNumber, "", "The requested worksheet does not exist."));
                return new ImportResult<T>(records.AsReadOnly(), errors.AsReadOnly());
            }
        }

        var rowNumber = 0;
        while (rowNumber < options.HeaderRowNumber && reader.Read())
            rowNumber++;
        var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (rowNumber == options.HeaderRowNumber)
        {
            for (var index = 0; index < reader.FieldCount; index++)
            {
                var value = reader.GetValue(index) ?? (object?)reader.GetCellError(index);
                if (value is null)
                    continue;
                if (value is not string text)
                {
                    errors.Add(new ImportError(options.HeaderRowNumber, "", "Headers must be text cells."));
                    continue;
                }

                var header = Normalize(text);
                if (header.Length > 0 && !headers.TryAdd(header, index))
                    errors.Add(new ImportError(options.HeaderRowNumber, header, "Duplicate column header."));
            }
        }

        foreach (var header in schema.Values.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!headers.ContainsKey(header))
                errors.Add(new ImportError(options.HeaderRowNumber, header, "Required column is missing."));
        }

        if (errors.Count == 0)
        {
            var mappedColumns = schema.ToDictionary(pair => pair.Key,
                pair => (Index: headers[pair.Value], Header: pair.Value), StringComparer.OrdinalIgnoreCase);
            while (reader.Read())
            {
                rowNumber++;
                var values = new object?[reader.FieldCount];
                for (var index = 0; index < values.Length; index++)
                    values[index] = reader.GetValue(index) ?? (object?)reader.GetCellError(index);
                if (values.All(value => value is null || value is string text && string.IsNullOrWhiteSpace(text)))
                    continue;

                var previousErrors = errors.Count;
                var record = map(new ExcelRow(rowNumber, values, mappedColumns, options.Culture, errors));
                if (record is null)
                    throw new InvalidOperationException("The row mapper must return a record.");
                if (errors.Count == previousErrors)
                    records.Add(record);
            }
        }

        return new ImportResult<T>(records.AsReadOnly(), errors.AsReadOnly());
    }

    internal static string Normalize(string value)
        => string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}

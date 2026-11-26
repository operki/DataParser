# DataParser

[![CI](https://github.com/operki/DataParser/actions/workflows/ci.yml/badge.svg)](https://github.com/operki/DataParser/actions/workflows/ci.yml)

Typed Excel table imports with explicit header mapping and row-level validation errors.

**Status:** `DataParsers.ExcelParser` **0.1.0-alpha.1**, targeting .NET 10. The alpha covers Excel imports only. The older HTML, XML, ZIP, HTTP, and shared utility projects remain in the repository as legacy code and are not part of the alpha solution or its test coverage.

## Build and run

Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0), then run from the repository root:

```sh
dotnet restore DataParsers.slnx
dotnet build DataParsers.slnx --configuration Release --no-restore
dotnet test DataParsers.slnx --configuration Release --no-build
dotnet run --project DataParsers.ExcelParserExample --configuration Release --no-build
dotnet pack DataParsers.ExcelParser --configuration Release --no-build --output artifacts
```

The example imports both `.xlsx` workbooks in [`samples`](samples). The valid workbook produces two records. The invalid workbook produces one valid record and three errors on row 3 (missing name, invalid price, and invalid date). An additional `.xls` fixture covers native Excel numbers, dates, and booleans in the tests. No database, credentials, or network access is needed after package restore.

To import your own product catalog:

```sh
dotnet run --project DataParsers.ExcelParserExample -- samples/products-valid.xlsx
dotnet run --project DataParsers.ExcelParserExample -- catalog.xlsx de-DE
```

The CLI expects `Product ID`, `Name`, `Price`, `Available On`, and `Active` headers. It writes records and errors as JSON. Exit codes: `0` for success, `2` for validation errors when a file is supplied, and `1` for a file, format, or argument error. Running without arguments demonstrates both sample files and exits successfully, including the expected validation failures.

## Library usage

Reference `DataParsers.ExcelParser/DataParsers.ExcelParser.csproj`, or install the package produced by `dotnet pack` from a local feed. CI builds the package but does not publish it to NuGet.

```csharp
using System.Globalization;
using DataParsers.ExcelParser;

var columns = new Dictionary<string, string>
{
    ["id"] = "Product ID",
    ["name"] = "Name",
    ["price"] = "Price"
};

using var input = File.OpenRead("products.xlsx");
var result = ExcelParser.Read(input, columns,
    row => new Product(row.GetInt32("id"), row.GetString("name"), row.GetDecimal("price")),
    new ExcelParserOptions { Culture = CultureInfo.InvariantCulture });

foreach (var product in result.Records)
    Console.WriteLine(product);
foreach (var error in result.Errors)
    Console.WriteLine($"Row {error.RowNumber}, {error.Column}: {error.Message}");

record Product(int Id, string Name, decimal Price);
```

The mapping dictionary associates logical names used by the row mapper with source headers. Header matching ignores case and normalizes whitespace. Column order does not matter; extra columns are ignored. Missing mapped headers, duplicate nonempty headers, and non-text headers are reported before any records are mapped.

## Conversion and validation

| Getter | Accepted values |
| --- | --- |
| `GetString` | Nonempty text or a native scalar converted using the configured culture. Text is trimmed. |
| `GetInt32` | An integral Excel number in the `Int32` range, or integer text. Fractions are rejected. |
| `GetDecimal` | An Excel number, or decimal text using the configured culture. Group separators are rejected to avoid silently misreading a decimal separator. |
| `GetDateTime` | An Excel date cell, or date text parsed with the configured culture. Unformatted numeric cells are not treated as dates. |
| `GetBoolean` | An Excel boolean, or English `true`/`false`/`yes`/`no` text, ignoring case. |

Every getter requires a nonempty value. Invalid cells add an `ImportError` with the physical, one-based row number and source header. Getters return a default value after recording an error so the mapper can read the remaining columns. Records from rows with any reported errors are excluded from `Records`; valid rows remain in input order. Completely blank rows are skipped. Rows with an empty first cell are still validated.

Keep the mapper focused on constructing a record. Do not perform external writes from it: the row may later be rejected. All required values should be read through the getters, and `ExcelRow` must not be retained outside the mapper. Custom mapper exceptions, unknown logical column names, and invalid configuration are programming errors and propagate to the caller.

## Options and resource ownership

| Option | Default | Meaning |
| --- | --- | --- |
| `SheetIndex` | `0` | Zero-based worksheet index. |
| `HeaderRowNumber` | `1` | One-based header row. Records begin on the following row. |
| `Culture` | Invariant | Culture for text numbers and dates. Native Excel numbers and dates are culture independent. |

The parser accepts `.xlsx` and `.xls` using ExcelDataReader. It reads from the input stream's current position and leaves the caller's stream open, including when the import fails. Non-seekable streams are supported. Workbooks and temporary buffers are disposed internally. Missing worksheets are reported as import errors; unreadable or unsupported workbook data throws a file-format exception from ExcelDataReader.

Formula cells use their saved cached results. The parser does not evaluate formulas or load external workbooks; recalculate and save a workbook in a spreadsheet application when fresh formula values are required. Excel error cells such as `#DIV/0!` are reported explicitly when read by a getter.

## Scope and compatibility

The Excel alpha replaces the old `DataParser.HtmlParser.ExcelParser` and `NpoiCell` API. Use `DataParsers.ExcelParser.ExcelParser.Read` with a row mapper instead of `GetSheets`, `ParseTable`, and cell wrappers. The other modules retain their existing APIs and can still be built through their individual project files.

This implementation deliberately buffers the input file and keeps the results in memory. It is intended for small and medium tables, not streaming large files. It does not provide optional-field schemas, reflection-based mapping, business-rule validation, exports, or a web API. The consuming application owns domain validation and file-size limits.

## License

MIT. See [LICENSE](LICENSE). ExcelDataReader and other dependencies retain their respective licenses.

using System.Globalization;
using System.Text.Json;
using DataParsers.ExcelParser;

var columns = new Dictionary<string, string>
{
    ["id"] = "Product ID",
    ["name"] = "Name",
    ["price"] = "Price",
    ["available"] = "Available On",
    ["active"] = "Active"
};
var paths = args.Length == 0
    ? new[] { "products-valid.xlsx", "products-invalid.xlsx" }
        .Select(name => Path.Combine(AppContext.BaseDirectory, "samples", name)).ToArray()
    : new[] { args[0] };

try
{
    if (args.Length > 2)
        throw new ArgumentException("Usage: DataParsers.ExcelParserExample [file.xlsx|file.xls] [culture]");
    var options = new ExcelParserOptions
    {
        Culture = args.Length == 2 ? CultureInfo.GetCultureInfo(args[1]) : CultureInfo.InvariantCulture
    };

    var hasErrors = false;
    foreach (var path in paths)
    {
        using var input = File.OpenRead(path);
        var result = ExcelParser.Read(input, columns, row => new Product(
            row.GetInt32("id"), row.GetString("name"), row.GetDecimal("price"),
            row.GetDateTime("available"), row.GetBoolean("active")), options);
        Console.WriteLine($"File: {Path.GetFileName(path)}");
        Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        hasErrors |= !result.IsSuccess;
    }

    return args.Length > 0 && hasErrors ? 2 : 0;
}
catch (Exception error)
{
    Console.Error.WriteLine($"Import failed: {error.Message}");
    return 1;
}

internal sealed record Product(int Id, string Name, decimal Price, DateTime AvailableOn, bool Active);

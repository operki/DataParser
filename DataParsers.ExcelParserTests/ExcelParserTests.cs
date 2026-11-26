using System.Globalization;
using DataParsers.ExcelParser;
using System.IO.Compression;
using System.Xml.Linq;
using NUnit.Framework;
using TableParser = DataParsers.ExcelParser.ExcelParser;

namespace DataParsers.ExcelParserTests;

public sealed class ExcelParserTests
{
    private static readonly Dictionary<string, string> Columns = new()
    {
        ["id"] = "Product ID", ["name"] = "Name", ["price"] = "Price",
        ["available"] = "Available On", ["active"] = "Active"
    };

    private static readonly object?[] Headers = ["Product ID", "Name", "Price", "Available On", "Active"];
    private static readonly DateTime AvailableOn = new(2026, 11, 1);

    [TestCase(false)]
    [TestCase(true)]
    public void ReadsNativeExcelTypesFromXlsxAndXls(bool legacy)
    {
        using Stream input = legacy
            ? File.OpenRead(Path.Combine(TestContext.CurrentContext.TestDirectory, "samples", "products-native.xls"))
            : Workbook([Headers, [1, "Pen", 12.50m, AvailableOn, true]]);

        var result = ReadProducts(input);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Records, Is.EqualTo(new[] { new Product(1, "Pen", 12.50m, AvailableOn, true) }));
        Assert.That(input.CanRead, Is.True);
    }

    [TestCase("en-US", "12.50", "11/25/2026")]
    [TestCase("de-DE", "12,50", "25.11.2026")]
    [TestCase("fr-FR", "12,50", "25/11/2026")]
    public void ParsesTextUsingTheSpecifiedCulture(string culture, string price, string date)
    {
        using var input = Workbook([Headers, ["1", " Pen ", price, date, "YES"]]);

        var result = ReadProducts(input, new ExcelParserOptions { Culture = CultureInfo.GetCultureInfo(culture) });

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Records[0], Is.EqualTo(new Product(1, "Pen", 12.50m, new DateTime(2026, 11, 25), true)));
    }

    [Test]
    public void NativeNumbersAndDatesDoNotDependOnTextCulture()
    {
        using var input = Workbook([Headers, [1, "Pen", 12.50m, AvailableOn, true]]);

        var result = ReadProducts(input, new ExcelParserOptions { Culture = CultureInfo.GetCultureInfo("de-DE") });

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Records[0].Price, Is.EqualTo(12.50m));
        Assert.That(result.Records[0].AvailableOn, Is.EqualTo(AvailableOn));
    }

    [Test]
    public void NormalizesHeadersAndSupportsReorderedColumns()
    {
        using var input = Workbook([
            ["EXTRA", " available   on ", " PRICE ", " active ", "NAME", " product\tID "],
            ["ignored", AvailableOn, 12.50m, false, "Pen", 1]
        ]);

        var result = ReadProducts(input);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Records[0], Is.EqualTo(new Product(1, "Pen", 12.50m, AvailableOn, false)));
    }

    [Test]
    public void MapsSourceHeadersToDifferentLogicalNames()
    {
        using var input = Workbook([["Item Code"], [42]]);
        var columns = new Dictionary<string, string> { ["id"] = "Item Code" };

        var result = TableParser.Read(input, columns, row => row.GetInt32(" ID "));

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Records, Is.EqualTo(new[] { 42 }));
    }

    [Test]
    public void ReportsEveryReadCellErrorAndKeepsValidRows()
    {
        using var input = Workbook([
            Headers,
            [1, "Pen", 12.50m, AvailableOn, true],
            ["invalid", null, "free", "not a date", "maybe"],
            [2, "Notebook", 20m, AvailableOn, false]
        ]);

        var result = ReadProducts(input);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Records.Select(product => product.Id), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(result.Errors, Has.Count.EqualTo(5));
            Assert.That(result.Errors.All(error => error.RowNumber == 3), Is.True);
            Assert.That(result.Errors.Select(error => error.Column),
                Is.EqualTo(new[] { "Product ID", "Name", "Price", "Available On", "Active" }));
        });
    }

    [Test]
    public void DoesNotDropRowsWithAnEmptyFirstCell()
    {
        using var input = Workbook([Headers, [null, "Pen", 12.50m, AvailableOn, true]]);

        var result = ReadProducts(input);

        Assert.That(result.Records, Is.Empty);
        Assert.That(result.Errors, Is.EqualTo(new[] { new ImportError(2, "Product ID", "A value is required.") }));
    }

    [Test]
    public void SkipsCompletelyBlankRowsAndPreservesPhysicalRowNumbers()
    {
        using var input = Workbook([Headers, [], [" ", null, "\t"], [1, "Pen", "bad", AvailableOn, true]]);

        var result = ReadProducts(input);

        Assert.That(result.Errors, Has.Count.EqualTo(1));
        Assert.That(result.Errors[0].RowNumber, Is.EqualTo(4));
    }

    [Test]
    public void ReportsMissingHeadersBeforeCallingTheMapper()
    {
        using var input = Workbook([["Product ID", "Name"], [1, "Pen"]]);
        var calls = 0;

        var result = TableParser.Read(input, Columns, row => { calls++; return row.GetInt32("id"); });

        Assert.That(calls, Is.Zero);
        Assert.That(result.Records, Is.Empty);
        Assert.That(result.Errors.Select(error => error.Column), Is.EqualTo(new[] { "Price", "Available On", "Active" }));
        Assert.That(result.Errors.All(error => error.RowNumber == 1), Is.True);
    }

    [Test]
    public void RejectsDuplicateHeadersAfterNormalization()
    {
        using var input = Workbook([["Product ID", "Name", "Price", "Available On", "Active", " NAME "]]);

        var result = ReadProducts(input);

        Assert.That(result.Errors, Is.EqualTo(new[] { new ImportError(1, "NAME", "Duplicate column header.") }));
    }

    [Test]
    public void RejectsNonTextHeaders()
    {
        using var input = Workbook([["Product ID", "Name", "Price", "Available On", "Active", 123]]);

        var result = ReadProducts(input);

        Assert.That(result.Errors.Single().Message, Is.EqualTo("Headers must be text cells."));
    }

    [Test]
    public void HeaderOnlyTablesAreValid()
    {
        using var input = Workbook([Headers]);

        var result = ReadProducts(input);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Records, Is.Empty);
    }

    [Test]
    public void EmptyWorksheetsReportMissingHeaders()
    {
        using var input = Workbook([]);

        var result = ReadProducts(input);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Errors, Has.Count.EqualTo(5));
    }

    [Test]
    public void SelectsTheRequestedWorksheetAndHeaderRow()
    {
        using var input = Workbook([], [["Product catalog"], Headers, [1, "Pen", 12.50m, AvailableOn, true]]);

        var result = ReadProducts(input, new ExcelParserOptions { SheetIndex = 1, HeaderRowNumber = 2 });

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Records[0].Id, Is.EqualTo(1));
    }

    [Test]
    public void MissingWorksheetsReturnAnImportError()
    {
        using var input = Workbook([Headers]);

        var result = ReadProducts(input, new ExcelParserOptions { SheetIndex = 1 });

        Assert.That(result.Errors.Single().Message, Is.EqualTo("The requested worksheet does not exist."));
    }

    [TestCase(1.5)]
    [TestCase(2147483648d)]
    [TestCase(-2147483649d)]
    public void RejectsFractionalAndOutOfRangeIntegers(double id)
    {
        using var input = Workbook([Headers, [id, "Pen", 1, AvailableOn, true]]);

        var result = ReadProducts(input);

        Assert.That(result.Records, Is.Empty);
        Assert.That(result.Errors.Single(), Is.EqualTo(new ImportError(2, "Product ID", "Expected an integer.")));
    }

    [TestCase("12,50")]
    [TestCase("1,234.50")]
    public void RejectsWrongDecimalSeparatorsInsteadOfChangingTheValue(string price)
    {
        using var input = Workbook([Headers, [1, "Pen", price, AvailableOn, true]]);

        var result = ReadProducts(input);

        Assert.That(result.Errors.Single().Column, Is.EqualTo("Price"));
    }

    [TestCase("true", true)]
    [TestCase("FALSE", false)]
    [TestCase("yes", true)]
    [TestCase("No", false)]
    public void ReadsEnglishBooleanText(string active, bool expected)
    {
        using var input = Workbook([Headers, [1, "Pen", 1, AvailableOn, active]]);

        var result = ReadProducts(input);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Records[0].Active, Is.EqualTo(expected));
    }

    [Test]
    public void ExcelErrorCellsAreReportedWithoutStoppingTheImport()
    {
        using var input = Workbook([Headers, [1, "Pen", new ErrorCell("#DIV/0!"), AvailableOn, true]]);

        var result = ReadProducts(input);

        Assert.That(result.Records, Is.Empty);
        Assert.That(result.Errors.Single().Column, Is.EqualTo("Price"));
        Assert.That(result.Errors.Single().Message, Is.EqualTo("Excel cell error: DIV0."));
    }

    [Test]
    public void RowsContainingOnlyAnExcelErrorAreNotSkipped()
    {
        using var input = Workbook([["Price"], [new ErrorCell("#DIV/0!")]]);
        var columns = new Dictionary<string, string> { ["price"] = "Price" };

        var result = TableParser.Read(input, columns, row => row.GetDecimal("price"));

        Assert.That(result.Records, Is.Empty);
        Assert.That(result.Errors.Single(), Is.EqualTo(new ImportError(2, "Price", "Excel cell error: DIV0.")));
    }

    [Test]
    public void TrimsBooleanText()
    {
        using var input = Workbook([Headers, [1, "Pen", 1, AvailableOn, " yes "]]);

        var result = ReadProducts(input);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Records[0].Active, Is.True);
    }

    [Test]
    public void ReadsCachedFormulaResultsWithoutRecalculating()
    {
        using var input = Workbook([Headers, [1, "Pen", new FormulaCell("2+3", 5), AvailableOn, true]]);

        var result = ReadProducts(input);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Records[0].Price, Is.EqualTo(5m));
        input.Position = 0;
        using var archive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
        using var sheet = archive.GetEntry("xl/worksheets/sheet1.xml")!.Open();
        var xml = XDocument.Load(sheet);
        Assert.That(xml.Descendants(Spreadsheet + "f").Single().Value, Is.EqualTo("2+3"));
    }

    [Test]
    public void SupportsNonSeekableInputStreams()
    {
        using var workbook = Workbook([Headers, [1, "Pen", 1, AvailableOn, true]]);
        using var input = new NonSeekableStream(workbook);

        var result = ReadProducts(input);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(workbook.CanRead, Is.True);
    }

    [Test]
    public void MapperErrorsPropagateAndLeaveTheInputOpen()
    {
        using var input = Workbook([Headers, [1, "Pen", 1, AvailableOn, true]]);

        Assert.Throws<InvalidOperationException>(() => TableParser.Read<int>(input, Columns,
            _ => throw new InvalidOperationException("Mapping failed.")));
        Assert.That(input.CanRead, Is.True);
    }

    [Test]
    public void UnknownMappingKeysAreProgrammingErrors()
    {
        using var input = Workbook([Headers, [1, "Pen", 1, AvailableOn, true]]);

        Assert.Throws<ArgumentException>(() => TableParser.Read(input, Columns, row => row.GetInt32("missing")));
    }

    [Test]
    public void DuplicateMappingKeysAreRejected()
    {
        using var input = Workbook([Headers]);
        var columns = new Dictionary<string, string> { ["id"] = "Product ID", [" ID "] = "Name" };

        Assert.Throws<ArgumentException>(() => TableParser.Read(input, columns, row => row.GetInt32("id")));
    }

    [Test]
    public void InvalidWorkbookDataThrowsAndLeavesTheInputOpen()
    {
        using var input = new MemoryStream([1, 2, 3, 4]);

        Assert.Catch(() => ReadProducts(input));
        Assert.That(input.CanRead, Is.True);
    }

    [TestCase("products-valid.xlsx", 2, 0)]
    [TestCase("products-invalid.xlsx", 1, 3)]
    public void ImportsTheShippedExamples(string name, int records, int errors)
    {
        using var input = File.OpenRead(Path.Combine(TestContext.CurrentContext.TestDirectory, "samples", name));

        var result = ReadProducts(input);

        Assert.That(result.Records, Has.Count.EqualTo(records));
        Assert.That(result.Errors, Has.Count.EqualTo(errors));
    }

    private static ImportResult<Product> ReadProducts(Stream input, ExcelParserOptions? options = null)
        => TableParser.Read(input, Columns, row => new Product(row.GetInt32("id"), row.GetString("name"),
            row.GetDecimal("price"), row.GetDateTime("available"), row.GetBoolean("active")), options);

    private static readonly XNamespace Spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    private static MemoryStream Workbook(object?[][] rows, object?[][]? secondSheet = null)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var sheets = secondSheet == null ? new[] { rows } : new[] { rows, secondSheet };
            XNamespace relationships = "http://schemas.openxmlformats.org/package/2006/relationships";
            XNamespace documentRelationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            XNamespace types = "http://schemas.openxmlformats.org/package/2006/content-types";
            WriteXml(archive, "[Content_Types].xml", new XElement(types + "Types",
                new XElement(types + "Default", new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
                new XElement(types + "Default", new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")),
                new XElement(types + "Override", new XAttribute("PartName", "/xl/workbook.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")),
                new XElement(types + "Override", new XAttribute("PartName", "/xl/styles.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml")),
                Enumerable.Range(1, sheets.Length).Select(i => new XElement(types + "Override", new XAttribute("PartName", $"/xl/worksheets/sheet{i}.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")))));
            WriteXml(archive, "_rels/.rels", new XElement(relationships + "Relationships",
                new XElement(relationships + "Relationship", new XAttribute("Id", "rId1"), new XAttribute("Type", documentRelationships.NamespaceName + "/officeDocument"), new XAttribute("Target", "xl/workbook.xml"))));
            WriteXml(archive, "xl/workbook.xml", new XElement(Spreadsheet + "workbook",
                new XElement(Spreadsheet + "sheets", Enumerable.Range(1, sheets.Length).Select(i =>
                    new XElement(Spreadsheet + "sheet", new XAttribute("name", $"Sheet{i}"), new XAttribute("sheetId", i), new XAttribute(documentRelationships + "id", $"rId{i}"))))));
            WriteXml(archive, "xl/_rels/workbook.xml.rels", new XElement(relationships + "Relationships",
                Enumerable.Range(1, sheets.Length).Select(i => new XElement(relationships + "Relationship", new XAttribute("Id", $"rId{i}"), new XAttribute("Type", documentRelationships.NamespaceName + "/worksheet"), new XAttribute("Target", $"worksheets/sheet{i}.xml"))),
                new XElement(relationships + "Relationship", new XAttribute("Id", "styles"), new XAttribute("Type", documentRelationships.NamespaceName + "/styles"), new XAttribute("Target", "styles.xml"))));
            WriteXml(archive, "xl/styles.xml", new XElement(Spreadsheet + "styleSheet",
                new XElement(Spreadsheet + "cellXfs", new XAttribute("count", 2),
                    new XElement(Spreadsheet + "xf", new XAttribute("numFmtId", 0)),
                    new XElement(Spreadsheet + "xf", new XAttribute("numFmtId", 14)))));
            for (var sheet = 0; sheet < sheets.Length; sheet++)
            {
                WriteXml(archive, $"xl/worksheets/sheet{sheet + 1}.xml", new XElement(Spreadsheet + "worksheet",
                    new XElement(Spreadsheet + "sheetData", sheets[sheet].Select((values, index) =>
                        new XElement(Spreadsheet + "row", new XAttribute("r", index + 1), values.Select((value, column) => Cell(value, $"{(char)('A' + column)}{index + 1}")))))));
            }
        }
        stream.Position = 0;
        return stream;
    }

    private static XElement Cell(object? value, string reference)
    {
        var cell = new XElement(Spreadsheet + "c", new XAttribute("r", reference));
        switch (value)
        {
            case null: break;
            case string text:
                cell.Add(new XAttribute("t", "inlineStr"), new XElement(Spreadsheet + "is", new XElement(Spreadsheet + "t", text)));
                break;
            case bool flag:
                cell.Add(new XAttribute("t", "b"), new XElement(Spreadsheet + "v", flag ? 1 : 0));
                break;
            case DateTime date:
                cell.Add(new XAttribute("s", 1), new XElement(Spreadsheet + "v", date.ToOADate().ToString(CultureInfo.InvariantCulture)));
                break;
            case FormulaCell formula:
                cell.Add(new XElement(Spreadsheet + "f", formula.Expression), new XElement(Spreadsheet + "v", formula.CachedValue));
                break;
            case ErrorCell error:
                cell.Add(new XAttribute("t", "e"), new XElement(Spreadsheet + "v", error.Code));
                break;
            default:
                cell.Add(new XElement(Spreadsheet + "v", Convert.ToString(value, CultureInfo.InvariantCulture)));
                break;
        }
        return cell;
    }

    private static void WriteXml(ZipArchive archive, string name, XElement xml)
    {
        using var stream = archive.CreateEntry(name).Open();
        new XDocument(xml).Save(stream);
    }

    private sealed record FormulaCell(string Expression, double CachedValue);
    private sealed record ErrorCell(string Code);

    private sealed class NonSeekableStream(Stream inner) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed record Product(int Id, string Name, decimal Price, DateTime AvailableOn, bool Active);
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DataParsers.Base.Helpers;
using NPOI.SS.UserModel;

namespace DataParser.HtmlParser;

/// <summary>
///     Требует пакет NPOI
///     + Известен тип данных в ячейке
///     - Плохо работает с большими файлами (используй ExcelReader.cs)
/// </summary>
public partial class ExcelParser
{

    public ExcelParser(byte[] fileData, string fileName)
    {
        this.fileName = fileName;
        try
        {
            using var stream = new MemoryStream(fileData);
            var workbook = WorkbookFactory.Create(stream);
            var formulaEvaluator = workbook.GetCreationHelper().CreateFormulaEvaluator();
            formulaEvaluator.IgnoreMissingWorkbooks = true;
            formulaEvaluator.EvaluateAll();
            sheets = Enumerable.Range(0, workbook.NumberOfSheets)
                .Select(workbook.GetSheetAt)
                .ToList();
        }
        catch(Exception)
        {
            try
            {
                using var stream = new MemoryStream(fileData);
                cells = ParseOldFormat(stream);
            }
            catch(Exception e)
            {
                throw new Exception($"Can't parse workbook. Exception: {e}");
            }
        }
    }

    public int SheetNumbers => sheets?.Count ?? cells.Count;

    public IEnumerable<IEnumerable<List<NpoiCell>>> GetSheets()
    {
        return Enumerable.Range(0, SheetNumbers)
            .Select(GetSheet);
    }

    public IEnumerable<List<NpoiCell>> GetSheet(int sheetNumber)
    {
        if(sheets == null)
        {
            foreach(var row in cells[sheetNumber])
                yield return row;
            yield break;
        }

        var sheet = sheets[sheetNumber];
        var maxColumns = 0;
        for(var i = 0; i < sheet.LastRowNum + 1; i++)
        {
            var currentColumns = sheet.GetRow(i)?.LastCellNum;
            if(currentColumns == null)
                continue;

            if(maxColumns < currentColumns)
                maxColumns = (int)currentColumns;
        }

        for(var i = 0; i < sheet.LastRowNum + 1; i++)
        {
            var row = sheet.GetRow(i);
            if(row?.GetCell(0) == null || row.GetCell(0)?.CellType == CellType.Blank)
                continue;

            yield return GetCells(row, maxColumns).ToList();
        }
    }

    public IEnumerable<Dictionary<string, NpoiCell>> ParseTable(int sheetNumber, Dictionary<string, string> headersMapper)
    {
        headersMapper = headersMapper
            .ToDictSafe(kvp => ModifyColumn(kvp.Key), kvp => kvp.Value);
        var rows = GetSheet(sheetNumber).ToList();
        return GetRowDicts(rows, headersMapper, fileName);
    }
}

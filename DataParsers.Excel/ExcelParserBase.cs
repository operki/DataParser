using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DataParsers.Base.Helpers;
using ExcelDataReader;
using NPOI.SS.UserModel;

namespace DataParser.HtmlParser;

public partial class ExcelParser
{
    private readonly List<List<List<NpoiCell>>> cells;
    private readonly string fileName;
    private readonly List<ISheet> sheets;

    private static List<List<List<NpoiCell>>> ParseOldFormat(Stream stream)
    {
        var result = new List<List<List<NpoiCell>>>();
        using var reader = ExcelReaderFactory.CreateReader(stream);
        do
        {
            var sheet = new List<List<NpoiCell>>();
            while(reader.Read())
            {
                var fields = new object[reader.FieldCount];
                var row = new List<NpoiCell>();
                for(var i = 0; i < fields.Length; i++)
                    row.Add(new NpoiCell(reader.GetValue(i).ToString().TrimToNull()));
                if(row.IsSignificant())
                    sheet.Add(row);
            }

            result.Add(sheet);
        } while(reader.NextResult());

        return result;
    }

    private static IEnumerable<NpoiCell> GetCells(IRow row, int? columnsCount = null)
    {
        return Enumerable.Range(0, columnsCount ?? row.LastCellNum)
            .Select(cellNumber => new NpoiCell(row.GetCell(cellNumber, MissingCellPolicy.CREATE_NULL_AS_BLANK)));
    }

    private static IEnumerable<Dictionary<string, NpoiCell>> GetRowDicts(IReadOnlyList<List<NpoiCell>> sheetRows, IReadOnlyDictionary<string, string> headersMapper, string fileName)
    {
        if(!sheetRows.IsSignificant())
            throw new Exception($"Can't find headers in file: '{fileName}'");

        var indexMapper = new Dictionary<int, string>();
        var indexMapperIndex = -1;
        _ = sheetRows.Select((row, index) => (row, index))
            .FirstOrDefault(tuple =>
            {
                var (row, rowIndex) = tuple;
                indexMapperIndex = rowIndex;
                indexMapper = row?.Select((cell, index) =>
                    {
                        var cellColumnValue = ModifyColumn(cell);
                        return cellColumnValue != default && headersMapper.ContainsKey(cellColumnValue)
                            ? (index, value: headersMapper[cellColumnValue])
                            : default;
                    })
                    .WhereNotDefault()
                    .ToDictSafe(tuple => tuple.index, tuple => tuple.value);
                return indexMapper?.Count == headersMapper.Count;
            });

        if(indexMapper == default)
            throw new Exception($"Can't find excel headers mapping in file: '{fileName}'");

        return sheetRows
            .Skip(indexMapperIndex + 1)
            .Select(row => row.Select((cell, index) => (cell, index))
                .Where(tuple => indexMapper.ContainsKey(tuple.index))
                .ToDictSafe(tuple => indexMapper[tuple.index], tuple => tuple.cell));
    }

    private static string ModifyColumn(NpoiCell cell)
    {
        return cell.ToString()?.RemoveExtraSpaces().TrimToNull()?.ToLower();
    }

    private static string ModifyColumn(string str)
    {
        return str?.RemoveExtraSpaces().TrimToNull()?.ToLower();
    }
}

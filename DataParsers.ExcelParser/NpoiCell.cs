using System;
using System.Globalization;
using DataParsers.Base.Helpers;
using NPOI.SS.UserModel;

namespace DataParser.HtmlParser;

public class NpoiCell
{
    public static DateTime XlsStartDate = new(1899, 12, 30);

    private static readonly CultureInfo RuCultureInfo = new("ru");
    private readonly ICell cell;
    private readonly string cellValue;

    public NpoiCell(string cellValue)
    {
        this.cellValue = cellValue;
    }

    public NpoiCell(ICell cell)
    {
        this.cell = cell;
    }

    public NpoiCell(ICell cell, CultureInfo cultureInfo)
    {
        this.cell = cell;
        CultureInfo = cultureInfo;
    }

    public bool IsDateTime => DateUtil.IsCellDateFormatted(cell);
    public CellType CellType => cell.CellType;
    private CultureInfo CultureInfo { get; } = RuCultureInfo;

    public override string ToString()
    {
        return GetString(CultureInfo);
    }

    public double ToDouble()
    {
        if(cell != null && cell.CellType == CellType.Numeric)
            return cell.NumericCellValue;

        var cellStr = cell == null
            ? cellValue
            : GetString(CultureInfo);
        return cellStr != default && double.TryParse(cellStr, NumberStyles.Float, RuCultureInfo, out var result)
            ? result
            : default;
    }

    public int ToInt()
    {
        if(cell.CellType == CellType.Numeric)
            return (int)cell.NumericCellValue;

        var cellStr = cell == null
            ? cellValue
            : GetString(CultureInfo);
        return cellStr != default && int.TryParse(cellStr, NumberStyles.Integer, RuCultureInfo, out var result)
            ? result
            : default;
    }

    public bool ToBool()
    {
        if(cell.CellType == CellType.Numeric)
            return cell.BooleanCellValue;

        var cellStr = cell == null
            ? cellValue
            : GetString(null);
        return cellStr switch
        {
            "Да" => true,
            "Нет" => false,
            _ => default
        };
    }

    public DateTime? ToDate()
    {
        var cellStr = cell == null
            ? cellValue
            : GetString(CultureInfo);
        int.TryParse(cellStr, out var days);
        return days <= 0
            ? null
            : XlsStartDate.AddDays(days);
    }

    private string GetString(CultureInfo numericCultureInfo)
    {
        if(cell == null)
            return null;

        switch(cell.CellType)
        {
            case CellType.String:
                return cell.StringCellValue.TrimToNull();
            case CellType.Numeric:
                return cell.NumericCellValue.ToString(numericCultureInfo).TrimToNull();
            case CellType.Boolean:
                return cell.BooleanCellValue ? "true" : "false";
            case CellType.Formula:
                return ReplaceFormulaCell(numericCultureInfo).StringCellValue.TrimToNull();
            case CellType.Error:
                return FormulaError.ForInt(cell.ErrorCellValue).String.TrimToNull();
            case CellType.Unknown:
            case CellType.Blank:
                return null;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private ICell ReplaceFormulaCell(CultureInfo numericCultureInfo = null)
    {
        if(cell.CellType != CellType.Formula)
            return cell;

        var cellValue = GetFormulaText(numericCultureInfo);
        cell.SetCellType(CellType.String);
        cell.SetCellValue(cellValue);
        return cell;
    }

    private string GetFormulaText(CultureInfo numericCultureInfo = null)
    {
        try
        {
            return cell.StringCellValue;
        }
        catch(Exception)
        {
            try
            {
                return cell.NumericCellValue.ToString(numericCultureInfo ?? RuCultureInfo);
            }
            catch(Exception)
            {
                try
                {
                    return cell.BooleanCellValue ? "true" : "false";
                }
                catch(Exception)
                {
                    return cell.CellFormula;
                }
            }
        }
    }
}

using System.Text.RegularExpressions;

namespace DataParsers.Base.Helpers;

public static class StringUtils
{
    private static readonly Regex WhiteSpaces = new(@"\s+", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex ExtraWhiteSpaces = new(@"\s{2,}", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static bool IsSignificant(this string value)
    {
        return !string.IsNullOrWhiteSpace(value);
    }

    public static string RemoveExtraSpaces(this string value)
    {
        return value == null ? null : ExtraWhiteSpaces.Replace(value, " ");
    }

    public static string TrimToNull(this string value, params char[] chars)
    {
        if(value == null)
            return null;
        var result = value.Trim(chars);
        return result == string.Empty ? null : result;
    }

    public static string NormalizeSpaces(this string value)
    {
        return value == null ? null : WhiteSpaces.Replace(value, " ");
    }
}

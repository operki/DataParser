using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace DataParsers.Base.Helpers;

public static class UrlHelper
{
    private static readonly Regex UrlWithPath = new("/.+$");

    [ThreadStatic] private static IdnMapping idnMapping;

    private static readonly Regex PunycodedRegex = new(@"xn--[^\s/]+(?=\s|$|/)");
    private static readonly Regex ProtocolRegex = new(@"\w+://");
    private static readonly Regex DomainRegex = new(@"://([^/#?]+)([/#?]|$)");
    private static readonly Regex DomainRegex2 = new(@"^([^/#?]+)([/#?]|$)");

    public static string TryNormalizeIfDomain(this string url)
    {
        if(url == null || UrlWithPath.IsMatch(url))
            return null;

        return url.Trim('/');
    }

    public static string TrimStartProto(this string url)
    {
        return ProtocolRegex.Replace(url, "");
    }

    public static string TryNormalizeUrlOrSource(string url, UrlParts parts, bool skipDefaultPort = false)
    {
        try
        {
            return NormalizeUrl(url, parts, skipDefaultPort);
        }
        catch
        {
            return url;
        }
    }

    public static string TryNormalizeUrl(string url, UrlParts parts, bool skipDefaultPort = false)
    {
        try
        {
            return NormalizeUrl(url, parts, skipDefaultPort);
        }
        catch
        {
            return null;
        }
    }

    public static string NormalizeUrl(string url, UrlParts parts, bool skipDefaultPort = false)
    {
        if(url == null)
            return null;

        var uri = new UriBuilder(url).Uri;
        var result = new StringBuilder(url.Length);

        if(parts.HasFlag(UrlParts.Scheme))
            result.AppendFormat("{0}://", uri.Scheme);

        result.Append(NormalizeHost(uri.Host));

        if(parts.HasFlag(UrlParts.Port) && !(skipDefaultPort && uri.IsDefaultPort))
            result.AppendFormat(":{0}", uri.Port);

        if(parts.HasFlag(UrlParts.PathAndQuery))
        {
            var normalizePathAndQuery = NormalizePathAndQuery(uri.PathAndQuery);
            if(normalizePathAndQuery != "/")
                result.AppendFormat(normalizePathAndQuery);
        }

        return result.ToString();
    }

    public static string NormalizeHost(string host)
    {
        return TryDecodePunyCode(host);
    }

    public static string TryDecodePunyCode(string str)
    {
        idnMapping = idnMapping ?? new IdnMapping();
        foreach(Match match in PunycodedRegex.Matches(str))
        {
            var pCode = match.Value;
            try
            {
                str = str.Replace(pCode, idnMapping.GetUnicode(idnMapping.GetAscii(pCode)));
            }
            catch
            {
                // ignored
            }
        }

        return str;
    }

    public static string TryEncodePunyCode(string str)
    {
        idnMapping = idnMapping ?? new IdnMapping();
        var match = DomainRegex.Match(str);
        if(!match.Success)
            match = DomainRegex2.Match(str);
        if(!match.Success)
            return null;

        var domainMatch = match.Groups[1];
        if(domainMatch.Value.All(c => c < 128))
            return $"{str.Substring(0, domainMatch.Index)}" +
                   $"{domainMatch.Value}" +
                   $"{str.Substring(domainMatch.Index + domainMatch.Length)}";

        var mapping = DoIt.TryOrDefault(() => idnMapping.GetAscii(domainMatch.Value.ToLower()));
        if(mapping == null)
            return null;
        return $"{str.Substring(0, domainMatch.Index)}" +
               $"{mapping}" +
               $"{str.Substring(domainMatch.Index + domainMatch.Length)}";
    }

    public static string NormalizePathAndQuery(string pathAndQuery)
    {
        return WebUtility.UrlDecode(pathAndQuery);
    }

    public static Uri GetUrlWithoutPath(this Uri uri)
    {
        return new Uri(uri.Scheme + Uri.SchemeDelimiter + uri.Host);
    }

    public static string GetSecondLevelDomain(this Uri uri)
    {
        return string.Join(".", uri.Host.Split('.').GetLast(2));
    }

    public static string GetThirdLevelDomain(this Uri uri)
    {
        return string.Join(".", uri.Host.Split('.').GetLast(3));
    }

    public static int GetDomainLevel(this Uri uri)
    {
        return uri.Host.Count(c => c == '.') + 1;
    }



    public static string GetSite(string url)
    {
        return IsCorrectUrl(url)
            ? new Uri(url).GetLeftPart(UriPartial.Authority)
            : null;
    }

    public static string GetPathAndQuery(string url)
    {
        return IsCorrectUrl(url)
            ? new Uri(url).PathAndQuery
            : null;
    }

    public static string PunycodeUrl(string url)
    {
        return new IdnMapping().GetAscii(url);
    }

    public static string SecureUrl(string url)
    {
        var uri = new UriBuilder(url);
        var hadDefaultPort = uri.Uri.IsDefaultPort;
        uri.Scheme = Uri.UriSchemeHttps;
        uri.Port = hadDefaultPort ? -1 : uri.Port;
        return uri.ToString();
    }

    public static string CorrectUrl(string site, string url)
    {
        if(!TryCorrectUrl(site, url, out var resultLink))
            throw new Exception($"Incorrect uri from '{site}' and link '{url}'");

        return resultLink;
    }

    public static bool TryCorrectUrl(string site, string url, out string resultUrl)
    {
        if(!Uri.TryCreate(url, UriKind.Absolute, out var resultUri)
           && !Uri.TryCreate(new Uri(site), url, out resultUri))
        {
            resultUrl = null;
            return false;
        }

        resultUrl = resultUri.ToString();
        return true;
    }

    public static bool IsCorrectUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out _);
    }
}

public static class UrlExtractor
{

    private static readonly Regex DomainValidator = new(@"(?:\.ru|\.su|\.рф|\.com|\.net|\.info|\.kz|\.biz|\.xn--[^\.]{1,16}|\.by)(?:$|/)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex Url = new(@"\b(?<url>(?<protocol>https?://)?(?<urlWithoutProtocol>(?:[\w\d\-]{1,64}\.)(?:[\w\d\-]{1,64}\.)*(?:[a-zа-яё]{2,64})(?::\d{1,5})?(?:/[^\s""]{0,64}){0,20}))\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static IEnumerable<string> ExtractUrls(string str)
    {
        return Url.Matches(str)
            .Cast<Match>()
            .Select(m => m.Groups["url"].Value)
            .Where(ValidateUrl);
    }

    public static IEnumerable<Match> MatchUrls(string text)
    {
        return Url.Matches(text).Cast<Match>();
    }

    private static bool ValidateUrl(string str)
    {
        return str.StartsWith("http://")
               || str.StartsWith("https://")
               || str.Contains("www.")
               || DomainValidator.IsMatch(str);
    }
}

[Flags]
public enum UrlParts
{
    DomainOnly = 0,
    Scheme = 1,
    Port = 1 << 1,
    PathAndQuery = 1 << 2
}

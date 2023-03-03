using System;

namespace DataParsers.Base.Helpers;

public static class UrlHelper
{
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
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using DataParsers.Base.Helpers;
using HtmlAgilityPack;

namespace DataParsers.HtmlParser;

public static class HtmlParser
{
    private static readonly List<string> UnremovedAttributes = new()
    {
        "id",
        "class"
    };

    public static HtmlDocument InitDocument(string html)
    {
        var htmlDoc = new HtmlDocument();
        htmlDoc.LoadHtml(html);
        return htmlDoc;
    }

    public static HtmlDocument InitCleanDocument(string html)
    {
        var htmlDoc = new HtmlDocument();
        htmlDoc.LoadHtml(html);
        htmlDoc.RemoveScripts();
        return htmlDoc;
    }

    public static HtmlDocument InitSchemeDocument(string html)
    {
        var htmlDoc = new HtmlDocument();
        htmlDoc.LoadHtml(html);
        htmlDoc.RemoveScripts();
        htmlDoc.RemoveAttributes();
        return htmlDoc;
    }

    public static HtmlNode GetNode(this HtmlDocument document, string pattern)
    {
        return document.DocumentNode.SelectSingleNode(pattern);
    }

    public static HtmlNode GetNode(this HtmlNode parentNode, string pattern)
    {
        return parentNode.SelectSingleNode(pattern);
    }

    public static bool TryGetNode(this HtmlDocument document, string pattern, out HtmlNode node)
    {
        node = document.GetNode(pattern);
        return node != null;
    }

    public static bool TryGetNode(this HtmlNode parentNode, string pattern, out HtmlNode node)
    {
        node = parentNode.GetNode(pattern);
        return node != null;
    }

    public static IEnumerable<HtmlNode> GetNodes(this HtmlDocument document, string pattern)
    {
        return document.DocumentNode.SelectNodes(pattern);
    }

    public static IEnumerable<HtmlNode> GetNodes(this HtmlNode parentNode, string pattern)
    {
        return parentNode.SelectNodes(pattern);
    }

    public static bool TryGetNodes(this HtmlDocument document, string pattern, out IEnumerable<HtmlNode> node)
    {
        node = document.GetNodes(pattern);
        return node.IsSignificant();
    }

    public static bool TryGetNodes(this HtmlNode parentNode, string pattern, out IEnumerable<HtmlNode> nodes)
    {
        nodes = parentNode.GetNodes(pattern);
        return nodes.IsSignificant();
    }

    public static string HtmlTrim(string htmlText)
    {
        return HttpUtility.HtmlDecode(htmlText ?? "")
            .Replace("&nbsp", "\u00A0")
            .NormalizeSpaces()
            .TrimToNull();
    }

    public static string OuterHtmlTrim(this HtmlNode node)
    {
        return HtmlTrim(node.OuterHtml);
    }

    public static string InnerHtmlTrim(this HtmlNode node)
    {
        return HtmlTrim(node.InnerHtml);
    }

    public static string InnerTextTrim(this HtmlNode node)
    {
        return HtmlTrim(node.InnerText);
    }

    public static string RemoveScripts(string html)
    {
        var htmlDoc = InitDocument(html);
        htmlDoc.RemoveScripts();
        return htmlDoc.DocumentNode.InnerHtml;
    }

    public static void RemoveScripts(this HtmlDocument document)
    {
        document.DocumentNode.RemoveScripts();
    }

    public static void RemoveScripts(this IEnumerable<HtmlNode> nodes)
    {
        nodes.ForEach(node => node.RemoveScripts());
    }

    public static void RemoveScripts(this HtmlNode node)
    {
        node.Descendants()
            .ToList()
            .ForEach(element =>
            {
                if(element.Name == "script" || element.Name == "style")
                    element.Remove();
                else if(element.NodeType == HtmlNodeType.Element && element.Attributes.Any())
                    element.Attributes
                        .Where(attr => attr.Name == "style" || attr.Name.StartsWith("on"))
                        .Select(attr => attr.Name)
                        .ToList()
                        .ForEach(attr => element.Attributes[attr]?.Remove());
            });
    }

    public static string RemoveAttributes(string html)
    {
        var htmlDoc = InitDocument(html);
        htmlDoc.RemoveAttributes();
        return htmlDoc.DocumentNode.InnerHtml;
    }

    public static void RemoveAttributes(this HtmlDocument document)
    {
        document.DocumentNode?.RemoveAttributes();
    }

    public static void RemoveAttributes(this IEnumerable<HtmlNode> nodes)
    {
        nodes.ForEach(node => node.RemoveAttributes());
    }

    public static void RemoveAttributes(this HtmlNode node)
    {
        node.Descendants()
            ?.Where(element => element.NodeType == HtmlNodeType.Element && element.Attributes.Any())
            .ForEach(element =>
            {
                var unremovedAttributes = new List<string>(UnremovedAttributes);
                if(element.Name == "a" || element.Name == "link")
                    unremovedAttributes.Add("href");

                var attrs = element.Attributes
                    .Where(attr => !unremovedAttributes.Contains(attr.Name))
                    .Select(attr => attr.Name)
                    .ToArray();

                foreach(var attr in attrs)
                    element.Attributes[attr].Remove();
            });
    }

    //source: https://github.com/ceee/ReadSharp/blob/master/ReadSharp/HtmlUtilities.cs
    public static string GetOnlyText(string html)
    {
        if(!html.IsSignificant())
            return null;

        var doc = InitDocument(html);
        var sw = new StringWriter();
        ConvertTo(doc.DocumentNode, sw);
        return sw.ToString();
    }

    private static void ConvertContentTo(HtmlNode node, TextWriter outText)
    {
        node.ChildNodes.ForEach(subNode => ConvertTo(subNode, outText));
    }

    private static void ConvertTo(HtmlNode node, TextWriter outText)
    {
        switch(node.NodeType)
        {
            case HtmlNodeType.Comment:
                break;

            case HtmlNodeType.Document:
                ConvertContentTo(node, outText);
                break;

            case HtmlNodeType.Text:
                var parentName = node.ParentNode.Name;
                if(parentName == "script" || parentName == "style")
                    break;

                var html = ((HtmlTextNode)node).Text;
                if(HtmlNode.IsOverlappedClosingElement(html))
                    break;

                if(html.Trim().Length > 0)
                    outText.Write(HtmlEntity.DeEntitize(html));
                break;

            case HtmlNodeType.Element:
                switch(node.Name)
                {
                    case "p":
                        outText.Write("\r\n");
                        break;
                    case "br":
                        outText.Write("\r\n");
                        break;
                }

                if(node.HasChildNodes)
                    ConvertContentTo(node, outText);
                break;

            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public static bool TryGetUrls(this HtmlNode parentNode, out List<string> links, string resultSite = null, string aPattern = ".//a[@href]")
    {
        links = GetUrls(parentNode, resultSite, aPattern);
        return links.IsSignificant();
    }

    public static List<string> GetUrls(this HtmlNode parentNode, string resultSite = null, string aPattern = ".//a[@href]")
    {
        return parentNode.TryGetNodes(aPattern, out var aNodes)
            ? aNodes
                .Select(aNode =>
                {
                    var link = aNode.GetAttributeValue("href", null);
                    if(resultSite == null)
                        return link;
                    UrlHelper.TryCorrectUrl(resultSite, link, out var resultLink);
                    return resultLink;
                })
                .WhereNotNull()
                .ToList()
            : null;
    }
}

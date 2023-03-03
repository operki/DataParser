using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using DataParsers.Base.Helpers;

namespace DataParsers.XmlTree;

/// <summary>
///     Генерируется в XmlTree.cs
/// </summary>
public class XmlNode
{
    private readonly Dictionary<string, string> attributes = new();
    private readonly Dictionary<string, List<XmlNode>> innerNodes = new();
    private readonly string name;
    private readonly bool nodeGetAttributeCanBeNull;
    private readonly bool nodeGetCanBeNull;
    public readonly string Value;

    internal XmlNode(System.Xml.XmlNode xmlNode, bool getNodeCanBeNull, bool getAttributeCanBeNull)
    {
        try
        {
            name = xmlNode.Name;
            nodeGetCanBeNull = getNodeCanBeNull;
            nodeGetAttributeCanBeNull = getAttributeCanBeNull;
            if(xmlNode.NodeType != XmlNodeType.Element)
                return;

            attributes = xmlNode.Attributes
                ?.Cast<XmlAttribute>()
                .ToDictSafe(attr => attr.Name, attr => attr.Value.TrimToNull());

            if(!xmlNode.HasChildNodes)
                return;

            string nodeValue = null;
            xmlNode.ChildNodes
                .Cast<System.Xml.XmlNode>()
                .ForEach(node =>
                {
                    if(node.NodeType == XmlNodeType.Text)
                    {
                        nodeValue = xmlNode.InnerText.TrimToNull();
                    }
                    else
                    {
                        if(!innerNodes.ContainsKey(node.Name))
                            innerNodes[node.Name] = new List<XmlNode>();
                        innerNodes[node.Name].Add(new XmlNode(node, getNodeCanBeNull, getAttributeCanBeNull));
                    }
                });
            Value = nodeValue;
        }
        catch(Exception e)
        {
            throw new FormatException($"Can't parse '{name}' node. {e}");
        }
    }

    public string this[string attributeName, bool? getAttributeCanBeNull = null]
    {
        get
        {
            getAttributeCanBeNull ??= nodeGetAttributeCanBeNull;
            var attributeText = attributes.ContainsKey(attributeName)
                ? attributes[attributeName]
                : null;
            if(attributeText.IsSignificant() || getAttributeCanBeNull.Value)
                return attributeText;

            throw new ArgumentNullException($"Can't find '{name}[{attributeName}]' in \r\n'{ToString()}'");
        }
    }

    public override string ToString()
    {
        return ToString(null);
    }

    private string ToString(string tabPrefix)
    {
        if(!Value.IsSignificant() && !innerNodes.IsSignificant())
            return $"{tabPrefix}<{name} />";

        var sb = new StringBuilder();
        sb.AppendLine($"{tabPrefix}<{name}{string.Join(" ", attributes.Select(atr => $" {atr.Key}=\"{atr.Value}\"")).RemoveExtraSpaces()}>");
        if(Value.IsSignificant())
            sb.AppendLine($"{tabPrefix}\t{Value}");

        innerNodes?.Values
            .SelectMany()
            .Where(node => node.name != "#whitespace")
            .ForEach(node => sb.AppendLine($"{tabPrefix}{node.ToString(tabPrefix + "\t")}"));
        sb.Append($"{tabPrefix}</{name}>");
        return sb.ToString();
    }

	/// <summary>
	///     Получить значение из элемента
	/// </summary>
	/// <param name="nodeName">Название элемента в который надо опуститься, при наличии нескольких таких элементов в текущем элементе вернется первый из них</param>
	/// <param name="nodeNameDown">Название элемента из которого надо взять значение, при наличии нескольких таких элементов в nodeName вернется первый из них</param>
	/// <param name="getNodeCanBeNull">
	///     true: если элемента нет, то метод вернет null
	///     false: если элемента нет, то бросается Exception
	///     null: берется значение, установленное при инициализации XmlTree
	/// </param>
	/// <returns>Значение элемента nodeNameDown</returns>
	public string GetValue(string nodeName, string nodeNameDown, bool? getNodeCanBeNull = null)
    {
        return Get(nodeName, getNodeCanBeNull)?.GetValue(nodeNameDown, getNodeCanBeNull);
    }

	/// <summary>
	///     Получить значение из элемента
	/// </summary>
	/// <param name="nodeName">Название элемента из которого надо взять значение, при наличии нескольких таких элементов в текущем элементе вернется первый из них</param>
	/// <param name="getNodeCanBeNull">
	///     true: если элемента нет, то метод вернет null
	///     false: если элемента нет, то бросается Exception
	///     null: берется значение, установленное при инициализации XmlTree
	/// </param>
	/// <returns>Значение элемента nodeName</returns>
	public string GetValue(string nodeName, bool? getNodeCanBeNull = null)
    {
        return Get(nodeName, getNodeCanBeNull)?.Value;
    }

	/// <summary>
	///     Получить элемент
	/// </summary>
	/// <param name="nodeName">Название элемента, при наличии нескольких таких элементов в текущем элементе вернется первый из них</param>
	/// <param name="getNodeCanBeNull">
	///     true: если элемента нет, то метод вернет null
	///     false: если элемента нет, то бросается Exception
	///     null: берется значение, установленное при инициализации XmlTree
	/// </param>
	/// <returns>Элемент nodeName</returns>
	public XmlNode Get(string nodeName, bool? getNodeCanBeNull = null)
    {
        return GetAll(nodeName, getNodeCanBeNull)?.FirstOrDefault();
    }

	/// <summary>
	///     Получить элементы
	/// </summary>
	/// <param name="nodeName">Название элемента, при наличии нескольких таких элементов в текущем элементе вернется первый из них</param>
	/// <param name="getNodeCanBeNull">
	///     true: если элемента нет, то метод вернет null
	///     false: если элемента нет, то бросается Exception
	///     null: берется значение, установленное при инициализации XmlTree
	/// </param>
	/// <returns>Элемент nodeName</returns>
	public List<XmlNode> GetAll(string nodeName, bool? getNodeCanBeNull = null)
    {
        getNodeCanBeNull ??= nodeGetCanBeNull;
        var result = innerNodes.ContainsKey(nodeName)
            ? innerNodes[nodeName]
            : null;
        if(getNodeCanBeNull.Value || result != null)
            return result;

        throw new ArgumentNullException($"Can't find '{name}/{nodeName}' in \r\n'{ToString()}'");
    }

	/// <summary>
	///     Получить все элементы
	/// </summary>
	/// <returns>Все элементы, дочерние по отношению к текущему</returns>
	public Dictionary<string, List<XmlNode>> GetAll()
    {
        return innerNodes;
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Schema;

namespace DataParsers.XmlTree;

/// <summary>
/// Builds a tree of XML elements for parsing.
/// Exposes element names, values, attributes, and child elements.
/// </summary>
public class XmlTree
{
    public readonly XmlNode RootNode;

	/// <summary>
	/// Initializes the XML tree.
	/// </summary>
	/// <param name="stream">Input stream, read to construct the XML tree.</param>
	/// <param name="xsdPath">Local path to the XSD schema.</param>
	/// <param name="getValueCanBeNull">
	/// true: XmlNode.GetValue may return null.
	/// false: XmlNode.GetValue throws when the value is missing.
	/// </param>
	/// <param name="getAttributeCanBeNull">
	/// true: XmlNode[] may return null.
	/// false: XmlNode[] throws when the attribute is missing.
	/// </param>
	/// <exception cref="XmlSchemaValidationException">XML schema validation failed.</exception>
	/// <exception cref="Exception">The input could not be identified as XML.</exception>
	public XmlTree(Stream stream, string xsdPath, bool getValueCanBeNull = false, bool getAttributeCanBeNull = false)
    {
        var settings = new XmlReaderSettings
        {
            ValidationType = ValidationType.Schema,
            ValidationFlags = XmlSchemaValidationFlags.ReportValidationWarnings |
                              XmlSchemaValidationFlags.ProcessIdentityConstraints |
                              XmlSchemaValidationFlags.ProcessInlineSchema |
                              XmlSchemaValidationFlags.ProcessSchemaLocation
        };
        settings.Schemas.Add(null, xsdPath);

        XmlDocument document;
        try
        {
            document = CreateXml(stream, settings);
        }
        catch(XmlSchemaValidationException e)
        {
            throw new XmlSchemaValidationException($"[line: {e.LineNumber}, offset: {e.LinePosition}]", e);
        }

        document.Validate(ValidationEventHandler);

        if(document.DocumentElement == null)
            throw new Exception("Can't identify data as xml");

        RootNode = new XmlNode(document.DocumentElement, getValueCanBeNull, getAttributeCanBeNull);
    }

	/// <summary>
	/// Initializes the XML tree.
	/// </summary>
	/// <param name="xmlData">XML document text.</param>
	/// <param name="getValueCanBeNull">
	/// true: XmlNode.GetValue may return null.
	/// false: XmlNode.GetValue throws when the value is missing.
	/// </param>
	/// <param name="getAttributeCanBeNull">
	/// true: XmlNode[] may return null.
	/// false: XmlNode[] throws when the attribute is missing.
	/// </param>
	public XmlTree(string xmlData, bool getValueCanBeNull = false, bool getAttributeCanBeNull = false)
        : this(new MemoryStream(Encoding.UTF8.GetBytes(xmlData ?? "")), getValueCanBeNull, getAttributeCanBeNull) { }

	/// <summary>
	/// Initializes the XML tree.
	/// </summary>
	/// <param name="stream">Input stream, read to construct the XML tree.</param>
	/// <param name="getValueCanBeNull">
	/// true: XmlNode.GetValue may return null.
	/// false: XmlNode.GetValue throws when the value is missing.
	/// </param>
	/// <param name="getAttributeCanBeNull">
	/// true: XmlNode[] may return null.
	/// false: XmlNode[] throws when the attribute is missing.
	/// </param>
	public XmlTree(Stream stream, bool getValueCanBeNull = false, bool getAttributeCanBeNull = false)
    {
        var document = CreateXml(stream);
        if(document.DocumentElement == null)
            throw new Exception("Can't identify data as xml");

        RootNode = new XmlNode(document.DocumentElement, getValueCanBeNull, getAttributeCanBeNull);
    }

	/// <summary>
	/// An attribute of the root element.
	/// </summary>
	/// <param name="attributeName">Attribute name.</param>
	public string this[string attributeName] => RootNode[attributeName];

    private static void ValidationEventHandler(object sender, ValidationEventArgs e)
    {
        switch(e.Severity)
        {
            case XmlSeverityType.Error:
                throw new Exception($"Xml validation error: {e.Message}");
            case XmlSeverityType.Warning:
                throw new Exception($"Xml validation warning: {e.Message}");
            default:
                throw new Exception($"Xml validation unexpected error: {e.Severity}/{e.Message}");
        }
    }

    private static XmlDocument CreateXml(Stream stream, XmlReaderSettings settings = null)
    {
        var document = new XmlDocument();
        using var reader = XmlReader.Create(stream, settings);
        document.Load(reader);
        stream.Position = 0;
        return document;
    }

	/// <summary>
	/// Enumerates XML elements using System.Xml.XmlReader.
	/// </summary>
	/// <param name="inputStream">Input stream.</param>
	/// <param name="elementName">Name of the elements to extract.</param>
	/// <param name="getValueCanBeNull">
	/// true: XmlNode.GetValue may return null.
	/// false: XmlNode.GetValue throws when the value is missing.
	/// </param>
	/// <param name="getAttributeCanBeNull">
	/// true: XmlNode[] may return null.
	/// false: XmlNode[] throws when the attribute is missing.
	/// </param>
	/// <returns>Matching elements.</returns>
	public static IEnumerable<XmlNode> Parse(Stream inputStream, string elementName, bool getValueCanBeNull = false, bool getAttributeCanBeNull = false)
    {
        var reader = XmlReader.Create(inputStream, new XmlReaderSettings());
        var document = new XmlDocument();
        reader.MoveToContent();
        reader.ReadToDescendant(elementName);
        do
        {
            yield return new XmlNode(document.ReadNode(reader), getValueCanBeNull, getAttributeCanBeNull);
            if(reader.Name != elementName)
                reader.MoveToContent();
        } while(reader.Name == elementName);

        reader.Close();
    }

	/// <summary>
	/// Enumerates XML elements using System.Xml.XmlReader.
	/// </summary>
	/// <param name="filePath">Local file path.</param>
	/// <param name="elementName">Name of the elements to extract.</param>
	/// <param name="getValueCanBeNull">
	/// true: XmlNode.GetValue may return null.
	/// false: XmlNode.GetValue throws when the value is missing.
	/// </param>
	/// <param name="getAttributeCanBeNull">
	/// true: XmlNode[] may return null.
	/// false: XmlNode[] throws when the attribute is missing.
	/// </param>
	/// <returns>Matching elements.</returns>
	public static IEnumerable<XmlNode> Parse(string filePath, string elementName, bool getValueCanBeNull = false, bool getAttributeCanBeNull = false)
    {
        var reader = XmlReader.Create(filePath, new XmlReaderSettings());
        var document = new XmlDocument();
        reader.MoveToContent();
        reader.ReadToDescendant(elementName);
        do
        {
            yield return new XmlNode(document.ReadNode(reader), getValueCanBeNull, getAttributeCanBeNull);
            if(reader.Name != elementName)
                reader.MoveToContent();
        } while(reader.Name == elementName);

        reader.Close();
    }

	/// <summary>
	/// Checks whether a stream contains an XML document.
	/// </summary>
	/// <param name="stream">Input stream, read to construct the XML tree.</param>
	/// <returns>Whether the stream contains a valid XML document.</returns>
	public static bool IsXml(Stream stream)
    {
        TextReader textReader = new StreamReader(stream);
        return IsXml(textReader);
    }

	/// <summary>
	/// Checks whether a string contains an XML document.
	/// </summary>
	/// <param name="xmlData">XML document text.</param>
	/// <returns>Whether the string contains a valid XML document.</returns>
	public static bool IsXml(string xmlData)
    {
        TextReader textReader = new StringReader(xmlData);
        return IsXml(textReader);
    }

    private static bool IsXml(TextReader textReader)
    {
        try
        {
            var document = new XmlDocument();
            document.Load(textReader);
            return document.DocumentElement != null;
        }
        catch(Exception)
        {
            return false;
        }
    }

	/// <summary>
	/// Gets an element value.
	/// </summary>
	/// <param name="nodeName">Element name; uses the first match when several elements share the name.</param>
	/// <param name="nodeNameDown">Child element name; uses the first matching child.</param>
	/// <returns>The value of nodeNameDown.</returns>
	public string GetValue(string nodeName, string nodeNameDown)
    {
        return RootNode.GetValue(nodeName, nodeNameDown);
    }

	/// <summary>
	/// Gets an element value.
	/// </summary>
	/// <param name="nodeName">Element name; uses the first match when several elements share the name.</param>
	/// <returns>The value of nodeName.</returns>
	public string GetValue(string nodeName)
    {
        return RootNode.GetValue(nodeName);
    }

	/// <summary>
	/// Gets the first matching element.
	/// </summary>
	/// <param name="nodeName">Element name; uses the first match when several elements share the name.</param>
	/// <returns>The first nodeName element.</returns>
	public XmlNode Get(string nodeName)
    {
        return RootNode.Get(nodeName);
    }

	/// <summary>
	/// Gets matching elements.
	/// </summary>
	/// <param name="nodeName">Element name; uses the first match when several elements share the name.</param>
	/// <returns>All nodeName elements.</returns>
	public List<XmlNode> GetAll(string nodeName)
    {
        return RootNode.GetAll(nodeName);
    }

	/// <summary>
	/// Gets all child elements.
	/// </summary>
	/// <returns>All children of the root element.</returns>
	public Dictionary<string, List<XmlNode>> GetAll()
    {
        return RootNode.GetAll();
    }
}

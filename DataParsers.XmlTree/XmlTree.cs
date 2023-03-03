using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Schema;

namespace DataParsers.XmlTree;

/// <summary>
///     Класс, формирующий дерево из xml элементов в удобном для парсинга виде
///     Оперирует названием элемента, его значением, аттрибутами и вложенными элементами
/// </summary>
public class XmlTree
{
    public readonly XmlNode RootNode;

	/// <summary>
	///     Инициализация дерева
	/// </summary>
	/// <param name="stream">Поток, будет полностью вычитан для формирования дерева</param>
	/// <param name="xsdPath">Локальный путь к схеме для файла</param>
	/// <param name="getValueCanBeNull">
	///     true: Метод XmlNode.GetValue может вернуть null
	///     false: Если метод XmlNode.GetValue пытается вернуть null, то бросается Exception
	/// </param>
	/// <param name="getAttributeCanBeNull">
	///     true: Метод XmlNode[] может вернуть null
	///     false: Если метод XmlNode[] пытается вернуть null, то бросается Exception
	/// </param>
	/// <exception cref="XmlSchemaValidationException">Ошибка при валидации xml схемы из xsdPath</exception>
	/// <exception cref="Exception">Не удалось определить файл как xml</exception>
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
	///     Инициализация дерева
	/// </summary>
	/// <param name="xmlData">Строка с xml данными</param>
	/// <param name="getValueCanBeNull">
	///     true: Метод XmlNode.GetValue может вернуть null
	///     false: Если метод XmlNode.GetValue пытается вернуть null, то бросается Exception
	/// </param>
	/// <param name="getAttributeCanBeNull">
	///     true: Метод XmlNode[] может вернуть null
	///     false: Если метод XmlNode[] пытается вернуть null, то бросается Exception
	/// </param>
	public XmlTree(string xmlData, bool getValueCanBeNull = false, bool getAttributeCanBeNull = false)
        : this(new MemoryStream(Encoding.UTF8.GetBytes(xmlData ?? "")), getValueCanBeNull, getAttributeCanBeNull) { }

	/// <summary>
	///     Инициализация дерева
	/// </summary>
	/// <param name="stream">Поток, будет полностью вычитан для формирования дерева</param>
	/// <param name="getValueCanBeNull">
	///     true: Метод XmlNode.GetValue может вернуть null
	///     false: Если метод XmlNode.GetValue пытается вернуть null, то бросается Exception
	/// </param>
	/// <param name="getAttributeCanBeNull">
	///     true: Метод XmlNode[] может вернуть null
	///     false: Если метод XmlNode[] пытается вернуть null, то бросается Exception
	/// </param>
	public XmlTree(Stream stream, bool getValueCanBeNull = false, bool getAttributeCanBeNull = false)
    {
        var document = CreateXml(stream);
        if(document.DocumentElement == null)
            throw new Exception("Can't identify data as xml");

        RootNode = new XmlNode(document.DocumentElement, getValueCanBeNull, getAttributeCanBeNull);
    }

	/// <summary>
	///     Аттрибут корневого элемента дерева
	/// </summary>
	/// <param name="attributeName">Название аттрибута</param>
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
	///     Потоково парсит xml файл, используя стандартный System.Xml.XmlReader
	/// </summary>
	/// <param name="inputStream">Входной стрим для парсинга</param>
	/// <param name="elementName">Название элементов, которые необходимо вытащить</param>
	/// <param name="getValueCanBeNull">
	///     true: Метод XmlNode.GetValue может вернуть null
	///     false: Если метод XmlNode.GetValue пытается вернуть null, то бросается Exception
	/// </param>
	/// <param name="getAttributeCanBeNull">
	///     true: Метод XmlNode[] может вернуть null
	///     false: Если метод XmlNode[] пытается вернуть null, то бросается Exception
	/// </param>
	/// <returns>Список элементов</returns>
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
	///     Потоково парсит xml файл, используя стандартный System.Xml.XmlReader
	/// </summary>
	/// <param name="filePath">Локальный путь к файлу</param>
	/// <param name="elementName">Название элементов, которые необходимо вытащить</param>
	/// <param name="getValueCanBeNull">
	///     true: Метод XmlNode.GetValue может вернуть null
	///     false: Если метод XmlNode.GetValue пытается вернуть null, то бросается Exception
	/// </param>
	/// <param name="getAttributeCanBeNull">
	///     true: Метод XmlNode[] может вернуть null
	///     false: Если метод XmlNode[] пытается вернуть null, то бросается Exception
	/// </param>
	/// <returns>Список элементов</returns>
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
	///     Определяет является ли поток xml документом
	/// </summary>
	/// <param name="stream">Поток данных</param>
	/// <returns>Является ли поток корректным xml документом</returns>
	public static bool IsXml(Stream stream)
    {
        TextReader textReader = new StreamReader(stream);
        return IsXml(textReader);
    }

	/// <summary>
	///     Определяет является ли строка xml документом
	/// </summary>
	/// <param name="xmlData">Строка xml данных</param>
	/// <returns>Является ли строка корректным xml документом</returns>
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
	///     Получить значение из элемента
	/// </summary>
	/// <param name="nodeName">Название элемента в который надо опуститься, при наличии нескольких такихэлементов в головном документе вернется первый из них</param>
	/// <param name="nodeNameDown">Название элемента из которого надо взять значение, при наличии нескольких таких элементов в nodeName вернется первый из них</param>
	/// <returns>Значение элемента nodeNameDown</returns>
	public string GetValue(string nodeName, string nodeNameDown)
    {
        return RootNode.GetValue(nodeName, nodeNameDown);
    }

	/// <summary>
	///     Получить значение из элемента
	/// </summary>
	/// <param name="nodeName">Название элемента, при наличии нескольких таких элементов в головном документе вернется первый из них</param>
	/// <returns>Значение элемента nodeName</returns>
	public string GetValue(string nodeName)
    {
        return RootNode.GetValue(nodeName);
    }

	/// <summary>
	///     Получить элемент
	/// </summary>
	/// <param name="nodeName">Название элемента, при наличии нескольких таких элементов в головном документе вернется первый из них</param>
	/// <returns>Элемент nodeName</returns>
	public XmlNode Get(string nodeName)
    {
        return RootNode.Get(nodeName);
    }

	/// <summary>
	///     Получить элементы
	/// </summary>
	/// <param name="nodeName">Название элементов</param>
	/// <returns>Элементы nodeName</returns>
	public List<XmlNode> GetAll(string nodeName)
    {
        return RootNode.GetAll(nodeName);
    }

	/// <summary>
	///     Получить все элементы
	/// </summary>
	/// <returns>Все элементы, дочерние по отношению к головному</returns>
	public Dictionary<string, List<XmlNode>> GetAll()
    {
        return RootNode.GetAll();
    }
}

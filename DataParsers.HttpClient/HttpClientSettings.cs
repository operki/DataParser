using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace DataParsers.HttpClient;

public enum DownloadStrategyFileName
{
    PathGet,
    Random,
    Specify
}

/// <summary>
///     Настройки для HttpDataFactory
/// </summary>
public class HttpClientSettings
{
    public const int DownloadTimeoutDefault = 1_000 * 60 * 15;
    public const int PreLoadTimeoutDefault = 1_000;
    public const int RetriesCountDefault = 5;

    public HttpClientSettings(IReadOnlyDictionary<string, string> settings)
    {
        if(settings.ContainsKey("baseUrl"))
            BaseUrl = settings["baseUrl"];
        if(settings.ContainsKey("downloadTimeout"))
            DownloadTimeout = int.Parse(settings["downloadTimeout"]);
        if(settings.ContainsKey("cookiesPath"))
            CookiesPath = settings["cookiesPath"];
        if(settings.ContainsKey("preLoadTimeout"))
            PreLoadTimeout = int.Parse(settings["preLoadTimeout"]);
        if(settings.ContainsKey("retriesCount"))
            RetriesCount = int.Parse(settings["retriesCount"]);
    }

    public HttpClientSettings() { }

	/// <summary>
	///     Добавляет префикс к запросам с  относительными путями. Если указан то при попытке скачивания данных по урлу с отличающимся хостом будет бросать Exception
	/// </summary>
	public string BaseUrl { get; set; }

	/// <summary>
	///     Стратегия именования файла при скачивании на диск через методы, возвращающие HttpStreamResult
	///     PathGet - файл на диске будет назван через Path.GetFileName(url), позволяет докачивать файлы при обрыве соединения
	///     Random - файл на диске будет назван случайно, позволяет избегать ошибок при параллельном скачивании, например '01.01.2020\data.xml' и '01.01.2021\data.xml'
	///     Specify - требует при каждом запросе на скачивание указывать имя файла
	/// </summary>
	public DownloadStrategyFileName StrategyFileName { get; set; } = DownloadStrategyFileName.PathGet;

	/// <summary>
	///     Разрешает скачивать только по защищенному протоколу https://, не учитывается если указан Proxy
	/// </summary>
	public bool OnlyHttps { get; set; } = true;

	/// <summary>
	///     Проксирование запросов
	/// </summary>
	public IWebProxy Proxy { get; set; } = null;

    public int DownloadTimeout { get; set; } = DownloadTimeoutDefault;

	/// <summary>
	///     Контейнер для куков, используется при инициализации клиента
	/// </summary>
	public CookieContainer CookieContainer { get; set; } = null;

	/// <summary>
	///     Локальный путь к кукам, применяется при инициализации если не указан CookieContainer и при сохранении куков в Dispose
	/// </summary>
	public string CookiesPath { get; set; }

	/// <summary>
	///     Сертификат сервера, не учитывается если указан SslValidation
	/// </summary>
	public X509Certificate2 ServerCert { get; set; } = null;

	/// <summary>
	///     Валидация запроса с учетом сертификата
	/// </summary>
	public Func<HttpRequestMessage, X509Certificate2, X509Chain, SslPolicyErrors, bool> SslValidation { get; set; } = null;

	/// <summary>
	///     Модификация HttpClientHandler, применяется разово при инициализации HttpDataFactory
	/// </summary>
	public Action<HttpClientHandler> ModifyClientHandler { get; set; } = null;

	/// <summary>
	///     Модификация HttpClient, применяется разово при инициализации HttpDataFactory
	/// </summary>
	public Action<System.Net.Http.HttpClient> ModifyClient { get; set; } = null;

	/// <summary>
	///     Модификация HttpClient, применяется разово при post-запросах
	///     Позволяет, например, указывать ContentType
	///     content => content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded")
	/// </summary>
	public Action<ByteArrayContent> ModifyContent { get; set; } = null;

	/// <summary>
	///     Креды для HttpClientHandler, применяется разово при инициализации HttpDataFactory
	/// </summary>
	public ICredentials Credentials { get; set; } = null;

	/// <summary>
	///     Задержка перед отправкой запроса, помогает ограничить траффик к источнику, можно менять после инициализации
	/// </summary>
	public int PreLoadTimeout { get; set; } = PreLoadTimeoutDefault;

	/// <summary>
	///     Количество попыток до возврата ошибки скачивания, можно менять после инициализации
	/// </summary>
	public int RetriesCount { get; set; } = RetriesCountDefault;
}

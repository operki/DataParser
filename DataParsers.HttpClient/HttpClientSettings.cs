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
/// Settings for HttpDataFactory.
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
	/// Base URL for relative requests. Requests to a different host are rejected when set.
	/// </summary>
	public string BaseUrl { get; set; }

	/// <summary>
	/// File naming strategy for downloads returning HttpStreamResult.
	/// PathGet uses Path.GetFileName(url) and supports resuming interrupted downloads.
	/// Random uses a random file name to avoid collisions between concurrent downloads.
	/// Specify requires a file name for each download request.
	/// </summary>
	public DownloadStrategyFileName StrategyFileName { get; set; } = DownloadStrategyFileName.PathGet;

	/// <summary>
	/// Allows only HTTPS downloads; ignored when Proxy is set.
	/// </summary>
	public bool OnlyHttps { get; set; } = true;

	/// <summary>
	/// Proxy for outgoing requests.
	/// </summary>
	public IWebProxy Proxy { get; set; } = null;

    public int DownloadTimeout { get; set; } = DownloadTimeoutDefault;

	/// <summary>
	/// Cookie container used when initializing the client.
	/// </summary>
	public CookieContainer CookieContainer { get; set; } = null;

	/// <summary>
	/// Cookie file used during initialization when CookieContainer is absent, and saved on disposal.
	/// </summary>
	public string CookiesPath { get; set; }

	/// <summary>
	/// Server certificate; ignored when SslValidation is set.
	/// </summary>
	public X509Certificate2 ServerCert { get; set; } = null;

	/// <summary>
	/// Custom server certificate validation.
	/// </summary>
	public Func<HttpRequestMessage, X509Certificate2, X509Chain, SslPolicyErrors, bool> SslValidation { get; set; } = null;

	/// <summary>
	/// Callback applied to HttpClientHandler when initializing HttpDataFactory.
	/// </summary>
	public Action<HttpClientHandler> ModifyClientHandler { get; set; } = null;

	/// <summary>
	/// Callback applied to HttpClient when initializing HttpDataFactory.
	/// </summary>
	public Action<System.Net.Http.HttpClient> ModifyClient { get; set; } = null;

	/// <summary>
	/// Callback applied to the content of each POST request.
	/// For example, use this callback to set ContentType.
	///     content => content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded")
	/// </summary>
	public Action<ByteArrayContent> ModifyContent { get; set; } = null;

	/// <summary>
	/// Handler credentials applied when initializing HttpDataFactory.
	/// </summary>
	public ICredentials Credentials { get; set; } = null;

	/// <summary>
	/// Delay before sending requests to limit traffic; can be changed after initialization.
	/// </summary>
	public int PreLoadTimeout { get; set; } = PreLoadTimeoutDefault;

	/// <summary>
	/// Attempt count before returning a download error; can be changed after initialization.
	/// </summary>
	public int RetriesCount { get; set; } = RetriesCountDefault;
}

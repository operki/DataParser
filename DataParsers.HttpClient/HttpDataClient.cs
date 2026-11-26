using System;
using System.Net;
using System.Threading.Tasks;
using DataParsers.Base.Environment;
using DataParsers.Base.Helpers;
using DataParsers.HttpClient.Results;

namespace DataParsers.HttpClient;

/// <summary>
/// Data downloader supporting GET and POST requests.
/// </summary>
public partial class HttpDataClient
{

    public HttpDataClient(DefaultEnvironment environment, HttpClientSettings settings = null)
    {
        Environment = environment;
        settings ??= new HttpClientSettings();
        onlyHttps = settings.OnlyHttps || settings.Proxy != null;
        strategyFileName = settings.StrategyFileName;
        LocalHelper.TryClearDir(TempDir, strategyFileName == DownloadStrategyFileName.Random
            ? 0
            : SkipFilesWhenClear);

        PreLoadTimeout = settings.PreLoadTimeout;
        RetriesCount = settings.RetriesCount;
        cookiesPath = settings.CookiesPath;
        httpDataFactory = new HttpDataFactory(settings);
    }

    public int PreLoadTimeout { get; set; }
    public int RetriesCount { get; set; }

    public CookieContainer CookieContainer => httpDataFactory.ClientHandler.CookieContainer;

	/// <summary>
	/// GET request.
	/// </summary>
	/// <param name="environment">Logging and metrics environment.</param>
	/// <param name="url">Request URL; may be relative to settings.BaseUrl. The host must match when a base URL is configured.</param>
	/// <param name="settings">Settings for HttpDataFactory.</param>
	/// <param name="traceId">Log prefix; assigned automatically when omitted.</param>
	/// <returns>The download result.</returns>
	public static DataResult JustGet(DefaultEnvironment environment, string url, HttpClientSettings settings = null, string traceId = null)
    {
        settings ??= new HttpClientSettings();
        return GetAsyncInternal(environment, url, traceId, new HttpDataFactory(settings), settings.OnlyHttps, settings.PreLoadTimeout, settings.RetriesCount).ConfigureAwait(false).GetAwaiter().GetResult();
    }

	/// <summary>
	/// GET request; throws on failure.
	/// </summary>
	/// <param name="environment">Logging and metrics environment.</param>
	/// <param name="url">Request URL; may be relative to settings.BaseUrl. The host must match when a base URL is configured.</param>
	/// <param name="settings">Settings for HttpDataFactory.</param>
	/// <param name="traceId">Log prefix; assigned automatically when omitted.</param>
	/// <returns>The download result.</returns>
	public static DataResult JustGetSuccess(DefaultEnvironment environment, string url, HttpClientSettings settings = null, string traceId = null)
    {
        settings ??= new HttpClientSettings();
        var result = GetAsyncInternal(environment, url, traceId, new HttpDataFactory(settings), settings.OnlyHttps, settings.PreLoadTimeout, settings.RetriesCount).ConfigureAwait(false).GetAwaiter().GetResult();
        if(!result.IsSuccess)
            throw new Exception($"{IdGenerator.GetPrefix(traceId)}Can't download data from '{url}'");

        return result;
    }

	/// <summary>
	/// POST request.
	/// </summary>
	/// <param name="environment">Logging and metrics environment.</param>
	/// <param name="url">Request URL; may be relative to settings.BaseUrl. The host must match when a base URL is configured.</param>
	/// <param name="body">Request body.</param>
	/// <param name="settings">Settings for HttpDataFactory.</param>
	/// <param name="traceId">Log prefix; assigned automatically when omitted.</param>
	/// <returns>The download result.</returns>
	public static DataResult JustPost(DefaultEnvironment environment, string url, byte[] body, HttpClientSettings settings = null, string traceId = null)
    {
        settings ??= new HttpClientSettings();
        return PostAsyncInternal(environment, url, body, traceId, new HttpDataFactory(settings), settings.OnlyHttps, settings.PreLoadTimeout, settings.RetriesCount).ConfigureAwait(false).GetAwaiter().GetResult();
    }

	/// <summary>
	/// GET request; throws on failure.
	/// </summary>
	/// <param name="url">Request URL; may be relative to settings.BaseUrl. The host must match when a base URL is configured.</param>
	/// <param name="traceId">Log prefix; assigned automatically when omitted.</param>
	/// <returns>The download result.</returns>
	public DataResult GetSuccess(string url, string traceId = null)
    {
        var result = GetAsync(url, traceId).ConfigureAwait(false).GetAwaiter().GetResult();
        if(!result.IsSuccess)
            throw new Exception($"{IdGenerator.GetPrefix(traceId)}Can't download data from '{url}'");

        return result;
    }

	/// <summary>
	/// GET request.
	/// </summary>
	/// <param name="url">Request URL; may be relative to settings.BaseUrl. The host must match when a base URL is configured.</param>
	/// <param name="traceId">Log prefix; assigned automatically when omitted.</param>
	/// <returns>The download result.</returns>
	public DataResult Get(string url, string traceId = null)
    {
        return GetAsync(url, traceId).ConfigureAwait(false).GetAwaiter().GetResult();
    }

	/// <summary>
	/// Asynchronous GET request.
	/// </summary>
	/// <param name="url">Request URL; may be relative to settings.BaseUrl. The host must match when a base URL is configured.</param>
	/// <param name="traceId">Log prefix; assigned automatically when omitted.</param>
	/// <returns>The download result.</returns>
	public async Task<DataResult> GetAsync(string url, string traceId = null)
    {
        return await GetAsyncInternal(Environment, url, traceId, httpDataFactory, onlyHttps, PreLoadTimeout, RetriesCount);
    }

	/// <summary>
	/// POST request.
	/// </summary>
	/// <param name="url">Request URL; may be relative to settings.BaseUrl. The host must match when a base URL is configured.</param>
	/// <param name="body">Request body.</param>
	/// <param name="traceId">Log prefix; assigned automatically when omitted.</param>
	/// <returns>The download result.</returns>
	public DataResult PostSuccess(string url, byte[] body, string traceId = null)
    {
        var result = PostAsync(url, body, traceId).ConfigureAwait(false).GetAwaiter().GetResult();
        if(!result.IsSuccess)
            throw new Exception($"{IdGenerator.GetPrefix(traceId)}Can't download post data from '{url}'");

        return result;
    }

	/// <summary>
	/// POST request.
	/// </summary>
	/// <param name="url">Request URL; may be relative to settings.BaseUrl. The host must match when a base URL is configured.</param>
	/// <param name="body">Request body.</param>
	/// <param name="traceId">Log prefix; assigned automatically when omitted.</param>
	/// <returns>The download result.</returns>
	public DataResult Post(string url, byte[] body, string traceId = null)
    {
        return PostAsync(url, body, traceId).ConfigureAwait(false).GetAwaiter().GetResult();
    }

	/// <summary>
	/// Asynchronous POST request.
	/// </summary>
	/// <param name="url">Request URL; may be relative to settings.BaseUrl. The host must match when a base URL is configured.</param>
	/// <param name="body">Request body.</param>
	/// <param name="traceId">Log prefix; assigned automatically when omitted.</param>
	/// <returns>The download result.</returns>
	public async Task<DataResult> PostAsync(string url, byte[] body, string traceId = null)
    {
        return await PostAsyncInternal(Environment, url, body, traceId, httpDataFactory, onlyHttps, PreLoadTimeout, RetriesCount);
    }

	/// <summary>
	/// GET request returning a stream; throws on failure.
	/// </summary>
	/// <param name="url">Request URL; may be relative to settings.BaseUrl. The host must match when a base URL is configured.</param>
	/// <param name="fileName">Local file name; download methods use the naming strategy when omitted.</param>
	/// <param name="traceId">Log prefix; assigned automatically when omitted.</param>
	/// <returns>The download result.</returns>
	public HttpStreamResult GetStreamSuccess(string url, string fileName = null, string traceId = null)
    {
        var result = GetStreamAsync(url, fileName, traceId).ConfigureAwait(false).GetAwaiter().GetResult();
        if(!result.IsSuccess)
            throw new Exception($"{IdGenerator.GetPrefix(traceId)}Can't download data from '{url}'");

        return result;
    }

	/// <summary>
	/// GET request returning a stream.
	/// </summary>
	/// <param name="url">Request URL; may be relative to settings.BaseUrl. The host must match when a base URL is configured.</param>
	/// <param name="fileName">Local file name; download methods use the naming strategy when omitted.</param>
	/// <param name="traceId">Log prefix; assigned automatically when omitted.</param>
	/// <returns>The download result.</returns>
	public HttpStreamResult GetStream(string url, string fileName = null, string traceId = null)
    {
        return GetStreamAsync(url, fileName, traceId).ConfigureAwait(false).GetAwaiter().GetResult();
    }
}

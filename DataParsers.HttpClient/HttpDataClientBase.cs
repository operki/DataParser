using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DataParsers.Base.Environment;
using DataParsers.Base.Helpers;
using DataParsers.HttpClient.Results;

namespace DataParsers.HttpClient;

public partial class HttpDataClient
{
    private const string TempDir = "tempDownloads";
    private const int SkipFilesWhenClear = 20;
    private const int RetriesStopGrowing = 8;
    private const int MaxReadLength = 1048576 * 1024;
    private readonly string cookiesPath;
    private readonly DefaultEnvironment Environment;
    private readonly HttpDataFactory httpDataFactory;
    private readonly bool onlyHttps;
    private readonly DownloadStrategyFileName strategyFileName;

    ~HttpDataClient()
    {
        LocalHelper.TryClearDir(TempDir);
        SaveCookies();
    }

    private void SaveCookies()
    {
        if(cookiesPath == null)
            return;

        using var outStream = File.Create(cookiesPath);
        JsonSerializer.Serialize(outStream, httpDataFactory.ClientHandler.CookieContainer);
    }

    private static async Task<DataResult> GetAsyncInternal(DefaultEnvironment environment, string url, string traceId, HttpDataFactory httpDataFactory, bool onlyHttps = true, int preLoadTimeout = HttpClientSettings.PreLoadTimeoutDefault, int retriesCount = HttpClientSettings.RetriesCountDefault)
    {
        var tracePrefix = IdGenerator.GetPrefixAnyway(traceId);
        var (getResult, response, elapsed) = await GetWithRetriesInternalAsync(environment, () => httpDataFactory.Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead), tracePrefix, httpDataFactory.BaseUrl, url, onlyHttps, preLoadTimeout, retriesCount, null);
        var result = new DataResult(response);
        switch(getResult)
        {
            case GetResult.Fail:
                environment.Log.Info($"{tracePrefix}Failed get '{url}'");
                break;
            case GetResult.Success:
                environment.Log.Info($"{tracePrefix}Get '{url}' ({(int)response.StatusCode} {response.StatusCode}): result length {result.Content?.Length}, elapsed {elapsed}");
                break;
            case GetResult.StopException:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        return result;
    }

    private static async Task<DataResult> PostAsyncInternal(DefaultEnvironment environment, string url, byte[] body, string traceId, HttpDataFactory httpDataFactory, bool onlyHttps = true, int preLoadTimeout = HttpClientSettings.PreLoadTimeoutDefault, int retriesCount = HttpClientSettings.RetriesCountDefault)
    {
        var tracePrefix = IdGenerator.GetPrefixAnyway(traceId);
        var (getResult, response, elapsed) = await GetWithRetriesInternalAsync(environment, () =>
        {
            var httpContent = new ByteArrayContent(body);
            httpDataFactory.ModifyContent?.Invoke(httpContent);
            return httpDataFactory.Client.PostAsync(url, httpContent);
        }, tracePrefix, httpDataFactory.BaseUrl, url, onlyHttps, preLoadTimeout, retriesCount, null);
        var result = new DataResult(response);
        switch(getResult)
        {
            case GetResult.Fail:
                environment.Log.Info($"{tracePrefix}Failed post '{url}'");
                break;
            case GetResult.Success:
                environment.Log.Info($"{tracePrefix}Post '{url}' ({(int)response.StatusCode} {response.StatusCode}): result length {result.Content?.Length}, elapsed {elapsed}");
                break;
            case GetResult.StopException:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        return result;
    }

    private async Task<HttpStreamResult> GetStreamAsync(string url, string fileName = null, string traceId = null)
    {
        return await GetStreamAsyncInternal(Environment, url, traceId, httpDataFactory, strategyFileName, fileName, httpDataFactory.BaseUrl, onlyHttps, PreLoadTimeout, RetriesCount);
    }

    private static async Task<HttpStreamResult> GetStreamAsyncInternal(DefaultEnvironment environment, string url, string traceId, HttpDataFactory httpDataFactory, DownloadStrategyFileName strategyFileName = DownloadStrategyFileName.PathGet, string fileName = null, string site = null, bool onlyHttps = true, int preLoadTimeout = HttpClientSettings.PreLoadTimeoutDefault, int retriesCount = HttpClientSettings.RetriesCountDefault)
    {
        var tracePrefix = IdGenerator.GetPrefixAnyway(traceId);
        var tmpFileName = GetFileName(strategyFileName, url, fileName);
        long totalSize = 0;

        environment.Log.Info($"{tracePrefix}Start download from '{url}'...");
        var sw = Stopwatch.StartNew();
        while(true)
        {
            var resumeDownload = File.Exists(tmpFileName);
            if(resumeDownload)
            {
                totalSize = new FileInfo(tmpFileName).Length;
                environment.Log.Info($"{tracePrefix}Already downloaded {totalSize} bytes from '{url}'. Continue download...");
            }

            var (getResult, response, _) = GetWithRetriesInternalAsync(environment, () =>
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.Range = resumeDownload
                        ? new RangeHeaderValue(new FileInfo(tmpFileName).Length, new FileInfo(tmpFileName).Length + MaxReadLength)
                        : new RangeHeaderValue(0, MaxReadLength);

                    return httpDataFactory.Client.SendAsync(request);
                }, tracePrefix, site, url, onlyHttps, preLoadTimeout, retriesCount, exception => exception.ToString().Contains("416"))
                .ConfigureAwait(false).GetAwaiter().GetResult();
            switch(getResult)
            {
                case GetResult.StopException:
                    return totalSize == 0
                        ? new HttpStreamResult(response)
                        : new HttpStreamResult(tmpFileName, response, true);
                case GetResult.Fail:
                    return new HttpStreamResult(response);
                case GetResult.Success:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            bool endDownload;
            using(var httpReadStream = response.Content.ReadAsStreamAsync())
            {
                using(var fileWriteStream = File.Open(tmpFileName, FileMode.Append))
                {
                    var data = new byte[MaxReadLength];
                    var dataLength = await httpReadStream.Result.ReadAsync(data, 0, data.Length);
                    fileWriteStream.Write(data, 0, dataLength);
                    totalSize += dataLength;
                    endDownload = dataLength < data.Length;
                }
            }

            if(!endDownload)
                continue;

            var elapsed = sw.Elapsed;
            var result = new HttpStreamResult(tmpFileName, response);
            environment.Log.Info($"{tracePrefix}Downloaded '{url}' ({(int)result.ResponseMessage.StatusCode} {result.ResponseMessage.StatusCode}): result length {result.Length}, elapsed {elapsed}, rate {(decimal)(result.Length / (elapsed.TotalSeconds + 0.0001) / 1000000)::0.00 MB/s}");
            return result;
        }
    }

    private static string GetFileName(DownloadStrategyFileName strategyFileName, string url, string fileName)
    {
        if(!string.IsNullOrWhiteSpace(fileName))
            return Path.Combine(TempDir, LocalHelper.GetSafeFileName(fileName));

        return strategyFileName switch
        {
            DownloadStrategyFileName.PathGet => Path.Combine(TempDir, LocalHelper.GetSafeFileName(url)),
            DownloadStrategyFileName.Random => Path.Combine(TempDir, Guid.NewGuid().ToString()),
            DownloadStrategyFileName.Specify => throw new Exception("FileName must be significant or use other download strategy"),
            _ => throw new ArgumentOutOfRangeException(nameof(strategyFileName), strategyFileName, null)
        };
    }

    private static async Task<(GetResult, HttpResponseMessage, TimeSpan?)> GetWithRetriesInternalAsync(DefaultEnvironment environment, Func<Task<HttpResponseMessage>> httpGetter, string tracePrefix, string baseSite, string url, bool onlyHttps, int preLoadTimeout, int retriesCount, Func<Exception, bool> stopDownload)
    {
        Directory.CreateDirectory(TempDir);
        HttpResponseMessage response = null;
        var getResult = GetResult.Fail;
        try
        {
            UrlCheckOrThrow(baseSite, url, onlyHttps);
            var sleepTime = preLoadTimeout;
            for(var i = 0; i < retriesCount; i++)
            {
                if(getResult == GetResult.StopException)
                    break;

                Exception exception = null;
                environment.MetricProvider.Add(ToLowerFirstChar(DownloadMetrics.UrlTotalRequests.ToString()), 1);
                Thread.Sleep(sleepTime);
                var sw = Stopwatch.StartNew();
                try
                {
                    response = await httpGetter.Invoke();

                    if(!response.IsSuccessStatusCode)
                    {
                        exception = new HttpRequestException($"{tracePrefix}({(int)response.StatusCode} {response.StatusCode})");
                    }
                    else
                    {
                        environment.MetricProvider.Add(ToLowerFirstChar(DownloadMetrics.UrlGoodRequests.ToString()), 1);
                        return (GetResult.Success, response, sw.Elapsed);
                    }
                }
                catch(Exception e)
                {
                    sw.Stop();
                    exception = e;
                }
                finally
                {
                    sw.Stop();
                    if(exception != null)
                    {
                        environment.MetricProvider.Add(ToLowerFirstChar(DownloadMetrics.UrlBadRequests.ToString()), 1);
                        if(stopDownload != null && stopDownload.Invoke(exception))
                        {
                            getResult = GetResult.StopException;
                            environment.Log.Info(exception, $"{tracePrefix}Stop '{url}': elapsed {sw.Elapsed}");
                        }
                        else
                        {
                            sleepTime = preLoadTimeout * ((i > RetriesStopGrowing ? RetriesStopGrowing : i) + 2);
                            environment.Log.Error(exception, $"{tracePrefix}Failed '{url}': elapsed {sw.Elapsed}, try again after {sleepTime} milliseconds");
                        }
                    }
                }
            }

            return (getResult, response, null);
        }
        catch(Exception e)
        {
            environment.Log.Fatal(e, $"{tracePrefix}Failed '{url}'");
            return (GetResult.Fail, response, null);
        }
    }

    private static void UrlCheckOrThrow(string baseSite, string url, bool onlyHttps)
    {
        var uri = HttpDataFactory.GetUri(url, out var uriKind);
        if(baseSite == null)
        {
            if(uriKind == UriKind.Relative)
                throw new Exception($"Can't get request with '{url}', need absolute path");

            if(uri.Scheme == Uri.UriSchemeHttps)
                return;

            if(onlyHttps)
                throw new Exception($"Can't get request with '{url}', only {Uri.UriSchemeHttps} allowed");
            if(uri.Scheme != Uri.UriSchemeHttp)
                throw new Exception($"Can't get request with '{url}', only {Uri.UriSchemeHttp} and {Uri.UriSchemeHttps} allowed");
        }
        else if(uriKind == UriKind.Absolute && uri.GetLeftPart(UriPartial.Authority) != baseSite)
        {
            throw new Exception($"Can't get request with '{url}', only site '{baseSite}' allowed");
        }
    }

    private static string ToLowerFirstChar(string str)
    {
        return string.IsNullOrEmpty(str)
            ? str
            : char.ToLowerInvariant(str[0]) + str.Substring(1);
    }

    private enum GetResult
    {
        Fail,
        Success,
        StopException
    }
}

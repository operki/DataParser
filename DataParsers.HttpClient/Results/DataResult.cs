using System;
using System.Net.Http;

namespace DataParsers.HttpClient.Results;

public class DataResult
{
    public DataResult(HttpResponseMessage responseMessage)
    {
        ResponseMessage = responseMessage;
    }

    public HttpResponseMessage ResponseMessage { get; }
    public bool IsSuccess => ResponseMessage?.IsSuccessStatusCode ?? false;

    public string Content
    {
        get
        {
            try
            {
                return ResponseMessage?.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            }
            catch(Exception)
            {
                return null;
            }
        }
    }

    public byte[] Data
    {
        get
        {
            try
            {
                return ResponseMessage?.Content.ReadAsByteArrayAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            }
            catch(Exception)
            {
                return null;
            }
        }
    }
}

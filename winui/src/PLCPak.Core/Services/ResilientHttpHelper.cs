using System.Net;
using System.Text.RegularExpressions;

namespace PLCPak.Core.Services;

public static class ResilientHttpHelper
{
    private static readonly Regex Http5xxPattern = new(@"\b5\d{2}\b", RegexOptions.Compiled);

    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromMilliseconds(500),
        TimeSpan.FromMilliseconds(1500),
        TimeSpan.FromMilliseconds(3500)
    ];

    public static string UserAgent => $"PLCPak/{AppVersion.Current}";

    public static void EnsureUserAgent(HttpClient http)
    {
        if (http.DefaultRequestHeaders.UserAgent.Count == 0)
            http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
    }

    public static Task<HttpResponseMessage> GetAsync(
        HttpClient http,
        string requestUri,
        CancellationToken cancellationToken = default)
        => GetAsync(http, requestUri, HttpCompletionOption.ResponseContentRead, cancellationToken);

    public static Task<HttpResponseMessage> GetAsync(
        HttpClient http,
        string requestUri,
        HttpCompletionOption completionOption,
        CancellationToken cancellationToken = default)
        => SendWithRetriesAsync(
            () => http.GetAsync(requestUri, completionOption, cancellationToken),
            cancellationToken);

    public static Task<HttpResponseMessage> PostAsync(
        HttpClient http,
        string requestUri,
        HttpContent content,
        CancellationToken cancellationToken = default)
        => SendWithRetriesAsync(
            () => http.PostAsync(requestUri, content, cancellationToken),
            cancellationToken);

    private static async Task<HttpResponseMessage> SendWithRetriesAsync(
        Func<Task<HttpResponseMessage>> send,
        CancellationToken cancellationToken)
    {
        Exception? lastException = null;

        for (var attempt = 0; attempt <= RetryDelays.Length; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var response = await send().ConfigureAwait(false);
                if (IsRetryableStatusCode(response.StatusCode))
                {
                    var statusCode = response.StatusCode;
                    response.Dispose();
                    if (attempt < RetryDelays.Length)
                    {
                        await Task.Delay(RetryDelays[attempt], cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    throw new HttpRequestException(ClassifyHttpStatus(statusCode));
                }

                return response;
            }
            catch (Exception ex) when (IsRetryableException(ex))
            {
                lastException = ex;
                if (attempt >= RetryDelays.Length)
                    throw WrapException(ex);

                await Task.Delay(RetryDelays[attempt], cancellationToken).ConfigureAwait(false);
            }
        }

        throw WrapException(lastException ?? new HttpRequestException("HTTP 请求失败"));
    }

    private static bool IsRetryableStatusCode(HttpStatusCode statusCode)
        => (int)statusCode == 429 || (int)statusCode >= 500;

    private static bool IsRetryableException(Exception ex)
    {
        if (ex is TaskCanceledException or OperationCanceledException)
            return true;

        if (ex is HttpRequestException httpEx)
        {
            if (httpEx.StatusCode is HttpStatusCode statusCode)
                return IsRetryableStatusCode(statusCode) || statusCode == HttpStatusCode.Forbidden;

            var message = httpEx.Message;
            return message.Contains("timeout", StringComparison.OrdinalIgnoreCase)
                || message.Contains("403", StringComparison.OrdinalIgnoreCase)
                || message.Contains("429", StringComparison.OrdinalIgnoreCase)
                || message.Contains("5xx", StringComparison.OrdinalIgnoreCase);
        }

        return ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase);
    }

    internal static string ClassifyHttpStatus(HttpStatusCode statusCode)
        => (int)statusCode switch
        {
            403 => "HTTP 403 Forbidden",
            429 => "HTTP 429 Too Many Requests",
            >= 500 and <= 599 => $"HTTP 5xx Server Error ({(int)statusCode})",
            _ => $"HTTP {(int)statusCode}"
        };

    private static Exception WrapException(Exception ex)
    {
        if (ex is TaskCanceledException or OperationCanceledException)
            return new HttpRequestException("HTTP timeout", ex);

        if (ex is HttpRequestException httpEx)
        {
            if (httpEx.StatusCode is HttpStatusCode statusCode)
                return new HttpRequestException(ClassifyHttpStatus(statusCode), ex, statusCode);

            if (ex.Message.Contains("403", StringComparison.OrdinalIgnoreCase))
                return new HttpRequestException("HTTP 403 Forbidden", ex);

            if (ex.Message.Contains("429", StringComparison.OrdinalIgnoreCase))
                return new HttpRequestException("HTTP 429 Too Many Requests", ex);

            if (ex.Message.Contains("5xx", StringComparison.OrdinalIgnoreCase)
                || Http5xxPattern.IsMatch(ex.Message))
                return new HttpRequestException("HTTP 5xx Server Error", ex);

            if (ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
                return new HttpRequestException("HTTP timeout", ex);
        }

        if (ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
            return new HttpRequestException("HTTP timeout", ex);

        return ex;
    }
}
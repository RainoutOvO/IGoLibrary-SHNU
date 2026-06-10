using System.Net;
using IGoLibrary.Ex.Application.Abstractions;
using IGoLibrary.Ex.Application.Protocol;
using RestSharp;

namespace IGoLibrary.Ex.Infrastructure.Api;

internal sealed class TraceIntCookieTransport(
    IProtocolTemplateStore protocolTemplateStore,
    TraceIntRequestPolicy requestPolicy,
    ITraceIntCookieHttpClient httpClient)
{
    public async Task<TraceIntCookieHttpResponse> GetCookieAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        return await GetCookieAsync(code, authorizationLink: null, cancellationToken);
    }

    public async Task<TraceIntCookieHttpResponse> GetCookieAsync(
        string code,
        string? authorizationLink,
        CancellationToken cancellationToken = default)
    {
        var templates = await protocolTemplateStore.GetEffectiveTemplatesAsync(cancellationToken);
        var requestUrl = ResolveCookieRequestUrl(code, authorizationLink, templates);

        return await requestPolicy.ExecuteAsync(async requestToken =>
        {
            TraceIntCookieHttpResponse result;
            try
            {
                result = await httpClient.ExecuteGetAsync(requestUrl, requestToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException and not HttpRequestException)
            {
                throw new HttpRequestException("获取 Cookie 请求失败，请检查网络连接或授权链接是否可访问。", ex);
            }

            if (result.Cookies?.Count >= 2)
            {
                return result;
            }

            if (IsRetriableCookieFailure(result.Response))
            {
                throw CreateRetryException(result.Response);
            }

            return result;
        }, "获取 Cookie", cancellationToken);
    }

    private static string ResolveCookieRequestUrl(
        string code,
        string? authorizationLink,
        TraceIntGraphQlTemplates templates)
    {
        var trimmedLink = authorizationLink?.Trim();
        if (IsTrustedAuthorizationLink(trimmedLink, code))
        {
            return trimmedLink!;
        }

        return templates.GetCookieUrlTemplate.Replace("ReplaceMeByCode", code, StringComparison.Ordinal);
    }

    private static bool IsTrustedAuthorizationLink(string? authorizationLink, string code)
    {
        if (string.IsNullOrWhiteSpace(authorizationLink) ||
            !Uri.TryCreate(authorizationLink, UriKind.Absolute, out var uri) ||
            (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) ||
            string.IsNullOrWhiteSpace(uri.Host))
        {
            return false;
        }

        var host = uri.Host;
        var trustedHost =
            host.EndsWith(".traceint.com", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(host, "traceint.com", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(host, "libseat.shnu.edu.cn", StringComparison.OrdinalIgnoreCase);

        return trustedHost &&
               authorizationLink.Contains($"code={code}", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRetriableCookieFailure(RestResponse response)
    {
        return response.ResponseStatus != ResponseStatus.Completed ||
               TraceIntRequestPolicy.IsTransient(ToNullableStatusCode(response.StatusCode));
    }

    private static HttpRequestException CreateRetryException(RestResponse response)
    {
        var reason = response.ErrorMessage;
        if (string.IsNullOrWhiteSpace(reason))
        {
            reason = response.StatusDescription;
        }

        if (string.IsNullOrWhiteSpace(reason) && response.ResponseStatus != ResponseStatus.Completed)
        {
            reason = response.ResponseStatus.ToString();
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            reason = "请检查授权链接是否过期或网络是否可用";
        }

        var statusCode = ToNullableStatusCode(response.StatusCode);
        return statusCode is null
            ? new HttpRequestException($"获取 Cookie 请求失败：{reason}", response.ErrorException, statusCode)
            : new HttpRequestException(
                $"获取 Cookie 请求失败，HTTP {(int)response.StatusCode} {response.StatusCode}：{reason}",
                response.ErrorException,
                statusCode);
    }

    private static HttpStatusCode? ToNullableStatusCode(HttpStatusCode statusCode)
    {
        return statusCode == 0 ? null : statusCode;
    }
}

internal sealed record TraceIntCookieHttpResponse(
    RestResponse Response,
    IReadOnlyList<string>? Cookies);

internal interface ITraceIntCookieHttpClient
{
    Task<TraceIntCookieHttpResponse> ExecuteGetAsync(string requestUrl, CancellationToken cancellationToken);
}

internal sealed class RestSharpTraceIntCookieHttpClient : ITraceIntCookieHttpClient
{
    public async Task<TraceIntCookieHttpResponse> ExecuteGetAsync(
        string requestUrl,
        CancellationToken cancellationToken)
    {
        using var client = new RestClient(requestUrl);
        var request = new RestRequest
        {
            Method = Method.Get
        };

        var response = await client.ExecuteAsync(request, cancellationToken);
        var responseCookies = response.Cookies?.Select(cookie => cookie.ToString()).ToArray();
        return new TraceIntCookieHttpResponse(response, responseCookies);
    }
}

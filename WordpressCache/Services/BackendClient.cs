using System.Net.Http.Headers;
using Microsoft.Net.Http.Headers;

namespace WordpressCache.Services;

public sealed class BackendClient {
    private readonly string _host;
    private readonly Uri _baseAddress;
    private readonly IHttpClientFactory _httpClientFactory;

    public BackendClient(IConfiguration config, IHttpClientFactory httpClientFactory) {
        _httpClientFactory = httpClientFactory;
        _baseAddress = new Uri(config["BackendAddress"]);
        _host = _baseAddress.Host;
    }

    public async Task<HttpResponseMessage> GetAsync(string path, CancellationToken cancellationToken = default) {
        var client = _httpClientFactory.CreateClient("WP");
        var uri = MakeUri(_baseAddress, path);
        using var message = new HttpRequestMessage(HttpMethod.Get, uri) {
            Headers = {
                {HeaderNames.Host, _host},
                {HeaderNames.CacheControl, "no-cache"}
            }
        };

        return await client.SendAsync(message, cancellationToken);
    }

    public async Task ForwardRequestAsync(HttpContext context) {
        var client = _httpClientFactory.CreateClient("WP");
        var request = context.Request;
        // CopyRequestHeader(request, client);
        var uri = new UriBuilder(_baseAddress) {
            Path = request.Path,
            Query = request.QueryString.ToUriComponent()
        };

        using var message = new HttpRequestMessage(GetMethod(request), uri.Uri);

        CopyRequestHeaders(context.Request, message.Headers);
        message.Content = new StreamContent(context.Request.Body);
        var responseMessage = await client.SendAsync(message);

        var response = context.Response;
        var contentLength = responseMessage.Content.Headers.ContentLength ?? 0;
        CopyResponseHeaders(responseMessage, context.Response);
        response.StatusCode = (int) responseMessage.StatusCode;
        if (contentLength > 0) {
            await responseMessage.Content.CopyToAsync(response.Body);
        }
    }

    private static void CopyRequestHeaders(HttpRequest request, HttpHeaders headers) {
        foreach (var item in request.Headers) {
            headers.Add(item.Key, string.Join("; ", item.Value));
        }
    }

    private static void CopyResponseHeaders(HttpResponseMessage message, HttpResponse response) {
        // foreach ((string? key, var value) in message.Headers) {
        //     response.Headers[key] = string.Join("; ", value);
        // }

        foreach ((string? key, var value) in message.Content.Headers) {
            response.Headers[key] = string.Join("; ", value);
        }
    }

    private static HttpMethod GetMethod(HttpRequest request) {
        var method = request.Method;
        return method switch {
            "GET" => HttpMethod.Get,
            "POST" => HttpMethod.Post,
            "PUT" => HttpMethod.Put,
            "DELETE" => HttpMethod.Delete,
            _ => throw new NotSupportedException()
        };
    }

    private static Uri MakeUri(Uri baseAddress, string path) {
        var q = path.IndexOf('?');
        if (q == -1) {
            var uri = new UriBuilder(baseAddress) {
                Path = path
            };
            return uri.Uri;
        } else {
            var uri = new UriBuilder(baseAddress) {
                Path = path[..q],
                Query = path[q..]
            };
            return uri.Uri;
        }
    }
}

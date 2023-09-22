using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using WordpressCache.Config;
using WordpressCache.Extensions;
using WordpressCache.Models;
using WordpressCache.Services;

namespace WordpressCache;

public sealed class ProxyMiddleware {
    // private readonly GlobalOptions _options;
    private static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    public ProxyMiddleware(RequestDelegate next) { }

    public async Task InvokeAsync(HttpContext context) {
        var services = context.RequestServices.GetRequiredService<ServiceContainer>();
        // var httpClient = services.HttpClient;
        var logger = services.Logger;
        var sessionId = context.Connection.Id;

        var method = context.Request.Method;
        if (logger.IsInformation()) {
            logger.LogInformation(
                "{Session} ({Remote}): {Method} {Path}{QueryString}",
                sessionId,
                context.Connection.RemoteIpAddress,
                method,
                context.Request.Path,
                context.Request.QueryString
            );
        }

        var headers = context.Request.Headers;
        var hasCookie = headers.Cookie.Count > 0;
        if (!HttpMethods.IsGet(method) || hasCookie) {
            // logger.LogInformation("{SessionId} Method is {Method}", sessionId, method);
            // var requestMessage = new HttpRequestMessage(method, path);
            // context.Response.StatusCode = (int) HttpStatusCode.MethodNotAllowed;
            // await context.Response.BodyWriter.WriteAsync(Array.Empty<byte>());
            var client = services.HttpClient;
            await ForwardRequestAsync(context, client);
            return;
        }

        var path = context.Request.Path + context.Request.QueryString;
        var cache = services.Cache;
        var saved = cache.GetValue(path);
        var serverStatus = services.ServerStatus; // context.RequestServices.GetRequiredService<ServerStatus>();

        var disableCache = hasCookie || (headers.CacheControl.Count > 0 && headers.CacheControl.Contains("no-cache"));

        if (!disableCache 
            && saved?.Content != null 
            && (saved.Expire >= Now || serverStatus.IsError)
           ) {
            // if (saved.Expire >= Now) {
            await Serve(context, saved);
            if (logger.IsInformation()) {
                logger.LogInformation("{SessionId}: Use cached response", sessionId);
            }

            return;
            // }
            //
            // if (serverStatus.IsError) {
            //     await Serve(context, saved);
            //     return;
            // }
        }

        try {
            // var options = services
            var client = services.HttpClient;
            // CopyRequestHeader(context.Request, client);
            HttpResponseMessage response;
            if (hasCookie) {
                var url = new UriBuilder(client.BaseAddress!) {
                    Path = context.Request.Path,
                    Query = context.Request.QueryString.ToUriComponent()
                };
                var msg = new HttpRequestMessage(GetMethod(context.Request), url.Uri);
                msg.Headers.Add("Cache-Control", string.Join("; ", headers.CacheControl));
                msg.Headers.Add("Cookie", string.Join("; ", headers.Cookie));
                response = await client.SendAsync(msg);
            } else {
                response = await client.GetAsync(path);
            }

            // var response = await client.GetAsync(path);
            if ((int) response.StatusCode >= 500) {
                throw new HttpRequestException($"{sessionId}: Server error - {response.StatusCode}");
            }

            if (logger.IsInformation()) {
                logger.LogInformation("{SessionId}: Status code {StatusCode}", sessionId, (int) response.StatusCode);
            }

            if (disableCache) {
                logger.LogInformation("{SessionId} Cache disabled", sessionId);
                await ServeNoCaching(context, response);
                return;
            }

            var body = await Serve(context, response);

            if (response.IsSuccessStatusCode) {
                // if (context.Request.Query.Count == 0) {
                cache.Save(path, response, body);
                // }

                if (logger.IsInformation()) {
                    logger.LogInformation("{SessionId}: Save response to cache: {Method} {Path}", sessionId, method, path);
                }
                // } else if (saved != null && (int) response.StatusCode >= 500) {
                //     logger.LogWarning(
                //         "Error from backend server: {StatusCode} {Method} {Path}, serving cached response",
                //         response.StatusCode, method, path
                //     );
                // await Serve(context, saved);
            } else {
                logger.LogError("{SessionId} Error {StatusCode} - {Method} {Path}", sessionId, response.StatusCode, method, path);
                // await UnderMaintenance(context);
            }
        } catch (HttpRequestException e) {
            serverStatus.MaskAsError();
            if (saved != null) {
                logger.LogWarning("{SessionId}: Failed to contact backend server: {Method} {Path}, serving cached response", sessionId, method, path);
                await Serve(context, saved);
            } else {
                logger.LogError(e, "{SessionId}: Failed to contact backend server: {Method} {Path}, no cached response", sessionId, method, path);
                await UnderMaintenance(context);
            }
        } catch (NotSupportedException) {
            context.Response.StatusCode = (int) HttpStatusCode.BadRequest;
        }
    }

    private static async Task ForwardRequestAsync(HttpContext context, HttpClient client) {
        var request = context.Request;
        CopyRequestHeader(request, client);
        var uri = new UriBuilder(request.Path) {
            Query = request.QueryString.ToUriComponent()
        };
        using var message = new HttpRequestMessage(GetMethod(request), uri.Uri);
        var responseMessage = await client.SendAsync(message);

        var response = context.Response;
        CopyResponseHeaders(responseMessage, context.Response);
        response.StatusCode = (int) responseMessage.StatusCode;
        await responseMessage.Content.CopyToAsync(response.Body);
    }

    private static void SetRequestHeaders(HttpRequestHeaders targetHeaders, IHeaderDictionary contextHeaders) {
        targetHeaders.UserAgent.ParseAdd(contextHeaders.UserAgent);
        targetHeaders.Connection.ParseAdd(contextHeaders.Connection);
        // targetHeaders.Date = DateTimeOffset.Parse(contextHeaders.Date);
        targetHeaders.CacheControl = CacheControlHeaderValue.Parse(contextHeaders.CacheControl);
    }

    private static void CopyResponseHeaders(HttpResponseMessage message, HttpResponse response) {
        // foreach ((string? key, var value) in message.Headers) {
        //     response.Headers[key] = string.Join("; ", value);
        // }

        foreach ((string? key, var value) in message.Content.Headers) {
            response.Headers[key] = string.Join("; ", value);
        }
    }

    private static HttpMethod GetMethod(HttpContext context) => GetMethod(context.Request);

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

    private static async Task UnderMaintenance(HttpContext context) {
        var response = context.Response;
        response.Headers.RetryAfter = "3600";
        response.StatusCode = (int) HttpStatusCode.ServiceUnavailable;
        await response.WriteAsync("Under Maintenance");
    }

    private static async Task<byte[]> Serve(HttpContext context, HttpResponseMessage message) {
        var options = context.RequestServices.GetRequiredService<IOptions<GlobalOptions>>().Value;
        CopyResponseHeaders(message, context.Response);
        if (message.Content.Headers.ContentLength <= options.MaxSize) {
            var body = await message.Content.ReadAsByteArrayAsync();
            await context.Response.BodyWriter.WriteAsync(body);
            return body;
        }

        context.Response.StatusCode = (int) message.StatusCode;

        await message.Content.CopyToAsync(context.Response.Body);
        return Array.Empty<byte>();
        // await using var writer = new StreamWriter(context.Response.Body);
    }

    private static void CopyRequestHeader(HttpRequest request, HttpClient client) {
        foreach (var item in request.Headers) {
            client.DefaultRequestHeaders.Add(item.Key, string.Join("; ", item.Value));
        }
    }

    private static async Task ServeNoCaching(HttpContext context, HttpResponseMessage message) {
        CopyResponseHeaders(message, context.Response);
        context.Response.StatusCode = (int) message.StatusCode;
        await message.Content.CopyToAsync(context.Response.Body);
        // await using var writer = new StreamWriter(context.Response.Body);
    }

    private static async Task Serve(HttpContext context, CachedContent content) {
        foreach ((string? key, string? value) in content.Headers) {
            context.Response.Headers[key] = value;
        }

        await context.Response.BodyWriter.WriteAsync(content.Content);
    }
}

using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using WordpressCache.Config;
using WordpressCache.Extensions;
using WordpressCache.Models;

namespace WordpressCache;

public sealed class ProxyMiddleware {
    // private readonly GlobalOptions _options;
    private static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    public ProxyMiddleware(RequestDelegate next) { }

    public async Task InvokeAsync(HttpContext context) {
        var path = context.Request.Path;

        var services = context.RequestServices.GetRequiredService<ServiceContainer>();
        var httpClient = services.HttpClient;
        var cache = services.Cache;
        var logger = services.Logger;

        var method = GetMethod(context);
        if (logger.IsInformation()) logger.LogInformation("{Method} {Path}", method, path);

        if (method == HttpMethod.Get) {
            var saved = cache.GetValue(path);
            if (saved is not null && saved.Expire >= Now) {
                await Serve(context, saved);
                if (logger.IsInformation()) logger.LogInformation("Use cached response");
                return;
            }

            try {
                var response = await httpClient.GetAsync(path);
                var body = await Serve(context, response);
                if (response.IsSuccessStatusCode) {
                    if (context.Request.Query.Count == 0) {
                        cache.SaveAsync(path, response, body);
                    }

                    if (logger.IsInformation()) {
                        logger.LogInformation("Save response to cache: {Method} {Path}", method, path);
                    }
                } else if (saved != null && (int) response.StatusCode >= 500) {
                    logger.LogWarning(
                        "Error from backend server: {StatusCode} {Method} {Path}, serving cached response",
                        response.StatusCode, method, path
                    );
                    await Serve(context, saved);
                } else {
                    logger.LogError("Error {StatusCode} - {Method} {Path}", response.StatusCode, method, path);
                }
            } catch (HttpRequestException e) {
                if (saved != null) {
                    logger.LogWarning("Failed to contact backend server: {Method} {Path}, serving cached response", method, path);
                    await Serve(context, saved);
                } else {
                    logger.LogError(e, "Failed to contact backend server: {Method} {Path}, no cached response", method, path);
                }
            }
        } else {
            // var requestMessage = new HttpRequestMessage(method, path);
            context.Response.StatusCode = (int) HttpStatusCode.MethodNotAllowed;
            await context.Response.BodyWriter.WriteAsync(Array.Empty<byte>());

            // SetRequestHeaders(requestMessage.Headers, context.Request.Headers);
            //
            // var response = await httpClient.SendAsync(requestMessage);
            // await Serve(context, response);
        }
    }

    private static void SetRequestHeaders(HttpRequestHeaders targetHeaders, IHeaderDictionary contextHeaders) {
        targetHeaders.UserAgent.ParseAdd(contextHeaders.UserAgent);
        targetHeaders.Connection.ParseAdd(contextHeaders.Connection);
        // targetHeaders.Date = DateTimeOffset.Parse(contextHeaders.Date);
        targetHeaders.CacheControl = CacheControlHeaderValue.Parse(contextHeaders.CacheControl);
    }

    private static void MapHeaders(HttpResponseMessage message, HttpResponse response) {
        // foreach ((string? key, var value) in message.Headers) {
        //     response.Headers[key] = string.Join("; ", value);
        // }

        foreach ((string? key, var value) in message.Content.Headers) {
            response.Headers[key] = string.Join("; ", value);
        }
    }

    private static HttpMethod GetMethod(HttpContext context) {
        var method = context.Request.Method;
        return method switch {
            "GET" => HttpMethod.Get,
            "POST" => HttpMethod.Post,
            "PUT" => HttpMethod.Put,
            "DELETE" => HttpMethod.Delete,
            _ => throw new NotSupportedException()
        };
    }

    private static async Task<byte[]> Serve(HttpContext context, HttpResponseMessage message) {
        var options = context.RequestServices.GetRequiredService<IOptions<GlobalOptions>>().Value;
        MapHeaders(message, context.Response);
        if (message.Content.Headers.ContentLength < options.MaxSize) {
            var body = await message.Content.ReadAsByteArrayAsync();
            await context.Response.BodyWriter.WriteAsync(body);
            return body;
        }

        await message.Content.CopyToAsync(context.Response.Body);
        return Array.Empty<byte>();
        // await using var writer = new StreamWriter(context.Response.Body);
    }

    private static async Task Serve(HttpContext context, CachedContent content) {
        foreach ((string? key, string? value) in content.Headers) {
            context.Response.Headers[key] = value;
        }

        await context.Response.BodyWriter.WriteAsync(content.Content);
    }
}

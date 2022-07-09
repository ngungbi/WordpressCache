using System.Net.Http.Headers;
using System.Text.Json;
using WordpressCache.Extensions;
using WordpressCache.Models;

namespace WordpressCache;

public class ProxyMiddleware {
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
            if (saved is not null) {
                await Serve(context, saved);
                if (logger.IsInformation()) logger.LogInformation("Use cached response");
                return;
            }

            var response = await httpClient.GetAsync(path);
            await Serve(context, response);
            if (response.IsSuccessStatusCode) {
                await cache.SaveAsync(path, response);
                if (logger.IsInformation()) logger.LogInformation("Save response to cache");
            } else {
                logger.LogError("Error {StatusCode} - {Method} {Path} ", response.StatusCode, method, path);
            }
        } else {
            var requestMessage = new HttpRequestMessage(method, path);

            SetRequestHeaders(requestMessage.Headers, context.Request.Headers);

            var response = await httpClient.SendAsync(requestMessage);
            await Serve(context, response);
        }
    }

    private static void SetRequestHeaders(HttpRequestHeaders targetHeaders, IHeaderDictionary contextHeaders) {
        targetHeaders.UserAgent.ParseAdd(contextHeaders.UserAgent);
        targetHeaders.Connection.ParseAdd(contextHeaders.Connection);
        // targetHeaders.Date = DateTimeOffset.Parse(contextHeaders.Date);
        targetHeaders.CacheControl = CacheControlHeaderValue.Parse(contextHeaders.CacheControl);
    }

    private static void MapHeaders(HttpResponseMessage message, HttpResponse response) {
        foreach ((string? key, var value) in message.Headers) {
            response.Headers[key] = string.Join("; ", value);
        }

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

    private static async Task Serve(HttpContext context, HttpResponseMessage message) {
        MapHeaders(message, context.Response);
        var body = await message.Content.ReadAsByteArrayAsync();
        await context.Response.BodyWriter.WriteAsync(body);
    }

    private static async Task Serve(HttpContext context, CachedMessage message) {
        foreach ((string? key, string? value) in message.Headers) {
            context.Response.Headers[key] = value;
        }

        await context.Response.BodyWriter.WriteAsync(message.Content);
    }
}

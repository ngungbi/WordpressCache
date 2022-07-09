using System.Net.Http.Headers;
using System.Text.Json;
using WordpressCache.Models;

namespace WordpressCache;

public class Proxy {
    public Proxy(RequestDelegate next) { }

    public async Task InvokeAsync(HttpContext context) {
        var path = context.Request.Path;

        var services = context.RequestServices.GetRequiredService<ServiceContainer>();
        var httpClient = services.HttpClient;
        var cache = services.Cache;

        var method = GetMethod(context);

        HttpResponseMessage responseMessage;
        if (method == HttpMethod.Get) {
            var saved = cache.GetValue(path);
            if (saved is not null) {
                await Serve(context, saved);
                return;
            }

            responseMessage = await httpClient.GetAsync(path);
        } else {
            var requestMessage = new HttpRequestMessage(method, path);
            requestMessage.Content = new StreamContent(context.Request.Body);
            responseMessage = await httpClient.SendAsync(requestMessage);
        }

        MapHeaders(responseMessage, context.Response);

        var body = await responseMessage.Content.ReadAsByteArrayAsync();
        await Serve(context, body);
        await cache.SetValueAsync(path, responseMessage);
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
        return context.Request.Method switch {
            "GET" => HttpMethod.Get,
            "POST" => HttpMethod.Post,
            "PUT" => HttpMethod.Put,
            "DELETE" => HttpMethod.Delete,
            _ => throw new NotSupportedException()
        };
    }

    private static async Task Serve(HttpContext context, ReadOnlyMemory<byte> body) {
        await context.Response.BodyWriter.WriteAsync(body);
    }

    private static async Task Serve(HttpContext context, CachedMessage message) {
        foreach ((string? key, string? value) in message.Headers) {
            context.Response.Headers[key] = value;
        }

        await context.Response.BodyWriter.WriteAsync(message.Content);
    }
}

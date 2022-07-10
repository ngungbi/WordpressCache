using System.Net;
using WordpressCache.Extensions;
using WordpressCache.Models;
using WordpressCache.Services;

namespace WordpressCache;

public class ProxyMiddleware {
    public ProxyMiddleware(RequestDelegate next) { }

    public async Task InvokeAsync(HttpContext context) {
        var path = context.Request.Path;

        var services = context.RequestServices.GetRequiredService<ServiceContainer>();
        var httpClient = services.HttpClient;
        var cache = services.Cache;
        var logger = services.Logger;

        var method = context.GetHttpMethod();
        if (logger.IsInformation()) logger.LogInformation("{Method} {Path}", method, path);

        if (method == HttpMethod.Get) {
            var saved = cache.GetValue(path);
            if (saved is not null) {
                await Serve(context, saved);
                if (logger.IsInformation()) logger.LogInformation("Use cached response");
                return;
            }

            var response = await httpClient.GetAsync(path);
            await context.Response.WriteAsync(response);
            if (response.IsSuccessStatusCode) {
                await cache.SaveAsync(path, response);
                if (logger.IsInformation()) logger.LogInformation("Save response to cache");
            } else {
                logger.LogError("Error {StatusCode} - {Method} {Path} ", response.StatusCode, method, path);
            }
        } else {
            // other method will be handled by nginx
            context.Response.StatusCode = (int) HttpStatusCode.MethodNotAllowed;
            await context.Response.BodyWriter.WriteAsync(Array.Empty<byte>());
            
        }
    }

    private static async Task Serve(HttpContext context, CachedMessage message) {
        foreach ((string? key, string? value) in message.Headers) {
            context.Response.Headers[key] = value;
        }

        await context.Response.BodyWriter.WriteAsync(message.Content);
    }
}

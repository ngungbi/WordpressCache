using Microsoft.Extensions.Options;
using WordpressCache.Config;

namespace WordpressCache;

public sealed class LoggerMiddleware {
    private readonly RequestDelegate _next;

    public LoggerMiddleware(RequestDelegate next) {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context) {
        await _next(context);

        if (context.Request.ContentType != "text/html") {
            return;
        }

        var options = context.RequestServices.GetRequiredService<IOptions<GlobalOptions>>().Value;
        if (options.DisableLogging) {
            return;
        }

        var ip = context.Connection.RemoteIpAddress;
        var userAgent = context.Request.Headers.UserAgent;
        var url = context.Request.Path;

        var record = $"{DateTimeOffset.Now:HH:mm:ss zz} - {url} \t{userAgent} {Environment.NewLine}";
        var path = Path.Combine(options.CacheDir, "logs", DateTime.Now.ToString("yyyyMMdd"), $"{ip}.txt");
        await File.AppendAllTextAsync(path, record);

        // await _next(context);
    }
}

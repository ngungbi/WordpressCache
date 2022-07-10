namespace WordpressCache.Extensions;

public static class HttpContextExtension {
    public static HttpMethod GetHttpMethod(this HttpContext context) {
        var method = context.Request.Method;
        return method switch {
            "GET" => HttpMethod.Get,
            "POST" => HttpMethod.Post,
            "PUT" => HttpMethod.Put,
            "DELETE" => HttpMethod.Delete,
            _ => throw new NotSupportedException()
        };
    }
    
   
    public static async Task WriteAsync(this HttpResponse response, HttpResponseMessage message) {
        foreach ((string? key, var value) in message.Content.Headers) {
            response.Headers[key] = string.Join("; ", value);
        }
        var body = await message.Content.ReadAsByteArrayAsync();
        await response.BodyWriter.WriteAsync(body);
    }
}

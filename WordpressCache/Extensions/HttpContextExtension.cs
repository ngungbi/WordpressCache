namespace WordpressCache.Extensions;

public static class HttpContextExtension {
    public static HttpMethod GetHttpMethod(this HttpContext context) {
        var method = context.Request.Method;
        var length = method.Length;
        switch (length) {
            case 3:
                if (method == "GET") return HttpMethod.Get;
                if (method == "PUT") return HttpMethod.Put;
                break;
            case 4:
                if (method == "POST") return HttpMethod.Post;
                break;
            case 6:
                if (HttpMethods.IsDelete(method)) return HttpMethod.Delete;
                break;
        }

        throw new NotSupportedException();

        // return method switch {
        //     "GET" => HttpMethod.Get,
        //     "POST" => HttpMethod.Post,
        //     "PUT" => HttpMethod.Put,
        //     "DELETE" => HttpMethod.Delete,
        //     _ => throw new NotSupportedException()
        // };
    }

    public static string GetClientIP(this HttpContext context) => GetClientIP(context.Request);

    public static string GetClientIP(this HttpRequest request) {
        var header = request.Headers["x-forwarded-for"];
        return string.Join(',', header);
    }

    public static async Task WriteAsync(this HttpResponse response, HttpResponseMessage message) {
        foreach ((string? key, var value) in message.Content.Headers) {
            response.Headers[key] = string.Join("; ", value);
        }

        await using var stream = await message.Content.ReadAsStreamAsync();
        await stream.CopyToAsync(response.Body);
        // var body = await message.Content.ReadAsByteArrayAsync();
        // await response.BodyWriter.WriteAsync(body);
    }
}

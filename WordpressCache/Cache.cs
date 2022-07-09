using System.Net.Http.Headers;
using StackExchange.Redis;
using WordpressCache.Extensions;
using WordpressCache.Models;

namespace WordpressCache;

public interface ICache {
    CachedMessage? GetValue(string path);
    Task SaveAsync(string path, HttpResponseMessage message);
}

public class Cache : ICache {
    private readonly IDatabase _contentDb;
    private readonly IDatabase _headerDb;

    private static readonly TimeSpan DefaultExpiry = TimeSpan.FromHours(1);
    private static readonly TimeSpan ContentExpiry = DefaultExpiry * 1.5;

    public Cache(IConnectionMultiplexer mux) {
        _headerDb = mux.GetDatabase(0);
        _contentDb = mux.GetDatabase(1);
    }

    public CachedMessage? GetValue(string path) {
        var headers = GetHeaders(path);
        if (headers is null) return null;
        var content = GetContent(path);
        var result = new CachedMessage {
            Headers = headers,
            Content = content
        };
        return result;
    }

    public async Task SaveAsync(string path, HttpResponseMessage message) {
        var headers = AppendHeaders(message.Headers, message.Content.Headers);
        var content = await message.Content.ReadAsByteArrayAsync();
        var value = new CachedMessage {
            Headers = headers,
            Content = content
        };
        SetValue(path, value);
    }

    private void SetValue(string path, CachedMessage value) {
        _headerDb.StringSet(path, value.Headers.ToJsonString(), DefaultExpiry, When.Always, CommandFlags.FireAndForget);
        _contentDb.StringSet(path, value.Content, ContentExpiry, When.Always, CommandFlags.FireAndForget);
    }

    private byte[]? GetContent(string path) {
        return _contentDb.StringGet(path);
    }

    private IDictionary<string, string>? GetHeaders(string path) {
        var headerValues = _headerDb.StringGet(path);
        if (!headerValues.HasValue) return null;
        var results = headerValues.ToObject<Dictionary<string, string>>();
        return results;
    }

    private static IDictionary<string, string> AppendHeaders(HttpResponseHeaders responseHeaders, HttpContentHeaders contentHeaders) {
        var headers = contentHeaders.ToDictionary(item => item.Key, item => string.Join(';', item.Value));

        foreach ((string? key, var value) in responseHeaders) {
            headers.TryAdd(key, string.Join("; ", value));
        }

        return headers;
    }
}

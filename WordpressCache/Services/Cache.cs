using System.Diagnostics;
using StackExchange.Redis;
using WordpressCache.Extensions;
using WordpressCache.Models;

namespace WordpressCache.Services;

public interface ICache {
    CachedContent? GetValue(string path);
    void SaveAsync(string path, HttpResponseMessage message, byte[] content);
}

public class Cache : ICache {
    private readonly IDatabase _contentDb;
    private readonly IDatabase _headerDb;

    private readonly TimeSpan _defaultExpiry;
    private readonly TimeSpan _contentExpiry;

    public Cache(IConnectionMultiplexer mux, GlobalConfig config) {
        _defaultExpiry = config.DefaultExpiry;
        _contentExpiry = config.ContentExpiry;
        Debug.Assert(_contentExpiry > _defaultExpiry);
        _headerDb = mux.GetDatabase(0);
        _contentDb = mux.GetDatabase(1);
    }

    public CachedContent? GetValue(string path) {
        var headers = GetHeaders(path);
        if (headers is null) return null;
        var content = GetContent(path);
        var result = new CachedContent(headers) {
            Content = content
        };
        return result;
    }

    public void SaveAsync(string path, HttpResponseMessage message, byte[] content) {
        var headers = message.Content.Headers.ToDictionary(x => x.Key, x => string.Join("; ", x.Value));
        // var content = await message.Content.ReadAsByteArrayAsync();
        var value = new CachedContent(headers) {
            Content = content
        };
        SetValue(path, value);
        // return Task.CompletedTask;
    }

    private void SetValue(string path, CachedContent value) {
        _headerDb.StringSet(path, value.Headers.ToJsonString(), _defaultExpiry, When.Always, CommandFlags.FireAndForget);
        _contentDb.StringSet(path, value.Content, _contentExpiry, When.Always, CommandFlags.FireAndForget);
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
    
}

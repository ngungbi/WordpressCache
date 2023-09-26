using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Options;
using WordpressCache.Config;
using WordpressCache.Models;

namespace WordpressCache.Services;

public sealed class MemoryCache : ICache {
    private readonly GlobalOptions _options;
    private readonly Dictionary<string, CachedContent> _cache = new();

    private static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    public IDictionary<string, CachedContent> Values => _cache;

    public MemoryCache(IOptions<GlobalOptions> options) {
        _options = options.Value;
    }

    public CachedContent? GetValue(string path) {
        ref var content = ref CollectionsMarshal.GetValueRefOrNullRef(_cache, path);
        return Unsafe.IsNullRef(ref content) ? null : content;

        // return _cache.TryGetValue(path, out var content) ? content : null;
    }

    public void Save(string path, HttpResponseMessage message, byte[]? content) {
        var statusCode = (int) message.StatusCode;
        if (statusCode >= 300 && statusCode < 400) {
            return;
        }

        var headers = message.Content.Headers.ToDictionary(x => x.Key, x => string.Join("; ", x.Value));
        var contentLength = message.Content.Headers.ContentLength ?? 0L;

        // var content = contentLength < _options.MaxSize ? await message.Content.ReadAsByteArrayAsync() : null;

        var value = new CachedContent(headers) {
            Content = contentLength < _options.MaxSize ? content : null,
            ContentLength = contentLength,
            StatusCode = (int) message.StatusCode,
            Expire = Now + _options.CacheTtl
        };

        SaveValue(path, value);
    }

    public void SaveValue(string path, CachedContent content) {
        ref var item = ref CollectionsMarshal.GetValueRefOrAddDefault(_cache, path, out _);
        item = content;
        // if (Unsafe.IsNullRef(ref item)) {
        // } else {
        //     
        // }
    }
}

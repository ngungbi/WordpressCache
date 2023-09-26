using System.Text.Json;
using Microsoft.Extensions.Options;
using WordpressCache.Config;
using WordpressCache.Models;

namespace WordpressCache.Services;

public interface IPreloader {
    Task LoadAsync();
    Task SaveAsync();
    Task UpdateAllAsync(CancellationToken cancellationToken = default);
}

public sealed class Preloader : IPreloader {
    private readonly ICache _cache;
    private readonly IDictionary<string, CachedContent> _dictionary;
    private readonly GlobalOptions _options;

    private readonly ILogger<Preloader> _logger;

    // private readonly IHttpClientFactory _httpClientFactory;
    private readonly BackendClient _client;

    public Preloader(ICache cache, IOptions<GlobalOptions> options, ILogger<Preloader> logger, BackendClient client) {
        _cache = cache;
        _logger = logger;
        // _httpClientFactory = httpClientFactory;
        _client = client;
        _options = options.Value;
        var memoryCache = (MemoryCache) cache;
        _dictionary = memoryCache.Values;
        _logger.LogInformation("Cache Directory: {Dir}", _options.CacheDir);
    }

    public async Task LoadAsync() {
        var filePath = Path.Combine(_options.CacheDir, "index.conf");
        if (!File.Exists(filePath)) {
            File.Create(filePath);
        }

        // await using var file = File.Open(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
        var lines = await File.ReadAllLinesAsync(filePath);
        foreach (string line in lines) {
            if (string.IsNullOrEmpty(line)) {
                continue;
            }

            var questionMark = line.IndexOf('?');
            var hasQueryString = questionMark >= 0;
            var path = hasQueryString ? line[..questionMark] : line;
            var headerPath = GetMetadataPath(path);
            if (!File.Exists(headerPath)) {
                _dictionary.TryAdd(line, new CachedContent());
                continue;
            }

            await using var stream = File.Open(headerPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var cached = await JsonSerializer.DeserializeAsync<CachedContent>(stream);
            if (cached is null) {
                _dictionary.TryAdd(line, new CachedContent());
                continue;
            }

            var contentPath = GetContentPath(path);
            if (File.Exists(contentPath)) {
                var content = await File.ReadAllBytesAsync(contentPath);
                cached.Content = content;
                if (hasQueryString) {
                    cached.Expire = 0;
                }
            } else {
                cached.Expire = 0;
            }

            _dictionary.TryAdd(line, cached);
        }
    }

    private string GetMetadataPath(string path) => Path.Combine(_options.CacheDir, path + ".metadata.json");
    private string GetContentPath(string path) => Path.Combine(_options.CacheDir, path + ".content");

    public async Task SaveAsync() {
        var filePath = Path.Combine(_options.CacheDir, "index.conf");
        await File.WriteAllLinesAsync(filePath, _dictionary.Keys.OrderBy(x => x));
        foreach ((string? key, var value) in _dictionary) {
            if (key.Length == 0 || key[0] != '/') {
                continue;
            }

            var questionMark = key.IndexOf('?');
            var hasQueryString = questionMark >= 0;
            var path = hasQueryString ? key[1..questionMark] : key[1..];
            var headerPath = GetMetadataPath(path);
            var contentPath = GetContentPath(path);

            var dir = Path.GetDirectoryName(headerPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) {
                Directory.CreateDirectory(dir);
            }

            await using var fileStream = File.Open(headerPath, FileMode.Create, FileAccess.Write, FileShare.Read);
            _logger.LogInformation("Saving metadata to {File}", headerPath);
            await JsonSerializer.SerializeAsync(fileStream, value);
            await File.WriteAllBytesAsync(contentPath, value.Content ?? Array.Empty<byte>());
        }
    }

    public async Task UpdateAllAsync(CancellationToken cancellationToken = default) {
        // var client = _httpClientFactory.CreateClient("WP");
        var list = _dictionary.Keys.ToList();
        foreach (string item in list) {
            try {
                _logger.LogInformation("Updating {Path}", item);
                var responseMessage = await _client.GetAsync(item, cancellationToken);
                if (!responseMessage.IsSuccessStatusCode) {
                    _logger.LogWarning("Error response {StatusCode}", responseMessage.StatusCode);
                    continue;
                }

                var content = await responseMessage.Content.ReadAsByteArrayAsync(cancellationToken);
                if (content.Length > _options.MaxSize) {
                    continue;
                }

                _cache.Save(item, responseMessage, content);
                _logger.LogInformation("Saved {Path}", item);
            } catch (HttpRequestException e) {
                _logger.LogError(e, "Failed to update");
            }
        }
    }
}

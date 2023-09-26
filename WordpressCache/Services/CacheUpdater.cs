using Microsoft.Extensions.Options;
using WordpressCache.Config;
using WordpressCache.Models;

namespace WordpressCache.Services;

public sealed class CacheUpdater : BackgroundService {
    private readonly ICache _cache;
    private readonly IEnumerable<string> _paths;
    private readonly ILogger<CacheUpdater> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ServerStatus _serverStatus;
    private readonly GlobalOptions _options;
    private readonly IPreloader _preloader;

    public CacheUpdater(
        ICache cache,
        ILogger<CacheUpdater> logger,
        IHttpClientFactory httpClientFactory,
        ServerStatus serverStatus,
        IOptions<GlobalOptions> options,
        IPreloader preloader
    ) {
        _cache = cache;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _serverStatus = serverStatus;
        _preloader = preloader;
        _options = options.Value;
        if (cache is MemoryCache memoryCache) {
            _paths = memoryCache.Values.Keys;
        } else {
            _paths = Array.Empty<string>();
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        var interval = _options.UpdateInterval;
        if (interval <= 0) {
            _logger.LogInformation("Periodic update disabled");
            return;
        }

        var timer = new PeriodicTimer(TimeSpan.FromSeconds(interval));
        try {
            while (!stoppingToken.IsCancellationRequested) {
                await timer.WaitForNextTickAsync(stoppingToken);
                _logger.LogInformation("Updating cache contents");
                if (_serverStatus.IsError) {
                    continue;
                }

                try {
                    foreach (string path in _paths) {
                        var client = _httpClientFactory.CreateClient("wp");
                        _logger.LogInformation("Updating {Path}", path);
                        var response = await client.GetAsync(MakeUri(client.BaseAddress!, path), stoppingToken);
                        if (!response.IsSuccessStatusCode) {
                            continue;
                        }

                        var contentLength = response.Content.Headers.ContentLength;
                        if (contentLength <= _options.MaxSize) {
                            var body = await response.Content.ReadAsByteArrayAsync(stoppingToken);
                            _cache.Save(path, response, body);
                        } else {
                            _cache.Save(path, response, Array.Empty<byte>());
                        }

                        await Task.Delay(20_000, stoppingToken);
                    }
                } catch (HttpRequestException) {
                    _serverStatus.MaskAsError();
                } finally {
                    await _preloader.SaveAsync();
                }
            }
        } catch (TaskCanceledException) {
            await _preloader.SaveAsync();
        }

        throw new InvalidOperationException();
    }

    private static Uri MakeUri(Uri baseAddress, string path) {
        if (!path.Contains('?')) {
            var uri = new UriBuilder(baseAddress) {
                Path = path
            };
            return uri.Uri;
        } else {
            var parts = path.Split('?');
            var uri = new UriBuilder(baseAddress) {
                Path = parts[0],
                Query = parts[1]
            };
            return uri.Uri;
        }
    }

    private static string JoinUrlPath(string baseUrl, string path) {
        if (string.IsNullOrEmpty(baseUrl)) {
            return path;
        }

        if (string.IsNullOrEmpty(path)) {
            return baseUrl;
        }

        var baseEnd = baseUrl.EndsWith('/');
        var pathStart = path[0] == '/';

        if (baseEnd && pathStart) {
            return baseUrl + path[1..];
        }

        if (!baseEnd && !pathStart) {
            return baseUrl + '/' + path;
        }

        return baseUrl + path;
    }
}

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

    public CacheUpdater(
        ICache cache,
        ILogger<CacheUpdater> logger,
        IHttpClientFactory httpClientFactory,
        ServerStatus serverStatus,
        IOptions<GlobalOptions> options
    ) {
        _cache = cache;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _serverStatus = serverStatus;
        _options = options.Value;
        if (cache is MemoryCache memoryCache) {
            _paths = memoryCache.Values.Keys;
        } else {
            _paths = Array.Empty<string>();
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        var timer = new PeriodicTimer(TimeSpan.FromHours(2));
        while (!stoppingToken.IsCancellationRequested) {
            await timer.WaitForNextTickAsync(stoppingToken);
            if (_serverStatus.IsError) {
                continue;
            }

            var client = _httpClientFactory.CreateClient("wp");
            try {
                foreach (string path in _paths) {
                    var response = await client.GetAsync(path, stoppingToken);
                    if (!response.IsSuccessStatusCode) continue;
                    var contentLength = response.Content.Headers.ContentLength;
                    if (contentLength < _options.MaxSize) {
                        var body = await response.Content.ReadAsByteArrayAsync(stoppingToken);
                        _cache.SaveAsync(path, response, body);
                    } else {
                        _cache.SaveAsync(path, response, Array.Empty<byte>());
                    }

                    await Task.Delay(20_000, stoppingToken);
                }
            } catch (HttpRequestException) {
                _serverStatus.MaskAsError();
            }
        }

        throw new InvalidOperationException();
    }
}

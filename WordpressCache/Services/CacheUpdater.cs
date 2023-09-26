using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using WordpressCache.Config;
using WordpressCache.Models;

namespace WordpressCache.Services;

public sealed class CacheUpdater : BackgroundService {
    private readonly ICache _cache;
    private readonly IEnumerable<string> _paths;
    private readonly ILogger<CacheUpdater> _logger;
    // private readonly IHttpClientFactory _httpClientFactory;
    private readonly ServerStatus _serverStatus;
    private readonly GlobalOptions _options;
    private readonly IPreloader _preloader;
    private readonly BackendClient _client;

    public CacheUpdater(
        ICache cache,
        ILogger<CacheUpdater> logger,
        // IHttpClientFactory httpClientFactory,
        ServerStatus serverStatus,
        IOptions<GlobalOptions> options,
        IPreloader preloader,
        BackendClient client
    ) {
        _cache = cache;
        _logger = logger;
        // _httpClientFactory = httpClientFactory;
        _serverStatus = serverStatus;
        _preloader = preloader;
        _client = client;
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
                        // var client = _httpClientFactory.CreateClient("wp");
                        _logger.LogInformation("Updating {Path}", path);
                        // using var message = new HttpRequestMessage(HttpMethod.Get, MakeUri(client.BaseAddress!, path)) {
                        //     Headers = {
                        //         {HeaderNames.CacheControl, "no-cache"}
                        //     }
                        // };

                        // var response = await client.GetAsync(MakeUri(client.BaseAddress!, path), stoppingToken);
                        // var response = await client.SendAsync(message, stoppingToken);
                        var response = await _client.GetAsync(path, stoppingToken);
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
        var q = path.IndexOf('?');
        if (q == -1) {
            var uri = new UriBuilder(baseAddress) {
                Path = path
            };
            return uri.Uri;
        } else {
            var uri = new UriBuilder(baseAddress) {
                Path = path[..q],
                Query = path[q..]
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

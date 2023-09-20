using System.Net;
using Microsoft.Extensions.Options;
using WordpressCache.Config;

namespace WordpressCache.Services;

public sealed class ServerStatus {
    public bool IsError { get; private set; }
    public IPAddress? PublicIP { get; private set; }

    // private long _nextRetry;
    private long _lastCheck;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GlobalOptions _options;
    private readonly ILogger<ServerStatus> _logger;

    private static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    public ServerStatus(IHttpClientFactory httpClientFactory, IOptions<GlobalOptions> options, ILogger<ServerStatus> logger) {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _options = options.Value;
    }

    public void MaskAsError() {
        _lastCheck = Now;
        IsError = true;
    }

    public async Task CheckStatusAsync() {
        if (_lastCheck > Now - _options.CheckInterval) {
            return;
        }

        var client = _httpClientFactory.CreateClient("WP");
        try {
            var respone = await client.GetAsync("/");
            if (respone.IsSuccessStatusCode) {
                IsError = false;
            } else {
                _logger.LogWarning("Response code is not success status code");
            }
        } catch (HttpRequestException) {
            _logger.LogError("Backend server down");
            IsError = true;
        }
    }

    public void SetIP(string ip) {
        PublicIP = IPAddress.Parse(ip);
    }
}

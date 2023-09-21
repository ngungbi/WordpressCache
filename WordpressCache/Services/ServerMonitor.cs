using Microsoft.Extensions.Options;
using WordpressCache.Config;

namespace WordpressCache.Services;

public sealed class ServerMonitor : BackgroundService {
    private readonly ServerStatus _serverStatus;
    private readonly GlobalOptions _options;
    private readonly ILogger<ServerMonitor> _logger;

    public ServerMonitor(ServerStatus serverStatus, IOptions<GlobalOptions> options, ILogger<ServerMonitor> logger) {
        _serverStatus = serverStatus;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        var interval = _options.CheckInterval;
        if (interval <= 0) {
            _logger.LogWarning("Server monitor disabled");
            return;
        }

        var timer = new PeriodicTimer(TimeSpan.FromSeconds(interval));
        while (!stoppingToken.IsCancellationRequested) {
            await timer.WaitForNextTickAsync(stoppingToken);
            await _serverStatus.CheckStatusAsync();
        }

        throw new InvalidOperationException();
    }
}

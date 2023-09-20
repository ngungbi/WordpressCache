using Microsoft.Extensions.Options;
using WordpressCache.Config;

namespace WordpressCache.Services;

public sealed class ServerMonitor : BackgroundService {
    private readonly ServerStatus _serverStatus;
    private readonly GlobalOptions _options;

    public ServerMonitor(ServerStatus serverStatus, IOptions<GlobalOptions> options) {
        _serverStatus = serverStatus;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.CheckInterval));
        while (!stoppingToken.IsCancellationRequested) {
            await timer.WaitForNextTickAsync(stoppingToken);
            await _serverStatus.CheckStatusAsync();
        }

        throw new InvalidOperationException();
    }
}

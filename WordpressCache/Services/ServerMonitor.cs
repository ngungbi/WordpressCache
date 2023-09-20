namespace WordpressCache.Services;

public sealed class ServerMonitor : BackgroundService {
    private readonly ServerStatus _serverStatus;

    public ServerMonitor(ServerStatus serverStatus) {
        _serverStatus = serverStatus;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        while (!stoppingToken.IsCancellationRequested) {
            await timer.WaitForNextTickAsync(stoppingToken);
            await _serverStatus.CheckStatusAsync();
        }

        throw new InvalidOperationException();
    }
}

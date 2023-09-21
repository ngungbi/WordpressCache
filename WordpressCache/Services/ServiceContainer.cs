namespace WordpressCache.Services;

public sealed class ServiceContainer {
    public HttpClient HttpClient => _services.GetRequiredService<IHttpClientFactory>().CreateClient("WP");
    public ICache Cache => _services.GetRequiredService<ICache>();
    public ServerStatus ServerStatus => _services.GetRequiredService<ServerStatus>();
    public ILogger<ProxyMiddleware> Logger { get; }

    private readonly IServiceProvider _services;
    // private readonly IServiceScope _scope;

    public ServiceContainer(ILoggerFactory logger, IServiceProvider services) {
        // HttpClient = httpClientFactory.CreateClient("WP");
        // _scope = serviceScopeFactory.CreateAsyncScope();
        _services = services;
        Logger = logger.CreateLogger<ProxyMiddleware>();
    }

    // public void Dispose() {
    //     _scope.Dispose();
    //     // HttpClient.Dispose();
    // }
}

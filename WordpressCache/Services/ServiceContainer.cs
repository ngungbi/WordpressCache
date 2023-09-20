namespace WordpressCache.Services; 

public sealed class ServiceContainer {
    public HttpClient HttpClient { get; }
    public ICache Cache { get; }
    public ILogger<ProxyMiddleware> Logger { get; }

    public ServiceContainer(IHttpClientFactory httpClientFactory, ICache cache, ILoggerFactory logger) {
        HttpClient = httpClientFactory.CreateClient("wp");
        Cache = cache;
        Logger = logger.CreateLogger<ProxyMiddleware>();
    }
}

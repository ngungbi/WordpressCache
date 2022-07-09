using StackExchange.Redis;

namespace WordpressCache; 

public class ServiceContainer {
    public HttpClient HttpClient { get; }
    public ICache Cache { get; }

    public ServiceContainer(IHttpClientFactory httpClientFactory, ICache cache) {
        HttpClient = httpClientFactory.CreateClient("wp");
        Cache = cache;
    }
}

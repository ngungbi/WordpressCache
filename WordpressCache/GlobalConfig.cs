using Ngb.Configuration;

namespace WordpressCache;

public class GlobalConfig {
    [FromConfig("PUBLIC_ADDRESS")]
    public string PublicAddress { get; init; } = string.Empty;

    [FromConfig("HOST")]
    public string Host { get; init; } = string.Empty;

    [FromConfig]
    public string Scheme { get; init; } = "https";

    [FromConfig("BACKEND_ADDRESS")]
    public string BackendAddress { get; init; } = string.Empty;

    [FromConfig("REDIS_HOST")]
    public string RedisHost { get; init; } = "localhost:6379";

    [FromConfig("CACHE_TTL")]
    public int CacheExpiry { get; init; } = 7200;

    public TimeSpan DefaultExpiry { get; init; }
    public TimeSpan ContentExpiry { get; init; }

    public GlobalConfig(IConfiguration? configuration) {
        ConfigReader.ReadAll(this, configuration);
        DefaultExpiry = TimeSpan.FromSeconds(CacheExpiry);
        ContentExpiry = TimeSpan.FromSeconds(CacheExpiry + 300);
    }
}

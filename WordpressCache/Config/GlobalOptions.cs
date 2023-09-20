namespace WordpressCache.Config;

public sealed class GlobalOptions {
    // private Uri _publicAddress = null!;
    //
    // public string PublicAddress {
    //     get => _publicAddress.ToString();
    //     set => _publicAddress = new Uri(value);
    // }

    // public string Host => _publicAddress.Host;
    // public string Scheme => _publicAddress.Scheme;

    // public Uri BackendAddress { get; set; } = null!;

    public int CacheTtl { get; set; } = 2 * 3600;
    public long MaxSize { get; set; } = 10 * 1024 * 1024;
    public int CheckInterval { get; set; } = 600;
}

using Microsoft.Extensions.Primitives;

namespace WordpressCache.Models;

public class CachedContent {
    public CachedContent() {
        Headers = new Dictionary<string, string>();
    }

    public CachedContent(IDictionary<string, string> headers) { Headers = headers; }
    public IDictionary<string, string> Headers { get; }
    public long Expire { get; set; }
    public long ContentLength { get; set; }
    public byte[]? Content { get; set; }
}

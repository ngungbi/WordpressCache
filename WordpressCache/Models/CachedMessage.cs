using Microsoft.Extensions.Primitives;

namespace WordpressCache.Models;

public class CachedMessage {
    public CachedMessage() {
        Headers = new Dictionary<string, string>();
    }

    public CachedMessage(IDictionary<string, string> headers) { Headers = headers; }
    public IDictionary<string, string> Headers { get; }
    public byte[]? Content { get; set; }
}

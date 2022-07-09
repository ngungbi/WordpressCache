using Microsoft.Extensions.Primitives;

namespace WordpressCache.Models;

public class CachedMessage {
    public IDictionary<string, string> Headers { get; set; }
    public byte[]? Content { get; set; }
}

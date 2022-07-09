using System.Text.Json;
using StackExchange.Redis;

namespace WordpressCache.Extensions;

public static class RedisValueExtension {
    public static string ToJsonString<T>(this T value) {
        return JsonSerializer.Serialize(value);
    }

    public static T? ToObject<T>(this RedisValue value) where T : class {
        string? stringValue = value;
        return string.IsNullOrEmpty(stringValue) ? null : JsonSerializer.Deserialize<T>(stringValue);
    }
}

namespace WordpressCache.Extensions;

public static class LoggerExtension {
    public static bool IsInformation(this ILogger logger) {
        return logger.IsEnabled(LogLevel.Information);
    }

    public static bool IsError(this ILogger logger) {
        return logger.IsEnabled(LogLevel.Error);
    }

    public static bool IsWarning(this ILogger logger) {
        return logger.IsEnabled(LogLevel.Warning);
    }

    public static bool IsDebug(this ILogger logger) {
        return logger.IsEnabled(LogLevel.Debug);
    }

    public static bool IsCritical(this ILogger logger) {
        return logger.IsEnabled(LogLevel.Critical);
    }
}

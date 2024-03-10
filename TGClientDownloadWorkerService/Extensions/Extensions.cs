using TGClientDownloadWorkerService.Logger;

namespace TGClientDownloadWorkerService.Extensions
{
    public static class DbLoggerExtensions
    {
        public static ILoggingBuilder AddDbLogger(this ILoggingBuilder builder)
        {
            builder.Services.AddSingleton<ILoggerProvider, DbLoggerProvider>();
            return builder;
        }
    }

    public static class LoggerExtensions
    {
        public static void Info(this ILogger logger, string message, Exception? exception = null)
        {
            logger.Log(LogLevel.Information, 0, message, exception, MessageFormatter);
        }
        public static void Debug(this ILogger logger, string message, Exception? exception = null)
        {
            logger.Log(LogLevel.Debug, 0, message, exception, MessageFormatter);
        }
        public static void Error(this ILogger logger, string message, Exception? exception = null)
        {
            logger.Log(LogLevel.Error, 0, message, exception, MessageFormatter);
        }
        public static void Warning(this ILogger logger, string message, Exception? exception = null)
        {
            logger.Log(LogLevel.Warning, 0, message, exception, MessageFormatter);
        }

        private static string MessageFormatter(string arg1, Exception? exception)
        {
            return string.Empty;
        }
    }

    public static class CustomExtensions
    {
        public static bool HasElements<T>(this IEnumerable<T> myList) => myList is not null && myList.Any();
    }
}

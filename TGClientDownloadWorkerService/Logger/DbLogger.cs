using Microsoft.EntityFrameworkCore;
using TGClientDownloadDAL;

namespace TGClientDownloadWorkerService.Logger
{
    public class DbLogger : ILogger
    {
        /// <summary>
        /// Instance of <see cref="IServiceProvider" />
        /// </summary>
        private readonly IServiceProvider _serviceProvider;

        /// <summary>
        /// Calling class
        /// </summary>
        private readonly string _category;

        /// <summary>
        /// Creates a new instance of <see cref="FileLogger" />.
        /// </summary>
        /// <param name="fileLoggerProvider">Instance of <see cref="FileLoggerProvider" />.</param>
        /// <param name="category">Calling class</param>
        public DbLogger(IServiceProvider serviceProvider, string category)
        {
            _serviceProvider = serviceProvider;
            _category = category;
        }


        public IDisposable BeginScope<TState>(TState state)
        {
            return null;
        }

        /// <summary>
        /// Whether to log the entry.
        /// </summary>
        /// <param name="logLevel"></param>
        /// <returns></returns>
        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel != LogLevel.None;
        }


        /// <summary>
        /// Writes a log entry.
        /// </summary>
        /// <param name="logLevel">Entry will be written on this level.</param>
        /// <param name="eventId">Id of the event.</param>
        /// <param name="state">The entry to be written. Can be also an object.</param>
        /// <param name="exception">The exception related to this entry.</param>
        /// <param name="formatter">Function to create a <see cref="string"/> message of the <paramref name="state"/> and <paramref name="exception"/>.</param>
        /// <typeparam name="TState">The type of the object to be written.</typeparam>
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string>? formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            // Store record.
            var date = DateTime.UtcNow;
            using (var scope = _serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider
                        .GetRequiredService<TGDownDBContext>();

                dbContext.Database.ExecuteSqlRaw("INSERT INTO system_log (\"LogEntryDatetime\", \"Level\", \"Message\", \"Exception\", \"StackTrace\", \"ClassName\") VALUES ({0},{1},{2},{3},{4},{5})",
                                            date, logLevel.ToString(), state?.ToString(), exception?.Message, exception?.StackTrace, _category);
                dbContext.Dispose();
            }
        }
    }
}

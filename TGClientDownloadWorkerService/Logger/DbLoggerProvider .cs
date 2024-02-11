namespace TGClientDownloadWorkerService.Logger
{
    public class DbLoggerProvider : ILoggerProvider
    {
        protected readonly IConfiguration _configuration;
        private readonly IServiceProvider _serviceProvider;

        public DbLoggerProvider(IConfiguration configuration, IServiceProvider serviceProvider)
        {
            _configuration = configuration;
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// Creates a new instance of the db logger.
        /// </summary>
        /// <param name="categoryName"></param>
        /// <returns></returns>
        public ILogger CreateLogger(string categoryName)
        {
            return new DbLogger(_serviceProvider, categoryName);
        }

        public void Dispose()
        {
        }
    }
}

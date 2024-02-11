using TGClientDownloadDAL;
using TGClientDownloadWorkerService.Configuration;
using TGClientDownloadWorkerService.Extensions;

namespace TGClientDownloadWorkerService.Services
{
    public abstract class ScheduledTaskService : BackgroundService
    {
        private readonly ILogger<ScheduledTaskService> _internalLogger;
        private readonly IServiceProvider _serviceProvider;
        private readonly AppSettings _configuration = new AppSettings();

        public ScheduledTaskService(ILogger<ScheduledTaskService> logger, IServiceProvider serviceProvider, IConfiguration configuration)
        {
            _internalLogger = logger;
            _serviceProvider = serviceProvider;
            _configuration = new AppSettings();
            configuration.GetRequiredSection("AppSettings").Bind(_configuration);
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            _internalLogger.Info($"Starting {GetType().Name} Task");

            //using (var scope = _serviceProvider.CreateScope())
            //{
            //    var dbContext = scope.ServiceProvider.GetRequiredService<TGDownDBContext>();
            //    if (dbContext.Database.GetPendingMigrations().Any())
            //    {
            //        dbContext.Database.Migrate();
            //    }
            //}
            while (!cancellationToken.IsCancellationRequested)
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<TGDownDBContext>();

                    var thisTask = dbContext.ScheduledTasks.FirstOrDefault(st => st.TasksName == GetType().Name);

                    int delay = thisTask?.Interval ?? 5000;

                    if (thisTask == null || !thisTask.Enabled)
                    {
                        await Task.Delay(10000, cancellationToken);
                        continue;
                    }

                    Run(dbContext, _serviceProvider, cancellationToken);

                    await Task.Delay(delay, cancellationToken);

                    dbContext.Dispose();
                }
            }


        }
        public abstract void Run(TGDownDBContext _dbContext, IServiceProvider _serviceProvider, CancellationToken cancellationToken);
    }
}

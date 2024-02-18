using TGClientDownloadDAL;
using TGClientDownloadWorkerService.Extensions;

namespace TGClientDownloadWorkerService.Services
{
    public abstract class ScheduledTaskService : BackgroundService
    {
        private readonly ILogger<ScheduledTaskService> _log;
        private readonly IServiceProvider _serviceProvider;
        protected IServiceScope _serviceScope;
        protected TGDownDBContext _dbContext;

        public ScheduledTaskService(ILogger<ScheduledTaskService> logger, IServiceProvider serviceProvider)
        {
            _log = logger;
            _serviceProvider = serviceProvider;
        }
        public override Task StartAsync(CancellationToken cancellationToken)
        {
            Setup();
            return base.StartAsync(cancellationToken);
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            Cleanup();
            return base.StopAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            _log.Info($"Starting {GetType().Name} Task");

            while (!cancellationToken.IsCancellationRequested)
            {
                int delay = 5000;

                using (_serviceScope = _serviceProvider.CreateScope())
                using (_dbContext = _serviceScope.ServiceProvider.GetService<TGDownDBContext>())
                {
                    var thisTask = _dbContext.ScheduledTasks.FirstOrDefault(st => st.TasksName == GetType().Name);
                    if (thisTask is not null)
                    {
                        _log.Debug($"Task found with name {thisTask.TasksName} and status {thisTask.Enabled}");
                    }
                    else
                    {
                        _log.Debug($"No Task found! {GetType().Name}");
                    }
                    delay = thisTask?.Interval ?? 5000;

                    if (thisTask == null || !thisTask.Enabled)
                    {
                        await Task.Delay(10000, cancellationToken);
                        continue;
                    }
                    thisTask.LastStart = DateTime.UtcNow;
                    thisTask.IsRunning = true;
                    _dbContext.SaveChanges();

                    await Run(cancellationToken);

                    thisTask.LastFinish = DateTime.UtcNow;
                    thisTask.IsRunning = false;
                    _dbContext.SaveChanges();
                }

                await Task.Delay(delay, cancellationToken);
                _dbContext = null;
                _serviceScope = null;
            }
            _log.Info($"{GetType().Name} has been stopped");

        }

        public abstract Task Run(CancellationToken cancellationToken);

        public abstract void Setup();

        public abstract void Cleanup();
    }
}

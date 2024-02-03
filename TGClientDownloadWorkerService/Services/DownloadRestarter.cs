using Microsoft.EntityFrameworkCore;
using TGClientDownloadDAL;
using TGClientDownloadWorkerService.Configuration;

namespace TGClientDownloadWorkerService.Services
{
    public class DownloadRestarter : BackgroundService
    {
        private readonly ILogger<DownloadRestarter> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly AppSettings _configuration;

        public DownloadRestarter(ILogger<DownloadRestarter> logger, IServiceProvider serviceProvider, IConfiguration configuration)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _configuration = new AppSettings();
            configuration.GetRequiredSection("AppSettings").Bind(_configuration);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<TGDownDBContext>();
                if (dbContext.Database.GetPendingMigrations().Any())
                {
                    dbContext.Database.Migrate();
                }
            }
            while (!stoppingToken.IsCancellationRequested)
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<TGDownDBContext>();
                var thisTask = dbContext.ScheduledTasks.FirstOrDefault(st => st.TasksName == nameof(DownloadRestarter));

                int delay = thisTask?.Interval ?? 5000;

                if (thisTask == null || !thisTask.Enabled)
                {
                    await Task.Delay(10000, stoppingToken);
                    continue;
                }
                var t = dbContext.TgChannels.FirstOrDefault();
                if (t is null)
                {
                    Console.WriteLine("Nessun Dato");
                }
                else
                {
                    Console.WriteLine("C'è qualcosa");
                }
                await Task.Delay(delay, stoppingToken);
            }

            Console.WriteLine("Exiting");
        }
    }
}

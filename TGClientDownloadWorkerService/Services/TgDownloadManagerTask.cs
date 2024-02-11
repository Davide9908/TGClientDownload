using TGClientDownloadDAL;
using TGClientDownloadWorkerService.Extensions;

namespace TGClientDownloadWorkerService.Services
{
    public class TgDownloadManagerTask : ScheduledTaskService
    {
        private readonly TelegramClient _client;
        private readonly ILogger<ScheduledTaskService> _log;
        private readonly IServiceProvider _serviceProvider;
        public TgDownloadManagerTask(ILogger<TgDownloadManagerTask> logger, IServiceProvider serviceProvider, IConfiguration configuration, TelegramClient client) : base(logger, serviceProvider, configuration)
        {
            _client = client;
            _log = logger;
            _serviceProvider = serviceProvider;
        }

        public override void Run(TGDownDBContext _dbContext, IServiceProvider _serviceProvider, CancellationToken cancellationToken)
        {
            _log.Info("Sto girando");
        }
    }
}

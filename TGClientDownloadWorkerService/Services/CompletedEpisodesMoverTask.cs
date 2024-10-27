using Microsoft.EntityFrameworkCore;
using TGClientDownloadDAL.Entities;
using TGClientDownloadDAL.SupportClasses;
using TGClientDownloadWorkerService.Extensions;

namespace TGClientDownloadWorkerService.Services
{
    public class CompletedEpisodesMoverTask : ScheduledTaskService
    {
        private readonly ILogger<CompletedEpisodesMoverTask> _log;
        private readonly IServiceProvider _serviceProvider;
        private MalApiService? _malApiService;
        private ConfigParameterService? _configuration;
        public CompletedEpisodesMoverTask(ILogger<CompletedEpisodesMoverTask> logger, IServiceProvider serviceProvider) : base(logger, serviceProvider)
        {
            _log = logger;
            _serviceProvider = serviceProvider;
        }

        public override void Cleanup()
        {
        }

        public override Task Run(CancellationToken cancellationToken)
        {
            _malApiService = _serviceScope.ServiceProvider.GetRequiredService<MalApiService>();
            _configuration = _serviceScope.ServiceProvider.GetRequiredService<ConfigParameterService>();

            string? downloadFolder = _configuration.GetValue(ParameterNames.DefaultDownloadLocation);
            if (downloadFolder is null)
            {
                _log.Error("No default download folder found");
                _malApiService = null;
                _configuration = null;
                return Task.CompletedTask;
            }

            string[]? fileNames = GetDownloadedFileNames(downloadFolder);

            if (!fileNames.HasElements())
            {
                _malApiService = null;
                _configuration = null;
                return Task.CompletedTask;
            }

            List<AnimeEpisodesSetting> activeAnimeChannel = _dbContext.AnimeEpisodesSettings.Where(s => s.TelegramChannel.AutoDownloadEnabled && s.TelegramChannel.Status == ChannelStatus.Active).ToList();

            List<MALAnimeData>? animeWatchingList = _malApiService.GetWatchingAnimeList();
            List<MALAnimeData>? animeCompletedList = _malApiService.GetCompletedAnimeList();
            if (animeWatchingList is null || animeCompletedList is null)
            {
                _log.Error("Returned anime lists is null. No work will be done");
                _malApiService = null;
                _configuration = null;
                return Task.CompletedTask;
            }

            if (!activeAnimeChannel.HasElements())
            {
                _malApiService = null;
                _configuration = null;
                return Task.CompletedTask;
            }

            var dbFiles = _dbContext.TelegramMediaDocuments.Where(f => f.DownloadStatus == DownloadStatus.Success && fileNames.Contains(f.FileName))
                                                            .Join(_dbContext.AnimeEpisodesSettings, d => d.SourceChatId, c => c.TelegramChannelId, (x, u) => new { x.FileName, EpisodeSetting = u })
                                                            .ToDictionary(x => x.FileName, x => x.EpisodeSetting);

            foreach (var filename in fileNames)
            {
                if (!dbFiles.ContainsKey(filename))
                {
                    continue;
                }

                AnimeEpisodesSetting? setting = dbFiles.GetValueOrDefault(filename);
                if (setting?.AnimeFolderPath is null || setting?.MALAnimeId is null)
                {
                    _log.Warning("AnimeEpisodesSetting folder or MAL id are not configured");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(setting.FileNameTemplate))
                {
                    _log.Warning("Filename template not configured, skipping...");
                    continue;
                }
                string epNumberString = filename.Replace(setting.FileNameTemplate, string.Empty).Split(".").FirstOrDefault();
                if (string.IsNullOrWhiteSpace(epNumberString))
                {
                    _log.Error("Could not extract ep number from file name");
                    continue;
                }
                int epNumber;
                try
                {
                    epNumber = int.Parse(epNumberString);
                }
                catch (Exception ex)
                {
                    _log.Error("Could not parse episode number from file name", ex);
                    continue;
                }
                if (setting.CourEpisodeNumberGap.HasValue)
                {
                    epNumber -= setting.CourEpisodeNumberGap.Value;
                }
                //I look first on watching list. If it's not present, i look into the completed ones
                var animeEntry = animeWatchingList.FirstOrDefault(l => l.node.id == setting.MALAnimeId)?.list_status;
                animeEntry ??= animeCompletedList.FirstOrDefault(l => l.node.id == setting.MALAnimeId)?.list_status;
                //run the check again to see if i found it
                if (animeEntry is null)
                {
                    _log.Warning($"Anime with id {setting.MALAnimeId} not found in MAL");
                    continue;
                }
                if (animeEntry.num_episodes_watched >= epNumber)
                {
                    string fileWithPath = Path.Combine(downloadFolder, filename);
                    string destination = Path.Combine(setting.AnimeFolderPath, filename);
                    File.Move(fileWithPath, destination);
                }
            }

            _malApiService = null;
            _configuration = null;
            return Task.CompletedTask;
        }

        public override void Setup()
        {
        }

        private string[]? GetDownloadedFileNames(string downloadFolder)
        {
            string[] files;
            try
            {
                files = Directory.GetFiles(downloadFolder).Select(Path.GetFileName).ToArray();
            }
            catch (Exception ex)
            {
                _log.Error("An error occurred retrieving files from download path", ex);
                return null;
            }
            return files;
        }
    }
}

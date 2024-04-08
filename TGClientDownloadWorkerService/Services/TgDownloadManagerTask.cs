using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using TGClientDownloadDAL;
using TGClientDownloadDAL.Entities;
using TGClientDownloadDAL.SupportClasses;
using TGClientDownloadWorkerService.Extensions;
using TL;

namespace TGClientDownloadWorkerService.Services
{
    public partial class TgDownloadManagerTask : ScheduledTaskService
    {
        private readonly TelegramClientService _client;
        private readonly ILogger<ScheduledTaskService> _log;
        private readonly IServiceProvider _serviceProvider;
        private int _loginConnectionAttemps = 1;
        private ConfigParameterService _configParameterService;
        private bool _startupRetrieve = true;

        private ConcurrentDictionary<int, Document> _inProgress; //the key is the messageId
        private ConcurrentDictionary<int, Document> _inError; //the key is the messageId
        private ConcurrentDictionary<int, decimal> _downloadProgress;

        public TgDownloadManagerTask(ILogger<TgDownloadManagerTask> logger, IServiceProvider serviceProvider, TelegramClientService client) : base(logger, serviceProvider)
        {
            _client = client;
            _log = logger;
            _serviceProvider = serviceProvider;
        }
        public override void Cleanup()
        {
            _client.Dispose();
        }

        public override void Setup()
        {
            //_configParameterService = _serviceScope.ServiceProvider.GetRequiredService<ConfigParameterService>();
            _inError = new ConcurrentDictionary<int, Document>();
            _inProgress = new ConcurrentDictionary<int, Document>();
            _downloadProgress = new ConcurrentDictionary<int, decimal>();
        }

        public async override Task Run(CancellationToken cancellationToken)
        {
            _configParameterService = _serviceScope.ServiceProvider.GetRequiredService<ConfigParameterService>();
            if (!_client.IsConnected)
            {
                if (_loginConnectionAttemps > 3)
                {
                    _log.Warning("Connection attemps exeeded the maximum threshold of 3 attemps. No more attemps will be made");
                    return;
                }
                User? loggedUser = await _client.Connect();

                if (loggedUser is null)
                {
                    _log.Error("Error while connecting and login to telegram. Trying again in the next task run");
                    _loginConnectionAttemps++;
                    return;
                }
                _loginConnectionAttemps = 1;
                //return; //I will start doing stuff at the next run
            }
            var lastRefreshParam = _configParameterService.GetConfigurationParameter(ParameterNames.LastChatsRefresh);
            if (_startupRetrieve)
            {
                _startupRetrieve = false;
                await RetrieveFailedOrIncompleteDownloads();
                //await _client.LoadAllChats();
                //if (lastRefreshParam is null)
                //{
                //    lastRefreshParam = new ConfigurationParameter()
                //    {
                //        ParameterName = ParameterNames.LastChatsRefresh,
                //        ParameterType = ConfigurationParameterType.DateTime,
                //        ParameterValue = DateTime.Now.ToString()
                //    };
                //    _dbContext.Add(lastRefreshParam);
                //}
                //else
                //{
                //    lastRefreshParam.ParameterValue = DateTime.Now.ToString();
                //}
            }
            var refreshDBChannelParam = _configParameterService.GetConfigurationParameter(ParameterNames.RefreshDBChannels);
            if (refreshDBChannelParam is not null && bool.Parse(refreshDBChannelParam.ParameterValue))
            {
                RefreshDBChannelFromCache();
                refreshDBChannelParam.ParameterValue = false.ToString();
            }
            //var lastRefreshPeriod = TimeSpan.FromTicks(DateTime.Now.Ticks - DateTime.Parse(lastRefreshParam.ParameterValue).Ticks);

            //if (lastRefreshPeriod.TotalHours >= 6)
            //{
            //    await _client.LoadAllChats();
            //    lastRefreshParam.ParameterValue = DateTime.Now.ToString();
            //}

            //List<Task> tasks = [Task.Run(() => HandleChannelUpdates(cancellationToken), CancellationToken.None),
            //    Task.Run(() => HandleDownloadInError(cancellationToken), CancellationToken.None)];

            //await Task.WhenAll(tasks);

            await HandleFirstDownload(cancellationToken);
            HandleChannelUpdates(cancellationToken);
            HandleDownloadInError(cancellationToken);

            _dbContext.SaveChanges();
            _configParameterService = null;
        }

        #region Handlers
        private void HandleChannelUpdates(CancellationToken cancellationToken)
        {
            ConcurrentQueue<ChannelFileUpdate> fileUpdateQueue = _client.GetChannelFileUpdatesQueue();
            while (!fileUpdateQueue.IsEmpty)
            {
                bool result = fileUpdateQueue.TryDequeue(out ChannelFileUpdate fileUpdate);
                if (!result) { continue; }

                Channel channel = fileUpdate.Channel;
                TelegramChannel? channelConfig = _dbContext.TelegramChannels.Where(c => c.ChatId == channel.ID && c.AccessHash == channel.access_hash)
                                                                            .Include(c => c.AnimeEpisodesSetting)
                                                                            .FirstOrDefault();
                if (channelConfig is null && ManagePossibleNewChannel(fileUpdate, ref channelConfig, channel))
                {
                    continue;
                }

                if (!channelConfig.AutoDownloadEnabled || channelConfig.Status != ChannelStatus.Active)
                {
                    _log.Warning($"Channel is not active or automatic downloads are disabled");
                    continue;
                }

                Message message = fileUpdate.Message;
                if (message.media is not MessageMediaDocument)
                {
                    _log.Debug($"Message seems to not be a MessageMediaDocument. It's {message.media.GetType().Name}");
                    continue;
                }
                Match match = EpRegex().Match(message.message);
                if (!match.Success)
                {
                    _log.Warning($"Ep number could not be extrapolated from message: {message.message}");
                }
                string epNumber = match.Value.ToLowerInvariant().Replace("#ep", "");
                if (epNumber.Length == 1) //if the ep number is single digit (0-9), I add the 0 in front of it (01, 02, 03 etc...)
                {
                    epNumber = epNumber.PadLeft(2, '0');
                }

                var media = message.media as MessageMediaDocument;
                Document doc = (Document)media.document;

                string? extension = null;
                if (doc.Filename is null)
                {
                    extension = "." + doc.mime_type.Split("/").LastOrDefault();
                }
                else
                {
                    extension = FileExtensionRegex().Match(doc.Filename).Value;
                }

                if (extension is null)
                {
                    _log.Warning("File extension could not be determined");
                }
                string filename;
                if (channelConfig.AnimeEpisodesSetting is null || string.IsNullOrWhiteSpace(channelConfig.AnimeEpisodesSetting.FileNameTemplate))
                {
                    filename = doc.Filename ?? (doc.ID.ToString() + extension);
                }
                else
                {
                    if (channelConfig.AnimeEpisodesSetting.CourEpisodeNumberGap.HasValue)
                    {
                        epNumber = (int.Parse(epNumber) + channelConfig.AnimeEpisodesSetting.CourEpisodeNumberGap.Value).ToString();
                    }
                    filename = channelConfig.AnimeEpisodesSetting.FileNameTemplate + epNumber + extension;
                }

                string? downloadFolder = _configParameterService.GetValue(ParameterNames.DefaultDownloadLocation);
                if (downloadFolder is null)
                {
                    _log.Error("No default download folder found");
                    continue;
                }


                string filePath = Path.Combine(downloadFolder, filename);
                FileStream? fileStream;
                try
                {
                    fileStream = File.Open(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
                }
                catch (Exception ex)
                {
                    _log.Error($"A error occured while creating the file {filePath}", ex);
                    continue;
                }

                //If a file has been reuploaded, I will re-download it. I don't know if it is what I actually want, but for now let's keep it
                TelegramMediaDocument? episode = _dbContext.TelegramMediaDocuments.Include(x => x.TelegramMessage).FirstOrDefault(x => x.FileId == doc.ID && x.AccessHash == doc.access_hash);
                TelegramMessage telegramMessage;

                if (episode is not null)
                {
                    episode.DownloadStatus = DownloadStatus.Downloading;
                    episode.LastUpdate = DateTime.UtcNow;

                    telegramMessage = episode.TelegramMessage;
                }
                else
                {
                    telegramMessage = new TelegramMessage();
                    telegramMessage.MessageId = message.id;

                    episode = new TelegramMediaDocument
                    {
                        SourceChatId = channelConfig.TelegramChatId,
                        FileName = filename,
                        DownloadStatus = DownloadStatus.Downloading,
                        Size = doc.size,
                        FileId = doc.ID,
                        AccessHash = doc.access_hash,
                        TelegramMessage = telegramMessage
                    };

                    _dbContext.TelegramMessages.Add(telegramMessage);
                    _dbContext.TelegramMediaDocuments.Add(episode);
                }

                _dbContext.SaveChanges();

                _ = DownloadEpisode(doc, fileStream, telegramMessage, episode, cancellationToken);
            }
            _log.Debug("Update queue is empty");
        }

        private void HandleDownloadInError(CancellationToken cancellationToken)
        {
            List<int> messageIds = _inError.Keys.ToList();
            //var docs = _dbContext.TelegramMediaDocuments.Where(d => d.DownloadStatus == DownloadStatus.Error)
            //                                .Include(x => x.TelegramMessage)
            //                                .Join(_dbContext.TelegramChannels, x => x.SourceChatId, x => x.TelegramChatId, (d, c) => d)

            //                                .ToList();

            var dbMessages = _dbContext.TelegramMessages.Where(m => messageIds.Contains(m.MessageId)).ToList(); //in this way i should find the property in the TelegramMediaDocument list

            var query = from doc in _dbContext.TelegramMediaDocuments
                        join channel in _dbContext.TelegramChannels on doc.SourceChatId equals channel.TelegramChatId
                        where doc.DownloadStatus == DownloadStatus.Error && channel.AutoDownloadEnabled
                        select doc;
            List<TelegramMediaDocument> docs = query.ToList();
            foreach (int messageId in messageIds)
            {
                var episode = docs.FirstOrDefault(d => d.TelegramMessage.MessageId == messageId);
                _inError.TryGetValue(messageId, out var doc);
                if (_configParameterService is null) { _log.Debug("_configParameterService è null"); continue; }
                if (episode is null) { _log.Debug("episode è null"); continue; }

                string filePath = _configParameterService.GetValue(ParameterNames.DefaultDownloadLocation) + episode.FileName;
                FileStream? fileStream;
                try
                {
                    fileStream = File.Open(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
                }
                catch (Exception ex)
                {
                    _log.Error($"A error occured while creating the file {filePath}", ex);
                    continue;
                }
                episode.DownloadStatus = DownloadStatus.Downloading;
                episode.LastUpdate = DateTime.UtcNow;

                _dbContext.SaveChanges();

                _ = DownloadEpisode(doc, fileStream, episode.TelegramMessage, episode, cancellationToken);
            }

            _dbContext.TelegramChannels.Where(c => !c.AutoDownloadEnabled)
                                        .SelectMany(c => c.MediaDocuments)
                                        .Where(d => d.DownloadStatus == DownloadStatus.Error)
                                        .ToList()
                                        .ForEach(d =>
                                        {
                                            d.DownloadStatus = DownloadStatus.Aborted;
                                            d.LastUpdate = DateTime.UtcNow;
                                            d.ErrorType = null;
                                        });
            _dbContext.SaveChanges();
        }

        private async Task HandleFirstDownload(CancellationToken cancellationToken)
        {
            List<TelegramChannel> channelToDownload = _dbContext.TelegramChannels.Where(c => c.AnimeEpisodesSetting.DownloadLastEpisode).Include(c => c.AnimeEpisodesSetting).ToList();
            bool multipleRequests = channelToDownload.Count > 3;
            int count = 0;
            foreach (TelegramChannel channel in channelToDownload)
            {
                var tgChannel = _client.GetCachedChatById(channel.ChatId);
                if (tgChannel is null) { continue; }

                var messagesBase = (await _client.GetChannelHistory(tgChannel.ToInputPeer()));
                var messages = messagesBase.Where(m => m is Message).Select(m => (Message)m).ToList();

                var lastVideo = messages.OrderByDescending(m => m.date)
                                        .FirstOrDefault(m => m.media is MessageMediaDocument);
                if (lastVideo is null)
                {
                    _log.Info($"No episode found for channel {channel}");
                    continue;
                }

                _client.GetChannelFileUpdatesQueue().Enqueue(new ChannelFileUpdate(tgChannel as Channel, lastVideo, false));
                channel.AnimeEpisodesSetting.DownloadLastEpisode = false;
                _dbContext.SaveChanges();

                count++;
                if (count > 3)
                {
                    await Task.Delay(2000, cancellationToken);
                    count = 0;
                }
            }


        }
        #endregion

        #region Util Methods

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileUpdate"></param>
        /// <param name="telegramChannel"></param>
        /// <param name="channel"></param>
        /// <returns>true to skip to the next iteration</returns>
        private bool ManagePossibleNewChannel(ChannelFileUpdate fileUpdate, ref TelegramChannel? telegramChannel, Channel channel)
        {
            if (fileUpdate.SusChannel)//if the channel is one of the min ones, i will look only by id, maybe it exist with the wrong hash
            {
                telegramChannel = _dbContext.TelegramChannels.Include(c => c.AnimeEpisodesSetting).FirstOrDefault(c => c.ChatId == channel.ID);
                if (telegramChannel is null) //if it's still null it's a new one, but it's sus
                {
                    _log.Warning($"Channel {channel} is new but came with min flag, therefore I cannot trust its access_hash ");
                }
                else //update access hash
                {
                    _log.Info($"Updating Channel {channel} access_hash: old(wrong) one {telegramChannel.AccessHash}, new one {channel.access_hash}");
                    telegramChannel.AccessHash = channel.access_hash;
                    _dbContext.SaveChanges();
                    return false;
                }
            }
            _log.Warning($"Channel {channel} was not found in database, adding it to DB in order to confirm it");
            telegramChannel = new(channel.ID, channel.access_hash, channel.Title, false)
            {
                AnimeEpisodesSetting = new AnimeEpisodesSetting(),
                Status = fileUpdate.SusChannel ? ChannelStatus.AccessHashToVerify : ChannelStatus.ToConfirm
            };
            _dbContext.TelegramChannels.Add(telegramChannel);
            _dbContext.SaveChanges();
            return true;
        }

        private async Task RetrieveFailedOrIncompleteDownloads()
        {
            var activeChannelIds = _dbContext.TelegramChannels.Where(c => c.AutoDownloadEnabled).Select(c => c.TelegramChatId).ToList();

            var fileInError = _dbContext.TelegramMediaDocuments
                                            .Where(md => activeChannelIds.Contains(md.SourceChatId) && md.DownloadStatus == DownloadStatus.Error)
                                            .Include(x => x.SourceChat)
                                            .Include(x => x.TelegramMessage)
                                            .ToList();
            var downloadsNotStopped = _dbContext.TelegramMediaDocuments
                                            .Where(x => activeChannelIds.Contains(x.SourceChatId) && x.DownloadStatus == DownloadStatus.Downloading) //this should not happen, but it's better to check
                                            .Include(x => x.SourceChat)
                                            .Include(x => x.TelegramMessage)
                                            .ToList();
            if (downloadsNotStopped.HasElements())
            {
                foreach (var download in downloadsNotStopped)
                {
                    download.DownloadStatus = DownloadStatus.Error;
                    download.LastUpdate = DateTime.UtcNow;
                }
                _dbContext.SaveChanges();

                fileInError.AddRange(downloadsNotStopped);
            }
            var messagesByChannel = fileInError.GroupBy(x => x.SourceChatId).Select(x => new { x.Key, messages = x.ToList() }).ToList();

            List<MessageBase> messageList = new List<MessageBase>();
            foreach (var message in messagesByChannel)
            {
                var sourceChannel = message.messages.First().SourceChat;
                Messages_MessagesBase? messages;
                try
                {
                    messages = await _client.GetChannelMessagesbyIds(sourceChannel.ChatId, sourceChannel.AccessHash, message.messages.Select(x => x.TelegramMessage.MessageId).ToList());
                }
                catch (Exception ex)
                {
                    _log.Error("Could not fetch message of failed/incompleted downloads", ex);
                    continue;
                }

                if (messages is null)
                {
                    _log.Error("GetChannelMessagesbyIds returned null value, probably connection was not actually established");
                    continue;
                }

                messageList.AddRange(messages.Messages);
            }

            //It sucks but i don't know if a better solution exist.
            _inError = new ConcurrentDictionary<int, Document>(messageList.ToDictionary(x => (x as Message).id, x => (Document)((x as Message).media as MessageMediaDocument).document));
        }

        private void DownloadProgressCallback(long transmitted, long totalSize, TelegramMediaDocument dbFile, TGDownDBContext tempDBContext, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException("Download has been cancelled as per request", cancellationToken);
            }

            dbFile.DataTransmitted = transmitted;
            dbFile.LastUpdate = DateTime.UtcNow;
            tempDBContext.SaveChanges();
            decimal percentage = decimal.Divide(transmitted, totalSize) * 100;
            bool found = _downloadProgress.TryGetValue(dbFile.TelegramMessageId, out decimal oldPercentage);
            if (!found || (percentage - oldPercentage > Convert.ToDecimal(1.5)))
            {
                //_log.Info($"found={found} oldPercentage={oldPercentage:F} diff={percentage - oldPercentage} convert={Convert.ToDecimal(0.5)}");
                _downloadProgress.AddOrUpdate(dbFile.TelegramMessageId, percentage, (key, old) => percentage);
                _log.Info($"{percentage:F} - Downloading file {dbFile.FileName}: {transmitted}/{totalSize}");
                return;
            }
            //if (percentage - oldPercentage < Convert.ToDecimal(0.5))
            //{
            //    _downloadProgress.AddOrUpdate(dbFile.TelegramMessageId, percentage, (key, old) => percentage);
            //    _log.Info($"{percentage:F} - Downloading file {dbFile.FileName}: {transmitted}/{totalSize}");
            //}

        }

        private async Task DownloadEpisode(Document doc, FileStream fileStream, TelegramMessage dbMessage, TelegramMediaDocument dbFile, CancellationToken cancellationToken)
        {
            bool downloadResult;
            _inProgress.TryAdd(dbMessage.MessageId, doc);
            string path = fileStream.Name;
            using (var tempScope = _serviceProvider.CreateScope())
            using (var tempDBContext = tempScope.ServiceProvider.GetRequiredService<TGDownDBContext>())
            {
                var tempDBMessage = tempDBContext.TelegramMessages.First(x => x.TelegramMessageId == dbMessage.TelegramMessageId);
                var tempDBFile = tempDBContext.TelegramMediaDocuments.First(x => x.TelegramFileId == dbFile.TelegramFileId);
                try
                {
                    int time = Random.Shared.Next(3000, 11000);
                    await Task.Delay(time);
                    downloadResult = await _client.DownloadFileAsync(doc, fileStream, (transmitted, totalSize) => { DownloadProgressCallback(transmitted, totalSize, tempDBFile, tempDBContext, cancellationToken); });
                }
                catch (Exception ex)
                {
                    _log.Error($"An error occured downloading file {doc.Filename} to {fileStream.Name}", ex);

                    tempDBFile.DownloadStatus = DownloadStatus.Error;
                    if (cancellationToken.IsCancellationRequested)
                    {
                        tempDBFile.ErrorType = DownloadErrorType.Cancelled;
                    }
                    else if (_client.IsConnected)
                    {
                        tempDBFile.ErrorType = DownloadErrorType.NetworkIssue;
                    }
                    else
                    {
                        tempDBFile.ErrorType = DownloadErrorType.Other;
                    }
                    tempDBFile.LastUpdate = DateTime.UtcNow;
                    tempDBContext.SaveChanges();

                    _inProgress.TryRemove(new KeyValuePair<int, Document>(tempDBMessage.MessageId, doc));
                    _inError.TryAdd(tempDBMessage.MessageId, doc);
                    fileStream.Dispose();
                    try
                    {
                        File.Delete(path);
                    }
                    catch (Exception delete)
                    {
                        _log.Error("Unable to delete file for failed download", delete);
                    }
                    return;
                }

                _inProgress.TryRemove(new KeyValuePair<int, Document>(tempDBMessage.MessageId, doc));

                if (!downloadResult)
                {
                    _log.Error($"An error occured downloading file {doc.Filename} to {fileStream.Name}: Client was disconnected");
                    tempDBFile.DownloadStatus = DownloadStatus.Error;
                    tempDBFile.ErrorType = DownloadErrorType.NetworkIssue;
                    tempDBFile.LastUpdate = DateTime.UtcNow;

                    _inError.TryAdd(tempDBMessage.MessageId, doc);
                    tempDBContext.SaveChanges();

                    fileStream.Dispose();
                    try
                    {
                        File.Delete(path);
                    }
                    catch (Exception delete)
                    {
                        _log.Error("Unable to delete file for failed download", delete);
                    }

                    return;
                }

                tempDBFile.DownloadStatus = DownloadStatus.Success;
                tempDBFile.ErrorType = null;
                tempDBFile.LastUpdate = DateTime.UtcNow;
                tempDBContext.SaveChanges();
                fileStream.Dispose();
                _log.Info($"Download of file {doc.Filename} to {fileStream.Name} completed");
            }
        }

        [GeneratedRegex(@"#ep[0-9]{1,3}", RegexOptions.IgnoreCase, "it-IT")]
        private static partial Regex EpRegex();

        [GeneratedRegex(@"\..*", RegexOptions.IgnoreCase | RegexOptions.RightToLeft, "it-IT")]
        private static partial Regex FileExtensionRegex();

        private void RefreshDBChannelFromCache()
        {
            var cachedChannels = _client.GetCachedChats().Where(c => c is Channel).Select(c => c as Channel).ToList();
            var availableChannelIds = _dbContext.TelegramChannels.Select(c => c.ChatId).ToList();

            var newChannels = cachedChannels.Where(c => !availableChannelIds.Contains(c.ID))
                                            .Select(c => new TelegramChannel(c.ID, c.access_hash, c.Title, false))
                                            .ToList();
            if (newChannels.HasElements())
            {
                _dbContext.AddRange(newChannels);
                _dbContext.SaveChanges();
            }
        }
        #endregion
    }
}

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
        private readonly TelegramClient _client;
        private readonly ILogger<ScheduledTaskService> _log;
        private readonly IServiceProvider _serviceProvider;
        private int _loginConnectionAttemps = 1;
        private ConfigParameterService _configParameterService;
        private bool _startupRetrieve = true;

        private ConcurrentDictionary<int, Document> _inProgress; //the key is the messageId
        private ConcurrentDictionary<int, Document> _inError; //the key is the messageId

        public TgDownloadManagerTask(ILogger<TgDownloadManagerTask> logger, IServiceProvider serviceProvider, TelegramClient client) : base(logger, serviceProvider)
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
                return; //I will start doing stuff at the next run
            }

            if (_startupRetrieve)
            {
                _startupRetrieve = false;
                await RetrieveFailedOrIncompleteDownloads();
            }

            //List<Task> tasks = [Task.Run(() => HandleChannelUpdates(cancellationToken), CancellationToken.None),
            //    Task.Run(() => HandleDownloadInError(cancellationToken), CancellationToken.None)];

            //await Task.WhenAll(tasks);
            HandleChannelUpdates(cancellationToken);
            HandleDownloadInError(cancellationToken);

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

                var enabledChannels = _dbContext.TelegramChannels.Where(c => c.AutoDownloadEnabled).ToList();
                if (!enabledChannels.HasElements()) { continue; }

                Channel channel = fileUpdate.Channel;
                TelegramChannel? channelConfig = enabledChannels.Where(c => c.ChatId == channel.ID && c.AccessHash == channel.access_hash).FirstOrDefault();
                if (channelConfig is null)
                {
                    _log.Warning($"Channel {channel} was not found in database, adding it to DB in order to confirm it");
                    TelegramChannel telegramChannel = new(channel.ID, channel.access_hash, channel.Title, null, false);
                    _dbContext.TelegramChannels.Add(telegramChannel);
                    _dbContext.SaveChanges();
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

                string filename = channelConfig.FileNameTemplate + "_" + epNumber + extension;
                string filePath = _configParameterService.GetValue(ParameterNames.DefaultDownloadLocation) + filename;
                FileStream? fileStream;
                try
                {
                    fileStream = File.Create(filePath);
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
                    fileStream = File.Create(filePath);
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

        #endregion

        #region Util Methods

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

            //if (transmitted > 10000000) //just for test purpose
            //{
            //    throw new OperationCanceledException("Exception Test");
            //}

            dbFile.DataTransmitted = transmitted;
            dbFile.LastUpdate = DateTime.UtcNow;
            tempDBContext.SaveChanges();

            decimal percentage = decimal.Divide(transmitted, totalSize) * 100;
            _log.Info($"{percentage:F} - Downloading file {dbFile.FileName}: {transmitted}/{totalSize}");

        }

        private async Task DownloadEpisode(Document doc, FileStream fileStream, TelegramMessage dbMessage, TelegramMediaDocument dbFile, CancellationToken cancellationToken)
        {
            bool downloadResult;
            _inProgress.TryAdd(dbMessage.MessageId, doc);
            string path = fileStream.Name;
            using (var tempScope = _serviceProvider.CreateScope())
            using (var tempDBContext = tempScope.ServiceProvider.GetService<TGDownDBContext>())
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

        #endregion
    }
}

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

        public override void Setup(TGDownDBContext _dbContext)
        {
            _configParameterService = _serviceScope.ServiceProvider.GetRequiredService<ConfigParameterService>();
            _inError = new ConcurrentDictionary<int, Document>();
            _inProgress = new ConcurrentDictionary<int, Document>();
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
                    download.LastUpdate = DateTime.Now;
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

        public async override Task Run(CancellationToken cancellationToken)
        {
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

            List<Task> tasks = [Task.Run(() => HandleChannelUpdates(cancellationToken), CancellationToken.None),
                Task.Run(() => HandleDownloadInError(cancellationToken), CancellationToken.None)];

            await Task.WhenAll(tasks);
        }

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
                    _log.Warning($"Channel {channel} was not found in database");
                    continue;
                }

                Message message = fileUpdate.Message;
                if (message.media is not MessageMediaDocument)
                {
                    _log.Debug($"Message seems to not be a MessageMediaDocument. It's {message.media.GetType().Name}");
                    continue;
                }
                Match match = EpRegex().Match(message.message);
                if (match.Success)
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

                match = FileExtensionRegex().Match(doc.Filename);

                string filePath = _configParameterService.GetValue(ParameterNames.DefaultDownloadLocation) + channelConfig.FileNameTemplate + "_" + epNumber + match.Value;
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
                TelegramMessage telegramMessage = new TelegramMessage();
                telegramMessage.MessageId = message.id;

                TelegramMediaDocument episode = new TelegramMediaDocument();
                episode.SourceChatId = channelConfig.TelegramChatId;
                episode.DownloadStatus = DownloadStatus.Downloading;
                episode.Size = doc.size;
                episode.FileId = doc.ID;
                episode.AccessHash = doc.access_hash;
                episode.TelegramMessage = telegramMessage;

                _dbContext.TelegramMessages.Add(telegramMessage);
                _dbContext.TelegramMediaDocuments.Add(episode);

                _dbContext.SaveChanges();

                _ = DownloadEpisode(doc, fileStream, telegramMessage, episode, cancellationToken);
            }
        }

        private void HandleDownloadInError(CancellationToken cancellationToken)
        {

        }

        private async Task DownloadEpisode(Document doc, FileStream fileStream, TelegramMessage dbMessage, TelegramMediaDocument dbFile, CancellationToken cancellationToken)
        {
            bool downloadResult;
            _inProgress.TryAdd(dbMessage.MessageId, doc);
            try
            {
                downloadResult = await _client.DownloadFileAsync(doc, fileStream);
            }
            catch (Exception ex)
            {
                _log.Error($"An error occured downloading file {doc.Filename} to {fileStream.Name}", ex);

                dbFile.DownloadStatus = DownloadStatus.Error;
                if (cancellationToken.IsCancellationRequested)
                {
                    dbFile.ErrorType = DownloadErrorType.Cancelled;
                }
                else if (_client.IsConnected)
                {
                    dbFile.ErrorType = DownloadErrorType.NetworkIssue;
                }
                else
                {
                    dbFile.ErrorType = DownloadErrorType.Other;
                }
                dbFile.LastUpdate = DateTime.Now;
                _dbContext.SaveChanges();

                _inProgress.TryRemove(new KeyValuePair<int, Document>(dbMessage.MessageId, doc));
                _inError.TryAdd(dbMessage.MessageId, doc);

                return;
            }

            _inProgress.TryRemove(new KeyValuePair<int, Document>(dbMessage.MessageId, doc));

            if (!downloadResult)
            {
                _log.Error($"An error occured downloading file {doc.Filename} to {fileStream.Name}: Client was disconnected");
                dbFile.DownloadStatus = DownloadStatus.Error;
                dbFile.ErrorType = DownloadErrorType.NetworkIssue;
                dbFile.LastUpdate = DateTime.Now;

                _inError.TryAdd(dbMessage.MessageId, doc);
                _dbContext.SaveChanges();

                return;
            }

            dbFile.DownloadStatus = DownloadStatus.Success;
            dbFile.ErrorType = null;
            dbFile.LastUpdate = DateTime.Now;
            _dbContext.SaveChanges();

            _log.Info($"Download of file {doc.Filename} to {fileStream.Name} completed");
        }

        //private struct FileTask
        //{
        //    public Document File { get; set; }
        //    public Task? DownloadTask { get; set; }
        //}

        [GeneratedRegex(@"#ep[0-9]{1,3}", RegexOptions.IgnoreCase, "it-IT")]
        private static partial Regex EpRegex();

        [GeneratedRegex(@"\..*", RegexOptions.IgnoreCase | RegexOptions.RightToLeft, "it-IT")]
        private static partial Regex FileExtensionRegex();
    }
}

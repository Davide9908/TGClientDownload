using System.Collections.Concurrent;
using TGClientDownloadWorkerService.Configuration;
using TGClientDownloadWorkerService.Extensions;
using TL;
using WTelegram;

namespace TGClientDownloadWorkerService.Services
{
    public class TelegramClient : IDisposable
    {
        private readonly ILogger<TelegramClient> _log;
        private readonly IServiceProvider _serviceProvider;
        private readonly IServiceScope _serviceScope;
        private readonly TGAuthenticationSettings _configuration;

        private Client _tgClient;
        private SemaphoreSlim _semaphoreConnect;
        private SemaphoreSlim _semaphoreDisconnect;
        private CancellationToken? _token;
        private readonly bool _isDev;

        private ConcurrentQueue<ChannelFileUpdate> _channelFileUpdates;



        public TelegramClient(ILogger<TelegramClient> log, IServiceProvider serviceProvider, IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _log = log;
            _serviceScope = serviceProvider.CreateScope();
            ConfigParameterService config = _serviceScope.ServiceProvider.GetService<ConfigParameterService>();
            _configuration = config.GetTGAuthenticationSettings();
            _isDev = _serviceScope.ServiceProvider.GetService<IHostEnvironment>().IsDevelopment();
            _semaphoreConnect = new SemaphoreSlim(1);
            _semaphoreDisconnect = new SemaphoreSlim(1);
            _token = null;
            _tgClient = new Client(ClientConfig);
            _channelFileUpdates = new ConcurrentQueue<ChannelFileUpdate>();
        }

        #region Utils

        public ConcurrentQueue<ChannelFileUpdate> GetChannelFileUpdatesQueue() => _channelFileUpdates;

        public void Dispose()
        {
            Disconnect();
            _serviceScope.Dispose();
        }
        private string? ClientConfig(string what)
        {
            switch (what)
            {
                case "api_id": return _configuration.ApiId;
                case "api_hash": return _configuration.ApiHash;
                case "session_pathname": return _isDev ? "G:\\Projects\\TGClientDownload\\TGClientDownload\\bin\\WTelegram.session" : _configuration.SessionPath;
                case "phone_number": return _configuration.PhoneNumber;
                //case "verification_code": Console.Write("Code: "); return Console.ReadLine();
                //case "first_name": return "John";      // if sign-up is required
                //case "last_name": return "Doe";        // if sign-up is required
                case "password": return _configuration.Password;     // if user has enabled 2FA
                default: return null;                  // let WTelegramClient decide the default config
            }
        }
        public CancellationToken? CancellationToken
        {
            get
            {
                return _token;
            }
            set
            {
                if (_token is null)
                {
                    _token = value;
                }
            }
        }

        public bool IsConnected => _tgClient.User is not null;

        public async Task<User?> Connect()
        {
            User? loggedUser = null;
            //Evito che piu task possano effettuare il login
            await _semaphoreConnect.WaitAsync();
            if (IsConnected)
            {
                return _tgClient.User;
            }
            else
            {
                _tgClient.Dispose();
            }

            _tgClient = new Client(ClientConfig);
            _tgClient.OnUpdate += Client_OnUpdate;
            _tgClient.OnOther += Client_OnOther;
            try
            {
                loggedUser = await _tgClient.LoginUserIfNeeded();
            }
            catch (Exception ex)
            {
                _log.Info("Unable to login or connect to Telegram", ex);
            }
            finally
            {
                _semaphoreConnect.Release();
            }

            return loggedUser;
        }

        public void Disconnect()
        {
            _semaphoreDisconnect.Wait();
            if (_tgClient is null || _tgClient.Disconnected)
            {
                return;
            }
            _tgClient.Dispose();
            try
            {
                _log.Info("Telegram client has been disconnected");
            }
            catch (AggregateException ex)
            {
                Console.WriteLine("Telegram client has been disconnected, but seems IServiceProvider has been already disposed", ex);
            }
            _semaphoreDisconnect.Release();
        }

        #endregion

        #region Methods

        private static void ProgressCallback(long transmitted, long totalSize, int id, CancellationToken? cancellationToken, ILogger log)
        {
            if (cancellationToken.HasValue && cancellationToken.Value.IsCancellationRequested)
            {
                throw new OperationCanceledException("Download is being cancelled as per request", cancellationToken.Value);
            }
            double percentageUnround = ((double)transmitted / (double)totalSize) * 100;
            int percentage = Convert.ToInt32(Math.Round(percentageUnround));
            Console.WriteLine($"ID: {id}{Environment.NewLine}Percentage: {percentage}% - {transmitted}/{transmitted}");
        }

        /// <summary>
        /// Read history of channel
        /// </summary>
        /// <param name="channel">The channel to set as read</param>
        /// <param name="messageId">Message's id to set as read</param>
        public async void ReadChannelHistory(Channel channel, int messageId)
        {
            int time = Random.Shared.Next(2000, 10000);
            await Task.Delay(time);
            await _tgClient.Channels_ReadHistory(channel, messageId);
        }

        /// <summary>
        /// Download file from Telegram and automatically catch any exception.
        /// </summary>
        /// <param name="document">The file to download from Telegram</param>
        /// <param name="outputStream">Output file stream where to save the data. If exception is thown dispose is handled automatically</param>
        /// <param name="progress">Process callback method</param>
        /// <exception cref="Exception"></exception>
        /// <returns>true if the client is connected and the download succeded, otherwise false</returns>
        public async Task<bool> TryDownloadFileAsync(Document document, FileStream outputStream, Client.ProgressCallback? progress = null)
        {
            if (_tgClient.Disconnected) { return false; }

            try
            {
                await DownloadFileAsync(document, outputStream, progress);
                return true;
            }
            catch (Exception ex)
            {
                _log.Error($"An error occured while downloading file {document.Filename} to {outputStream.Name}", ex);
                outputStream.Flush();
                outputStream.Dispose();
                return false;
            }
        }

        /// <summary>
        /// Download file from Telegram
        /// </summary>
        /// <param name="document">The file to download from Telegram</param>
        /// <param name="outputStream">Output file stream where to save the data</param>
        /// <param name="progress">Process callback method</param>
        /// <exception cref="Exception"/>
        /// <returns>false if client is not connected otherwise true</returns>
        public async Task<bool> DownloadFileAsync(Document document, FileStream outputStream, Client.ProgressCallback? progress = null)
        {
            if (_tgClient.Disconnected) { return false; }

            await _tgClient.DownloadFileAsync(document, outputStream, null, progress);
            outputStream.Flush();
            outputStream.Dispose();

            return true;
        }

        public Task<Messages_MessagesBase?> GetChannelMessagesbyIds(long channelId, long channelAccessHash, List<int> messageIds)
        {
            if (_tgClient.Disconnected) { return null; }

            InputPeerChannel inputPeerChannel = new InputPeerChannel(channelId, channelAccessHash);
            List<InputMessageID> inputMessageIDs = new List<InputMessageID>();
            foreach (var messageId in messageIds)
            {
                InputMessageID id = new InputMessageID();
                id.id = messageId;
                inputMessageIDs.Add(id);
            }

            return _tgClient.GetMessages(inputPeerChannel, inputMessageIDs.ToArray());
        }

        #endregion

        #region OnUpdate-OnOther
        private async Task Client_OnUpdate(UpdatesBase updates)
        {
            Dictionary<long, User> users = [];
            Dictionary<long, ChatBase> chats = [];
            updates.CollectUsersChats(users, chats);

            //I have no idea if they are actually needed, but since they were in the examples, i'll keep this if-else
            if (updates is UpdateShortMessage usm && !users.ContainsKey(usm.user_id))
            {
                (await _tgClient.Updates_GetDifference(usm.pts - usm.pts_count, usm.date, 0)).CollectUsersChats(users, chats);
            }
            else if (updates is UpdateShortChatMessage uscm && (!users.ContainsKey(uscm.from_id) || !chats.ContainsKey(uscm.chat_id)))
            {
                (await _tgClient.Updates_GetDifference(uscm.pts - uscm.pts_count, uscm.date, 0)).CollectUsersChats(users, chats);
            }

            foreach (var update in updates.UpdateList)
            {
                switch (update)
                {
                    case UpdateNewChannelMessage ncm:
                        chats.TryGetValue(ncm.message.Peer.ID, out var chat);

                        var message = ((Message)ncm.message);
                        Channel channel = (Channel)chat;

                        var channelUpdate = new ChannelFileUpdate();
                        channelUpdate.Channel = channel;
                        channelUpdate.Message = message;

                        ReadChannelHistory(channel, message.ID);

                        _channelFileUpdates.Enqueue(channelUpdate);

                        _log.Info($"Update has been received from channel {channel.Title}, id {channel.ID}, hash {channel.access_hash} with message {message.message}");



                        break;
                    //case UpdateNewMessage unm: await HandleMessage(unm.message); break;
                    //case UpdateEditMessage uem: await HandleMessage(uem.message, true); break;
                    //// Note: UpdateNewChannelMessage and UpdateEditChannelMessage are also handled by above cases
                    //case UpdateDeleteChannelMessages udcm: Console.WriteLine($"{udcm.messages.Length} message(s) deleted in {Chat(udcm.channel_id)}"); break;
                    //case UpdateDeleteMessages udm: Console.WriteLine($"{udm.messages.Length} message(s) deleted"); break;
                    //case UpdateUserTyping uut: Console.WriteLine($"{User(uut.user_id)} is {uut.action}"); break;
                    //case UpdateChatUserTyping ucut: Console.WriteLine($"{Peer(ucut.from_id)} is {ucut.action} in {Chat(ucut.chat_id)}"); break;
                    //case UpdateChannelUserTyping ucut2: Console.WriteLine($"{Peer(ucut2.from_id)} is {ucut2.action} in {Chat(ucut2.channel_id)}"); break;
                    //case UpdateChatParticipants { participants: ChatParticipants cp }: Console.WriteLine($"{cp.participants.Length} participants in {Chat(cp.chat_id)}"); break;
                    //case UpdateUserStatus uus: Console.WriteLine($"{User(uus.user_id)} is now {uus.status.GetType().Name[10..]}"); break;
                    //case UpdateUserName uun: Console.WriteLine($"{User(uun.user_id)} has changed profile name: {uun.first_name} {uun.last_name}"); break;
                    //case UpdateUser uu: Console.WriteLine($"{User(uu.user_id)} has changed infos/photo"); break;
                    default:
                        Console.WriteLine(update.GetType().Name);
                        _log.Debug($"Unmanaged update received: {update.GetType().Name}");
                        break; // there are much more update types than the above example cases
                }
            }
        }
        private async Task Client_OnOther(IObject arg)
        {
            if (arg is ReactorError err)
            {
                // typically: network connection was totally lost
                _log.Error("Fatal reactor error", err.Exception);
                while (true)
                {
                    _log.Error("Disposing the client and trying to reconnect in 5 seconds...");
                    _tgClient.Dispose();
                    await Task.Delay(5000);
                    try
                    {
                        _tgClient = new Client(ClientConfig);
                        _tgClient.OnUpdate += Client_OnUpdate;
                        _tgClient.OnOther += Client_OnOther;
                        await _tgClient.LoginUserIfNeeded();
                        break;
                    }
                    catch (Exception ex)
                    {
                        _log.Error("Connection still failing", ex);
                    }
                }
            }
            else
            {
                _log.Warning($"Client_OnOther: Other - {arg.GetType().Name}");
            }
        }
        #endregion
    }

    public struct ChannelFileUpdate
    {
        public Channel Channel { get; set; }
        public Message Message { get; set; }
    }
}

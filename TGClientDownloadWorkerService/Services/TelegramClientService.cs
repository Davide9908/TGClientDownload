using System.Collections.Concurrent;
using System.Text;
using TGClientDownloadDAL.Entities;
using TGClientDownloadWorkerService.Configuration;
using TGClientDownloadWorkerService.Extensions;
using TL;
using WTelegram;

namespace TGClientDownloadWorkerService.Services
{
    public class TelegramClientService : IDisposable
    {
        private readonly ILogger<TelegramClientService> _log;
        private readonly IServiceProvider _serviceProvider;
        private readonly IServiceScope _serviceScope;
        private readonly TGAuthenticationSettings _configuration;

        private Client _tgClient;
        private SemaphoreSlim _semaphoreConnect;
        private SemaphoreSlim _semaphoreDisconnect;
        private CancellationToken? _token;
        private readonly bool _isDev;
        private const string UPDATE_FILE = "updates.save";

        private ConcurrentQueue<ChannelFileUpdate> _channelFileUpdates;
        private List<ChatBase> _allChats = [];
        private UpdateManager _updateManager;

        private readonly StreamWriter WTelegramLogs;


        public TelegramClientService(ILogger<TelegramClientService> log, IServiceProvider serviceProvider, IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _log = log;
            _serviceScope = serviceProvider.CreateScope();
            ConfigParameterService config = _serviceScope.ServiceProvider.GetService<ConfigParameterService>();
            _configuration = config.GetTGAuthenticationSettings();
            string? logPath = config.GetValue(ParameterNames.WTelegramClientLogPath);
            if (!string.IsNullOrWhiteSpace(logPath))
            {
                WTelegramLogs = new StreamWriter(logPath, true, Encoding.UTF8) { AutoFlush = true };
                Helpers.Log += (lvl, str) => WTelegramLogs.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{"TDIWE!"[lvl]}] {str}");
            }
            _isDev = _serviceScope.ServiceProvider.GetService<IHostEnvironment>().IsDevelopment();
            _semaphoreConnect = new SemaphoreSlim(1);
            _semaphoreDisconnect = new SemaphoreSlim(1);
            _token = null;
            _tgClient = new Client(ClientConfig);
            _tgClient.MaxAutoReconnects = 0;
            _channelFileUpdates = new ConcurrentQueue<ChannelFileUpdate>();
        }

        #region Utils

        public ConcurrentQueue<ChannelFileUpdate> GetChannelFileUpdatesQueue()
        {
            return _channelFileUpdates;
        }

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
                case "verification_code": Console.Write("Code: "); return Console.ReadLine();
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

        public bool IsConnected => _tgClient?.User is not null;

        public async Task<User?> Connect(bool throwIfError = false, bool dispose = true)
        {
            User? loggedUser = null;
            //Evito che piu task possano effettuare il login
            try
            {
                await _semaphoreConnect.WaitAsync();
                if (IsConnected)
                {
                    return _tgClient.User;
                }
                else if (dispose)
                {
                    _tgClient?.Dispose();
                }

                _tgClient = new Client(ClientConfig);
                _tgClient.MaxAutoReconnects = 0;
                _updateManager = _tgClient.WithUpdateManager(Client_OnUpdate, UPDATE_FILE);

                //_tgClient.OnUpdates += Client_OnUpdate;
                _tgClient.OnOther += Client_OnOther;

                await _tgClient.ConnectAsync();
                loggedUser = await _tgClient.LoginUserIfNeeded();

                var dialogs = await _tgClient.Messages_GetAllDialogs(); // dialogs = groups/channels/users
                dialogs.CollectUsersChats(_updateManager.Users, _updateManager.Chats);
            }
            catch (Exception ex)
            {
                if (throwIfError) { throw; }
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
            _updateManager.SaveState(UPDATE_FILE);
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

        public Task SendMessageToChat(InputPeer peer, string message, bool markdown = false)
        {
            if (markdown)
            {
                var entities2 = _tgClient.MarkdownToEntities(ref message);
                return _tgClient.SendMessageAsync(peer, message, entities: entities2);
            }
            return _tgClient.SendMessageAsync(peer, message);
        }
        
        [Obsolete]
        public async Task LoadAllChats()
        {

            var chats = await _tgClient.Messages_GetAllChats();
            _allChats = chats.chats.Values.ToList();
        }

        public ChatBase? GetCachedChatById(long chatId)
        {
            _updateManager.Chats.TryGetValue(chatId, out var chat);
            return chat;
            //return _allChats?.FirstOrDefault(c => c.ID == chatId);
        }
        public List<ChatBase> GetCachedChats() => _updateManager.Chats.Values.ToList();

        public async Task<MessageBase[]> GetChannelHistory(InputPeer channelPeer)
        {
            return (await _tgClient.Messages_GetHistory(channelPeer)).Messages;
        }

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

            try
            {
                await _tgClient.Channels_ReadHistory(new InputChannel(channel.id, channel.access_hash), messageId);
            }
            catch (RpcException ex)
            {
                _log.Error("An error occured on ReadChannelHistory", ex);
            }
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
        private Task Client_OnUpdate(Update update)
        {
            Dictionary<long, User> users = [];
            Dictionary<long, ChatBase> chats = [];
            //update.CollectUsersChats(users, chats);


            switch (update)
            {
                case UpdateNewChannelMessage ncm:
                    _updateManager.Chats.TryGetValue(ncm.message.Peer.ID, out var chat);
                    //(ncm.message.Peer as PeerChannel).channel_id
                    //chats.Values.Where(x=>x.ID )
                    var message = ((Message)ncm.message);
                    Channel channel = (Channel)chat;
                    if (channel.flags.HasFlag(Channel.Flags.min)) //I cannot trust channel with min flag, the access_hash may not be correct
                    {
                        _log.Info($"Channel {channel.Title}, id {channel.ID} received from the update has min flag. I'll try to get access_hash from cached chats");
                        long? accessHash = (GetCachedChatById(channel.ID) as Channel)?.access_hash;
                        if (accessHash is not null)
                        {
                            channel.access_hash = accessHash.Value;
                        }
                        else
                        {
                            _log.Info($"Channel {channel.Title}, id {channel.ID} not found in cached chats. I'll try to mark as read, but it will probably fail");
                        }
                    }

                    var channelUpdate = new ChannelFileUpdate(channel, message, channel.flags.HasFlag(Channel.Flags.min));
                    //channelUpdate.Channel = channel;
                    //channelUpdate.Message = message;
                    //channelUpdate.SusChannel = channel.flags.HasFlag(Channel.Flags.min);

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
                    //Console.WriteLine(update.GetType().Name);
                    _log.Debug($"Unmanaged update received: {update.GetType().Name}");
                    break; // there are much more update types than the above example cases
            }

            return Task.CompletedTask;
        }

        [Obsolete]
        private async Task Client_OnUpdate(UpdatesBase updates)
        {
            Dictionary<long, User> users = [];
            Dictionary<long, ChatBase> chats = [];
            updates.CollectUsersChats(users, chats);

            foreach (var update in updates.UpdateList)
            {
                switch (update)
                {
                    case UpdateNewChannelMessage ncm:
                        chats.TryGetValue(ncm.message.Peer.ID, out var chat);
                        //(ncm.message.Peer as PeerChannel).channel_id
                        //chats.Values.Where(x=>x.ID )
                        var message = ((Message)ncm.message);
                        Channel channel = (Channel)chat;
                        if (channel.flags.HasFlag(Channel.Flags.min)) //I cannot trust channel with min flag, the access_hash may not be correct
                        {
                            _log.Info($"Channel {channel.Title}, id {channel.ID} received from the update has min flag. I'll try to get access_hash from cached chats");
                            long? accessHash = (GetCachedChatById(channel.ID) as Channel)?.access_hash;
                            if (accessHash is not null)
                            {
                                channel.access_hash = accessHash.Value;
                            }
                            else
                            {
                                _log.Info($"Channel {channel.Title}, id {channel.ID} not found in cached chats. I'll try to mark as read, but it will probably fail");
                            }
                        }

                        var channelUpdate = new ChannelFileUpdate(channel, message, channel.flags.HasFlag(Channel.Flags.min));

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
                        //Console.WriteLine(update.GetType().Name);
                        _log.Debug($"Unmanaged update received: {update.GetType().Name}");
                        break; // there are much more update types than the above example cases
                }
            }
        }
        private async Task Client_OnOther(IObject arg)
        {
            switch (arg)
            {
                case ReactorError err:
                    // typically: network connection was totally lost
                    _log.Error("Fatal reactor error", err.Exception);
                    while (true)
                    {
                        _log.Error("Disposing the client and trying to reconnect in 5 seconds...");
                        _updateManager.SaveState(UPDATE_FILE);
                        _updateManager = null;
                        _tgClient.Dispose();
                        _tgClient = null;
                        await Task.Delay(5000);
                        try
                        {
                            var user = await Connect(true, false);
                            _log.Info($"connected with user {user?.ID} - {user?.MainUsername}");
                            //_tgClient = new Client(ClientConfig);
                            ////_tgClient.OnUpdate += Client_OnUpdate;
                            //_tgClient.MaxAutoReconnects = 0;
                            //_tgClient.WithUpdateManager(Client_OnUpdate, UPDATE_FILE);
                            //_tgClient.OnOther += Client_OnOther;
                            //await _tgClient.LoginUserIfNeeded();
                            break;
                        }
                        catch (Exception ex)
                        {
                            _log.Error("Connection still failing", ex);
                        }
                    }

                    break;
                case Pong pong:
                    break;
                default:
                    _log.Warning($"Client_OnOther: Other - {arg.GetType().Name}");
                    break;
            }
        }
        #endregion
    }

    public record ChannelFileUpdate
    {
        public Channel Channel { get; set; }
        public Message Message { get; set; }
        public bool SusChannel { get; set; }

        public ChannelFileUpdate(Channel channel, Message message, bool isSus)
        {
            Channel = channel;
            Message = message;
            SusChannel = isSus;
        }
    }
}

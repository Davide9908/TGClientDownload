using System.Collections.Concurrent;
using TGClientDownloadDAL;
using TGClientDownloadWorkerService.Configuration;
using TGClientDownloadWorkerService.Extensions;
using TL;
using WTelegram;

namespace TGClientDownloadWorkerService.Services
{
    public class TelegramClient
    {
        private readonly ILogger<TelegramClient> _log;
        private readonly IServiceProvider _serviceProvider;
        private TGDownDBContext _dbContext;
        private IServiceScope _serviceScope;
        private Client _tgClient;
        private static AppSettings _configuration = new AppSettings();

        private SemaphoreSlim _semaphoreConnect;
        private SemaphoreSlim _semaphoreDisconnect;
        private CancellationToken? _token;

        public ConcurrentQueue<ChannelFileUpdate> _channelFileUpdates;



        public TelegramClient(ILogger<TelegramClient> log, IServiceProvider serviceProvider, IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _log = log;
            _serviceScope = serviceProvider.CreateScope();
            _dbContext = _serviceScope.ServiceProvider.GetRequiredService<TGDownDBContext>();
            configuration.GetRequiredSection("AppSettings").Bind(_configuration);
            _semaphoreConnect = new SemaphoreSlim(1);
            _semaphoreDisconnect = new SemaphoreSlim(1);
            _token = null;
            _tgClient = new Client(ClientConfig);
            _channelFileUpdates = new ConcurrentQueue<ChannelFileUpdate>();
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

        public bool IsConnected()
        {
            return !_tgClient.Disconnected;
        }

        public async Task<User?> Connect()
        {
            User? loggedUser = null;
            //Evito che piu task possano effettuare il login
            await _semaphoreConnect.WaitAsync();
            if (!_tgClient.Disconnected)
            {
                return _tgClient.User;
            }
            else
            {
                _tgClient.Dispose();
            }

            _tgClient = new Client(ClientConfig);
            _tgClient.OnOther += Client_OnOther;
            try
            {
                loggedUser = await _tgClient.LoginUserIfNeeded();
            }
            catch (Exception ex)
            {
                _log.LogInformation(ex, "Unable to login or connect to Telegram");
            }
            finally
            {
                _semaphoreConnect.Release();
            }

            return loggedUser;
        }


        public async Task Disconnect()
        {
            await _semaphoreDisconnect.WaitAsync();
            if (_tgClient == null || _tgClient.Disconnected)
            {
                return;
            }
            _tgClient.Dispose();
            _semaphoreDisconnect.Release();
        }

        public async Task DownloadFile(InputFileLocationBase inputFileLocation, FileStream outputstream, int fileId, int dc_id = 0, long fileSize = 0L)
        {
            if (_tgClient.Disconnected)
            {
                ///todo il download del file nel db va rimesso in errore o qualunque altro stato che indichi che è da rifare 
            }
            try
            {
                await _tgClient.DownloadFileAsync(inputFileLocation, outputstream, dc_id, fileSize, (transmitted, totalSize) => { ProgressCallback(transmitted, totalSize, fileId, _token, _log); });
            }
            catch (Exception ex)
            {
                _log.LogError(ex, $"An error occured while downloading a file {outputstream.Name}");
            }
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
        private async void ReadChannelHistory(Channel channel, int messageId)
        {
            int time = Random.Shared.Next(2000, 10000);
            await Task.Delay(time);
            await _tgClient.Channels_ReadHistory(channel, messageId);
        }
        private async Task Client_OnUpdate(UpdatesBase updates)
        {
            Dictionary<long, User> users = new Dictionary<long, User>();
            Dictionary<long, ChatBase> chats = new Dictionary<long, ChatBase>();
            updates.CollectUsersChats(users, chats);
            if (updates is UpdateShortMessage usm && !users.ContainsKey(usm.user_id))
                (await _tgClient.Updates_GetDifference(usm.pts - usm.pts_count, usm.date, 0)).CollectUsersChats(users, chats);
            else if (updates is UpdateShortChatMessage uscm && (!users.ContainsKey(uscm.from_id) || !chats.ContainsKey(uscm.chat_id)))
                (await _tgClient.Updates_GetDifference(uscm.pts - uscm.pts_count, uscm.date, 0)).CollectUsersChats(users, chats);
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

                        _channelFileUpdates.Enqueue(channelUpdate);

                        _log.Info($"Update from channel {channel.Title}, id {channel.ID}, hash {channel.access_hash} with message {message.message}");

                        ReadChannelHistory(channel, message.ID);

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
                    default: Console.WriteLine(update.GetType().Name); break; // there are much more update types than the above example cases
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
                _log.Error($"Client_OnOther: Other - {arg.GetType().Name}");
            }
        }
        public void StopClient()
        {
            Dispose();
        }

        private void Dispose()
        {
            _dbContext.Dispose();
            _serviceScope.Dispose();
        }
        private string? ClientConfig(string what)
        {
            switch (what)
            {
                case "api_id": return _configuration?.AuthenticationSettings?.ApiId;
                case "api_hash": return _configuration?.AuthenticationSettings?.ApiHash;
                case "session_pathname": return _configuration?.AuthenticationSettings?.SessionPath;
                case "phone_number": return _configuration?.AuthenticationSettings?.PhoneNumber;
                //case "verification_code": Console.Write("Code: "); return Console.ReadLine();
                //case "first_name": return "John";      // if sign-up is required
                //case "last_name": return "Doe";        // if sign-up is required
                case "password": return _configuration?.AuthenticationSettings?.Password;     // if user has enabled 2FA
                default: return null;                  // let WTelegramClient decide the default config
            }
        }
    }
    public struct ChannelFileUpdate
    {
        public Channel Channel { get; set; }
        public Message Message { get; set; }
    }
}

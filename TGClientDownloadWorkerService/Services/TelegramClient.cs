using TGClientDownloadDAL;
using TGClientDownloadWorkerService.Configuration;
using TL;
using WTelegram;

namespace TGClientDownloadWorkerService.Services
{
    public class TelegramClient
    {
        private readonly ILogger _log;
        private readonly IServiceProvider _serviceProvider;
        private TGDownDBContext _dbContext;
        private IServiceScope _serviceScope;
        private Client _tgClient;
        private static AppSettings _configuration;

        private SemaphoreSlim _semaphoreConnect;
        private SemaphoreSlim _semaphoreDisconnect;
        private CancellationToken? _token;



        public TelegramClient(ILogger log, IServiceProvider serviceProvider, IConfigurationManager configuration)
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
        }

        public CancellationToken? CancellationToken
        {
            get
            {
                return _token;
            }
            set
            {
                if (_token == null)
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
        private async Task Client_OnOther(IObject arg)
        {
            if (arg is ReactorError err)
            {
                // typically: network connection was totally lost
                _log.LogError(err.Exception, "Fatal reactor error");
                while (true)
                {
                    _log.LogError("Disposing the client and trying to reconnect in 5 seconds...");
                    _tgClient.Dispose();
                    await Task.Delay(5000);
                    try
                    {
                        _tgClient = new Client(ClientConfig);
                        break;
                    }
                    catch (Exception ex)
                    {
                        _log.LogError(ex, "Connection still failing");
                    }
                }
            }
            else
            {
                _log.LogError($"Client_OnOther: Other - {arg.GetType().Name}");
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
}

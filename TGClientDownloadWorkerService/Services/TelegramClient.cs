using TGClientDownloadDAL;
using TGClientDownloadWorkerService.Configuration;
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

        public TelegramClient(ILogger log, IServiceProvider serviceProvider, IConfigurationManager configuration)
        {
            _serviceProvider = serviceProvider;
            _log = log;
            _serviceScope = serviceProvider.CreateScope();
            _dbContext = _serviceScope.ServiceProvider.GetRequiredService<TGDownDBContext>();
            configuration.GetRequiredSection("AppSettings").Bind(_configuration);
            _semaphoreConnect = new SemaphoreSlim(1);
            _semaphoreDisconnect = new SemaphoreSlim(1);
        }
        public bool IsConnected()
        {
            return _tgClient != null && !_tgClient.Disconnected;
        }

        public async Task<TL.User?> Connect()
        {
            TL.User? loggedUser = null;
            //Evito che piu task possano effettuare il login
            await _semaphoreConnect.WaitAsync();
            if (_tgClient != null)
            {
                if (!_tgClient.Disconnected)
                {
                    return _tgClient.User;
                }

                _tgClient.Dispose();
            }
            _tgClient = new Client(ClientConfig);
            try
            {
                var user = await _tgClient.LoginUserIfNeeded();
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

        public void StopClient()
        {
            Dispose();
        }

        private void Dispose()
        {
            _dbContext.Dispose();
            _serviceScope.Dispose();
        }
        private static string? ClientConfig(string what)
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

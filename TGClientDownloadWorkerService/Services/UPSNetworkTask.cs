using NUTDotNetClient;
using TGClientDownloadDAL.Entities;
using TGClientDownloadWorkerService.Extensions;
using TL;

namespace TGClientDownloadWorkerService.Services
{
    public class UPSNetworkTask : ScheduledTaskService
    {
        private readonly ILogger<UPSNetworkTask> _log;
        private readonly IServiceProvider _serviceProvider;
        private NUTClient? _nutClient;
        private readonly TelegramClientService _tgClient;
        private string _upsIpAddress;
        private string _upsName;
        private string _upsStatusVariable;
        private long _upsAlarmUserId;
        private long _upsAlarmUserHash;
        private string _lastStatusValue = string.Empty;
        private const string Status_On_Line = "OL";
        private const string Status_On_Battery = "OB";
        public UPSNetworkTask(ILogger<UPSNetworkTask> logger, IServiceProvider serviceProvider, TelegramClientService tgClient) : base(logger, serviceProvider)
        {
            _log = logger;
            _serviceProvider = serviceProvider;
            _tgClient = tgClient;
        }

        public override void Setup()
        {
            using var scope = _serviceProvider.CreateScope();

            var configuration = scope.ServiceProvider.GetRequiredService<ConfigParameterService>();
            string? nasIp = configuration.GetValue(ParameterNames.UPSIpAddress);
            string? upsName = configuration.GetValue(ParameterNames.UPSName);
            string? upsStatus = configuration.GetValue(ParameterNames.UPSName);

            long? alarmUserId = configuration.GetValue<long>(ParameterNames.UPSAlarmUserId);
            long? alarmUserHash = configuration.GetValue<long>(ParameterNames.UPSAlarmUserId);

            if (nasIp is null || upsName is null || upsStatus is null || alarmUserId is null || alarmUserHash is null)
            {
                _log.Error("UPSNetworkTask missing");
                return;
            }
            _upsIpAddress = nasIp;
            _upsName = upsName;
            _upsStatusVariable = upsStatus;

            _upsAlarmUserId = alarmUserId.Value;
            _upsAlarmUserHash = alarmUserHash.Value;

            _nutClient = new NUTClient(_upsIpAddress);
            _nutClient.Connect();
        }

        public async override Task Run(CancellationToken cancellationToken)
        {
            if (_nutClient is null)
            {
                return;
            }
            if (!_nutClient.IsConnected)
            {
                _nutClient.Connect();
                if (!_nutClient.IsConnected)
                {
                    return;
                }
            }

            ClientUPS? ups = _nutClient.GetUPSes().FirstOrDefault(u => u.Name == _upsName);
            if (ups is null)
            {
                _log.Error($"UPS with name {_upsName} has not been found");
                return;
            }

            ups.GetVariables(true);
            var statusVariable = ups.GetVariableByName(_upsStatusVariable);
            if (statusVariable is null)
            {
                _log.Error($"UPS status variable {_upsStatusVariable} not found");
                return;
            }
            if (string.IsNullOrEmpty(_lastStatusValue))
            {
                _lastStatusValue = statusVariable.Value;
                return;
            }

            if (statusVariable.Value != _lastStatusValue)
            {
                switch (statusVariable.Value)//OB (on battery) OL (on line)
                {
                    case Status_On_Line:
                        await _tgClient.SendMessageToChat(new InputPeerUser(_upsAlarmUserId, _upsAlarmUserHash), "*Alimentazione Ripristinata!*", true);
                        break;
                    case Status_On_Battery:
                        await _tgClient.SendMessageToChat(new InputPeerUser(_upsAlarmUserId, _upsAlarmUserHash), "*Attenzione, Rilevata mancanza corrente!!!!*", true);
                        break;
                    default:
                        _log.Warning($"Unknown status value {statusVariable.Value}");
                        break;
                }
                _lastStatusValue = statusVariable.Value;
            }
        }

        public override void Cleanup()
        {
            _nutClient?.Dispose();
        }
    }
}

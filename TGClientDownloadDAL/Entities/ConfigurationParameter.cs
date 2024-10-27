using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TGClientDownloadDAL.SupportClasses;

namespace TGClientDownloadDAL.Entities
{
    [Table("system_ConfigParam")]
    public class ConfigurationParameter
    {
        public ConfigurationParameter() { }

        public int ConfigurationParameterId { get; set; }

        [Required]
        public string ParameterName { get; set; }

        public string ParameterValue { get; set; }

        public ConfigurationParameterType ParameterType { get; set; }
    }


    public static class ParameterNames
    {
        public const string DefaultDownloadLocation = nameof(DefaultDownloadLocation);
        public const string TGApiId = nameof(TGApiId);
        public const string TGApiHash = nameof(TGApiHash);
        public const string SessionPath = nameof(SessionPath);
        public const string PhoneNumber = nameof(PhoneNumber);
        public const string Password = nameof(Password);
        public const string WTelegramClientLogPath = nameof(WTelegramClientLogPath);
        public const string LastChatsRefresh = nameof(LastChatsRefresh);
        public const string MALApiLink = nameof(MALApiLink);
        public const string MALApiID = nameof(MALApiID);
        public const string MALUsername = nameof(MALUsername);
        public const string RefreshDBChannels = nameof(RefreshDBChannels);
        public const string UPSIpAddress = nameof(UPSIpAddress);
        public const string UPSName = nameof(UPSName);
        public const string UPSStatusVariable = nameof(UPSStatusVariable);
        public const string UPSAlarmUserId = nameof(UPSAlarmUserId);
        public const string UPSAlarmUserHash = nameof(UPSAlarmUserHash);
    }
}

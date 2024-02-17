using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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

    public enum ConfigurationParameterType
    {
        SysReserved = 0,
        Int,
        String,
        Bool,
        DateTime,
        Decimal
    }

    public static class ParameterNames
    {
        public const string DefaultDownloadLocation = nameof(DefaultDownloadLocation);
        public const string TGApiId = nameof(TGApiId);
        public const string TGApiHash = nameof(TGApiHash);
        public const string SessionPath = nameof(SessionPath);
        public const string PhoneNumber = nameof(PhoneNumber);
        public const string Password = nameof(Password);
    }
}

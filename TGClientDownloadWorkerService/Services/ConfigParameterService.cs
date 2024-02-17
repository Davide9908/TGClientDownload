using System.ComponentModel;
using TGClientDownloadDAL;
using TGClientDownloadDAL.Entities;
using TGClientDownloadWorkerService.Configuration;

namespace TGClientDownloadWorkerService.Services
{
    public class ConfigParameterService
    {
        private readonly TGDownDBContext _dbContext;
        private readonly ILogger<ConfigParameterService> _log;

        public ConfigParameterService(TGDownDBContext tGDownDBContext, ILogger<ConfigParameterService> log)
        {
            _log = log;
            _dbContext = tGDownDBContext;
        }

        public T GetValue<T>(string parameterName, T defaultValue = default)
        {
            var parameterValue = _dbContext.ConfigurationParameters.FirstOrDefault(p => p.ParameterName == parameterName)?.ParameterValue;
            return ConvertValue(parameterValue, defaultValue);
        }
        public string? GetValue(string parameterName)
        {
            return _dbContext.ConfigurationParameters.FirstOrDefault(p => p.ParameterName == parameterName)?.ParameterValue;
        }
        private T ConvertValue<T>(string value, T defaultValue = default)
        {
            return string.IsNullOrWhiteSpace(value) ? defaultValue : (T)TypeDescriptor.GetConverter(typeof(T)).ConvertFromString(value);
        }

        public TGAuthenticationSettings GetTGAuthenticationSettings()
        {
            var codes = new List<string>() { ParameterNames.TGApiId, ParameterNames.TGApiHash, ParameterNames.SessionPath, ParameterNames.PhoneNumber, ParameterNames.Password };

            var parameters = _dbContext.ConfigurationParameters.Where(p => codes.Contains(p.ParameterName)).ToList();
            return new TGAuthenticationSettings(
                parameters.FirstOrDefault(p => p.ParameterName == ParameterNames.TGApiId)?.ParameterValue,
                parameters.FirstOrDefault(p => p.ParameterName == ParameterNames.TGApiHash)?.ParameterValue,
                parameters.FirstOrDefault(p => p.ParameterName == ParameterNames.SessionPath)?.ParameterValue,
                parameters.FirstOrDefault(p => p.ParameterName == ParameterNames.PhoneNumber)?.ParameterValue,
                parameters.FirstOrDefault(p => p.ParameterName == ParameterNames.Password)?.ParameterValue
                );
        }
    }
}

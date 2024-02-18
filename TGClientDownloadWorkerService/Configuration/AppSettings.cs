
namespace TGClientDownloadWorkerService.Configuration
{
    public class AppSettings
    {
        public string? AppVersion { get; set; }
        public string? Url { get; set; }
    }

    public class TGAuthenticationSettings
    {
        public TGAuthenticationSettings() { }
        public TGAuthenticationSettings(string? apiId, string? apiHash, string? sessionPath, string? phoneNumber, string? password)
        {
            ApiId = apiId;
            ApiHash = apiHash;
            SessionPath = sessionPath;
            PhoneNumber = phoneNumber;
            Password = password;
        }

        public string? ApiId { get; set; }
        public string? ApiHash { get; set; }
        public string? SessionPath { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Password { get; set; }
    }
}

namespace TGClientDownloadWorkerService.Configuration
{
    public class AppSettings
    {
        public AuthenticationSettings? AuthenticationSettings { get; set; }
        public string? AppVersion { get; set; }
        public string? Url { get; set; }
    }

    public class AuthenticationSettings
    {
        public string? Key { get; set; }
    }

    public class Authentication
    {
        public string? GrantType { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
    }
}
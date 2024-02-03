
namespace TGClientDownloadWorkerService.Configuration
{
    public class AppSettings
    {
        public TGAuthenticationSettings AuthenticationSettings { get; set; }
        public string? AppVersion { get; set; }
        public string? Url { get; set; }
    }

    public class TGAuthenticationSettings
    {
        public string? ApiId { get; set; }
        public string? ApiHash { get; set; }
        public string? SessionPath { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Password { get; set; }
    }

    public class Authentication
    {
        public string? GrantType { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
    }
}
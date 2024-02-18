using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using TGClientDownloadDAL;
using TGClientDownloadWorkerService.Configuration;
using TGClientDownloadWorkerService.Extensions;
using TGClientDownloadWorkerService.Services;

namespace TGClientDownloadWorkerService
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("en-US");
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-US");
            Console.OutputEncoding = Encoding.UTF8;

            var builder = Host.CreateDefaultBuilder(args);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                builder = builder.UseSystemd();
            }
            var host = builder.ConfigureAppConfiguration(builder =>
            {
                builder.AddJsonFile("appsettings.json");
            })
            .ConfigureServices((hostContext, services) =>
            {
                services.AddHostedService<TgDownloadManagerTask>();
                services.AddSingleton<TelegramClient>();
                services.AddDbContext<TGDownDBContext>();
                services.AddScoped<ConfigParameterService>();
            })
            .ConfigureLogging(builder =>
            {
                builder.AddDbLogger();
            })
            .Build();
            using (var scope = host.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                var logger = services.GetRequiredService<ILogger<Program>>();
                try
                {
                    AppSettings settings = new AppSettings();
                    var config = new ConfigurationBuilder()
                        .SetBasePath(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location))
                        .AddJsonFile("appsettings.json")
                        .Build();
                    config.GetRequiredSection("AppSettings").Bind(settings);
                    logger.Info($"Starting Telegram Download Version {settings?.AppVersion}");

                    var context = services.GetService<TGDownDBContext>();
                    context.Migrate();
                }
                catch (Exception ex)
                {
                    logger.Error("An error occurred applying migrations", ex);
                }
            }
            if (!args.HasElements() || !args.Contains("--updateDB"))
            {
                host.Run();
            }
        }


    }
}
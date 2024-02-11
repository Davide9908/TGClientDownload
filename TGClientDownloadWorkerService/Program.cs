using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using TGClientDownloadDAL;
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
            //using (var db = new TGDownDBContext())
            //{
            //    db.Migrate();
            //}
            var builder = Host.CreateDefaultBuilder(args);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                builder = builder.UseSystemd();
            }
            builder.ConfigureAppConfiguration(builder =>
            {
                builder.AddJsonFile("appsettings.json");
            })

            .ConfigureServices((hostContext, services) =>
            {
                services.AddHostedService<TgDownloadManagerTask>();
                services.AddSingleton<TelegramClient>();
                services.AddDbContext<TGDownDBContext>();
            })
            .ConfigureLogging(builder =>
            {
                builder.AddDbLogger();
            })
            .Build()
            .Run();
        }

    }
}
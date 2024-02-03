using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using TGClientDownloadDAL;
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
            builder.ConfigureAppConfiguration(builder =>
            {
                builder.AddJsonFile("appsettings.json");
            })
            .ConfigureServices((hostContext, services) =>
            {
                services.AddHostedService<DownloadRestarter>();
                services.AddDbContext<TGDownDBContext>();
            })
            .Build()



            .Run();


        }

    }
}
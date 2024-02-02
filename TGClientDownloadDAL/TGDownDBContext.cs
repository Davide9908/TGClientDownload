using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Reflection;
using TGClientDownloadDAL.Entities;

namespace TGClientDownloadDAL
{
    public class TGDownDBContext : DbContext
    {
        protected readonly IConfiguration Configuration;

        public TGDownDBContext(IConfiguration configuration)
        {
            Configuration = configuration;
        }
        public TGDownDBContext()
        {

        }
        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            if (Configuration != null)
            {
                Console.WriteLine("sono qua");
                options.UseNpgsql(Configuration.GetConnectionString("TgDownDB"));
                return;
            }

            var ManualConfiguration = new ConfigurationBuilder()
            .SetBasePath(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location))
            .AddJsonFile("appsettings.json")
            .Build();

            options.UseNpgsql(ManualConfiguration.GetConnectionString("TgDownDB"));
        }

        public virtual DbSet<TgChannel> TgChannels { get; set; }
        public virtual DbSet<ScheduledTask> ScheduledTasks { get; set; }
    }
}

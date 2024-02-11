using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Reflection;
using TGClientDownloadDAL.Entities;

namespace TGClientDownloadDAL
{
    public class TGDownDBContext : DbContext
    {
        protected readonly IConfiguration Configuration;

        //public TGDownDBContext(IConfiguration configuration)
        //{
        //    Configuration = configuration;
        //}
        public TGDownDBContext()
        {

        }
        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            if (Configuration != null)
            {
                options.UseNpgsql(Configuration.GetConnectionString("TgDownDB"));
                return;
            }

            var ManualConfiguration = new ConfigurationBuilder()
            .SetBasePath(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location))
            .AddJsonFile("appsettings.json")
            .Build();

            options.UseNpgsql(ManualConfiguration.GetConnectionString("TgDownDB"));
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TelegramFile>()
                .HasIndex(p => new { p.FileId, p.AccessHash })
                .IsUnique(true);

            modelBuilder.Entity<TelegramChat>()
                .HasIndex(p => new { p.ChatId, p.AccessHash })
                .IsUnique(true);
        }

        public void Migrate()
        {
            if (Database.GetPendingMigrations().Any())
            {
                Database.Migrate();
            }
        }
        public virtual DbSet<TelegramChannel> TgChannels { get; set; }
        public virtual DbSet<TelegramChat> TelegramChats { get; set; }
        public virtual DbSet<TelegramFile> TelegramFiles { get; set; }
        public virtual DbSet<TelegramMediaDocument> TelegramMediaDocuments { get; set; }
        public virtual DbSet<ScheduledTask> ScheduledTasks { get; set; }
    }
}

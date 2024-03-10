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
                options.UseNpgsql(Configuration.GetConnectionString("TgDownDB"), x => x.MigrationsAssembly(Assembly.GetExecutingAssembly().GetName().Name));
                return;
            }

            var ManualConfiguration = new ConfigurationBuilder()
            .SetBasePath(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location))
            .AddJsonFile("appsettingsDAL.json")
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

            modelBuilder.Entity<ConfigurationParameter>()
                .HasIndex(p => new { p.ParameterName })
            .IsUnique(true);

            modelBuilder.Entity<TelegramFile>(entity =>
            {
                entity.HasKey(z => z.TelegramFileId);
                entity.HasOne(p => p.TelegramMessage);
            });

            modelBuilder.Entity<AnimeEpisodesSetting>(entity =>
            {
                entity.HasKey(z => z.TelegramChannelId);
                entity.HasOne(p => p.TelegramChannel);
            });
        }

        public void Migrate()
        {
            if (Database.GetPendingMigrations().Any())
            {
                Database.Migrate();
            }
        }

        public virtual DbSet<TelegramChannel> TelegramChannels { get; set; }
        public virtual DbSet<TelegramChat> TelegramChats { get; set; }
        public virtual DbSet<TelegramFile> TelegramFiles { get; set; }
        public virtual DbSet<TelegramMediaDocument> TelegramMediaDocuments { get; set; }
        public virtual DbSet<TelegramMessage> TelegramMessages { get; set; }
        public virtual DbSet<ScheduledTask> ScheduledTasks { get; set; }
        public virtual DbSet<ConfigurationParameter> ConfigurationParameters { get; set; }
        public virtual DbSet<AnimeEpisodesSetting> AnimeEpisodesSettings { get; set; }


    }
}

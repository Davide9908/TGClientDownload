using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TGClientDownloadDAL.Entities
{
    [Table(nameof(AnimeEpisodesSetting))]
    public class AnimeEpisodesSetting
    {
        [Key, ForeignKey(nameof(TelegramChannel))]
        public int TelegramChannelId { get; set; }
        public TelegramChannel TelegramChannel { get; set; }

        public string? FileNameTemplate { get; set; }

        public int? MALAnimeId { get; set; }

        public string? AnimeFolderPath { get; set; }

        public bool DownloadLastEpisode { get; set; }
    }
}

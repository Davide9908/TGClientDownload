using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TGClientDownloadDAL.Entities
{
    [Table(nameof(TelegramFile))]
    public abstract class TelegramFile
    {
        [Key]
        public int TelegramFileId { get; set; }

        public long FileId { get; set; }

        public long AccessHash { get; set; }
    }
}

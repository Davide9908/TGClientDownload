using System.ComponentModel.DataAnnotations.Schema;
using TGClientDownloadDAL.SupportClasses;

namespace TGClientDownloadDAL.Entities
{
    [Table(nameof(TelegramMediaDocument))]
    public class TelegramMediaDocument : TelegramFile
    {
        [ForeignKey(nameof(SourceChat))]
        public int SourceChatId { get; set; }
        public TelegramChat SourceChat { get; set; }

        public string FileName { get; set; }

        public long Size { get; set; }

        public long DataTransmitted { get; set; }

        public DateTime LastUpdate { get; set; }

        public DownloadStatus DownloadStatus { get; set; }

        public DownloadErrorType? ErrorType { get; set; }

        public TelegramMediaDocument()
        {
            DataTransmitted = 0;
            LastUpdate = DateTime.UtcNow;
            ErrorType = null;
        }

    }


}

using System.ComponentModel.DataAnnotations.Schema;

namespace TGClientDownloadDAL.Entities
{
    [Table(nameof(TelegramChannel))]
    public class TelegramChannel : TelegramChat
    {
        public string ChannelName { get; set; }

        public bool AutoDownloadEnabled { get; set; }

        public string FileNameTemplate { get; set; }

        public ChannelStatus Status { get; set; }

        public TelegramChannel()
        {

        }
        public TelegramChannel(long id, long accessHash, string channelName, string fileNameTemplate, bool enableAutoDownload)
        {
            ChatId = id;
            AccessHash = accessHash;
            ChannelName = channelName;
            FileNameTemplate = fileNameTemplate;
            AutoDownloadEnabled = enableAutoDownload;
            Status = ChannelStatus.ToConfirm;
        }
    }

    public enum ChannelStatus
    {
        SysReserved = 0,
        ToConfirm,
        Active,
        Obsolete
    }
}

using System.ComponentModel.DataAnnotations.Schema;

namespace TGClientDownloadDAL.Entities
{
    [Table(nameof(TelegramChannel))]
    public class TelegramChannel : TelegramChat
    {
        public string ChannelName { get; set; }

        public bool AutoDownloadEnabled { get; set; }

        public ChannelStatus Status { get; set; }

        public AnimeEpisodesSetting AnimeEpisodesSetting { get; set; }

        public TelegramChannel()
        {

        }
        public TelegramChannel(long id, long accessHash, string channelName, bool enableAutoDownload)
        {
            ChatId = id;
            AccessHash = accessHash;
            ChannelName = channelName;
            AutoDownloadEnabled = enableAutoDownload;
            Status = ChannelStatus.ToConfirm;
        }

        public override string ToString()
        {
            return string.Join(" - ", base.ToString(), ChannelName);
        }
    }

    public enum ChannelStatus
    {
        SysReserved = 0,
        ToConfirm,
        Active,
        Obsolete,
        AccessHashToVerify
    }
}

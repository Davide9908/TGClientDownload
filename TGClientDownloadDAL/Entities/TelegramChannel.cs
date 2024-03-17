using System.ComponentModel.DataAnnotations.Schema;
using TGClientDownloadDAL.SupportClasses;

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


}

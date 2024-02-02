using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace TGClientDownloadDAL.Entities
{
    [Table(nameof(TgChannel))]
    [PrimaryKey(nameof(ChannelId), nameof(AccessHash))]
    public class TgChannel
    {
        public int ChannelId { get; set; }

        public long AccessHash { get; set; }

        public string ChannelName { get; set; }

        public bool Enabled { get; set; }

        public TgChannel()
        {

        }
        public TgChannel(int channelId, long accessHash, string channelName)
        {
            ChannelId = channelId;
            AccessHash = accessHash;
            ChannelName = channelName;
        }
    }
}

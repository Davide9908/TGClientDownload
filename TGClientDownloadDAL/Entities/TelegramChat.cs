using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TGClientDownloadDAL.Entities
{
    /// <summary>
    ///  This is the generic chat (channel/group/user chat)  for chats with users, use TgUserChat
    /// </summary>

    [Table(nameof(TelegramChat))]
    public abstract class TelegramChat
    {
        [Key]
        public int TelegramChatId { get; set; }

        public long ChatId { get; set; }

        public long AccessHash { get; set; }

        public List<TelegramMediaDocument> MediaDocuments { get; set; }
    }
}

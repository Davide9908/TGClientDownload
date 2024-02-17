using System.ComponentModel.DataAnnotations.Schema;

namespace TGClientDownloadDAL.Entities
{
    [Table(nameof(TelegramMessage))]
    public class TelegramMessage
    {
        /// <summary>
        /// The id of the table. Do not use with tg cllient
        /// </summary>
        public int TelegramMessageId { get; set; }

        /// <summary>
        /// The actual tg id
        /// </summary>
        public int MessageId { get; set; }

        public TelegramFile? Document { get; set; }
    }
}

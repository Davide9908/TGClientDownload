
using System.ComponentModel.DataAnnotations.Schema;

namespace TGClientDownloadDAL.Entities
{
    [Table(nameof(ScheduledTask))]
    public class ScheduledTask
    {
        public int ScheduledTaskId { get; set; }
        public string TasksName { get; set; }
        public DateTime LastStart { get; set; }
        public DateTime LastFinish { get; set; }
        public bool Enabled { get; set; }
        public int Interval { get; set; }
        public bool IsRunning { get; set; }

        public ScheduledTask()
        {

        }
    }
}

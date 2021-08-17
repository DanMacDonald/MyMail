using System.IO;
using MimeKit;

namespace MyMail.Models
{
    public class InboxItemRecord
    {
        public int Id { get; set; }
        public string To { get; set; }
        public string From { get; set; }
        public string Subject { get; set; }
        public string Date { get; set; }
        public string ContentType { get; set; }
        public bool IsSeen { get; set; }
        public bool IsFlagged { get; set; }
        public bool IsRecent { get; set; }
    }
}
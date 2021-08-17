using System.IO;
using MimeKit;

namespace MyMail.Models
{
    public struct MimePartRecord
    {
        public string MimeType;
        public ContentEncoding ContentTransferEncoding;
        public byte[] Bytes;
        public MimeMessage MimeMessage;
        public string DataOld;
        private string stringData;
        public string StringData
        {
            get
            {
                // TODO: move to utility class
                if (stringData == null && Bytes.Length > 0)
                {
                    var stream = new MemoryStream(Bytes);
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        stringData = reader.ReadToEnd();
                    }
                }
                return stringData;
            }
        }
        public string From;
        public string To;
        public string Subject;
        public string Date;
        public bool IsMessageHeader;
        public bool IsHeader;
        public bool IsAttachment;
        public int PartKey;
        public int PartOrder;
        public int PartId;
    };
}
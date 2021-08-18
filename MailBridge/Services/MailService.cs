using MyMail.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using MailKit.Net.Smtp;
using MailKit;
using MimeKit;
using MailKit.Security;
using Npgsql;
using System.IO;
using MimeKit.Encodings;
using System.Text;

namespace MyMail.Services
{
    public static class MailService
    {
        /// <summary>
        /// Send a simple plaintext SMTP mail
        /// </summary>
        /// <param name="mail">A populate SmtpMail instance</param>
        public static void SendSMTPMail(SmtpMail mail)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(mail.FromName, mail.FromAddress));
            message.To.Add(new MailboxAddress(mail.ToName, mail.ToAddress));
            message.Subject = mail.Subject;

            message.Body = new TextPart("plain")
            {
                Text = mail.Body
            };

            using (var client = new SmtpClient())
            {
                client.Connect("mailserver.pixelsamurai.com", 587, false);

                // Note: only needed if the SMTP server requires authentication
                client.Authenticate("admin", "EqRa8D7gB7TmLRHApBX8zxP");
                client.Send(message);
                client.Disconnect(true);
            }
        }

        public static List<InboxItemRecord> GetInboxItems(string emailAddress)
        {
            var cs = Startup.ConnectionString;
            using var con = new NpgsqlConnection(cs);
            con.Open();

            // This query alone is enough to justify an entire rewrite without
            // dbmail
            var sql = $@"SELECT * FROM
            (SELECT mailbox_idnr, physmessage_id, seen_flag, seq, flagged_flag, recent_flag FROM public.dbmail_messages where mailbox_idnr = (SELECT mailbox_idnr FROM public.dbmail_mailboxes WHERE owner_idnr = (SELECT user_idnr from public.dbmail_users WHERE userid = 'admin@pixelsamurai.com') AND name = 'INBOX')) as MESSAGES
            LEFT JOIN (SELECT public.dbmail_header.physmessage_id, public.dbmail_headername.headername, public.dbmail_headervalue.headervalue, sortfield FROM (public.dbmail_header LEFT JOIN public.dbmail_headername ON public.dbmail_header.headername_id = public.dbmail_headername.id) LEFT JOIN public.dbmail_headervalue ON public.dbmail_header.headervalue_id = public.dbmail_headervalue.id WHERE public.dbmail_headerName.id in (2,3,6,8,9)) AS HEADERS ON MESSAGES.physmessage_id = HEADERS.physmessage_id";

            using var cmd = new NpgsqlCommand(sql, con);
            using NpgsqlDataReader rdr = cmd.ExecuteReader();

            var records = new Dictionary<int, InboxItemRecord>();

            while (rdr.Read())
            {
                var messageId = rdr.GetInt32(1);

                if (records.ContainsKey(messageId) == false)
                {
                    var record = new InboxItemRecord()
                    {
                        Id = messageId,
                        IsSeen = rdr.GetInt32(2) == 1,
                        IsFlagged = rdr.GetInt32(4) == 1,
                        IsRecent = rdr.GetInt32(5) == 1
                    };

                    records[messageId] = record;
                }

                var rec = records[messageId];
                var headerName = rdr.GetString(7);
                var headerValue = rdr.GetString(8);

                switch (headerName)
                {
                    case "to": rec.To = headerValue; break;
                    case "from": rec.From = headerValue; break;
                    case "date": rec.Date = headerValue; break;
                    case "content-type": rec.ContentType = headerValue; break;
                    case "subject": rec.Subject = headerValue; break;
                }
                records[messageId] = rec;
            }

            var inboxItems = records.Values.ToList();
            inboxItems = inboxItems.OrderBy(r => r.Id).ToList();
            return inboxItems;
        }

        /// <summary>
        /// Developer sanity function to test the DB
        /// </summary>
        /// <returns>A string with the postgres db version</returns>
        public static string LogDBVersion()
        {
            var cs = Startup.ConnectionString;
            using var con = new NpgsqlConnection(cs);
            con.Open();

            var sql = "SELECT version()";

            using var cmd = new NpgsqlCommand(sql, con);

            var version = cmd.ExecuteScalar().ToString();
            var versionString = $"PostgreSQL version: {version}";
            Console.WriteLine(versionString);
            return versionString;
        }

        /// <summary>
        /// Given an initialized MimePartRecord that contains the mime part bytes
        /// This helper method attemps to parse the bytes into an intialized
        /// MimeMessage instance and extract properties from it, storing them
        /// in the provided record.
        /// </summary>
        /// <param name="record">An initialized MimePartRecord</param>
        /// <returns>TRUE if parsing was successful, FALSE otherwise</returns>
        public static bool TryParseMimeMesasge(ref MimePartRecord record)
        {
            if (record.PartOrder != 0) return false;

            var stream = new MemoryStream(record.Bytes);
            var message = MimeMessage.Load(ParserOptions.Default, stream, true);

            record.From = message.Headers["From"];
            record.To = message.Headers["To"];
            record.Subject = message.Headers["Subject"];
            record.Date = message.Headers["Date"];
            record.IsMessageHeader = !(string.IsNullOrEmpty(record.From) || string.IsNullOrEmpty(record.To));
            record.MimeType = message.Body.ContentType.MimeType;
            record.MimeMessage = message;

            switch (message.Body)
            {
                case TextPart textPart:
                    var contentEncoding = textPart.ContentTransferEncoding;
                    record.ContentTransferEncoding = contentEncoding;
                    break;
            }

            var iter = new MimeIterator(message);
            while (iter.MoveNext())
            {
                var part = iter.Current as MimeEntity;
                record.IsAttachment |= part.IsAttachment;
            }

            return true;
        }

        /// <summary>
        /// Supports parsing individual records returned by the SQL Query in
        /// GetMimePartRecords()
        /// </summary>
        /// <param name="reader"></param>
        /// <returns>An initalized MimePartRecord</returns>
        static MimePartRecord CreateMimePartRecord(NpgsqlDataReader reader)
        {
            MimePartRecord record = new MimePartRecord();
            record.IsHeader = reader.GetInt32(1) == 1;
            record.PartKey = reader.GetInt32(2);
            // PartDepth = reader.GetInt32(3); // Not used.. yet?
            record.PartOrder = reader.GetInt32(4);
            record.PartId = reader.GetInt32(5);
            var size = reader.GetInt32(7);

            record.Bytes = new byte[size];
            var bytesRead = reader.GetBytes(6, 0, record.Bytes, 0, size);
            if (bytesRead != size)
                throw new ArgumentException("Number of bytes read does not match expected size");

            return record;
        }

        /// <summary>
        /// Utility method to extract a list of mime parts for a specific mail
        /// message out of dbmail
        /// </summary>
        /// <param name="messageId">Physical message Id</param>
        /// <returns>A list of MIME parts associated with the message</returns>
        public static List<MimePartRecord> GetMimePartRecords(int messageId)
        {
            var cs = Startup.ConnectionString;
            using var con = new NpgsqlConnection(cs);
            con.Open();

            var sql = $@"SELECT PARTS.*, public.dbmail_mimeparts.data, public.dbmail_mimeparts.size
            FROM (SELECT * FROM public.dbmail_partlists WHERE physmessage_id = @messageId) as PARTS
            LEFT JOIN public.dbmail_mimeparts ON PARTS.part_id = public.dbmail_mimeparts.id
            WHERE size > 0 ORDER BY part_key;";

            using var cmd = new NpgsqlCommand(sql, con);
            cmd.Parameters.AddWithValue("messageId", messageId);
            using NpgsqlDataReader rdr = cmd.ExecuteReader();

            var records = new List<MimePartRecord>();

            while (rdr.Read())
            {
                var partRecord = CreateMimePartRecord(rdr);
                records.Add(partRecord);
            }

            records.OrderBy(pk => pk.PartKey).ThenBy(po => po.PartOrder);
            return records;
        }

        /// <summary>
        /// Initializes a new SmtpMail object with plain text values after
        /// parsing the MIME parts queried from Postgress for the message ID
        /// </summary>
        /// <param name="messageId">physicalMessageId of the desired messsage</param>
        /// <returns>An initalized SmtpMail instance</returns>
        public static SmtpMail GetSmtpMail(int messageId)
        {
            var records = GetMimePartRecords(messageId);

            var mailMessage = new SmtpMail();
            var messageMimeType = "";

            for (int i = 0; i < records.Count; i++)
            {
                var record = records[i];
                if (record.PartOrder == 0 && TryParseMimeMesasge(ref record))
                {
                    // Update the records list
                    records[i] = record;

                    var message = record.MimeMessage;

                    // Is this the header for the message?
                    if (record.IsHeader && record.PartKey == 1)
                    {
                        // TODO: Handle GroupAddress
                        // From:
                        mailMessage.FromName = (message.From[0] as MailboxAddress).Name;
                        mailMessage.FromAddress = $"<{(message.From[0] as MailboxAddress).Address}>";

                        // To:
                        mailMessage.ToName = (message.To[0] as MailboxAddress).Name;
                        mailMessage.ToAddress =  $"<{(message.To[0] as MailboxAddress).Address}>";

                        // Subject:
                        mailMessage.Subject = message.Subject;

                        // MimeType
                        messageMimeType = record.MimeType;
                    }
                }
            }

            switch (messageMimeType)
            {
                case "text/plain":
                    var textPart = records.Where(part => part.MimeType == "text/plain").FirstOrDefault();
                    var textPartData = records.Where(dataPart =>
                        dataPart.PartKey == textPart.PartKey && dataPart.PartOrder == textPart.PartOrder + 1
                    ).FirstOrDefault();
                    mailMessage.Body = textPartData.StringData;
                    Console.WriteLine(textPartData.ContentTransferEncoding);
                    break;
                case "multipart/alternative":
                    // Get the "text/html" part
                    var htmlPart = records.Where(part => part.MimeType == "text/html").FirstOrDefault();
                    var htmlPartData = records.Where(dataPart =>
                        dataPart.PartKey == htmlPart.PartKey && dataPart.PartOrder == htmlPart.PartOrder + 1
                    ).FirstOrDefault();
                    switch (htmlPart.ContentTransferEncoding)
                    {
                        case ContentEncoding.QuotedPrintable:
                            var decoder = new QuotedPrintableDecoder();
                            Encoding Latin1;
                            try
                            {
                                Latin1 = Encoding.GetEncoding(28591, new EncoderExceptionFallback(), new DecoderExceptionFallback());
                            }
                            catch (NotSupportedException)
                            {
                                // Note: Some ASP.NET web hosts such as GoDaddy's Windows environment do not have
                                // iso-8859-1 support, they only have the built-in text encodings, so we need to
                                // hack around it by using an alternative encoding.

                                // Try to use Windows-1252 if it is available...
                                Latin1 = Encoding.GetEncoding(1252, new EncoderExceptionFallback(), new DecoderExceptionFallback());
                            }
                            decoder.Reset();
                            var output = new byte[htmlPartData.Bytes.Length];
                            var n = decoder.Decode(htmlPartData.Bytes, 0, htmlPartData.Bytes.Length, output);
                            mailMessage.Body = Latin1.GetString(output, 0, n);
                            break;
                        default:
                            mailMessage.Body = htmlPartData.StringData;
                            break;
                    }
                    break;
                case "multipart/report":
                    for (int i = 1; i < records.Count; i++)
                    {
                        if (records[i].IsHeader == false)
                        {
                            mailMessage.Body += records[i].StringData;
                        }
                        else if (records[i].IsMessageHeader)
                        {
                            var msgRecord = records[i];
                            mailMessage.Body += $"\nSubject: {msgRecord.Subject}\n";
                            mailMessage.Body += $"From: {msgRecord.From}\n";
                            mailMessage.Body += $"Date: {msgRecord.Date}\n";
                            mailMessage.Body += $"To: {msgRecord.To}\n";
                        }
                    }
                    break;
            }

            return mailMessage;
        }

        /// <summary>
        /// For now just print the MIME parts
        /// </summary>
        public static void GetMIMEParts(int messageId)
        {
            var records = GetMimePartRecords(messageId);
            foreach (var record in records)
            {
                Console.WriteLine("> isHeader:{0}, partKey:{1}, partOrder:{2} part_id:{3}, size:{4}, isAttachment:{5}\n bodyPart:{6}"
                , record.IsHeader
                , record.PartKey
                , record.PartOrder
                , record.PartId
                , record.Bytes.Length
                , record.IsAttachment
                , record.StringData);
            }
        }
    }
}
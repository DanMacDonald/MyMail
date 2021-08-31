using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using MyMail.Models;
using MyMail.Services;

namespace MyMail.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class MailController : ControllerBase
    {
        public MailController()
        {
        }

        // POST action
        [HttpPost]
        public IActionResult SendSMTP(SmtpMail mail)
        {
            MailService.SendSMTPMail(mail);
            // returns HttpCode 200
            return new OkObjectResult(mail);
        }

        // Sanity check Action
        /// <returns></returns>
        [HttpGet]
        public string GetDBVersion()
        {
            return MailService.LogDBVersion();
        }

        private string Encode (byte[] bs)
		{
			if (bs == null || bs.Length == 0)
				return "";
			string encodedStr = System.Convert.ToBase64String (bs, System.Base64FormattingOptions.None);
			encodedStr = encodedStr.Replace ('+', '-').Replace ('/', '_');
			return encodedStr;
		}


        [EnableCors]
        [HttpPost("message/{msgId:int}/flags")]
        public IActionResult SetMessageFlags(int msgId, FlagParams flags) {

            var rowUpdated = MailService.UpdateMessageFlags(msgId, flags);
            return new OkObjectResult(rowUpdated);
        }

        [EnableCors]
        [HttpGet("AuthToken")]
        public async Task<IActionResult> Authenticate(string address) {
            using var client = new HttpClient();
            var txid = await client.GetStringAsync($"https://arweave.net/wallet/{address}/last_tx");
            var transactionJson = await client.GetStringAsync($"https://arweave.net/tx/{txid}");
    
            var decodeString = "";
            using (JsonDocument document = JsonDocument.Parse(transactionJson))
            {
                var ownerElement = document.RootElement.GetProperty("owner");
                string base64String = ownerElement.GetString();
                string incoming = base64String.Replace('_', '/').Replace('-', '+');
                switch(base64String.Length % 4) {
                    case 2: incoming += "=="; break;
                    case 3: incoming += "="; break;
                }

                decodeString = incoming;
            }

            var publicKey = decodeString;
            var xmlPublicKey = $"<RSAPublicKey><Modulus>{publicKey}</Modulus><Exponent>AQAB</Exponent></RSAPublicKey>";

            // var keyBuf = cryptoRandom.GenerateSeed(256);
            // var keyString = Encoding.UTF8.GetString(keyBuf);
            // byte [] salt = Encoding.UTF8.GetBytes(new char [] {'s','a','l','t'});

           // var pbkdf2Buff = PBKDF2_SHA256_GetHash(keyString, salt, 100000, 32);

           string encodedStr = "";

            using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider())
            {
                byte[] cipherbytes;
                rsa.FromXmlString(xmlPublicKey);
                byte[] contentBytes = UTF8Encoding.UTF8.GetBytes("dmac");
                cipherbytes = rsa.Encrypt(contentBytes, true);
                encodedStr = Encode(cipherbytes);
                System.Console.WriteLine(encodedStr);
            }
            
            return new OkObjectResult(encodedStr);
        }


        // Authenticate struct
        public class InboxParams
        {
            public string emailAddress { get; set; }
            public string password { get; set; }
        }

        // Inbox message retrieval
        [EnableCors]
        [HttpPost("inbox")]
        public IActionResult GetInbox(InboxParams credentials)
        {
            var items = MailService.GetInboxItems(credentials.emailAddress);

            // foreach (var item in items)
            // {
            //     System.Console.WriteLine($"to:{item.To} from:{item.From} subject:{item.Subject} contentType:{item.ContentType}");
            // }

            return new OkObjectResult(items);
        }


        // Debuggin Methos: Logs out a messages MIME parts on the server console
        [HttpGet("mimeParts/{msgId:int}")]
        public IActionResult GetMimeParts(int msgId)
        {
            var mimeString = $"msgId:{msgId}";
            MailService.GetMIMEParts(msgId);
            return new OkObjectResult(mimeString);
        }

        // GET action
        // Retrieve a specific message body
        [EnableCors]
        [HttpGet("message/{msgId:int}")]
        public IActionResult GetSmtpMessage(int msgId)
        {
            //var smptMessage = MailService.GetSMTPMail(msgId);
            var smptMessage = MailService.GetSmtpMail(msgId);
            // var smptMessage = "balh";
            // MailService.PrintParts(msgId);
            return new OkObjectResult(smptMessage);
        }
    }
}
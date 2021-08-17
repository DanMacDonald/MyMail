using System.Collections.Generic;
using System.Linq;
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

            foreach (var item in items)
            {
                System.Console.WriteLine($"to:{item.To} from:{item.From} subject:{item.Subject} contentType:{item.ContentType}");
            }

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
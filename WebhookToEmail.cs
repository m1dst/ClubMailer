using CsvHelper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace ClubEmailer
{
    public static class WebhookToEmail
    {
        [FunctionName("WebhookToEmail")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {

            log.LogInformation("Starting...");

            var formData = await req.ReadFormAsync();
            string subject = formData["subject"];
            string messageBody = formData["message"];

            if (string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(messageBody))
            {
                log.LogCritical("Please pass a subject and body to the query string or in the request body.");
                return new BadRequestObjectResult("Please pass a subject and body to the query string or in the request body.");
            }
            else
            {
                log.LogInformation($"Subject: {subject}");
                log.LogInformation($"Message: {messageBody}");
            }

            // Download the membership database.
            log.LogInformation($"Downloading membership list: {Environment.GetEnvironmentVariable("MembershipListUrl")}");

            using (var webClient = new WebClient())
            using (var reader = new StreamReader(webClient.OpenRead(Environment.GetEnvironmentVariable("MembershipListUrl"))))
            using (var csv = new CsvReader(reader))
            {
                csv.Configuration.RegisterClassMap<MemberMap>();
                var members = csv.GetRecords<Member>()
                    .Where(x => x.GDPRConsent)
                    .Where(x => !string.IsNullOrWhiteSpace(x.EmailAddress))
                    .ToList();

                log.LogInformation($"Found {members.Count} members who have consented to being contacted.");

                log.LogInformation($"Sending emails from {Environment.GetEnvironmentVariable("FromEmailAddress")} {Environment.GetEnvironmentVariable("FromName")}.");

                var smtpClient = new SmtpClient(Environment.GetEnvironmentVariable("SmtpServer"))
                {
                    Port = Convert.ToInt32(Environment.GetEnvironmentVariable("SmtpServerPort")),
                    Credentials = new NetworkCredential(Environment.GetEnvironmentVariable("SmtpServerUsername"), Environment.GetEnvironmentVariable("SmtpServerPassword")),
                    EnableSsl = true,
                };

                members.ForEach(member =>
               {
                   try
                   {
                       var msg = new MailMessage
                       {
                           From = new MailAddress(Environment.GetEnvironmentVariable("FromEmailAddress"),
                               Environment.GetEnvironmentVariable("FromName")),
                           Subject = $"SDARC Mailer: {subject}",
                           Body = messageBody,
                           IsBodyHtml = false
                       };

                       msg.To.Add(new MailAddress(member.EmailAddress, $"{member.Name} - {member.Callsign}"));

                       smtpClient.Send(msg);

                       log.LogInformation($"Email was sent to {member.EmailAddress}.");
                   }
                   catch (Exception ex)
                   {
                       log.LogInformation($"There was a problem emailing {member.EmailAddress}.  Exception: {ex.Message}");
                   }

               });
            }

            log.LogInformation("Done.");

            return new JsonResult(new { success = true });
        }
    }
}

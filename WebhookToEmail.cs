using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using CsvHelper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace ClubEmailer
{
    public static class WebhookToEmail
    {
        [FunctionName("WebhookToEmail")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {

            log.LogInformation("Starting...");

            string subject = req.Query["subject"];
            string messageBody = req.Query["message"];

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            subject = subject ?? data?.subject;
            messageBody = messageBody ?? data?.messageBody;

            if (string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(messageBody))
            {
                return new BadRequestObjectResult("Please pass a subject and body to the query string or in the request body");
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

                var client = new SendGridClient(Environment.GetEnvironmentVariable("SendGridApiKey"));

                members.ForEach(async member =>
                {
                    try
                    {
                        var msg = new SendGridMessage();
                        msg.SetFrom(new EmailAddress(Environment.GetEnvironmentVariable("FromEmailAddress"), Environment.GetEnvironmentVariable("FromName")));
                        msg.AddTo(member.EmailAddress, member.Name);
                        msg.SetSubject($"SDARC Mailer: {subject}");
                        msg.AddContent(MimeType.Text, messageBody);
                        var response = await client.SendEmailAsync(msg);
                        log.LogInformation($"Email was sent to {member.EmailAddress}.  Response: {response.Body}");
                    }
                    catch (Exception ex)
                    {
                        log.LogInformation($"There was a problem emailing {member.EmailAddress}.  Exception: {ex.Message}");
                    }

                });
            }

            log.LogInformation("Done.");

            return new OkObjectResult("{success: true}");
        }
    }
}

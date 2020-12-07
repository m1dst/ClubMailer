using CsvHelper;
using Ical.Net;
using Ical.Net.CalendarComponents;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;

namespace ClubEmailer
{
    public static class ProgrammeReminder
    {
        [FunctionName("ProgrammeReminder")]
        public static void Run([TimerTrigger("0 0 12 * * Mon")] TimerInfo myTimer, ILogger log, ExecutionContext context)
        {
            var webClient = new WebClient();
            log.LogInformation($"SDARC Programme Reminder executed at: {DateTime.Now}");
            log.LogInformation($"Calendar: {Environment.GetEnvironmentVariable("CalendarUrl")}");

            var calendar = Calendar.Load(webClient.OpenRead(Environment.GetEnvironmentVariable("CalendarUrl")));
            log.LogInformation($"There are {calendar.Events.Count} events in the calendar");

            var thisWeeksEvents = calendar.GetOccurrences(DateTime.Now, DateTime.Now.AddMonths(1));
            log.LogInformation($"There are {thisWeeksEvents.Count} events coming up...");

            if (thisWeeksEvents.Count > 0)
            {
                // Load the templates
                var eventTemplate = File.ReadAllText(Path.Combine(context.FunctionAppDirectory, "EventTemplate.html"));
                var emailHtmlBody = File.ReadAllText(Path.Combine(context.FunctionAppDirectory, "ThisWeekEmailTemplate.html"));

                var eventsHtml = new List<string>();
                foreach (var occurence in thisWeeksEvents.OrderBy(x => x.Period.StartTime))
                {
                    var sourceEvent = occurence.Source as CalendarEvent;

                    var html = eventTemplate
                        .Replace("[Title]", sourceEvent.Summary)
                        .Replace("[StartDate]", occurence.Period.StartTime.Value.ToString("f"))
                        .Replace("[EndDate]", occurence.Period.EndTime.Value.ToString("f"))
                        .Replace("[Location]", sourceEvent.Location)
                        .Replace("[Description]", sourceEvent.Description.Replace("\n", "<br />"));

                    eventsHtml.Add(html);

                    log.LogInformation($"EVENT:\n\tTitle: {sourceEvent.Summary}\n\tStart: {occurence.Period.StartTime}\n\tEnd: {occurence.Period.EndTime}\n\tLocation: {sourceEvent.Location}\n\t{sourceEvent.Description}");
                }

                emailHtmlBody = emailHtmlBody.Replace("[EventGoesHere]", string.Join("", eventsHtml));

                // Download the membership database.
                log.LogInformation($"Downloading membership list: {Environment.GetEnvironmentVariable("MembershipListUrl")}");

                using (var reader = new StreamReader(webClient.OpenRead(Environment.GetEnvironmentVariable("MembershipListUrl"))))
                using (var csv = new CsvReader(reader))
                {
                    csv.Configuration.RegisterClassMap<MemberMap>();
                    var members = csv.GetRecords<Member>()
                        .Where(x => x.GDPRConsent)
                        .Where(x => !string.IsNullOrWhiteSpace(x.EmailAddress))
                        .ToList();

                    log.LogInformation($"Found {members.Count} members who have consented to being contacted.");

                    var smtpClient = new SmtpClient(Environment.GetEnvironmentVariable("SmtpServer"))
                    {
                        Port = Convert.ToInt32(Environment.GetEnvironmentVariable("SmtpServerPort")),
                        Credentials = new NetworkCredential(Environment.GetEnvironmentVariable("SmtpServerUsername"), Environment.GetEnvironmentVariable("SmtpServerPassword")),
                        EnableSsl = true,
                    };
                    log.LogInformation($"Sending emails from {Environment.GetEnvironmentVariable("FromEmailAddress")} {Environment.GetEnvironmentVariable("FromName")}.");

                    members.ForEach(member =>
                    {
                        try
                        {

                            var msg = new MailMessage
                            {
                                From = new MailAddress(Environment.GetEnvironmentVariable("FromEmailAddress"),
                                    Environment.GetEnvironmentVariable("FromName")),
                                Subject = "Coming Soon at the Swindon & District Amateur Radio Club",
                                Body = emailHtmlBody,
                                IsBodyHtml = true
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

                    log.LogInformation("Done.");

                }

            }


        }
    }
}

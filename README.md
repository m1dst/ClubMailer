# ClubMailer - Azure Function
Automated emailer to send Swindon & District Amateur Radio Club members updates of upcoming events (one month in advance).  Downloads a list of members from a private URL as CSV and uses the clubs ical feed to send members an email with a list of upcoming events.

Only sends to members who have provided GDPR consent.

Saves the club secretary manually having to retrieve the active members and sending the email manually.  Also reduces the risk of errors.

Runs every Monday morning at 6am.
using System.Net;
using System.Net.Mail;

namespace FairShare.Services;

public class SmtpEmailSender(IConfiguration config, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private readonly IConfiguration _config = config;
    private readonly ILogger<SmtpEmailSender> _logger = logger;

    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
    {
        string? host = _config["Email:Smtp:Host"];

        if (string.IsNullOrWhiteSpace(host))
        {
            _logger.LogWarning("SMTP host not configured. Logging email instead. To={To} Subject={Subject}", toEmail, subject);
            _logger.LogInformation("EMAIL Fallback\nTo: {To}\nSubject: {Subject}\nBody:\n{Body}", toEmail, subject, htmlBody);
            return;
        }

        int port = int.TryParse(_config["Email:Smtp:Port"], out int p) ? p : 587;
        bool enableSsl = bool.TryParse(_config["Email:Smtp:EnableSsl"], out bool ssl) ? ssl : true;
        string? user = _config["Email:Smtp:User"];
        string? pass = _config["Email:Smtp:Password"];
        string from = _config["Email:From"] ?? "no-reply@localhost";

        using SmtpClient client = new(host, port)
        {
            EnableSsl = enableSsl
        };

        if (!string.IsNullOrWhiteSpace(user))
        {
            client.Credentials = new NetworkCredential(user, pass);
        }

        using MailMessage msg = new()
        {
            From = new MailAddress(from),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };

        msg.To.Add(new MailAddress(toEmail));
        await client.SendMailAsync(msg, ct);
    }
}

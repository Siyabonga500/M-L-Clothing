using System.Net;
using ClothingStore.Data;
using ClothingStore.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace ClothingStore.Services;

public record EmailAttachment(string FileName, byte[] Content, string ContentType);

/// <summary>
/// Sends email over SMTP and keeps a copy in the database (Admin → Emails).
/// When SMTP isn't configured the email is only logged, so the app still works.
/// </summary>
public class EmailService
{
    private readonly ApplicationDbContext _db;
    private readonly EmailSettings _settings;
    private readonly ILogger<EmailService> _logger;

    public EmailService(ApplicationDbContext db, IOptions<EmailSettings> settings, ILogger<EmailService> logger)
    {
        _db = db;
        _settings = settings.Value;
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_settings.SmtpHost);

    public async Task SendAsync(string to, string subject, string htmlBody, params EmailAttachment[] attachments)
    {
        var log = new EmailLog { To = to, Subject = subject, Body = htmlBody };

        if (!IsConfigured)
        {
            log.Error = "SMTP not configured – email saved here only.";
            _logger.LogInformation("Email to {To} not sent (SMTP not configured): {Subject}", to, subject);
        }
        else
        {
            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(_settings.FromName,
                    string.IsNullOrEmpty(_settings.FromEmail) ? _settings.Username : _settings.FromEmail));
                message.To.Add(MailboxAddress.Parse(to));
                message.Subject = subject;
                var body = new BodyBuilder { HtmlBody = htmlBody };
                foreach (var a in attachments)
                    body.Attachments.Add(a.FileName, a.Content, ContentType.Parse(a.ContentType));
                message.Body = body.ToMessageBody();

                // Port 465 = SSL from the start; 587 (or others) = upgrade to TLS after connecting (STARTTLS).
                var security = !_settings.EnableSsl ? SecureSocketOptions.None
                    : _settings.SmtpPort == 465 ? SecureSocketOptions.SslOnConnect
                    : SecureSocketOptions.StartTls;

                using var client = new SmtpClient { Timeout = 20000 };
                await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, security);
                if (!string.IsNullOrEmpty(_settings.Username))
                    await client.AuthenticateAsync(_settings.Username, _settings.Password);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);
                log.Sent = true;
            }
            catch (Exception ex)
            {
                var message = Explain(ex);
                log.Error = message.Length > 1000 ? message[..1000] : message;
                _logger.LogError(ex, "Failed to send email to {To}", to);
            }
        }

        _db.EmailLogs.Add(log);
        await _db.SaveChangesAsync();
    }

    /// <summary>Turns common SMTP failures into a plain explanation of how to fix them.</summary>
    private string Explain(Exception ex)
    {
        var text = ex.Message + (ex.InnerException != null ? " " + ex.InnerException.Message : "");
        var gmail = _settings.SmtpHost.Contains("gmail", StringComparison.OrdinalIgnoreCase);
        if (text.Contains("5.7.0") || text.Contains("5.7.8") || text.Contains("Authentication", StringComparison.OrdinalIgnoreCase)
            || text.Contains("not authenticated", StringComparison.OrdinalIgnoreCase))
        {
            return gmail
                ? "Gmail rejected the login. Gmail does not accept your normal password here: turn on 2-Step Verification, " +
                  "create an App Password at https://myaccount.google.com/apppasswords and put its 16 letters (no spaces) in Email:Password. (" + text + ")"
                : "The email server rejected the username or password. (" + text + ")";
        }
        if (text.Contains("10060") || text.Contains("10061") || text.Contains("connection attempt failed", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Cannot connect", StringComparison.OrdinalIgnoreCase) || text.Contains("timed out", StringComparison.OrdinalIgnoreCase))
            return $"Your PC could not connect to {_settings.SmtpHost} on port {_settings.SmtpPort} – something is blocking outgoing email " +
                   "(antivirus mail shield, firewall, or a work/school network). " +
                   (_settings.SmtpPort == 587 ? "Try \"SmtpPort\": 465 instead. " : _settings.SmtpPort == 465 ? "Try \"SmtpPort\": 587 instead. " : "") +
                   "To check, run in PowerShell: Test-NetConnection " + _settings.SmtpHost + " -Port " + _settings.SmtpPort + " (" + text + ")";
        if (text.Contains("could not be resolved", StringComparison.OrdinalIgnoreCase)
            || text.Contains("actively refused", StringComparison.OrdinalIgnoreCase)
            || text.Contains("not known", StringComparison.OrdinalIgnoreCase) || text.Contains("No such host", StringComparison.OrdinalIgnoreCase))
            return "Could not reach the email server. Check SmtpHost/SmtpPort and your internet or firewall. (" + text + ")";
        return text;
    }

    /// <summary>Wraps content in the store's branded email layout.</summary>
    public static string Layout(string storeName, string heading, string bodyHtml) => $"""
        <div style="font-family:Segoe UI,Arial,sans-serif;max-width:560px;margin:auto;border:1px solid #e3e8e7;border-radius:12px;overflow:hidden">
          <div style="background:#183630;color:#fff;padding:20px 24px;font-size:20px;font-weight:600">{WebUtility.HtmlEncode(storeName)}</div>
          <div style="padding:24px;color:#1d2b29;line-height:1.5">
            <h2 style="margin-top:0;color:#023436">{WebUtility.HtmlEncode(heading)}</h2>
            {bodyHtml}
          </div>
          <div style="background:#023436;color:#cfe3df;padding:12px 24px;font-size:12px">Thank you for shopping with {WebUtility.HtmlEncode(storeName)}.</div>
        </div>
        """;
}

using System.Net;
using System.Net.Mail;
using ClothingStore.Data;
using ClothingStore.Models;
using Microsoft.Extensions.Options;

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
                using var message = new MailMessage
                {
                    From = new MailAddress(string.IsNullOrEmpty(_settings.FromEmail) ? _settings.Username : _settings.FromEmail, _settings.FromName),
                    Subject = subject,
                    Body = htmlBody,
                    IsBodyHtml = true
                };
                message.To.Add(to);
                foreach (var a in attachments)
                    message.Attachments.Add(new Attachment(new MemoryStream(a.Content), a.FileName, a.ContentType));

                using var client = new SmtpClient(_settings.SmtpHost, _settings.SmtpPort)
                {
                    EnableSsl = _settings.EnableSsl,
                    Credentials = new NetworkCredential(_settings.Username, _settings.Password)
                };
                await client.SendMailAsync(message);
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
        if (text.Contains("timed out", StringComparison.OrdinalIgnoreCase) || text.Contains("could not be resolved", StringComparison.OrdinalIgnoreCase)
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

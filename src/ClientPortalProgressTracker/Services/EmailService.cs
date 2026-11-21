using System.Net;
using System.Net.Mail;

namespace ClientPortalProgressTracker.Services;

public class EmailService
{
    private readonly IConfiguration _config;

    public EmailService(IConfiguration config)
    {
        _config = config;
    }

    public async Task SendQuoteEmailAsync(
        string clientEmail,
        string clientName,
        string freelancerName,
        string freelancerEmail,
        string businessName,
        string projectTitle,
        string referenceCode,
        decimal totalAmount,
        int depositPct,
        string? messageToClient,
        string portalUrl)
    {
        var host = _config["Smtp:Host"] ?? "://brevo.com";
        var port = int.TryParse(_config["Smtp:Port"], out var p) ? p : 587;
        var user = _config["Smtp:Username"] ?? throw new InvalidOperationException("Smtp:Username not configured.");
        var pass = _config["Smtp:Password"] ?? throw new InvalidOperationException("Smtp:Password not configured.");
        var fromEmail = _config["Smtp:FromEmail"] ?? user;

        using var smtp = new SmtpClient(host, port)
        {
            Credentials = new NetworkCredential(user, pass),
            EnableSsl = true
        };

        var depositAmount = totalAmount * (depositPct / 100m);
        var noteHtml = string.IsNullOrWhiteSpace(messageToClient)
            ? ""
            : $"<div style='background:#F8FAFC;border-left:3px solid #4F46E5;padding:12px 16px;margin:16px 0;color:#334155;'>{WebUtility.HtmlEncode(messageToClient).Replace("\n", "<br/>")}</div>";

        var body = $@"
            <div style='font-family:Inter,Arial,sans-serif;max-width:560px;margin:0 auto;color:#0F172A;'>
                <h2 style='margin-bottom:4px;'>Quote from {WebUtility.HtmlEncode(businessName)}</h2>
                <p style='color:#64748B;margin-top:0;'>Hi {WebUtility.HtmlEncode(clientName)},</p>
                <p>You have received an itemized project quote for <strong>{WebUtility.HtmlEncode(projectTitle)}</strong> (Ref: <strong>{WebUtility.HtmlEncode(referenceCode)}</strong>).</p>
                {noteHtml}
                <table style='width:100%;border-collapse:collapse;margin:16px 0;font-size:14px;'>
                    <tr><td style='padding:8px 0;color:#64748B;border-bottom:1px solid #E2E8F0;'>Total Quote Value</td><td style='padding:8px 0;text-align:right;font-weight:700;border-bottom:1px solid #E2E8F0;'>R {totalAmount:N2}</td></tr>
                    <tr><td style='padding:8px 0;color:#64748B;'>Deposit Required ({depositPct}%)</td><td style='padding:8px 0;text-align:right;font-weight:700;'>R {depositAmount:N2}</td></tr>
                </table>
                <p style='margin:24px 0;'>
                    <a href='{portalUrl}' style='background:#4F46E5;color:#FFFFFF;text-decoration:none;padding:12px 20px;border-radius:8px;font-weight:600;display:inline-block;'>
                        Review &amp; Sign Quote
                    </a>
                </p>
                <p style='font-size:12px;color:#94A3B8;'>Or copy this link: {portalUrl}</p>
            </div>";

        var mail = new MailMessage
        {
            From = new MailAddress(fromEmail, $"{freelancerName} via Client Portal"),
            Subject = $"Quote: {projectTitle} ({referenceCode}) — {businessName}",
            Body = body,
            IsBodyHtml = true
        };
        mail.To.Add(new MailAddress(clientEmail, clientName));
        if (!string.IsNullOrWhiteSpace(freelancerEmail))
        {
            mail.ReplyToList.Add(new MailAddress(freelancerEmail, freelancerName));
        }

        await smtp.SendMailAsync(mail);
    }
}

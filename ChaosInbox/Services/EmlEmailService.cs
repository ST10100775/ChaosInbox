using ChaosInbox.Models;
using MimeKit;

namespace ChaosInbox.Services;

public class EmlEmailService
{
    public async Task<EmailEnvelope> ParseAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        await using var stream = file.OpenReadStream();
        var message = await MimeMessage.LoadAsync(stream, cancellationToken);

        var id = string.IsNullOrWhiteSpace(message.MessageId)
            ? $"eml-{Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(file.FileName + file.Length)))}"
            : message.MessageId;

        var sender = message.From.Mailboxes.FirstOrDefault()?.Address
                     ?? message.From.ToString()
                     ?? "unknown";
        var body = message.TextBody;
        if (string.IsNullOrWhiteSpace(body))
            body = StripHtml(message.HtmlBody ?? "");

        var received = message.Date == DateTimeOffset.MinValue ? DateTime.UtcNow : message.Date.UtcDateTime;
        return new EmailEnvelope(id, sender, message.Subject ?? "(no subject)", body ?? "", received, "EML");
    }

    private static string StripHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return "";
        var noTags = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ");
        return System.Net.WebUtility.HtmlDecode(
            System.Text.RegularExpressions.Regex.Replace(noTags, "\\s+", " ")).Trim();
    }
}

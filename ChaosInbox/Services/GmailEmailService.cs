using System.Text;
using ChaosInbox.Models;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;

namespace ChaosInbox.Services;

public class GmailEmailService(IWebHostEnvironment environment, IConfiguration configuration)
{
    public async Task<List<EmailEnvelope>> GetRecentAsync(string userId, int maxResults, CancellationToken cancellationToken = default)
    {
        var credentialsPath = configuration["Gmail:CredentialsPath"]
            ?? Path.Combine(environment.ContentRootPath, "credentials", "credentials.json");

        if (!File.Exists(credentialsPath))
            throw new FileNotFoundException(
                "Gmail OAuth credentials were not found. Put the downloaded Google OAuth desktop-client JSON at credentials/credentials.json. Do not commit it.",
                credentialsPath);

        await using var stream = new FileStream(credentialsPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var secrets = GoogleClientSecrets.FromStream(stream).Secrets;
        var tokenFolder = Path.Combine(environment.ContentRootPath, ".tokens", userId);

        var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            secrets,
            new[] { GmailService.Scope.GmailReadonly },
            userId,
            cancellationToken,
            new FileDataStore(tokenFolder, true));

        using var service = new GmailService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "Chaos Inbox"
        });

        var listRequest = service.Users.Messages.List("me");
        listRequest.MaxResults = Math.Clamp(maxResults, 1, 50);
        listRequest.Q = "in:inbox -category:promotions";

        var listing = await listRequest.ExecuteAsync(cancellationToken);
        var output = new List<EmailEnvelope>();

        foreach (var item in listing.Messages ?? new List<Message>())
        {
            var get = service.Users.Messages.Get("me", item.Id);
            get.Format = UsersResource.MessagesResource.GetRequest.FormatEnum.Full;
            var message = await get.ExecuteAsync(cancellationToken);

            var headers = message.Payload?.Headers ?? new List<MessagePartHeader>();
            var subject = Header(headers, "Subject") ?? "(no subject)";
            var sender = Header(headers, "From") ?? "unknown";
            var body = ExtractBody(message.Payload);
            if (string.IsNullOrWhiteSpace(body)) body = message.Snippet ?? "";

            var received = message.InternalDate.HasValue
                ? DateTimeOffset.FromUnixTimeMilliseconds(message.InternalDate.Value).UtcDateTime
                : DateTime.UtcNow;

            output.Add(new EmailEnvelope(message.Id, sender, subject, body, received, "Gmail"));
        }

        return output;
    }

    private static string? Header(IEnumerable<MessagePartHeader> headers, string name) =>
        headers.FirstOrDefault(h => h.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value;

    private static string ExtractBody(MessagePart? part)
    {
        if (part is null) return "";

        if (part.MimeType == "text/plain" && !string.IsNullOrWhiteSpace(part.Body?.Data))
            return DecodeBase64Url(part.Body.Data);

        foreach (var child in part.Parts ?? new List<MessagePart>())
        {
            var body = ExtractBody(child);
            if (!string.IsNullOrWhiteSpace(body)) return body;
        }

        if (part.MimeType == "text/html" && !string.IsNullOrWhiteSpace(part.Body?.Data))
        {
            var html = DecodeBase64Url(part.Body.Data);
            var noTags = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ");
            return System.Net.WebUtility.HtmlDecode(
                System.Text.RegularExpressions.Regex.Replace(noTags, "\\s+", " ")).Trim();
        }

        return "";
    }

    private static string DecodeBase64Url(string value)
    {
        var normalized = value.Replace('-', '+').Replace('_', '/');
        normalized += (normalized.Length % 4) switch { 2 => "==", 3 => "=", _ => "" };
        return Encoding.UTF8.GetString(Convert.FromBase64String(normalized));
    }
}

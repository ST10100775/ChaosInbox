using System.Globalization;
using System.Text.RegularExpressions;
using ChaosInbox.Models;

namespace ChaosInbox.Services;

public class FallbackTicketAnalyzer
{
    public TicketAnalysis Analyse(EmailEnvelope email, DateTime utcNow)
    {
        var text = $"{email.Subject}\n{email.Body}";
        var lower = text.ToLowerInvariant();
        var deadline = ExtractDeadline(text, utcNow);

        var priority = TicketPriority.Medium;
        var confidence = 0.62;

        if (ContainsAny(lower, "critical", "immediately", "asap", "urgent", "production down", "security incident", "outage"))
        {
            priority = TicketPriority.Critical;
            confidence = 0.84;
        }
        else if (ContainsAny(lower, "high priority", "important", "eod", "end of day", "today", "tomorrow", "deadline", "due by"))
        {
            priority = TicketPriority.High;
            confidence = 0.76;
        }
        else if (ContainsAny(lower, "when you can", "no rush", "low priority", "fyi", "for your information"))
        {
            priority = TicketPriority.Low;
            confidence = 0.78;
        }

        if (deadline is not null)
        {
            var hours = (deadline.Value - utcNow).TotalHours;
            if (hours <= 24) priority = TicketPriority.Critical;
            else if (hours <= 72 && priority < TicketPriority.High) priority = TicketPriority.High;
        }

        var summary = BuildSummary(email);
        return new TicketAnalysis(summary, priority, deadline, confidence, false,
            "Local AI unavailable or returned invalid output; deterministic fallback used.");
    }

    private static bool ContainsAny(string value, params string[] needles) =>
        needles.Any(value.Contains);

    private static string BuildSummary(EmailEnvelope email)
    {
        var body = Regex.Replace(email.Body ?? "", "\\s+", " ").Trim();
        if (string.IsNullOrWhiteSpace(body))
            return string.IsNullOrWhiteSpace(email.Subject) ? "Email contained no readable body." : email.Subject.Trim();

        var sentence = Regex.Split(body, @"(?<=[.!?])\s+").FirstOrDefault() ?? body;
        var candidate = $"{email.Subject.Trim()}: {sentence.Trim()}".Trim(' ', ':');
        return candidate.Length <= 280 ? candidate : candidate[..277] + "...";
    }

    private static DateTime? ExtractDeadline(string text, DateTime utcNow)
    {
        var lower = text.ToLowerInvariant();
        var today = utcNow.Date;

        if (lower.Contains("end of day") || Regex.IsMatch(lower, @"\beod\b"))
            return today.AddHours(17);
        if (lower.Contains("tomorrow"))
            return today.AddDays(1).AddHours(17);
        if (Regex.IsMatch(lower, @"\btoday\b"))
            return today.AddHours(17);
        if (lower.Contains("end of week") || Regex.IsMatch(lower, @"\beow\b"))
        {
            var daysUntilFriday = ((int)DayOfWeek.Friday - (int)today.DayOfWeek + 7) % 7;
            return today.AddDays(daysUntilFriday).AddHours(17);
        }

        var iso = Regex.Match(text, @"\b(20\d{2})-(\d{1,2})-(\d{1,2})\b");
        if (iso.Success && DateTime.TryParseExact(iso.Value, "yyyy-M-d", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out var isoDate))
            return DateTime.SpecifyKind(isoDate.Date.AddHours(17), DateTimeKind.Utc);

        var slash = Regex.Match(text, @"\b(\d{1,2})/(\d{1,2})/(20\d{2})\b");
        if (slash.Success && DateTime.TryParseExact(slash.Value, "d/M/yyyy", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out var slashDate))
            return DateTime.SpecifyKind(slashDate.Date.AddHours(17), DateTimeKind.Utc);

        var named = Regex.Match(text,
            @"\b(?:Monday|Tuesday|Wednesday|Thursday|Friday|Saturday|Sunday)?\s*(\d{1,2})\s+(January|February|March|April|May|June|July|August|September|October|November|December)\s+(20\d{2})\b",
            RegexOptions.IgnoreCase);
        if (named.Success && DateTime.TryParse(named.Value, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out var namedDate))
            return DateTime.SpecifyKind(namedDate.Date.AddHours(17), DateTimeKind.Utc);

        return null;
    }
}

using ChaosInbox.Models;

namespace ChaosInbox.Services;

public class DemoEmailService
{
    public IReadOnlyList<EmailEnvelope> GetDemoEmails(DateTime utcNow) => new[]
    {
        new EmailEnvelope(
            $"demo-security-{utcNow:yyyyMMdd}",
            "ops@example.test",
            "URGENT: suspicious sign-in investigation",
            "Please investigate the suspicious sign-in and send me a short incident note by EOD today. This may affect the production account.",
            utcNow.AddMinutes(-12), "Demo"),
        new EmailEnvelope(
            $"demo-report-{utcNow:yyyyMMdd}",
            "manager@example.test",
            "Quarterly AI lab notes",
            "Please turn the attached notes into a one-page summary. Deadline is tomorrow. Focus on experiments, failures and next actions.",
            utcNow.AddHours(-2), "Demo"),
        new EmailEnvelope(
            $"demo-fyi-{utcNow:yyyyMMdd}",
            "newsletter@example.test",
            "FYI: developer tooling roundup",
            "For your information only. No action is required and there is no rush.",
            utcNow.AddHours(-4), "Demo"),
        new EmailEnvelope(
            $"demo-meeting-{utcNow:yyyyMMdd}",
            "team@example.test",
            "Prepare prototype screenshots",
            "Please prepare three screenshots of the prototype for the review on 14 September 2026.",
            utcNow.AddHours(-6), "Demo")
    };
}

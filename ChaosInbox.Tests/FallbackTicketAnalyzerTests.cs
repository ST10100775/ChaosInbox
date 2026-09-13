using ChaosInbox.Models;
using ChaosInbox.Services;

namespace ChaosInbox.Tests;

public class FallbackTicketAnalyzerTests
{
    private readonly FallbackTicketAnalyzer _analyzer = new();
    private readonly DateTime _now = new(2026, 9, 7, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void UrgentEmailBecomesCritical()
    {
        var email = new EmailEnvelope("1", "ops@test", "URGENT production outage", "Please investigate immediately.", _now, "Test");
        var result = _analyzer.Analyse(email, _now);
        Assert.Equal(TicketPriority.Critical, result.Priority);
        Assert.False(result.AiUsed);
    }

    [Fact]
    public void TomorrowCreatesDeadlineAndHighOrCriticalPriority()
    {
        var email = new EmailEnvelope("2", "boss@test", "Report", "Please finish this tomorrow.", _now, "Test");
        var result = _analyzer.Analyse(email, _now);
        Assert.NotNull(result.DeadlineUtc);
        Assert.True((int)result.Priority >= (int)TicketPriority.High);
    }

    [Fact]
    public void GarbageInputDoesNotThrow()
    {
        var email = new EmailEnvelope("3", "", "", "@@@ ???", _now, "Test");
        var result = _analyzer.Analyse(email, _now);
        Assert.False(string.IsNullOrWhiteSpace(result.Summary));
        Assert.InRange(result.Confidence, 0, 1);
    }
}

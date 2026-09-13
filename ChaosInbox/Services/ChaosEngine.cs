using ChaosInbox.Data;
using ChaosInbox.Models;

namespace ChaosInbox.Services;

public class ChaosEngine(ChaosInboxDbContext db)
{
    public async Task<ChaosEvent> InjectAsync(EmailTicket ticket, string mode, CancellationToken cancellationToken = default)
    {
        ChaosEvent evt;
        switch (mode.ToLowerInvariant())
        {
            case "deadline":
                ticket.DeadlineUtc = DateTime.UtcNow.AddMinutes(-30);
                ticket.ChaosScore = Math.Min(100, ticket.ChaosScore + 35);
                evt = New(ticket.Id, "ImpossibleDeadline", "Deadline moved into the past to test overdue handling.");
                break;
            case "summary":
                ticket.Summary = "";
                ticket.ChaosScore = Math.Min(100, ticket.ChaosScore + 30);
                evt = New(ticket.Id, "MissingSummary", "Summary removed to simulate malformed AI output.");
                break;
            case "sender":
                ticket.Sender = "";
                ticket.ChaosScore = Math.Min(100, ticket.ChaosScore + 20);
                evt = New(ticket.Id, "MissingSender", "Sender removed to simulate malformed source data.");
                break;
            default:
                ticket.LastProcessingError = "Simulated upstream API failure: HTTP 503.";
                ticket.ChaosScore = Math.Min(100, ticket.ChaosScore + 40);
                evt = New(ticket.Id, "ApiFailure", "A fake HTTP 503 was recorded without deleting the ticket.");
                break;
        }

        ticket.UpdatedAtUtc = DateTime.UtcNow;
        db.ChaosEvents.Add(evt);
        await db.SaveChangesAsync(cancellationToken);
        return evt;
    }

    private static ChaosEvent New(int ticketId, string type, string description) => new()
    {
        EmailTicketId = ticketId,
        Type = type,
        Description = description,
        CreatedAtUtc = DateTime.UtcNow
    };
}

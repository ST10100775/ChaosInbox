using ChaosInbox.Data;
using ChaosInbox.Models;
using Microsoft.EntityFrameworkCore;

namespace ChaosInbox.Services;

public class TicketProcessor(ChaosInboxDbContext db, OllamaTicketAnalyzer analyzer)
{
    public async Task<(EmailTicket Ticket, bool Created)> ProcessAsync(
        string userId,
        EmailEnvelope email,
        CancellationToken cancellationToken = default)
    {
        var existing = await db.Tickets.FirstOrDefaultAsync(x =>
            x.UserId == userId && x.Source == email.Source && x.ExternalMessageId == email.ExternalMessageId,
            cancellationToken);

        if (existing is not null)
            return (existing, false);

        var analysis = await analyzer.AnalyseAsync(email, cancellationToken);

        var ticket = new EmailTicket
        {
            UserId = userId,
            Source = email.Source,
            ExternalMessageId = email.ExternalMessageId,
            Sender = email.Sender,
            Subject = email.Subject,
            OriginalBody = email.Body,
            Summary = analysis.Summary,
            Priority = analysis.Priority,
            DeadlineUtc = analysis.DeadlineUtc,
            Status = TicketStatus.New,
            ReceivedAtUtc = email.ReceivedAtUtc,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            AiUsed = analysis.AiUsed,
            Confidence = analysis.Confidence,
            LastProcessingError = analysis.Warning
        };

        db.Tickets.Add(ticket);
        await db.SaveChangesAsync(cancellationToken);
        return (ticket, true);
    }
}

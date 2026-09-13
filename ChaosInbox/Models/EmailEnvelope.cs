namespace ChaosInbox.Models;

public record EmailEnvelope(
    string ExternalMessageId,
    string Sender,
    string Subject,
    string Body,
    DateTime ReceivedAtUtc,
    string Source);

public record TicketAnalysis(
    string Summary,
    TicketPriority Priority,
    DateTime? DeadlineUtc,
    double Confidence,
    bool AiUsed,
    string? Warning = null);

public record UpdateStatusRequest(TicketStatus Status);

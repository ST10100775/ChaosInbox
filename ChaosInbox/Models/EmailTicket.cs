using System.ComponentModel.DataAnnotations;

namespace ChaosInbox.Models;

public class EmailTicket
{
    public int Id { get; set; }

    [MaxLength(80)]
    public string UserId { get; set; } = "demo";

    [MaxLength(40)]
    public string Source { get; set; } = "Demo";

    [MaxLength(300)]
    public string ExternalMessageId { get; set; } = "";

    [MaxLength(500)]
    public string Sender { get; set; } = "";

    [MaxLength(1000)]
    public string Subject { get; set; } = "";

    public string OriginalBody { get; set; } = "";

    public string Summary { get; set; } = "";

    public TicketPriority Priority { get; set; } = TicketPriority.Medium;

    public DateTime? DeadlineUtc { get; set; }

    public TicketStatus Status { get; set; } = TicketStatus.New;

    public DateTime ReceivedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public bool AiUsed { get; set; }

    public double Confidence { get; set; }

    public int ChaosScore { get; set; }

    public string? LastProcessingError { get; set; }
}

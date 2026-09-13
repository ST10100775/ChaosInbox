namespace ChaosInbox.Models;

public class ChaosEvent
{
    public int Id { get; set; }
    public int EmailTicketId { get; set; }
    public EmailTicket? EmailTicket { get; set; }
    public string Type { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

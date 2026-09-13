namespace ChaosInbox.Models;

public enum TicketPriority
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public enum TicketStatus
{
    New = 0,
    InProgress = 1,
    Waiting = 2,
    Completed = 3,
    Archived = 4
}

using ChaosInbox.Models;

namespace ChaosInbox.Tests;

public class ChaosScoreTests
{
    [Fact]
    public void TicketDefaultsToSafeChaosScore()
    {
        var ticket = new EmailTicket();
        Assert.InRange(ticket.ChaosScore, 0, 100);
    }
}

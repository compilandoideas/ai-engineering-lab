using HitlSendReply.Domain;
using HitlSendReply.Policy;

namespace HitlSendReply.Tests;

public class FactCheckerTests
{
    private static readonly Order Shipped = SampleTickets.Get("T-1001").Order; // #A-1042, shipped 2026-11-12, ETA 2026-11-21, 129.90

    [Theory]
    [InlineData("According to our system, your order #A-1042 shipped on November 12.")]
    [InlineData("Order #A-1042 was dispatched on Nov 12th and should arrive on November 21, 2026.")]
    [InlineData("Your order shipped on 2026-11-12.")]
    [InlineData("The total of your order was $129.90.")]
    [InlineData("Thanks for your message, we have added it to your ticket.")]
    public void Verified_when_every_claim_matches_the_record(string text) =>
        Assert.True(FactChecker.Check(text, Shipped).Verified);

    [Theory]
    [InlineData("Your order #A-1042 shipped on November 14.", "does not match any date")]
    [InlineData("Your order #A-9999 shipped on November 12.", "does not match the ticket's order")]
    [InlineData("We will refund $500.", "has no source")]
    [InlineData("It will arrive tomorrow.", "cannot be verified")]
    [InlineData("It will arrive within two days.", "cannot be verified")]
    [InlineData("It will arrive in 3 days.", "Unverifiable number")]
    [InlineData("Your order has been delivered.", "contradicts the record")]
    [InlineData("Your order shipped on February 30.", "not a valid date")]
    public void Escalates_claims_it_cannot_verify(string text, string expectedIssue)
    {
        var result = FactChecker.Check(text, Shipped);

        Assert.False(result.Verified);
        Assert.Contains(result.Issues, i => i.Contains(expectedIssue, StringComparison.Ordinal));
    }
}

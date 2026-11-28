using HitlSendReply.Infrastructure.InMemory;
using HitlSendReply.Web.Client.Mailbox;
using HitlSendReply.Web.Mailbox;

namespace HitlSendReply.Tests.Mailbox;

/// <summary>The demo inbox and how a sender is shown in the list.</summary>
public sealed class DemoInboxTests
{
    private static readonly TicketMatcher Matcher = new(new SampleTicketDirectory());

    [Fact]
    public void Demo_emails_cover_the_review_cases_and_senders_with_no_ticket()
    {
        var emails = DemoMailboxSeeder.Emails;
        var tickets = emails.Select(e => Matcher.Match(e.From, e.Subject)?.Id).ToList();

        Assert.Equal(emails.Count, emails.Select(e => e.Id).Distinct().Count());
        Assert.All(emails, e => Assert.DoesNotMatch(@"\d{2}:\d{2}|\d{10,}", e.Subject));
        Assert.Equal(2, tickets.Count(t => t is null));
        Assert.Equal(["T-1001", "T-1002", "T-1003", "T-1005"], tickets.OfType<string>().Distinct().Order());
        Assert.Contains(emails, e => Matcher.Match(e.From, e.Subject) is { Language: Language.Es });
        Assert.Contains(emails, e => Matcher.Match(e.From, e.Subject) is { Language: Language.En });
    }

    [Theory]
    [InlineData("ana.perez@example.com", "Ana Perez")]
    [InlineData("li.wang@example.com", "Li Wang")]
    [InlineData("billing@northwind.example", "Northwind Billing")]
    [InlineData("chat-session-88", "chat-session-88")]
    public void A_sender_is_shown_by_a_name_from_the_address(string address, string name)
    {
        Assert.Equal(name, Senders.DisplayName(address));
    }

    [Fact]
    public void A_sender_keeps_the_same_color()
    {
        Assert.Equal(Senders.Tint("Ana.Perez@example.com"), Senders.Tint("ana.perez@example.com"));
        Assert.StartsWith("tint-", Senders.Tint("li.wang@example.com"));
    }
}

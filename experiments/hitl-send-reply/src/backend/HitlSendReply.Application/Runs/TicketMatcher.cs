using System.Text.RegularExpressions;

namespace HitlSendReply.Application.Runs;

/// <summary>
/// Finds the ticket an email belongs to: by the sender's address, else by a ticket id in the
/// subject ("Re: T-1002"). Matching only picks the ticket. The reply always goes to the ticket's
/// address, so a forged From with a ticket id in the subject cannot redirect anything.
/// </summary>
public sealed partial class TicketMatcher(ITicketDirectory directory)
{
    public Ticket? Match(string from, string subject)
    {
        var address = from.Trim();
        var bySender = Languages.SelectMany(directory.For)
            .FirstOrDefault(t => string.Equals(t.CustomerAddress, address, StringComparison.OrdinalIgnoreCase));
        if (bySender is not null)
        {
            return bySender;
        }

        // Ticket ids are the same in both sample sets; without a known sender, English is assumed.
        return TicketId().Match(subject) is { Success: true } id
            ? directory.For(Language.En).FirstOrDefault(t => t.Id == id.Value)
            : null;
    }

    private static readonly Language[] Languages = [Language.En, Language.Es];

    [GeneratedRegex(@"\bT-\d{4}\b")]
    private static partial Regex TicketId();
}

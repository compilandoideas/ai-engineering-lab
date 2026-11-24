using HitlSendReply.Tickets;

namespace HitlSendReply.Api.InMemory;

public sealed class InMemoryTicketStore(IEnumerable<Ticket> tickets) : ITicketStore
{
    private readonly Dictionary<string, Ticket> _tickets = tickets.ToDictionary(t => t.Id, StringComparer.OrdinalIgnoreCase);

    public Ticket? Find(string ticketId) => _tickets.GetValueOrDefault(ticketId);
}

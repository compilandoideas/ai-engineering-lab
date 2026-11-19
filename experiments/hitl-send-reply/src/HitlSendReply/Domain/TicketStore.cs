namespace HitlSendReply.Domain;

public interface ITicketStore
{
    Ticket? Find(string ticketId);
}

public sealed class InMemoryTicketStore(IEnumerable<Ticket> tickets) : ITicketStore
{
    private readonly Dictionary<string, Ticket> _tickets = tickets.ToDictionary(t => t.Id, StringComparer.OrdinalIgnoreCase);

    public Ticket? Find(string ticketId) => _tickets.GetValueOrDefault(ticketId);
}

/// <summary>Fictional data used by the demo, the tests and the evaluation set.</summary>
public static class SampleTickets
{
    public static readonly Ticket Shipped = new(
        "T-1001", "ana.perez@example.com", Channel.Email, false,
        new Order("A-1042", OrderStatus.Shipped, new DateOnly(2026, 11, 12), new DateOnly(2026, 11, 21), 129.90m, "USD"));

    public static readonly Ticket DamagedDelivery = new(
        "T-1002", "li.wang@example.com", Channel.Email, false,
        new Order("A-1077", OrderStatus.Delivered, new DateOnly(2026, 11, 3), new DateOnly(2026, 11, 7), 89.90m, "USD"));

    public static readonly Ticket Processing = new(
        "T-1003", "joao.silva@example.com", Channel.Email, false,
        new Order("A-1103", OrderStatus.Processing, null, new DateOnly(2026, 11, 28), 42.50m, "USD"));

    /// <summary>Arrived through chat; the support agent is only allowed to answer by email.</summary>
    public static readonly Ticket ChatOnly = new(
        "T-1004", "chat-session-88", Channel.Chat, false,
        new Order("A-1120", OrderStatus.Shipped, new DateOnly(2026, 11, 14), new DateOnly(2026, 11, 20), 15.00m, "USD"));

    public static readonly Ticket Closed = new(
        "T-1005", "maria.gomez@example.com", Channel.Email, true,
        new Order("A-0999", OrderStatus.Delivered, new DateOnly(2026, 10, 2), new DateOnly(2026, 10, 6), 60.00m, "USD"));

    public static IReadOnlyList<Ticket> All { get; } = [Shipped, DamagedDelivery, Processing, ChatOnly, Closed];

    public static InMemoryTicketStore Store() => new(All);
}

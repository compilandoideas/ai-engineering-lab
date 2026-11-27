
namespace HitlSendReply.Infrastructure.InMemory;

/// <summary>
/// Fictional data used by the demo, the tests and the evaluation sets. Same orders in both
/// languages; only the customer and the language of the ticket change.
/// </summary>
public static class SampleTickets
{
    private static readonly Order ShippedOrder = new("A-1042", OrderStatus.Shipped, new DateOnly(2026, 11, 12), new DateOnly(2026, 11, 21), 129.90m, "USD");
    private static readonly Order DeliveredOrder = new("A-1077", OrderStatus.Delivered, new DateOnly(2026, 11, 3), new DateOnly(2026, 11, 7), 89.90m, "USD");
    private static readonly Order ProcessingOrder = new("A-1103", OrderStatus.Processing, null, new DateOnly(2026, 11, 28), 1500.00m, "USD");
    private static readonly Order ChatOrder = new("A-1120", OrderStatus.Shipped, new DateOnly(2026, 11, 14), new DateOnly(2026, 11, 20), 15.00m, "USD");
    private static readonly Order ClosedOrder = new("A-0999", OrderStatus.Delivered, new DateOnly(2026, 10, 2), new DateOnly(2026, 10, 6), 60.00m, "USD");

    public static IReadOnlyList<Ticket> For(Language language) => language == Language.Es
        ?
        [
            new("T-1001", "lucia.fernandez@example.com", Channel.Email, false, ShippedOrder, Language.Es),
            new("T-1002", "mateo.rojas@example.com", Channel.Email, false, DeliveredOrder, Language.Es),
            new("T-1003", "valentina.soto@example.com", Channel.Email, false, ProcessingOrder, Language.Es),
            // Arrived through chat; the support agent may only answer by email.
            new("T-1004", "chat-session-88", Channel.Chat, false, ChatOrder, Language.Es),
            new("T-1005", "diego.munoz@example.com", Channel.Email, true, ClosedOrder, Language.Es),
        ]
        :
        [
            new("T-1001", "ana.perez@example.com", Channel.Email, false, ShippedOrder),
            new("T-1002", "li.wang@example.com", Channel.Email, false, DeliveredOrder),
            new("T-1003", "joao.silva@example.com", Channel.Email, false, ProcessingOrder),
            new("T-1004", "chat-session-88", Channel.Chat, false, ChatOrder),
            new("T-1005", "maria.gomez@example.com", Channel.Email, true, ClosedOrder),
        ];

    public static InMemoryTicketStore Store(Language language = Language.En) => new(For(language));

    public static Ticket Get(string id, Language language = Language.En) => For(language).Single(t => t.Id == id);
}

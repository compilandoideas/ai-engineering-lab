using HitlSendReply.Api.InMemory;
using HitlSendReply.Contracts;
using HitlSendReply.Tickets;

namespace HitlSendReply.Api.Scenarios;

public static class CatalogEndpoints
{
    public static void MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/scenarios", (string? lang) => ScenarioCatalog.For(Parse(lang)).Select(s =>
                new ScenarioDto(s.Number, s.Name, s.Ticket.Id, s.CustomerMessage, s.ExpectedFirstDecision.ToString(), s.Expectation)).ToList())
            .WithTags("Catalog").WithName("ListScenarios");

        app.MapGet("/api/tickets", (string? lang) => SampleTickets.For(Parse(lang)).Select(t =>
                new TicketDto(t.Id, t.CustomerAddress, t.Channel.ToString(), t.IsClosed, t.Language.ToString().ToLowerInvariant(),
                    new OrderDto(t.Order.Id, t.Order.Status.ToString(), t.Order.ShippedOn, t.Order.EstimatedDelivery, t.Order.Total, t.Order.Currency))).ToList())
            .WithTags("Catalog").WithName("ListTickets");
    }

    private static Language Parse(string? lang) => string.Equals(lang, "es", StringComparison.OrdinalIgnoreCase) ? Language.Es : Language.En;
}

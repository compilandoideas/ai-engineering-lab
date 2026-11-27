using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using HitlSendReply.Application.Tickets;
using HitlSendReply.Contracts;

namespace HitlSendReply.Presentation.Endpoints;

// Inside the namespace so it wins over HitlSendReply.Presentation.Results.
using Results = Microsoft.AspNetCore.Http.Results;

/// <summary>Group <c>api/tickets</c>: the sample tickets, which the inbox offers as senders for a new email.</summary>
public sealed class TicketEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder tickets = app.MapGroup("api/tickets").WithTags("Catalog");

        tickets.MapGet(string.Empty, async (string? lang, IQueryHandler<ListTicketsQuery, IReadOnlyList<TicketDto>> handler, CancellationToken ct) =>
                (await handler.Handle(new ListTicketsQuery(lang), ct)).Match(Results.Ok, ApiResults.Problem))
            .WithName("ListTickets");
    }
}

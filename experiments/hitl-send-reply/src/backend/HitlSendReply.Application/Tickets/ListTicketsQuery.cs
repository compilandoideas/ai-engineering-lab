using HitlSendReply.Contracts;

namespace HitlSendReply.Application.Tickets;

/// <summary>The sample tickets in one language: the inbox offers them as senders for a new email.</summary>
public sealed record ListTicketsQuery(string? Lang) : IQuery<IReadOnlyList<TicketDto>>;

internal sealed class ListTicketsQueryHandler(ITicketDirectory directory) : IQueryHandler<ListTicketsQuery, IReadOnlyList<TicketDto>>
{
    public Task<Result<IReadOnlyList<TicketDto>>> Handle(ListTicketsQuery query, CancellationToken cancellationToken)
    {
        var language = string.Equals(query.Lang, "es", StringComparison.OrdinalIgnoreCase) ? Language.Es : Language.En;
        IReadOnlyList<TicketDto> tickets = directory.For(language).Select(t =>
            new TicketDto(t.Id, t.CustomerAddress, t.Channel.ToString(), t.IsClosed, t.Language.ToString().ToLowerInvariant(),
                new OrderDto(t.Order.Id, t.Order.Status.ToString(), t.Order.ShippedOn, t.Order.EstimatedDelivery, t.Order.Total, t.Order.Currency)))
            .ToList();
        return Task.FromResult(Result.Success(tickets));
    }
}

namespace HitlSendReply.Infrastructure.InMemory;

public sealed class SampleTicketDirectory : ITicketDirectory
{
    public IReadOnlyList<Ticket> For(Language language) => SampleTickets.For(language);
}

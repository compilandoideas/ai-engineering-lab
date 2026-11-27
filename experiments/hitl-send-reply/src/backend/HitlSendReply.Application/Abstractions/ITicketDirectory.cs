namespace HitlSendReply.Application.Abstractions;

/// <summary>Every ticket the system of record knows, per language. Used to match an inbound email to its ticket.</summary>
public interface ITicketDirectory
{
    IReadOnlyList<Ticket> For(Language language);
}

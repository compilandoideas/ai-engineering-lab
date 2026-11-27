namespace HitlSendReply.Application.Abstractions;

public interface ITicketStore
{
    Ticket? Find(string ticketId);
}

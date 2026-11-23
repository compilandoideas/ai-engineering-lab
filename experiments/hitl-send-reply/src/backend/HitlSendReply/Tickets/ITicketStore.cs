namespace HitlSendReply.Tickets;

public interface ITicketStore
{
    Ticket? Find(string ticketId);
}

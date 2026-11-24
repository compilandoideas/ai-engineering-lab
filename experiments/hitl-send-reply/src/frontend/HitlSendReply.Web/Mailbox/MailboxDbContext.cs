using Microsoft.EntityFrameworkCore;

namespace HitlSendReply.Web.Mailbox;

public sealed class MailboxDbContext(DbContextOptions<MailboxDbContext> options) : DbContext(options)
{
    public DbSet<InboundEmail> Emails => Set<InboundEmail>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var email = modelBuilder.Entity<InboundEmail>();
        email.ToTable("inbound_emails");
        email.HasKey(e => e.Id);
        email.Property(e => e.From).HasMaxLength(320);
        email.Property(e => e.To).HasMaxLength(320);
        email.Property(e => e.Subject).HasMaxLength(500);
        email.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
        email.Property(e => e.TicketId).HasMaxLength(20);
        email.Property(e => e.RunId).HasMaxLength(64);
        email.HasIndex(e => e.ReceivedAt);
    }
}

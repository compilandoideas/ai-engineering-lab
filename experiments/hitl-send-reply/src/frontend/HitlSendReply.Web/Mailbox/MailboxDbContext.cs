using Microsoft.EntityFrameworkCore;

namespace HitlSendReply.Web.Mailbox;

public sealed class MailboxDbContext(DbContextOptions<MailboxDbContext> options) : DbContext(options)
{
    public DbSet<InboundEmail> Emails => Set<InboundEmail>();
    public DbSet<EmailReply> Replies => Set<EmailReply>();

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

        var reply = modelBuilder.Entity<EmailReply>();
        reply.ToTable("email_replies");
        reply.HasKey(r => r.Id);
        reply.HasOne<InboundEmail>().WithMany().HasForeignKey(r => r.EmailId);
        reply.Property(r => r.RunId).HasMaxLength(64);
        reply.Property(r => r.CallId).HasMaxLength(128);
        reply.Property(r => r.Recipient).HasMaxLength(320);
        reply.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
        reply.Property(r => r.Reviewer).HasMaxLength(200);
        // A review is stored once, however many times the run is read back.
        reply.HasIndex(r => new { r.EmailId, r.CallId }).IsUnique();
    }
}

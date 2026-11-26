using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TOEICMaster.Domain.Entities;

namespace TOEICMaster.Infrastructure.Persistence.Configurations;

public sealed class EmailOutboxMessageConfiguration : IEntityTypeConfiguration<EmailOutboxMessage>
{
    public void Configure(EntityTypeBuilder<EmailOutboxMessage> builder)
    {
        builder.ToTable("email_outbox_messages");
        builder.HasKey(message => message.Id);
        builder.Property(message => message.To).HasMaxLength(254).IsRequired();
        builder.Property(message => message.Subject).HasMaxLength(200).IsRequired();
        builder.Property(message => message.TextBody).IsRequired();
        builder.Property(message => message.Status).HasConversion<short>().IsRequired();
        builder.Property(message => message.AttemptCount).IsRequired();
        builder.Property(message => message.NextAttemptAtUtc).IsRequired();
        builder.Property(message => message.CreatedAtUtc).IsRequired();
        builder.Property(message => message.SentAtUtc);
        builder.Property(message => message.LastError).HasMaxLength(1000);
        builder.HasIndex(message => new { message.Status, message.NextAttemptAtUtc });
    }
}

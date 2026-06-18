using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RotationsPlus.Api.Modules.Payments;

/// <summary>Maps the webhook-idempotency ledger: the provider event id is the primary key, so a
/// re-delivered event collides on insert and is recognised as already-processed.</summary>
public sealed class ProcessedWebhookEventConfiguration : IEntityTypeConfiguration<ProcessedWebhookEvent>
{
    public void Configure(EntityTypeBuilder<ProcessedWebhookEvent> builder)
    {
        builder.ToTable("processed_webhook_events", "payments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasMaxLength(255);
    }
}

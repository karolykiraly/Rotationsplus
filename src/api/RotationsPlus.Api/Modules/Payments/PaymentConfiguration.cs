using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RotationsPlus.Api.Modules.Rotations;

namespace RotationsPlus.Api.Modules.Payments;

/// <summary>
/// Maps <see cref="Payment"/> into the <c>payments</c> schema: money precision, the status as a
/// readable string, and the provider/idempotency keys as unique so a payment can be looked up by its
/// intent id (webhook) and a retried create can't duplicate it.
/// </summary>
public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments", "payments");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Amount).HasPrecision(10, 2);
        builder.Property(x => x.TotalAmount).HasPrecision(10, 2);
        builder.Property(x => x.OutstandingAmount).HasPrecision(10, 2);
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(x => x.ProviderPaymentIntentId).HasMaxLength(255);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(255).IsRequired();

        builder.Property(x => x.CreatedBy).HasMaxLength(64);
        builder.Property(x => x.ModifiedBy).HasMaxLength(64);
        builder.Property(x => x.DeletedBy).HasMaxLength(64);

        builder.HasOne(x => x.Rotation)
            .WithMany()
            .HasForeignKey(x => x.RotationId)
            .OnDelete(DeleteBehavior.Restrict);

        // At most ONE live deposit per rotation in a non-terminal state (Pending or Succeeded), so two
        // concurrent "open a deposit" calls can't both insert a row and double-charge — the loser hits
        // this unique violation and falls back to the existing intent. A Failed payment is excluded, so
        // a genuine retry after failure can open a fresh deposit. This partial index is also the only one
        // on RotationId (a plain non-unique one would dedupe to the same column set and never materialise);
        // ActiveDepositAsync queries exactly the Pending/Succeeded rows it covers.
        builder.HasIndex(x => x.RotationId)
            .IsUnique()
            .HasDatabaseName("UX_payments_RotationId_active")
            .HasFilter("\"IsDeleted\" = false AND \"Status\" IN ('Pending', 'Succeeded')");

        // The webhook looks a payment up by its provider intent id; unique over live rows (the global
        // soft-delete filter keeps the partial index aligned with what queries can see).
        builder.HasIndex(x => x.ProviderPaymentIntentId)
            .IsUnique()
            .HasFilter("\"ProviderPaymentIntentId\" IS NOT NULL AND \"IsDeleted\" = false");

        // A retried create reuses the idempotency key; unique so it can't open two intents.
        builder.HasIndex(x => x.IdempotencyKey)
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RotationsPlus.Api.Modules.Rotations;

namespace RotationsPlus.Api.Modules.Payments;

/// <summary>
/// Maps <see cref="Honorarium"/> into the <c>payments</c> schema: money precision, the stage/status enums
/// as readable strings, and a unique (RotationId, Stage) index over live rows so a rotation's payout
/// schedule can be generated exactly once per stage (the generator's idempotency backstop).
/// </summary>
public sealed class HonorariumConfiguration : IEntityTypeConfiguration<Honorarium>
{
    public void Configure(EntityTypeBuilder<Honorarium> builder)
    {
        builder.ToTable("honorariums", "payments");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.PreceptorName).HasMaxLength(201).IsRequired(); // two 100-char names + space
        builder.Property(x => x.StudentName).HasMaxLength(201).IsRequired();

        builder.Property(x => x.Amount).HasPrecision(10, 2);
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();

        builder.Property(x => x.Stage)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(x => x.PaidBy).HasMaxLength(64);

        builder.Property(x => x.CreatedBy).HasMaxLength(64);
        builder.Property(x => x.ModifiedBy).HasMaxLength(64);
        builder.Property(x => x.DeletedBy).HasMaxLength(64);

        builder.HasOne(x => x.Rotation)
            .WithMany()
            .HasForeignKey(x => x.RotationId)
            .OnDelete(DeleteBehavior.Restrict);

        // One row per (rotation, stage) among live rows — the generator inserts the three stages once and
        // this rejects any duplicate insert (race backstop). Filtered to live rows so it aligns with the
        // global soft-delete query filter.
        builder.HasIndex(x => new { x.RotationId, x.Stage })
            .IsUnique()
            .HasDatabaseName("UX_honorariums_RotationId_Stage_live")
            .HasFilter("\"IsDeleted\" = false");

        // Serves the list endpoint's `WHERE Stage = … ORDER BY RotationStartDate, Id` (one tab at a time)
        // as an index-ordered scan with no sort node — the unique index above can't (its lead column is
        // RotationId, which the list never filters on). Honorariums grow ~3 per booked rotation (prod
        // already shows 600+ per stage tab), well past the few-hundred-row directory scale where a sort
        // index is deferred, so this ships now. Filtered to live rows to match the soft-delete query filter.
        builder.HasIndex(x => new { x.Stage, x.RotationStartDate, x.Id })
            .HasDatabaseName("IX_honorariums_Stage_RotationStartDate_Id_live")
            .HasFilter("\"IsDeleted\" = false");
    }
}

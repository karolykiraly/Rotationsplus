using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RotationsPlus.Contracts.Marketplace;

namespace RotationsPlus.Api.Modules.Marketplace;

/// <summary>
/// Maps <see cref="Preceptor"/> into the <c>marketplace</c> schema: unique email, status stored as
/// a readable string, a restricted FK to the primary specialty, and a small seeded directory for
/// DEV review. Deterministic GUIDs keep the seed stable across environments.
/// </summary>
public sealed class PreceptorConfiguration : IEntityTypeConfiguration<Preceptor>
{
    // Seeded specialty ids (see SpecialtyConfiguration).
    private const string InternalMedicine = "aaaaaaaa-0000-0000-0000-000000000001";
    private const string Pediatrics = "aaaaaaaa-0000-0000-0000-000000000007";

    public void Configure(EntityTypeBuilder<Preceptor> builder)
    {
        builder.ToTable("preceptors", "marketplace");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.LastName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(256).IsRequired();
        builder.HasIndex(x => x.Email).IsUnique();

        builder.Property(x => x.MedicalLicenseNumber).HasMaxLength(50);
        builder.Property(x => x.LicenseState).HasMaxLength(50);
        builder.Property(x => x.City).HasMaxLength(100);
        builder.Property(x => x.State).HasMaxLength(50);
        builder.Property(x => x.MobilePhone).HasMaxLength(32);
        builder.Property(x => x.Bio).HasMaxLength(4000);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.CreatedBy).HasMaxLength(64);
        builder.Property(x => x.ModifiedBy).HasMaxLength(64);
        builder.Property(x => x.DeletedBy).HasMaxLength(64);

        // Approval-queue audit (oid of the reviewer + rejection reason).
        builder.Property(x => x.ReviewedBy).HasMaxLength(64);
        builder.Property(x => x.RejectionReason).HasMaxLength(1000);

        builder.HasOne(x => x.PrimarySpecialty)
            .WithMany()
            .HasForeignKey(x => x.PrimarySpecialtyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.PrimarySpecialtyId);

        var seededAt = new DateTimeOffset(2026, 6, 15, 0, 0, 0, TimeSpan.Zero);
        builder.HasData(
            Seed("dddddddd-0000-0000-0000-000000000001", "Jane", "Carter", "jane.carter@example.com",
                InternalMedicine, "IL", "Chicago", "+1 312-555-0101", true, PreceptorStatus.MemberActivated, seededAt),
            Seed("dddddddd-0000-0000-0000-000000000002", "Omar", "Reyes", "omar.reyes@example.com",
                Pediatrics, "TX", "Houston", "+1 713-555-0102", false, PreceptorStatus.MemberValidated, seededAt),
            // A Pending preceptor so the admin Permission approval queue shows realistic data on DEV.
            Seed("dddddddd-0000-0000-0000-000000000003", "Nadia", "Khan", "nadia.khan@example.com",
                InternalMedicine, "NY", "New York", "+1 212-555-0103", false, PreceptorStatus.Pending, seededAt));
    }

    private static object Seed(
        string id, string firstName, string lastName, string email, string specialtyId,
        string state, string city, string mobilePhone, bool callScheduled, PreceptorStatus status, DateTimeOffset seededAt) => new
        {
            Id = Guid.Parse(id),
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            PrimarySpecialtyId = Guid.Parse(specialtyId),
            LicenseState = state,
            City = city,
            State = state,
            MobilePhone = mobilePhone,
            CallScheduled = callScheduled,
            Status = status,
            CreatedAtUtc = seededAt,
            CreatedBy = "seed",
            IsDeleted = false,
        };
}

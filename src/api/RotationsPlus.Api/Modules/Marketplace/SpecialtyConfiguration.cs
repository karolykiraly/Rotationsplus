using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RotationsPlus.Api.Modules.Marketplace;

/// <summary>
/// Maps <see cref="Specialty"/> into the per-module <c>marketplace</c> schema and seeds the
/// canonical clinical-specialty list carried over from the legacy app. Deterministic GUIDs keep
/// the seed stable across environments and migrations.
/// </summary>
public sealed class SpecialtyConfiguration : IEntityTypeConfiguration<Specialty>
{
    private static readonly (string Id, string Name)[] Seed =
    [
        ("aaaaaaaa-0000-0000-0000-000000000001", "Internal Medicine"),
        ("aaaaaaaa-0000-0000-0000-000000000002", "General Surgery"),
        ("aaaaaaaa-0000-0000-0000-000000000003", "Family Medicine"),
        ("aaaaaaaa-0000-0000-0000-000000000004", "Psychiatry"),
        ("aaaaaaaa-0000-0000-0000-000000000005", "Neurology"),
        ("aaaaaaaa-0000-0000-0000-000000000006", "Pathology"),
        ("aaaaaaaa-0000-0000-0000-000000000007", "Pediatrics"),
        ("aaaaaaaa-0000-0000-0000-000000000008", "OBGYN"),
        ("aaaaaaaa-0000-0000-0000-000000000009", "Orthopedic Surgery"),
        ("aaaaaaaa-0000-0000-0000-00000000000a", "Radiology"),
        ("aaaaaaaa-0000-0000-0000-00000000000b", "Anesthesiology"),
        ("aaaaaaaa-0000-0000-0000-00000000000c", "Dermatology"),
        ("aaaaaaaa-0000-0000-0000-00000000000d", "Emergency Medicine"),
        ("aaaaaaaa-0000-0000-0000-00000000000e", "Hematology/Oncology"),
        ("aaaaaaaa-0000-0000-0000-00000000000f", "All Core Rotations"),
    ];

    public void Configure(EntityTypeBuilder<Specialty> builder)
    {
        builder.ToTable("specialties", "marketplace");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => x.Name).IsUnique();

        builder.Property(x => x.CreatedBy).HasMaxLength(64);
        builder.Property(x => x.ModifiedBy).HasMaxLength(64);
        builder.Property(x => x.DeletedBy).HasMaxLength(64);

        // Seed rows are inserted by the migration (not via SaveChanges), so audit fields are set here.
        var seededAt = new DateTimeOffset(2026, 6, 15, 0, 0, 0, TimeSpan.Zero);
        builder.HasData(Seed.Select(s => new
        {
            Id = Guid.Parse(s.Id),
            s.Name,
            CreatedAtUtc = seededAt,
            CreatedBy = "seed",
            IsDeleted = false,
        }));
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RotationsPlus.Contracts.Marketplace;

namespace RotationsPlus.Api.Modules.Marketplace;

/// <summary>
/// Maps <see cref="RotationProgram"/> into the <c>marketplace</c> schema, stores the program type
/// as a readable string, pins money precision, and seeds a small sample catalog referencing the
/// seeded specialties.
/// </summary>
public sealed class RotationProgramConfiguration : IEntityTypeConfiguration<RotationProgram>
{
    // Seeded specialty ids (see SpecialtyConfiguration).
    private const string InternalMedicine = "aaaaaaaa-0000-0000-0000-000000000001";
    private const string FamilyMedicine = "aaaaaaaa-0000-0000-0000-000000000003";
    private const string Pediatrics = "aaaaaaaa-0000-0000-0000-000000000007";

    public void Configure(EntityTypeBuilder<RotationProgram> builder)
    {
        builder.ToTable("programs", "marketplace");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ProgramType)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.RetailAmountPerWeek).HasPrecision(10, 2);
        builder.Property(x => x.WeeklyHonorarium).HasPrecision(10, 2);
        builder.Property(x => x.Description).HasMaxLength(4000);
        builder.Property(x => x.CreatedBy).HasMaxLength(64);
        builder.Property(x => x.ModifiedBy).HasMaxLength(64);
        builder.Property(x => x.DeletedBy).HasMaxLength(64);

        builder.HasOne(x => x.Specialty)
            .WithMany()
            .HasForeignKey(x => x.SpecialtyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.SpecialtyId);

        var seededAt = new DateTimeOffset(2026, 6, 15, 0, 0, 0, TimeSpan.Zero);
        builder.HasData(
            Seed("cccccccc-0000-0000-0000-000000000001", InternalMedicine, ProgramType.InPerson, 2, 4, 1500m, 500m, "In-person internal medicine rotation.", seededAt),
            Seed("cccccccc-0000-0000-0000-000000000002", InternalMedicine, ProgramType.TeleRotation, 4, 2, 1000m, 300m, "Remote internal medicine tele-rotation.", seededAt),
            Seed("cccccccc-0000-0000-0000-000000000003", Pediatrics, ProgramType.InPerson, 1, 4, 1800m, 600m, "Hands-on pediatrics rotation.", seededAt),
            Seed("cccccccc-0000-0000-0000-000000000004", FamilyMedicine, ProgramType.Consultation, 3, 2, 900m, 250m, "Family medicine consultation rotation.", seededAt));
    }

    private static object Seed(
        string id, string specialtyId, ProgramType type, int maxStudents, int minWeeks,
        decimal retail, decimal honorarium, string description, DateTimeOffset seededAt) => new
        {
            Id = Guid.Parse(id),
            SpecialtyId = Guid.Parse(specialtyId),
            ProgramType = type,
            MaxStudentsPerRotation = maxStudents,
            MinWeeksPerRotation = minWeeks,
            RetailAmountPerWeek = retail,
            WeeklyHonorarium = honorarium,
            Description = description,
            CreatedAtUtc = seededAt,
            CreatedBy = "seed",
            IsDeleted = false,
        };
}

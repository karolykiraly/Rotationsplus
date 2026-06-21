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

    // Seeded preceptor ids (see PreceptorConfiguration).
    private const string JaneCarter = "dddddddd-0000-0000-0000-000000000001"; // Internal Medicine
    private const string OmarReyes = "dddddddd-0000-0000-0000-000000000002"; // Pediatrics

    public void Configure(EntityTypeBuilder<RotationProgram> builder)
    {
        builder.ToTable("programs", "marketplace");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ProgramType)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        // Sequential, server-assigned program number (DB identity). Seeded rows use explicit high
        // values (1001+); the migration restarts the sequence above them so new inserts never collide.
        builder.Property(x => x.ProgramNumber).UseIdentityByDefaultColumn();
        builder.HasIndex(x => x.ProgramNumber).IsUnique();

        builder.Property(x => x.City).HasMaxLength(120);
        builder.Property(x => x.State).HasMaxLength(120);
        // Tags map to a Postgres text[]; existing rows default to an empty array. Tags are deliberately
        // NOT seeded via HasData: EF Core re-compares a collection seed structurally on every model build
        // and never treats two equal lists as equal, which raises a perpetual PendingModelChangesWarning
        // (it aborts MigrateAsync and every integration test). Sample tag values for the seeded programs
        // are set by the AddProgramCatalogFields migration's raw UpdateData instead.
        builder.Property(x => x.Tags).HasDefaultValueSql("'{}'");

        // Blob name of the hospital image (e.g. "<programId>/<guid>.jpg"); bounded well above any key we mint.
        builder.Property(x => x.ImageBlobName).HasMaxLength(512);

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

        builder.HasOne(x => x.Preceptor)
            .WithMany()
            .HasForeignKey(x => x.PreceptorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.PreceptorId);

        var seededAt = new DateTimeOffset(2026, 6, 15, 0, 0, 0, TimeSpan.Zero);
        builder.HasData(
            Seed("cccccccc-0000-0000-0000-000000000001", 1001, InternalMedicine, JaneCarter, ProgramType.InPerson, 2, 4, 1500m, 500m, false, "Los Angeles", "CA", "In-person internal medicine rotation.", seededAt),
            // This tele-rotation is seeded "open" (instant-approval, charged in full) so DEV exercises
            // both the 100%-open and the 10%-deposit pricing paths against seed data.
            Seed("cccccccc-0000-0000-0000-000000000002", 1002, InternalMedicine, JaneCarter, ProgramType.TeleRotation, 4, 2, 1000m, 300m, true, "Remote", "NY", "Remote internal medicine tele-rotation.", seededAt),
            Seed("cccccccc-0000-0000-0000-000000000003", 1003, Pediatrics, OmarReyes, ProgramType.InPerson, 1, 4, 1800m, 600m, false, "Houston", "TX", "Hands-on pediatrics rotation.", seededAt),
            Seed("cccccccc-0000-0000-0000-000000000004", 1004, FamilyMedicine, null, ProgramType.Consultation, 3, 2, 900m, 250m, false, "Chicago", "IL", "Family medicine consultation rotation.", seededAt));
    }

    // Tags are intentionally omitted here — see the Property(x => x.Tags) note above; sample tag values
    // for these seeds are applied by the AddProgramCatalogFields migration's UpdateData.
    private static object Seed(
        string id, int programNumber, string specialtyId, string? preceptorId, ProgramType type, int maxStudents, int minWeeks,
        decimal retail, decimal honorarium, bool isOpen, string city, string state, string description, DateTimeOffset seededAt) => new
        {
            Id = Guid.Parse(id),
            ProgramNumber = programNumber,
            SpecialtyId = Guid.Parse(specialtyId),
            PreceptorId = preceptorId is null ? (Guid?)null : Guid.Parse(preceptorId),
            ProgramType = type,
            MaxStudentsPerRotation = maxStudents,
            MinWeeksPerRotation = minWeeks,
            RetailAmountPerWeek = retail,
            WeeklyHonorarium = honorarium,
            IsOpen = isOpen,
            City = city,
            State = state,
            Description = description,
            CreatedAtUtc = seededAt,
            CreatedBy = "seed",
            IsDeleted = false,
        };
}

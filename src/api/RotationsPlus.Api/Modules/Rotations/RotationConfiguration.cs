using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RotationsPlus.Api.Modules.Students;
using RotationsPlus.Contracts.Rotations;

namespace RotationsPlus.Api.Modules.Rotations;

/// <summary>
/// Maps <see cref="Rotation"/> into the <c>operations</c> schema: a restricted FK to the program,
/// status stored as a readable string, indexes for the admin list filters, and one seeded rotation
/// for DEV review. Deterministic GUIDs keep the seed stable across environments.
/// </summary>
public sealed class RotationConfiguration : IEntityTypeConfiguration<Rotation>
{
    // Seeded program (see RotationProgramConfiguration): Internal Medicine, in-person, Jane Carter.
    private const string InternalMedicineInPerson = "cccccccc-0000-0000-0000-000000000001";
    // Seeded student (see StudentConfiguration): Sam Rivera — the booked student for the seed rotation.
    private const string SamRivera = "ffffffff-0000-0000-0000-000000000001";

    public void Configure(EntityTypeBuilder<Rotation> builder)
    {
        builder.ToTable("rotations", "operations");
        builder.HasKey(x => x.Id);

        // Sequential, server-assigned rotation number (DB identity) shown as "R{number}". The seeded row
        // uses an explicit high value (1001); the migration restarts the sequence above it so new inserts
        // never collide. (At cutover the DataMigrator carries legacy rotation ids in and bumps the sequence.)
        builder.Property(x => x.RotationNumber).UseIdentityByDefaultColumn();
        builder.HasIndex(x => x.RotationNumber).IsUnique();

        // 256 (not 200) so the snapshot can always hold "FirstName LastName" — each is varchar(100) on
        // the student, so the composed name is up to 201 chars.
        builder.Property(x => x.StudentName).HasMaxLength(256).IsRequired();
        builder.Property(x => x.StudentEmail).HasMaxLength(256).IsRequired();
        builder.Property(x => x.StudentOid).HasMaxLength(64);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        // Admin-toggled dashboard flags; default false so existing/seed rows read as un-confirmed.
        builder.Property(x => x.DocumentsApproved).HasDefaultValue(false);
        builder.Property(x => x.PreceptorConfirmed).HasDefaultValue(false);

        builder.Property(x => x.CreatedBy).HasMaxLength(64);
        builder.Property(x => x.ModifiedBy).HasMaxLength(64);
        builder.Property(x => x.DeletedBy).HasMaxLength(64);

        builder.HasOne(x => x.Program)
            .WithMany()
            .HasForeignKey(x => x.ProgramId)
            .OnDelete(DeleteBehavior.Restrict);

        // Optional link to the directory student (no navigation property — the rotation snapshots the
        // student's identity on write). Restrict so a student with rotations can't be hard-deleted;
        // StudentEndpoints also blocks the soft-delete with a 409.
        builder.HasOne<Student>()
            .WithMany()
            .HasForeignKey(x => x.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ProgramId);
        builder.HasIndex(x => x.StudentId);

        // The admin list filters by Status and sorts by StartDate; the dashboard's today-cycle counts
        // filter by (Status, StartDate). One composite index serves both and subsumes the old
        // Status-only index. Partial on the soft-delete predicate so it matches the global query filter
        // (every query carries `WHERE NOT "IsDeleted"`) — smaller index, planner-aligned.
        builder.HasIndex(x => new { x.Status, x.StartDate })
            .HasFilter("\"IsDeleted\" = false");

        // The dashboard's "upcoming starts" (Where StartDate >= today, OrderBy StartDate) and the
        // start-date cycle counts filter/sort on StartDate alone, with no leading Status predicate.
        builder.HasIndex(x => x.StartDate)
            .HasFilter("\"IsDeleted\" = false");

        var seededAt = new DateTimeOffset(2026, 6, 16, 0, 0, 0, TimeSpan.Zero);
        builder.HasData(new
        {
            Id = Guid.Parse("eeeeeeee-0000-0000-0000-000000000001"),
            RotationNumber = 1001,
            ProgramId = Guid.Parse(InternalMedicineInPerson),
            StudentId = Guid.Parse(SamRivera),
            StudentName = "Sam Rivera",
            StudentEmail = "sam.rivera@example.com",
            StartDate = new DateOnly(2026, 7, 6),
            EndDate = new DateOnly(2026, 8, 3),
            Weeks = 4,
            Status = RotationStatus.Active,
            CreatedAtUtc = seededAt,
            CreatedBy = "seed",
            IsDeleted = false,
        });
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
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

    public void Configure(EntityTypeBuilder<Rotation> builder)
    {
        builder.ToTable("rotations", "operations");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.StudentName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.StudentEmail).HasMaxLength(256).IsRequired();
        builder.Property(x => x.StudentOid).HasMaxLength(64);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.CreatedBy).HasMaxLength(64);
        builder.Property(x => x.ModifiedBy).HasMaxLength(64);
        builder.Property(x => x.DeletedBy).HasMaxLength(64);

        builder.HasOne(x => x.Program)
            .WithMany()
            .HasForeignKey(x => x.ProgramId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ProgramId);
        builder.HasIndex(x => x.Status);

        var seededAt = new DateTimeOffset(2026, 6, 16, 0, 0, 0, TimeSpan.Zero);
        builder.HasData(new
        {
            Id = Guid.Parse("eeeeeeee-0000-0000-0000-000000000001"),
            ProgramId = Guid.Parse(InternalMedicineInPerson),
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

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RotationsPlus.Api.Modules.Documents;

/// <summary>
/// Maps <see cref="ProgramRequiredDocument"/> into the <c>documents</c> schema and seeds a sample
/// required-docs set on the seeded Internal-Medicine program so the upload/review/status loop has real
/// data to exercise on DEV before any admin configuration exists. Unique per (program, type) so a
/// program can't require the same document twice.
/// </summary>
public sealed class ProgramRequiredDocumentConfiguration : IEntityTypeConfiguration<ProgramRequiredDocument>
{
    // Seeded program (RotationProgramConfiguration): Internal Medicine, in-person.
    private const string InternalMedicineInPerson = "cccccccc-0000-0000-0000-000000000001";

    private static readonly (string Id, string DocumentTypeId)[] Seed =
    [
        ("d12e0000-0000-0000-0000-000000000001", DocumentTypeConfiguration.CovidVaccine),
        ("d12e0000-0000-0000-0000-000000000002", DocumentTypeConfiguration.ProofOfIdentity),
        ("d12e0000-0000-0000-0000-000000000003", DocumentTypeConfiguration.Bls),
        ("d12e0000-0000-0000-0000-000000000004", DocumentTypeConfiguration.Cv),
    ];

    public void Configure(EntityTypeBuilder<ProgramRequiredDocument> builder)
    {
        builder.ToTable("program_required_documents", "documents");
        builder.HasKey(x => x.Id);

        builder.HasOne(x => x.Program)
            .WithMany()
            .HasForeignKey(x => x.ProgramId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.DocumentType)
            .WithMany()
            .HasForeignKey(x => x.DocumentTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        // A program requires a given document type at most once.
        builder.HasIndex(x => new { x.ProgramId, x.DocumentTypeId })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");

        builder.Property(x => x.CreatedBy).HasMaxLength(64);
        builder.Property(x => x.ModifiedBy).HasMaxLength(64);
        builder.Property(x => x.DeletedBy).HasMaxLength(64);

        var seededAt = new DateTimeOffset(2026, 6, 21, 0, 0, 0, TimeSpan.Zero);
        builder.HasData(Seed.Select(s => new
        {
            Id = Guid.Parse(s.Id),
            ProgramId = Guid.Parse(InternalMedicineInPerson),
            DocumentTypeId = Guid.Parse(s.DocumentTypeId),
            CreatedAtUtc = seededAt,
            CreatedBy = "seed",
            IsDeleted = false,
        }));
    }
}

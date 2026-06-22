using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RotationsPlus.Contracts.Documents;

namespace RotationsPlus.Api.Modules.Documents;

/// <summary>
/// Maps <see cref="RotationDocument"/> into the <c>documents</c> schema. Seeds the four required
/// documents for the seeded rotation in mixed states (so the computed "Documents" column shows
/// "Documents Missing" on DEV). New rotations get their rows materialized on booking
/// (<see cref="RotationDocumentMaterializer"/>).
/// </summary>
public sealed class RotationDocumentConfiguration : IEntityTypeConfiguration<RotationDocument>
{
    private const string SeedRotation = "eeeeeeee-0000-0000-0000-000000000001";
    private const string SamRivera = "ffffffff-0000-0000-0000-000000000001";

    public void Configure(EntityTypeBuilder<RotationDocument> builder)
    {
        builder.ToTable("rotation_documents", "documents");
        builder.HasKey(x => x.Id);

        builder.HasOne(x => x.Rotation)
            .WithMany()
            .HasForeignKey(x => x.RotationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Student)
            .WithMany()
            .HasForeignKey(x => x.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.DocumentType)
            .WithMany()
            .HasForeignKey(x => x.DocumentTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();

        builder.Property(x => x.FileBlobName).HasMaxLength(256);
        builder.Property(x => x.FileName).HasMaxLength(256);
        builder.Property(x => x.RejectionReason).HasMaxLength(1000);

        builder.Property(x => x.ReviewedBy).HasMaxLength(64);
        builder.Property(x => x.CreatedBy).HasMaxLength(64);
        builder.Property(x => x.ModifiedBy).HasMaxLength(64);
        builder.Property(x => x.DeletedBy).HasMaxLength(64);

        // A rotation has at most one row per document type; also the lookup key for the tracker column.
        builder.HasIndex(x => new { x.RotationId, x.DocumentTypeId })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");

        var seededAt = new DateTimeOffset(2026, 6, 21, 0, 0, 0, TimeSpan.Zero);
        var dueDate = new DateOnly(2026, 6, 22); // rotation start (2026-07-06) − 14 days
        builder.HasData(
            SeedDoc("d40c0000-0000-0000-0000-000000000001", DocumentTypeConfiguration.CovidVaccine,
                DocumentStatus.Approved, dueDate, seededAt, "covid-card.pdf"),
            SeedDoc("d40c0000-0000-0000-0000-000000000002", DocumentTypeConfiguration.ProofOfIdentity,
                DocumentStatus.Submitted, dueDate, seededAt, "passport.pdf"),
            SeedDoc("d40c0000-0000-0000-0000-000000000003", DocumentTypeConfiguration.Bls,
                DocumentStatus.UploadNeeded, dueDate, seededAt, null),
            SeedDoc("d40c0000-0000-0000-0000-000000000004", DocumentTypeConfiguration.Cv,
                DocumentStatus.UploadNeeded, dueDate, seededAt, null));
    }

    private static object SeedDoc(string id, string typeId, DocumentStatus status, DateOnly dueDate,
        DateTimeOffset seededAt, string? fileName) => new
        {
            Id = Guid.Parse(id),
            RotationId = Guid.Parse(SeedRotation),
            StudentId = Guid.Parse(SamRivera),
            DocumentTypeId = Guid.Parse(typeId),
            Status = status,
            DueDate = dueDate,
            FileName = fileName,
            SubmittedAtUtc = status is DocumentStatus.Submitted or DocumentStatus.Approved
                ? seededAt
                : (DateTimeOffset?)null,
            CreatedAtUtc = seededAt,
            CreatedBy = "seed",
            IsDeleted = false,
        };
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RotationsPlus.Contracts.Documents;

namespace RotationsPlus.Api.Modules.Documents;

/// <summary>
/// Maps <see cref="DocumentType"/> into the <c>documents</c> schema and seeds a representative catalog
/// carried over from the legacy free-text document-type list. Deterministic GUIDs keep the seed stable.
/// Admins curate this list via the catalog form (PHASE 2g-3); the DataMigrator maps legacy rows by name.
/// </summary>
public sealed class DocumentTypeConfiguration : IEntityTypeConfiguration<DocumentType>
{
    // Stable ids (prefix "d0c..." = document catalog) so required-docs + rotation-docs seeds can reference them.
    public const string CovidVaccine = "d0c00000-0000-0000-0000-000000000001";
    public const string HepatitisB = "d0c00000-0000-0000-0000-000000000002";
    public const string Mmr = "d0c00000-0000-0000-0000-000000000003";
    public const string Varicella = "d0c00000-0000-0000-0000-000000000004";
    public const string Tdap = "d0c00000-0000-0000-0000-000000000005";
    public const string TbTest = "d0c00000-0000-0000-0000-000000000006";
    public const string DrugScreen = "d0c00000-0000-0000-0000-000000000007";
    public const string ProofOfIdentity = "d0c00000-0000-0000-0000-000000000008";
    public const string HealthInsurance = "d0c00000-0000-0000-0000-000000000009";
    public const string LiabilityInsurance = "d0c00000-0000-0000-0000-00000000000a";
    public const string Bls = "d0c00000-0000-0000-0000-00000000000b";
    public const string Hipaa = "d0c00000-0000-0000-0000-00000000000c";
    public const string Cv = "d0c00000-0000-0000-0000-00000000000d";
    public const string PhotoId = "d0c00000-0000-0000-0000-00000000000e";

    private static readonly (string Id, string Name, DocumentCategory Category)[] Seed =
    [
        (CovidVaccine, "COVID-19 Vaccine", DocumentCategory.Immunization),
        (HepatitisB, "Hepatitis B", DocumentCategory.Immunization),
        (Mmr, "MMR (Measles/Mumps/Rubella)", DocumentCategory.Immunization),
        (Varicella, "Varicella", DocumentCategory.Immunization),
        (Tdap, "Tdap", DocumentCategory.Immunization),
        (TbTest, "TB Test (PPD/Quantiferon)", DocumentCategory.MedicalTest),
        (DrugScreen, "10-Panel Drug Screen", DocumentCategory.MedicalTest),
        (ProofOfIdentity, "Proof of Identity", DocumentCategory.Identity),
        (HealthInsurance, "Proof of Health Insurance", DocumentCategory.Insurance),
        (LiabilityInsurance, "Proof of Liability Insurance", DocumentCategory.Insurance),
        (Bls, "Basic Life Support (BLS)", DocumentCategory.Certification),
        (Hipaa, "HIPAA Training", DocumentCategory.Certification),
        (Cv, "Curriculum Vitae (CV)", DocumentCategory.Professional),
        (PhotoId, "Photo for ID Badge", DocumentCategory.Professional),
    ];

    public void Configure(EntityTypeBuilder<DocumentType> builder)
    {
        builder.ToTable("document_types", "documents");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => x.Name).IsUnique();

        builder.Property(x => x.Category).HasConversion<string>().HasMaxLength(32).IsRequired();

        builder.Property(x => x.CreatedBy).HasMaxLength(64);
        builder.Property(x => x.ModifiedBy).HasMaxLength(64);
        builder.Property(x => x.DeletedBy).HasMaxLength(64);

        var seededAt = new DateTimeOffset(2026, 6, 21, 0, 0, 0, TimeSpan.Zero);
        builder.HasData(Seed.Select(s => new
        {
            Id = Guid.Parse(s.Id),
            s.Name,
            s.Category,
            CreatedAtUtc = seededAt,
            CreatedBy = "seed",
            IsDeleted = false,
        }));
    }
}

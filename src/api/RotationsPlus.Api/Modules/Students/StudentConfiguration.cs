using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RotationsPlus.Contracts.Students;

namespace RotationsPlus.Api.Modules.Students;

/// <summary>
/// Maps <see cref="Student"/> into the <c>members</c> schema: unique email, enums stored as readable
/// strings, an index on status for the directory filter, and a small seeded directory for DEV review.
/// Deterministic GUIDs keep the seed stable across environments.
/// </summary>
public sealed class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.ToTable("students", "members");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.LastName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(256).IsRequired();
        builder.HasIndex(x => x.Email).IsUnique();

        builder.Property(x => x.MobilePhone).HasMaxLength(40);
        builder.Property(x => x.MedicalSchool).HasMaxLength(200);
        builder.Property(x => x.MedicalSchoolCountry).HasMaxLength(100);
        builder.Property(x => x.City).HasMaxLength(100);
        builder.Property(x => x.State).HasMaxLength(50);
        builder.Property(x => x.StudentOid).HasMaxLength(64);
        // A CIAM oid identifies exactly one person, so at most one LIVE student may carry it (the portal
        // matches the signed-in caller to their student by oid — a duplicate would leak across students).
        // Filtered to live rows so a soft-deleted student doesn't block re-linking the oid; NULLs are
        // distinct in a Postgres unique index, so many students with no oid yet are fine.
        builder.HasIndex(x => x.StudentOid)
            .IsUnique()
            .HasFilter("\"StudentOid\" IS NOT NULL AND \"IsDeleted\" = false");

        builder.Property(x => x.AcademicStatus)
            .HasConversion<string>()
            .HasMaxLength(48)
            .IsRequired();

        builder.Property(x => x.VisaStatus)
            .HasConversion<string>()
            .HasMaxLength(32);

        // ---- Profile → Personal Information tab ----
        builder.Property(x => x.Gender).HasConversion<string>().HasMaxLength(16);
        builder.Property(x => x.ImmigrationStatus).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.ImmigrationStatusOther).HasMaxLength(120);
        builder.Property(x => x.PassportIssuedCountry).HasMaxLength(100);
        builder.Property(x => x.PassportNumber).HasMaxLength(60);
        builder.Property(x => x.SelectedIdType).HasConversion<string>().HasMaxLength(24);
        builder.Property(x => x.IdNumber).HasMaxLength(60);
        builder.Property(x => x.AvatarBlobName).HasMaxLength(256);

        // ---- Profile → Needs tab (text[] arrays map automatically; not seeded) ----
        builder.Property(x => x.PreferredSpecialty).HasMaxLength(200);
        builder.Property(x => x.CustomSpecialtyLocation).HasMaxLength(120);

        // ---- Profile → Education tab ----
        builder.Property(x => x.UsmleStep1).HasConversion<string>().HasMaxLength(16);
        builder.Property(x => x.UsmleStep2).HasConversion<string>().HasMaxLength(16);
        builder.Property(x => x.UsmleStep3).HasConversion<string>().HasMaxLength(16);
        builder.Property(x => x.UsmleScore1).HasMaxLength(16);
        builder.Property(x => x.UsmleScore2).HasMaxLength(16);
        builder.Property(x => x.UsmleScore3).HasMaxLength(16);
        builder.Property(x => x.ComlexLevel2).HasConversion<string>().HasMaxLength(16);
        builder.Property(x => x.ComlexLevel3).HasConversion<string>().HasMaxLength(16);
        builder.Property(x => x.ComlexLevel2Score).HasMaxLength(16);
        builder.Property(x => x.ComlexLevel3Score).HasMaxLength(16);
        builder.Property(x => x.EducationYear).HasConversion<string>().HasMaxLength(16);
        builder.Property(x => x.Undergrad).HasMaxLength(200);
        builder.Property(x => x.Association).HasMaxLength(200);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.HasIndex(x => x.Status);

        builder.Property(x => x.CreatedBy).HasMaxLength(64);
        builder.Property(x => x.ModifiedBy).HasMaxLength(64);
        builder.Property(x => x.DeletedBy).HasMaxLength(64);

        var seededAt = new DateTimeOffset(2026, 6, 16, 0, 0, 0, TimeSpan.Zero);
        builder.HasData(
            Seed("ffffffff-0000-0000-0000-000000000001", "Sam", "Rivera", "sam.rivera@example.com",
                AcademicStatus.InternationalMedicalGraduate, VisaStatus.NeedsVisaHelp,
                "Chicago", "IL", StudentStatus.MemberActivated, seededAt),
            Seed("ffffffff-0000-0000-0000-000000000002", "Dana", "Cole", "dana.cole@example.com",
                AcademicStatus.MdStudent, null,
                "Houston", "TX", StudentStatus.Registered, seededAt));
    }

    private static object Seed(
        string id, string firstName, string lastName, string email, AcademicStatus academicStatus,
        VisaStatus? visaStatus, string city, string state, StudentStatus status, DateTimeOffset seededAt) => new
        {
            Id = Guid.Parse(id),
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            AcademicStatus = academicStatus,
            VisaStatus = visaStatus,
            City = city,
            State = state,
            Status = status,
            CreatedAtUtc = seededAt,
            CreatedBy = "seed",
            IsDeleted = false,
        };
}

using RotationsPlus.Common.Domain;
using RotationsPlus.Contracts.Students;

namespace RotationsPlus.Api.Modules.Students;

/// <summary>
/// A student directory record — the demand side of the marketplace (the customer who books rotations).
/// This first slice models the identity + academic/visa classification core and the lifecycle status;
/// the deep profile (exam scores, documents, payments, favorites) and the rotation FK arrive in later
/// slices (see Plan_Student.md). <see cref="StudentOid"/> links the record to a CIAM account once the
/// student signs in to the portal.
/// </summary>
public sealed class Student : AuditableEntity
{
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required string Email { get; set; }
    public string? MobilePhone { get; set; }

    public AcademicStatus AcademicStatus { get; set; }
    public VisaStatus? VisaStatus { get; set; }

    public string? MedicalSchool { get; set; }
    public string? MedicalSchoolCountry { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }

    public StudentStatus Status { get; set; } = StudentStatus.Registered;

    /// <summary>CIAM object id, set once the student signs in to the portal (null until linked).</summary>
    public string? StudentOid { get; set; }
}

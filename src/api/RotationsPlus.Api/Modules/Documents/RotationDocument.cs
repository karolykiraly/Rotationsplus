using RotationsPlus.Api.Modules.Rotations;
using RotationsPlus.Api.Modules.Students;
using RotationsPlus.Common.Domain;
using RotationsPlus.Contracts.Documents;

namespace RotationsPlus.Api.Modules.Documents;

/// <summary>
/// One required document instance on a rotation — materialized from the program's
/// <see cref="ProgramRequiredDocument"/> list when the rotation is booked, then driven through the
/// <see cref="DocumentStatus"/> lifecycle by student upload + admin review. The student-facing
/// "Documents" tracker column is computed from these rows.
/// </summary>
public sealed class RotationDocument : AuditableEntity
{
    public required Guid RotationId { get; set; }
    public Rotation Rotation { get; set; } = null!;

    /// <summary>The booked student (denormalized from the rotation so a student's documents are queryable
    /// directly by student, mirroring the legacy student-documents view). Nullable only for legacy-style
    /// rows; new rows always carry it.</summary>
    public Guid? StudentId { get; set; }
    public Student? Student { get; set; }

    public required Guid DocumentTypeId { get; set; }
    public DocumentType DocumentType { get; set; } = null!;

    public DocumentStatus Status { get; set; } = DocumentStatus.UploadNeeded;

    /// <summary>When the document must be uploaded — derived on materialization from the rotation start.</summary>
    public DateOnly DueDate { get; set; }

    /// <summary>Storage key (blob name) of the uploaded file, or null until the student uploads. The API
    /// mints a short-lived read URL from it (the upload + serve path lands in PHASE 2g-2).</summary>
    public string? FileBlobName { get; set; }
    public string? FileName { get; set; }

    public DateTimeOffset? SubmittedAtUtc { get; set; }

    public DateTimeOffset? ReviewedAtUtc { get; set; }
    public string? ReviewedBy { get; set; }

    /// <summary>Why a submission was rejected (shown to the student so they can fix + re-upload).</summary>
    public string? RejectionReason { get; set; }
}

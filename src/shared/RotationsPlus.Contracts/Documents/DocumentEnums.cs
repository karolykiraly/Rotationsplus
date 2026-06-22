namespace RotationsPlus.Contracts.Documents;

/// <summary>
/// Lifecycle of a single required document on a rotation. Clean replacement for the legacy free-text
/// status soup (<c>Upload_Needed</c> / <c>Pending</c> / <c>Verification_In_Progress</c> / <c>Approved</c>
/// / <c>Completed</c> / <c>Rejected</c> / <c>Expired</c>) — collapsed to one explicit state machine.
/// </summary>
public enum DocumentStatus
{
    /// <summary>Required but the student hasn't uploaded a file yet.</summary>
    UploadNeeded,

    /// <summary>Uploaded by the student, awaiting admin review.</summary>
    Submitted,

    /// <summary>Reviewed and accepted.</summary>
    Approved,

    /// <summary>Reviewed and rejected — the student must re-upload.</summary>
    Rejected,

    /// <summary>The due date passed without an approved upload.</summary>
    Expired
}

/// <summary>Coarse grouping of a document type, for display/filtering. Derived from the legacy free-text
/// document-type strings.</summary>
public enum DocumentCategory
{
    Immunization,
    Identity,
    Insurance,
    Certification,
    Professional,
    MedicalTest,
    Agreement,
    Other
}

/// <summary>
/// The student-facing "Documents" column value on the rotations tracker, computed from the rotation's
/// required-document statuses (the rewrite analog of the legacy "Documents Missing" / "All Documents
/// Uploaded" derivation).
/// </summary>
public enum RotationDocumentsState
{
    /// <summary>The rotation has no required documents.</summary>
    NotRequired,

    /// <summary>At least one required document still needs a (re)upload (UploadNeeded/Rejected/Expired).</summary>
    Missing,

    /// <summary>Every required document has been uploaded (Submitted) or approved.</summary>
    Complete
}

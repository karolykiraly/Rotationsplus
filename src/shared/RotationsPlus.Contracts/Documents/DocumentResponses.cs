namespace RotationsPlus.Contracts.Documents;

/// <summary>A document-type in the catalog (the canonical vocabulary required-docs are configured from).</summary>
public sealed record DocumentTypeResponse(Guid Id, string Name, DocumentCategory Category);

/// <summary>
/// One required document on a rotation — the student's checklist row. <c>FileName</c> is set once the
/// student uploads; the short-lived read URL for the file is added by the upload slice (2g-2).
/// </summary>
public sealed record RotationDocumentResponse(
    Guid Id,
    string DocumentTypeName,
    DocumentCategory Category,
    DocumentStatus Status,
    DateOnly DueDate,
    string? FileName,
    DateTimeOffset? SubmittedAtUtc,
    string? RejectionReason);

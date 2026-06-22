namespace RotationsPlus.Contracts.Documents;

/// <summary>A student's document as shown on the admin review screen — carries the rotation context
/// (number) so the admin can filter by rotation, plus the upload/review metadata.</summary>
public sealed record AdminRotationDocumentResponse(
    Guid Id,
    Guid RotationId,
    int RotationNumber,
    string DocumentTypeName,
    DocumentCategory Category,
    DocumentStatus Status,
    DateOnly DueDate,
    string? FileName,
    string? FileUrl,
    DateTimeOffset? UploadedAtUtc,
    DateTimeOffset? ReviewedAtUtc,
    string? RejectionReason);

/// <summary>Admin sets a document's lifecycle status (the review dropdown). A rejection reason is
/// expected when moving to <see cref="DocumentStatus.Rejected"/>; ignored otherwise.</summary>
public sealed record SetDocumentStatusRequest(DocumentStatus Status, string? RejectionReason);

/// <summary>A program's required-documents configuration plus the full type catalog to choose from.</summary>
public sealed record ProgramRequiredDocumentsResponse(
    int DocumentDueDays,
    IReadOnlyList<Guid> RequiredDocumentTypeIds,
    IReadOnlyList<DocumentTypeResponse> Catalog);

/// <summary>Admin sets which document types a program requires (full replace) and the due-days.</summary>
public sealed record SetProgramRequiredDocumentsRequest(
    int DocumentDueDays,
    IReadOnlyList<Guid> RequiredDocumentTypeIds);

/// <summary>Admin adds a custom document type to the catalog ("Add Custom Document Type").</summary>
public sealed record CreateDocumentTypeRequest(string Name, DocumentCategory Category);

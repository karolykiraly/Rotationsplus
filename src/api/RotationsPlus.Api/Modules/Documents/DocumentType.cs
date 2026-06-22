using RotationsPlus.Common.Domain;
using RotationsPlus.Contracts.Documents;

namespace RotationsPlus.Api.Modules.Documents;

/// <summary>
/// A document-type in the catalog — the canonical vocabulary (e.g. "COVID Vaccine", "Proof of
/// Identity", "Curriculum Vitae"). Replaces the legacy 50+ free-text <c>document_type</c> strings with
/// a referenceable list that per-program required-documents and rotation documents point at.
/// </summary>
public sealed class DocumentType : AuditableEntity
{
    public required string Name { get; set; }
    public required DocumentCategory Category { get; set; }
}

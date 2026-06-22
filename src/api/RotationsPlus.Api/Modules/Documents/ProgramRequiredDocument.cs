using RotationsPlus.Api.Modules.Marketplace;
using RotationsPlus.Common.Domain;

namespace RotationsPlus.Api.Modules.Documents;

/// <summary>
/// Configures that a program requires a given document type — the clean replacement for the legacy
/// <c>program.require_documents</c> JSON blob. When a rotation is booked on the program, one
/// <see cref="RotationDocument"/> is materialized per row here. Managed by the admin required-docs
/// configuration form (PHASE 2g-3).
/// </summary>
public sealed class ProgramRequiredDocument : AuditableEntity
{
    public required Guid ProgramId { get; set; }
    public RotationProgram Program { get; set; } = null!;

    public required Guid DocumentTypeId { get; set; }
    public DocumentType DocumentType { get; set; } = null!;
}

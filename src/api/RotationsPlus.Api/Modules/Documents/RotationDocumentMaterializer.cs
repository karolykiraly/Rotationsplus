using Microsoft.EntityFrameworkCore;
using RotationsPlus.Api.Infrastructure;
using RotationsPlus.Api.Modules.Rotations;
using RotationsPlus.Contracts.Documents;

namespace RotationsPlus.Api.Modules.Documents;

/// <summary>
/// Materializes a rotation's required documents from its program's <see cref="ProgramRequiredDocument"/>
/// configuration — the rewrite of the legacy "create an Upload_Needed document per required type on
/// booking" step. Called from both booking paths (admin create + customer self-booking) after the
/// rotation is added to the context and before SaveChanges, so the rotation and its documents persist
/// together.
/// </summary>
public static class RotationDocumentMaterializer
{
    /// <summary>Documents are due this many days before the rotation starts (legacy default).</summary>
    private const int DueDaysBeforeStart = 14;

    /// <summary>
    /// Adds an <see cref="DocumentStatus.UploadNeeded"/> <see cref="RotationDocument"/> for each document
    /// type the rotation's program requires. Returns how many were created (0 when the program has no
    /// required docs).
    /// </summary>
    public static async Task<int> MaterializeAsync(
        RotationsDbContext db, Rotation rotation, CancellationToken cancellationToken)
    {
        var typeIds = await db.ProgramRequiredDocuments
            .Where(prd => prd.ProgramId == rotation.ProgramId)
            .Select(prd => prd.DocumentTypeId)
            .ToListAsync(cancellationToken);

        if (typeIds.Count == 0)
        {
            return 0;
        }

        var dueDate = rotation.StartDate.AddDays(-DueDaysBeforeStart);
        foreach (var typeId in typeIds)
        {
            db.RotationDocuments.Add(new RotationDocument
            {
                RotationId = rotation.Id,
                StudentId = rotation.StudentId,
                DocumentTypeId = typeId,
                Status = DocumentStatus.UploadNeeded,
                DueDate = dueDate,
            });
        }

        return typeIds.Count;
    }
}

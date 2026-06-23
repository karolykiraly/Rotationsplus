using Microsoft.EntityFrameworkCore;
using RotationsPlus.Api.Infrastructure;
using RotationsPlus.Common.Authorization;
using RotationsPlus.Contracts.Dashboard;
using RotationsPlus.Contracts.Documents;
using RotationsPlus.Contracts.Marketplace;
using RotationsPlus.Contracts.Rotations;

namespace RotationsPlus.Api.Modules.Dashboard;

/// <summary>
/// GET /api/dashboard/todos — the admin hub's "ToDo's" tab. Surfaces the actionable work queues an
/// admin clears: documents a student submitted that await review, rotations whose deposit hasn't been
/// paid yet, and preceptors awaiting approval. Each bucket returns the full outstanding count (for the
/// tab badge) plus a capped, deterministically-ordered preview the SPA renders inline. AdminOnly (it
/// surfaces student/rotation data). Soft-deleted rows are excluded by the global query filter.
/// </summary>
public static class DashboardTodosEndpoints
{
    /// <summary>How many items each bucket previews; the badge uses the full count, so the UI can show
    /// "+N more" and link into the owning screen.</summary>
    private const int PreviewCount = 10;

    public static IEndpointRouteBuilder MapDashboardTodosEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/dashboard/todos", async (RotationsDbContext db, CancellationToken cancellationToken) =>
        {
            // ---- Documents awaiting review (oldest submission first — first-in, first-reviewed) ----
            // Constrain to live rotations: a rotation's soft-delete does NOT cascade to its documents,
            // so a Submitted doc on a deleted rotation would otherwise surface as a dangling row (the
            // required Rotation nav resolves to NULL under its query filter → bad projection + a count
            // the preview can't render).
            var docsQuery = db.RotationDocuments
                .Where(d => d.Status == DocumentStatus.Submitted && !d.Rotation.IsDeleted);
            var docsCount = await docsQuery.CountAsync(cancellationToken);
            var docs = await docsQuery
                .OrderBy(d => d.SubmittedAtUtc)
                .ThenBy(d => d.DueDate)
                .Take(PreviewCount)
                .Select(d => new DocumentTodoItem(
                    d.Id,
                    d.RotationId,
                    d.Rotation.RotationNumber,
                    d.StudentId,
                    d.Rotation.StudentName,
                    d.DocumentType.Name,
                    d.DueDate,
                    d.SubmittedAtUtc))
                .ToListAsync(cancellationToken);

            // ---- Rotations awaiting payment (deposit not received → still Pending; soonest start first) ----
            var payQuery = db.Rotations.Where(r => r.Status == RotationStatus.Pending);
            var payCount = await payQuery.CountAsync(cancellationToken);
            var payments = await payQuery
                .OrderBy(r => r.StartDate)
                .ThenBy(r => r.RotationNumber)
                .Take(PreviewCount)
                .Select(r => new PaymentTodoItem(
                    r.Id,
                    r.RotationNumber,
                    r.StudentName,
                    r.Program.Specialty.Name,
                    r.StartDate))
                .ToListAsync(cancellationToken);

            // ---- Preceptors pending approval (oldest first — longest-waiting at the top) ----
            var precQuery = db.Preceptors.Where(p => p.Status == PreceptorStatus.Pending);
            var precCount = await precQuery.CountAsync(cancellationToken);
            var preceptors = await precQuery
                .OrderBy(p => p.CreatedAtUtc)
                .Take(PreviewCount)
                .Select(p => new PreceptorTodoItem(
                    p.Id,
                    p.FirstName + " " + p.LastName,
                    p.PrimarySpecialty.Name,
                    p.Email,
                    p.CreatedAtUtc))
                .ToListAsync(cancellationToken);

            var response = new DashboardTodosResponse(
                new TodoBucket<DocumentTodoItem>(docsCount, docs),
                new TodoBucket<PaymentTodoItem>(payCount, payments),
                new TodoBucket<PreceptorTodoItem>(precCount, preceptors));

            return Results.Ok(response);
        })
        .RequireAuthorization(AuthorizationPolicies.AdminOnly)
        .WithName("GetDashboardTodos")
        .WithTags("Dashboard");

        return routes;
    }
}

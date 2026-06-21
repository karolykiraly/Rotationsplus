using Microsoft.EntityFrameworkCore;
using RotationsPlus.Api.Infrastructure;
using RotationsPlus.Common.Authorization;
using RotationsPlus.Common.Security;
using RotationsPlus.Contracts.Rotations;

namespace RotationsPlus.Api.Modules.Rotations;

/// <summary>
/// Customer rotation endpoints (CustomerOnly): the signed-in student's own rotations tracker and
/// self-booking. The caller is matched to their directory <c>Student</c> by CIAM oid (so the link works
/// even if the admin set the oid after booking). Rotations in the student-hidden lifecycle states
/// (Cancelled / Refunded / Abandoned / Rejected, per Plan_Student §7) are excluded from the tracker.
/// </summary>
public static class CustomerRotationEndpoints
{
    // Upper bound on a booking's duration — matches the quote endpoint's guard so an absurd week count
    // can't multiply into an overflowing total.
    private const int MaxWeeks = 520;

    public static IEndpointRouteBuilder MapCustomerRotationEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/customer/rotations", async (ICurrentUser user, RotationsDbContext db, CancellationToken cancellationToken) =>
        {
            var oid = user.ObjectId;
            if (string.IsNullOrEmpty(oid))
            {
                return Results.Ok(Array.Empty<CustomerRotationResponse>());
            }

            var rotations = await db.Rotations
                // Match through the live directory record by oid (not the rotation's snapshot oid).
                .Where(r => db.Students.Any(s => s.Id == r.StudentId && s.StudentOid == oid))
                // Hide the terminal/negative states from the student tracker (Plan_Student §7).
                .Where(r => r.Status != RotationStatus.Cancelled
                         && r.Status != RotationStatus.Refunded
                         && r.Status != RotationStatus.Abandoned
                         && r.Status != RotationStatus.Rejected)
                .OrderByDescending(r => r.StartDate)
                .Select(r => new CustomerRotationResponse(
                    r.Id,
                    r.RotationNumber,
                    r.Program.Specialty.Name,
                    r.Program.ProgramType,
                    r.Program.Preceptor != null ? r.Program.Preceptor.FirstName + " " + r.Program.Preceptor.LastName : null,
                    r.StartDate,
                    r.EndDate,
                    r.Weeks,
                    r.Status))
                .ToListAsync(cancellationToken);

            return Results.Ok(rotations);
        })
        .RequireAuthorization(AuthorizationPolicies.CustomerOnly)
        .WithName("GetCustomerRotations")
        .WithTags("Rotations");

        // The student books a rotation for themselves: pick a program, a start date, and a duration. The
        // booking is created Pending — a student can't self-approve; approval follows the deposit payment.
        routes.MapPost("/api/customer/rotations", async (
            CustomerBookingRequest request, ICurrentUser user, RotationsDbContext db, TimeProvider clock,
            CancellationToken cancellationToken) =>
        {
            var oid = user.ObjectId;
            if (string.IsNullOrEmpty(oid))
            {
                return Results.NotFound();
            }

            // The caller must have a linked directory Student record (set when the admin provisions the
            // student / links the oid). A signed-in customer without one can't book yet.
            var student = await db.Students
                .Where(s => s.StudentOid == oid)
                .Select(s => new { s.Id, Name = s.FirstName + " " + s.LastName, s.Email, s.StudentOid })
                .FirstOrDefaultAsync(cancellationToken);
            if (student is null)
            {
                return Results.BadRequest("We couldn't find your student profile. Please contact support before booking.");
            }

            if (request.Weeks < 1 || request.Weeks > MaxWeeks)
            {
                return Results.BadRequest($"Weeks must be between 1 and {MaxWeeks}.");
            }

            var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
            if (request.StartDate < today)
            {
                return Results.BadRequest("The start date can't be in the past.");
            }

            var program = await db.Programs
                .Where(p => p.Id == request.ProgramId)
                .Select(p => new
                {
                    p.MinWeeksPerRotation,
                    p.ProgramType,
                    SpecialtyName = p.Specialty.Name,
                    PreceptorName = p.Preceptor != null ? p.Preceptor.FirstName + " " + p.Preceptor.LastName : null,
                })
                .FirstOrDefaultAsync(cancellationToken);
            if (program is null)
            {
                return Results.BadRequest("That program is no longer available.");
            }

            if (request.Weeks < program.MinWeeksPerRotation)
            {
                return Results.BadRequest($"This program requires at least {program.MinWeeksPerRotation} week(s).");
            }

            // NB: here EndDate is derived FROM Weeks (the student picks a duration), the opposite of the
            // admin path where Weeks is derived from a date range. The two stay consistent because EndDate
            // is computed as exactly Weeks*7 days; keep them in lockstep if either is ever edited.
            var endDate = request.StartDate.AddDays(request.Weeks * 7);
            var rotation = new Rotation
            {
                ProgramId = request.ProgramId,
                StudentId = student.Id,
                StudentName = student.Name,   // snapshot the directory identity at write time
                StudentEmail = student.Email,
                StudentOid = student.StudentOid,
                StartDate = request.StartDate,
                EndDate = endDate,
                Weeks = request.Weeks,
                Status = RotationStatus.Pending,
            };
            db.Rotations.Add(rotation);
            await db.SaveChangesAsync(cancellationToken);

            var response = new CustomerRotationResponse(
                rotation.Id, rotation.RotationNumber, program.SpecialtyName, program.ProgramType, program.PreceptorName,
                rotation.StartDate, rotation.EndDate, rotation.Weeks, rotation.Status);
            return Results.Created($"/api/customer/rotations/{rotation.Id}", response);
        })
        .RequireAuthorization(AuthorizationPolicies.CustomerOnly)
        .WithName("CreateCustomerBooking")
        .WithTags("Rotations");

        return routes;
    }
}

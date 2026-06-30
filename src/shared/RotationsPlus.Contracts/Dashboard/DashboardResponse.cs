using RotationsPlus.Contracts.Marketplace;
using RotationsPlus.Contracts.Rotations;

namespace RotationsPlus.Contracts.Dashboard;

/// <summary>The admin dashboard hub: domain totals, the program catalog broken out by delivery type,
/// the rotation pipeline by status, the next upcoming rotation starts, and the day's "LiveScore"
/// movement. Aggregated server-side so the SPA renders one cheap request.</summary>
public sealed record DashboardResponse(
    int Students,
    int Programs,
    int Preceptors,
    int Specialties,
    int Rotations,
    IReadOnlyList<ProgramTypeCount> ProgramsByType,
    IReadOnlyList<RotationStatusCount> RotationsByStatus,
    IReadOnlyList<UpcomingRotation> UpcomingStarts,
    TodayMetrics Today);

/// <summary>How many rotations are currently in a given lifecycle status.</summary>
public sealed record RotationStatusCount(RotationStatus Status, int Count);

/// <summary>How many catalog programs are of a given delivery type. The full per-type breakdown is
/// returned (all <see cref="ProgramType"/> values present in the data); the SPA groups families
/// (e.g. InPerson + InPersonResearch) into the rows it shows.</summary>
public sealed record ProgramTypeCount(ProgramType Type, int Count);

/// <summary>
/// The "Today's LiveScore" movement — what was created/what is happening within the current business
/// day (the business time zone, US/Pacific, matching the legacy dashboard). "New" counts are rows
/// created today; the rotation-cycle counts are date-driven activity for today.
/// </summary>
public sealed record TodayMetrics(
    int NewPrograms,
    IReadOnlyList<ProgramTypeCount> NewProgramsByType,
    int NewStudents,
    int NewPreceptors,
    int IssuesReported,
    int RotationsStarting,
    int RotationsInProgress,
    int RotationsCompleting,
    int RotationsCancelled);

/// <summary>A rotation starting on/after today, for the "upcoming starts" calendar + day-table. The
/// table shows the production columns: Preceptor · Student · Documents Approved · Preceptor Confirmed ·
/// Needs Visa. <c>PreceptorName</c> is null when the program has no assigned preceptor; <c>NeedsVisa</c>
/// is derived from the booked student's visa status; the two flags are admin-toggled on the row.</summary>
public sealed record UpcomingRotation(
    Guid Id,
    DateOnly StartDate,
    string? PreceptorName,
    string StudentName,
    bool DocumentsApproved,
    bool PreceptorConfirmed,
    bool NeedsVisa);

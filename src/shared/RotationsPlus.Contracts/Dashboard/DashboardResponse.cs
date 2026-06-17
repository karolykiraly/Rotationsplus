using RotationsPlus.Contracts.Rotations;

namespace RotationsPlus.Contracts.Dashboard;

/// <summary>The admin dashboard hub: domain totals, the rotation pipeline by status, and the next
/// upcoming rotation starts. Aggregated server-side so the SPA renders one cheap request.</summary>
public sealed record DashboardResponse(
    int Students,
    int Programs,
    int Preceptors,
    int Specialties,
    int Rotations,
    IReadOnlyList<RotationStatusCount> RotationsByStatus,
    IReadOnlyList<UpcomingRotation> UpcomingStarts);

/// <summary>How many rotations are currently in a given lifecycle status.</summary>
public sealed record RotationStatusCount(RotationStatus Status, int Count);

/// <summary>A rotation starting on/after today, for the "upcoming starts" widget.</summary>
public sealed record UpcomingRotation(
    Guid Id,
    string StudentName,
    string SpecialtyName,
    DateOnly StartDate,
    RotationStatus Status);

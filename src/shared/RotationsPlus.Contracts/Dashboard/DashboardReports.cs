namespace RotationsPlus.Contracts.Dashboard;

/// <summary>The admin dashboard "Reports" tab — read-only operational analytics: the booking-conversion
/// funnel (how many registered students have ever booked a rotation), a six-month registration trend
/// (students + preceptors joining per business month), and the busiest specialties by rotation volume.
/// All counts are over live (non-deleted) rows.</summary>
public sealed record DashboardReportsResponse(
    int TotalStudents,
    int StudentsWithBooking,
    int TotalRotations,
    IReadOnlyList<RegistrationsByMonth> Registrations,
    IReadOnlyList<RotationsBySpecialty> TopSpecialties);

/// <summary>New students and preceptors who registered within one business month (US/Pacific).</summary>
public sealed record RegistrationsByMonth(int Year, int Month, int Students, int Preceptors);

/// <summary>How many rotations belong to a given specialty (busiest first).</summary>
public sealed record RotationsBySpecialty(string SpecialtyName, int RotationCount);

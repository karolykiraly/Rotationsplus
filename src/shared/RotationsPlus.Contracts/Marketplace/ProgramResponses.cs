namespace RotationsPlus.Contracts.Marketplace;

/// <summary>A clinical-rotation program as shown in the catalog list. <c>PreceptorName</c> is null
/// when no preceptor is assigned yet. <c>ProgramNumber</c> is the server-assigned sequential number
/// the client formats into a typed code (e.g. "IP1042"); <c>IsOpen</c> drives the "Instant Approval"
/// chip; <c>Tags</c> are free-form marketplace chips. <c>ImageUrl</c> is a short-lived read URL for
/// the hospital image (null when none) — the client falls back to a placeholder.</summary>
public sealed record ProgramSummaryResponse(
    Guid Id,
    int ProgramNumber,
    string SpecialtyName,
    ProgramType ProgramType,
    int MaxStudentsPerRotation,
    int MinWeeksPerRotation,
    decimal RetailAmountPerWeek,
    string? PreceptorName,
    string? City,
    string? State,
    bool IsOpen,
    IReadOnlyList<string> Tags,
    string? ImageUrl);

/// <summary>Full detail for a single clinical-rotation program. <c>WeeklyHonorarium</c> (the
/// preceptor's pay, i.e. platform margin vs. the retail price) is staff-only — it is null for
/// customer callers so students/preceptors can't infer margin.</summary>
public sealed record ProgramDetailResponse(
    Guid Id,
    Guid SpecialtyId,
    string SpecialtyName,
    ProgramType ProgramType,
    int MaxStudentsPerRotation,
    int MinWeeksPerRotation,
    decimal RetailAmountPerWeek,
    decimal? WeeklyHonorarium,
    string? Description,
    Guid? PreceptorId,
    string? PreceptorName,
    bool IsOpen,
    int ProgramNumber,
    string? City,
    string? State,
    IReadOnlyList<string> Tags,
    string? ImageUrl);

/// <summary>Returned by the program image upload/delete endpoints. <c>ImageUrl</c> is the new
/// short-lived read URL after an upload, or null after the image is removed.</summary>
public sealed record ProgramImageResponse(string? ImageUrl);

namespace RotationsPlus.Contracts.Marketplace;

/// <summary>A clinical specialty as returned by the Marketplace reference-data endpoints.</summary>
public sealed record SpecialtyResponse(Guid Id, string Name);

namespace RotationsPlus.Contracts.Marketplace;

/// <summary>Admin payload to create a specialty.</summary>
public sealed record CreateSpecialtyRequest(string Name);

/// <summary>Admin payload to rename a specialty.</summary>
public sealed record UpdateSpecialtyRequest(string Name);

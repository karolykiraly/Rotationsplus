using RotationsPlus.Common.Domain;

namespace RotationsPlus.Api.Modules.Marketplace;

/// <summary>
/// A clinical specialty (e.g. "Internal Medicine") — reference data that programs, preceptors,
/// and student interests reference. First entity of the Marketplace module. Legacy "type" integer
/// is intentionally dropped from the clean model; the DataMigrator maps legacy rows by name.
/// </summary>
public sealed class Specialty : AuditableEntity
{
    public required string Name { get; set; }
}

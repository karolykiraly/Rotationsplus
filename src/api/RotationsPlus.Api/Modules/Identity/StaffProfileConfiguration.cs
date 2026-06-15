using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RotationsPlus.Api.Modules.Identity;

/// <summary>
/// Maps <see cref="StaffProfile"/> into the per-module <c>identity</c> schema (§3.2: one database,
/// module boundaries by schema/prefix). Discovered via ApplyConfigurationsFromAssembly.
/// </summary>
public sealed class StaffProfileConfiguration : IEntityTypeConfiguration<StaffProfile>
{
    public void Configure(EntityTypeBuilder<StaffProfile> builder)
    {
        builder.ToTable("staff_profiles", "identity");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.EntraObjectId).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => x.EntraObjectId).IsUnique();

        builder.Property(x => x.DisplayName).HasMaxLength(256);
        builder.Property(x => x.Email).HasMaxLength(320);
        builder.Property(x => x.CreatedBy).HasMaxLength(64);
        builder.Property(x => x.ModifiedBy).HasMaxLength(64);
        builder.Property(x => x.DeletedBy).HasMaxLength(64);
    }
}

using Microsoft.EntityFrameworkCore;
using RotationsPlus.Api.Modules.Identity;
using RotationsPlus.Api.Modules.Marketplace;
using RotationsPlus.Common.Data;

namespace RotationsPlus.Api.Infrastructure;

/// <summary>
/// The single application DbContext (one database, module boundaries enforced in code).
/// Entity configurations are discovered from this assembly as each domain module lands.
/// See Plan_Architecture.md §3.2 / §3.6.
/// </summary>
public class RotationsDbContext(DbContextOptions<RotationsDbContext> options) : DbContext(options)
{
    public DbSet<StaffProfile> StaffProfiles => Set<StaffProfile>();
    public DbSet<Specialty> Specialties => Set<Specialty>();
    public DbSet<RotationProgram> Programs => Set<RotationProgram>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RotationsDbContext).Assembly);
        modelBuilder.ApplySoftDeleteQueryFilters();
    }
}

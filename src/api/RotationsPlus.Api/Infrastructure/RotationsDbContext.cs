using Microsoft.EntityFrameworkCore;

namespace RotationsPlus.Api.Infrastructure;

/// <summary>
/// The single application DbContext (one database, module boundaries enforced in code).
/// Entity configurations are discovered from this assembly as each domain module lands.
/// See Plan_Architecture.md §3.2 / §3.6.
/// </summary>
public class RotationsDbContext(DbContextOptions<RotationsDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RotationsDbContext).Assembly);
    }
}

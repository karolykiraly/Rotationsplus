using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RotationsPlus.Api.Infrastructure;

/// <summary>
/// Design-time factory used by the EF Core tools (<c>dotnet ef migrations add</c> /
/// <c>migrations bundle</c>). It configures the Npgsql provider with a placeholder connection so
/// the model can be built without starting the web host or reaching a live database — the real
/// connection string is supplied at apply time (pipeline / Key Vault).
/// </summary>
public sealed class RotationsDbContextFactory : IDesignTimeDbContextFactory<RotationsDbContext>
{
    public RotationsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<RotationsDbContext>()
            .UseNpgsql("Host=localhost;Database=rotationsplus;Username=postgres;Password=postgres")
            .Options;

        return new RotationsDbContext(options);
    }
}

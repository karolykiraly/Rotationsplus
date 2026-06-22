using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RotationsPlus.Api.Infrastructure;

namespace RotationsPlus.Integration.Tests;

/// <summary>
/// PHASE 2f / PERF-2a: guards the rotation indexes that back the admin list filter+sort and the
/// dashboard date queries. The migration runs against the real Testcontainers Postgres, so this
/// asserts the actual created indexes (and that the superseded Status-only index is gone).
/// </summary>
public class RotationIndexTests(RotationsApiFactory factory) : IClassFixture<RotationsApiFactory>
{
    private async Task<List<string>> RotationIndexNamesAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RotationsDbContext>();
        return await db.Database
            .SqlQueryRaw<string>(
                "SELECT indexname AS \"Value\" FROM pg_indexes WHERE schemaname = 'operations' AND tablename = 'rotations'")
            .ToListAsync();
    }

    [Fact]
    public async Task The_composite_and_start_date_indexes_exist_and_the_status_only_index_is_gone()
    {
        var indexes = await RotationIndexNamesAsync();

        indexes.Should().Contain("IX_rotations_Status_StartDate");
        indexes.Should().Contain("IX_rotations_StartDate");
        // The Status-only index is subsumed by the composite (Status, StartDate) and was dropped.
        indexes.Should().NotContain("IX_rotations_Status");
    }
}

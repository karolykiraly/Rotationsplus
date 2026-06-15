using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RotationsPlus.Worker.Jobs;

namespace RotationsPlus.Worker.Tests;

public class HeartbeatJobTests
{
    [Fact]
    public async Task RunAsync_completes_without_throwing()
    {
        var job = new HeartbeatJob(NullLogger<HeartbeatJob>.Instance);

        await job.Invoking(j => j.RunAsync()).Should().NotThrowAsync();
    }
}

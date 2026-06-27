namespace RotationsPlus.Integration.Tests;

/// <summary>A <see cref="TimeProvider"/> whose <see cref="GetUtcNow"/> returns a fixed, settable instant —
/// for driving the time-boundary logic of scheduled jobs (e.g. the campaign sweep's stuck-after windows)
/// deterministically in tests.</summary>
public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}

/// <summary>A <see cref="TimeProvider"/> that returns <paramref name="first"/> on its first
/// <see cref="GetUtcNow"/> call and <paramref name="rest"/> on every subsequent call — used to simulate
/// time jumping past a deadline between a job's claim (first read) and its processing loop (later reads).</summary>
public sealed class SteppedTimeProvider(DateTimeOffset first, DateTimeOffset rest) : TimeProvider
{
    private int _calls;

    public override DateTimeOffset GetUtcNow() => _calls++ == 0 ? first : rest;
}

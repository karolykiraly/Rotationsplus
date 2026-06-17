using FluentAssertions;
using RotationsPlus.Api.Modules.Rotations;
using RotationsPlus.Contracts.Rotations;

namespace RotationsPlus.Api.Tests;

/// <summary>
/// Unit-tests the rotation lifecycle state machine: legal forward edges, blocked illegal jumps,
/// the always-allowed no-op stay, and the terminal states.
/// </summary>
public class RotationStatusMachineTests
{
    [Theory]
    [InlineData(RotationStatus.Pending, RotationStatus.NotStarted)]
    [InlineData(RotationStatus.Pending, RotationStatus.Rejected)]
    [InlineData(RotationStatus.Pending, RotationStatus.Cancelled)]
    [InlineData(RotationStatus.NotStarted, RotationStatus.Active)]
    [InlineData(RotationStatus.Active, RotationStatus.ToBeEvaluated)]
    [InlineData(RotationStatus.Active, RotationStatus.Completed)]
    [InlineData(RotationStatus.ToBeEvaluated, RotationStatus.Completed)]
    [InlineData(RotationStatus.Completed, RotationStatus.Refunded)]
    [InlineData(RotationStatus.Cancelled, RotationStatus.Refunded)]
    public void Allows_legal_forward_transitions(RotationStatus from, RotationStatus to)
    {
        RotationStatusMachine.CanTransition(from, to).Should().BeTrue();
    }

    [Theory]
    [InlineData(RotationStatus.Completed, RotationStatus.Pending)]
    [InlineData(RotationStatus.Completed, RotationStatus.Active)]
    [InlineData(RotationStatus.Pending, RotationStatus.Active)]      // must be approved (NotStarted) first
    [InlineData(RotationStatus.Pending, RotationStatus.Completed)]
    [InlineData(RotationStatus.NotStarted, RotationStatus.Completed)]
    [InlineData(RotationStatus.Rejected, RotationStatus.Active)]     // terminal
    [InlineData(RotationStatus.Refunded, RotationStatus.Active)]     // terminal
    [InlineData(RotationStatus.Abandoned, RotationStatus.Active)]    // terminal
    public void Blocks_illegal_transitions(RotationStatus from, RotationStatus to)
    {
        RotationStatusMachine.CanTransition(from, to).Should().BeFalse();
    }

    [Theory]
    [InlineData(RotationStatus.Pending)]
    [InlineData(RotationStatus.Active)]
    [InlineData(RotationStatus.Completed)]
    [InlineData(RotationStatus.Rejected)]
    public void Staying_on_the_same_status_is_always_allowed(RotationStatus status)
    {
        RotationStatusMachine.CanTransition(status, status).Should().BeTrue();
    }

    [Theory]
    [InlineData(RotationStatus.Rejected)]
    [InlineData(RotationStatus.Refunded)]
    [InlineData(RotationStatus.Abandoned)]
    public void Terminal_states_have_no_next_statuses(RotationStatus status)
    {
        RotationStatusMachine.NextFrom(status).Should().BeEmpty();
    }

    [Fact]
    public void NextFrom_excludes_the_current_status()
    {
        RotationStatusMachine.NextFrom(RotationStatus.Active).Should().NotContain(RotationStatus.Active);
    }

    /// <summary>Locks the WHOLE transition graph: every state's exact allowed-next set. Adding or
    /// dropping any edge breaks this test, so the lifecycle can't drift silently.</summary>
    [Fact]
    public void NextFrom_returns_the_exact_allowed_set_for_every_state()
    {
        RotationStatusMachine.NextFrom(RotationStatus.Pending).Should().BeEquivalentTo(
            new[] { RotationStatus.NotStarted, RotationStatus.Rejected, RotationStatus.Cancelled });
        RotationStatusMachine.NextFrom(RotationStatus.NotStarted).Should().BeEquivalentTo(
            new[] { RotationStatus.Active, RotationStatus.Cancelled, RotationStatus.Abandoned });
        RotationStatusMachine.NextFrom(RotationStatus.Active).Should().BeEquivalentTo(
            new[] { RotationStatus.ToBeEvaluated, RotationStatus.Completed, RotationStatus.Abandoned, RotationStatus.Cancelled });
        RotationStatusMachine.NextFrom(RotationStatus.ToBeEvaluated).Should().BeEquivalentTo(
            new[] { RotationStatus.Completed, RotationStatus.Abandoned });
        RotationStatusMachine.NextFrom(RotationStatus.Completed).Should().BeEquivalentTo(
            new[] { RotationStatus.Refunded });
        RotationStatusMachine.NextFrom(RotationStatus.Cancelled).Should().BeEquivalentTo(
            new[] { RotationStatus.Refunded });
        RotationStatusMachine.NextFrom(RotationStatus.Rejected).Should().BeEmpty();
        RotationStatusMachine.NextFrom(RotationStatus.Refunded).Should().BeEmpty();
        RotationStatusMachine.NextFrom(RotationStatus.Abandoned).Should().BeEmpty();
    }
}

using RotationsPlus.Common.Email;

namespace RotationsPlus.Integration.Tests;

/// <summary>An <see cref="IEmailSender"/> that records every recipient it was asked to send to, for
/// asserting the campaign job's fan-out. By default every send succeeds; pass <c>succeed: false</c> to make
/// them all fail, or supply per-address outcomes (<see cref="FailFor"/> / <see cref="ThrowFor"/>) to test
/// partial-failure and exception paths.</summary>
public sealed class RecordingEmailSender(bool succeed = true) : IEmailSender
{
    private readonly List<string> _sent = [];

    /// <summary>Addresses for which the sender returns <c>false</c> (a reported failed delivery).</summary>
    public HashSet<string> FailFor { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Addresses for which the sender throws (a sender that blows up on one bad address).</summary>
    public HashSet<string> ThrowFor { get; } = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> Sent
    {
        get { lock (_sent) { return _sent.ToList(); } }
    }

    public Task<bool> SendAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default)
    {
        lock (_sent) { _sent.Add(toEmail); }

        if (ThrowFor.Contains(toEmail))
        {
            // A message that embeds the recipient address — to prove the job never persists ex.Message.
            throw new InvalidOperationException($"SMTP 550 mailbox unavailable for {toEmail}");
        }

        var ok = succeed && !FailFor.Contains(toEmail);
        return Task.FromResult(ok);
    }
}

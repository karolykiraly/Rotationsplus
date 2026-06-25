using RotationsPlus.Common.Email;

namespace RotationsPlus.Integration.Tests;

/// <summary>An <see cref="IEmailSender"/> that records every recipient it was asked to send to (and can
/// be told to fail) — for asserting the campaign job's fan-out.</summary>
public sealed class RecordingEmailSender(bool succeed = true) : IEmailSender
{
    private readonly List<string> _sent = [];

    public IReadOnlyList<string> Sent
    {
        get { lock (_sent) { return _sent.ToList(); } }
    }

    public Task<bool> SendAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default)
    {
        lock (_sent) { _sent.Add(toEmail); }
        return Task.FromResult(succeed);
    }
}

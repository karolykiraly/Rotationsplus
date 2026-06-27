using Microsoft.Extensions.Logging;

namespace RotationsPlus.Common.Email;

/// <summary>
/// The pre-cutover email sender: records the send (logs it) and reports success without contacting any
/// provider — so the campaign loop runs end-to-end on DEV without sending real mail (and without a vendor
/// account, per the stealth rule). The real provider implementation replaces this at cutover.
/// </summary>
public sealed class FakeEmailSender(ILogger<FakeEmailSender> logger) : IEmailSender
{
    public Task<bool> SendAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "FakeEmailSender: would send \"{Subject}\" to {ToEmail} ({BodyLength} chars).",
            subject, toEmail, body.Length);
        return Task.FromResult(true);
    }
}

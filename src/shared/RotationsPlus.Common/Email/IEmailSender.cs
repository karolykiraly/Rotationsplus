namespace RotationsPlus.Common.Email;

/// <summary>
/// Abstraction over the outbound email provider. A <see cref="FakeEmailSender"/> backs DEV/PREPROD until
/// a real provider (Azure Communication Services / SendGrid) is wired at cutover — swapped in by
/// registration, with credentials from Key Vault, so no calling code changes. Mirrors the
/// <c>IPaymentGateway</c>/<c>FakePaymentGateway</c> seam.
/// </summary>
public interface IEmailSender
{
    /// <summary>Sends one email. Returns <c>true</c> on success, <c>false</c> on a (non-throwing)
    /// delivery failure so a caller fanning out over a list can tally sent vs. failed.</summary>
    Task<bool> SendAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default);
}

namespace RotationsPlus.Common.Jobs;

/// <summary>
/// The background-job contract for sending an email campaign. The API enqueues against this interface
/// (a Hangfire client), and the Worker provides the implementation — so neither project references the
/// other's concrete job type. Hangfire resolves the implementation from the Worker's DI container at
/// execution time. The <see cref="System.Threading.CancellationToken"/> parameter is Hangfire's
/// shutdown/abort token (substituted at run time; pass <c>CancellationToken.None</c> when enqueuing).
/// </summary>
public interface ICampaignSendJob
{
    Task SendAsync(Guid campaignId, CancellationToken cancellationToken);
}

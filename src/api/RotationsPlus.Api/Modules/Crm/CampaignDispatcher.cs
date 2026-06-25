using Hangfire;
using RotationsPlus.Common.Jobs;

namespace RotationsPlus.Api.Modules.Crm;

/// <summary>
/// Enqueues a campaign for the Worker's send job. A thin seam over Hangfire so the endpoint doesn't take a
/// hard dependency on <c>IBackgroundJobClient</c> (and so tests can record dispatches without a live
/// Hangfire storage).
/// </summary>
public interface ICampaignDispatcher
{
    void Dispatch(Guid campaignId);
}

/// <summary>Enqueues the campaign-send job against shared Hangfire/Postgres storage; the Worker (the only
/// Hangfire server) picks it up and resolves <see cref="ICampaignSendJob"/> from its DI container.</summary>
public sealed class HangfireCampaignDispatcher(IBackgroundJobClient jobs) : ICampaignDispatcher
{
    public void Dispatch(Guid campaignId) =>
        jobs.Enqueue<ICampaignSendJob>(job => job.SendAsync(campaignId, CancellationToken.None));
}

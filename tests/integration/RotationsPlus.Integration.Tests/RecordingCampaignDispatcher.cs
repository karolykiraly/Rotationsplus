using RotationsPlus.Api.Modules.Crm;

namespace RotationsPlus.Integration.Tests;

/// <summary>A test double for <see cref="ICampaignDispatcher"/> that records the campaign ids it was
/// asked to dispatch, instead of enqueuing a real Hangfire job.</summary>
public sealed class RecordingCampaignDispatcher : ICampaignDispatcher
{
    private readonly List<Guid> _dispatched = [];

    public IReadOnlyList<Guid> Dispatched
    {
        get { lock (_dispatched) { return _dispatched.ToList(); } }
    }

    public void Dispatch(Guid campaignId)
    {
        lock (_dispatched) { _dispatched.Add(campaignId); }
    }
}

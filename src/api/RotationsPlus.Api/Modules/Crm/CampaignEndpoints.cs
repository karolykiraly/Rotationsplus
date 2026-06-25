using Microsoft.EntityFrameworkCore;
using RotationsPlus.Api.Infrastructure;
using RotationsPlus.Common.Authorization;
using RotationsPlus.Contracts.Crm;

namespace RotationsPlus.Api.Modules.Crm;

/// <summary>
/// Admin email campaigns (the dashboard "Campaign" tab). Compose a campaign (saved as a Draft), list/read
/// them, and queue one for sending — which flips it to <see cref="CampaignStatus.Queued"/> and enqueues
/// the Worker's send job (the Worker fans out over the audience and records the counts). AdminOnly.
/// </summary>
public static class CampaignEndpoints
{
    public static IEndpointRouteBuilder MapCampaignEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/campaigns")
            .RequireAuthorization(AuthorizationPolicies.AdminOnly)
            .WithTags("Campaigns");

        group.MapGet("/", async (RotationsDbContext db, CancellationToken cancellationToken) =>
        {
            var campaigns = await db.EmailCampaigns
                .OrderByDescending(c => c.CreatedAtUtc)
                .Select(c => new CampaignSummaryResponse(
                    c.Id, c.Subject, c.Audience, c.Status,
                    c.RecipientCount, c.SentCount, c.FailedCount, c.CreatedAtUtc, c.SentAtUtc))
                .ToListAsync(cancellationToken);
            return Results.Ok(campaigns);
        });

        group.MapGet("/{id:guid}", async (Guid id, RotationsDbContext db, CancellationToken cancellationToken) =>
        {
            var campaign = await db.EmailCampaigns.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
            return campaign is null ? Results.NotFound() : Results.Ok(ToDetail(campaign));
        });

        group.MapPost("/", async (CreateCampaignRequest request, RotationsDbContext db, CancellationToken cancellationToken) =>
        {
            if (!TryValidate(request, out var error))
            {
                return Results.BadRequest(error);
            }

            var campaign = new EmailCampaign
            {
                Subject = request.Subject.Trim(),
                Body = request.Body.Trim(),
                Audience = request.Audience,
                Status = CampaignStatus.Draft
            };
            db.EmailCampaigns.Add(campaign);
            await db.SaveChangesAsync(cancellationToken);

            return Results.Created($"/api/campaigns/{campaign.Id}", ToDetail(campaign));
        });

        group.MapPost("/{id:guid}/send", async (
            Guid id, RotationsDbContext db, ICampaignDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var campaign = await db.EmailCampaigns.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
            if (campaign is null)
            {
                return Results.NotFound();
            }
            // Only a Draft can be sent — Queued/Sending/Sent/Failed are not re-sendable from here (a resend
            // would be a fresh campaign), so a double-send can't enqueue the job twice.
            if (campaign.Status != CampaignStatus.Draft)
            {
                return Results.Conflict("Only a draft campaign can be sent.");
            }

            campaign.Status = CampaignStatus.Queued;
            await db.SaveChangesAsync(cancellationToken);
            dispatcher.Dispatch(campaign.Id);

            return Results.Ok(ToDetail(campaign));
        });

        return routes;
    }

    private static bool TryValidate(CreateCampaignRequest request, out string error)
    {
        if (string.IsNullOrWhiteSpace(request.Subject))
        {
            error = "Subject is required.";
            return false;
        }
        if (request.Subject.Trim().Length > EmailCampaignConfiguration.MaxSubjectLength)
        {
            error = $"Subject must be at most {EmailCampaignConfiguration.MaxSubjectLength} characters.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(request.Body))
        {
            error = "Body is required.";
            return false;
        }
        if (request.Body.Trim().Length > EmailCampaignConfiguration.MaxBodyLength)
        {
            error = $"Body must be at most {EmailCampaignConfiguration.MaxBodyLength} characters.";
            return false;
        }
        if (!Enum.IsDefined(request.Audience))
        {
            error = "Audience is invalid.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static CampaignDetailResponse ToDetail(EmailCampaign c) => new(
        c.Id, c.Subject, c.Body, c.Audience, c.Status,
        c.RecipientCount, c.SentCount, c.FailedCount, c.CreatedAtUtc, c.SentAtUtc);
}

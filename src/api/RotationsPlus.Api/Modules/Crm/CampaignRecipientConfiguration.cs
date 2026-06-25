using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RotationsPlus.Api.Modules.Crm;

/// <summary>Maps <see cref="CampaignRecipient"/> into the <c>crm</c> schema. The unique
/// <c>(CampaignId, Email)</c> index makes materialisation idempotent — a re-run can only insert recipients
/// it hasn't already, so a resumed send never duplicates a row. Status is stored as a string.</summary>
public sealed class CampaignRecipientConfiguration : IEntityTypeConfiguration<CampaignRecipient>
{
    public const int MaxEmailLength = 320; // RFC 5321 max addressable length
    public const int MaxErrorLength = 512;

    public void Configure(EntityTypeBuilder<CampaignRecipient> builder)
    {
        builder.ToTable("campaign_recipients", "crm");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Email).HasMaxLength(MaxEmailLength).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(x => x.Error).HasMaxLength(MaxErrorLength);

        // One row per recipient per campaign — the idempotency key for materialisation/resume.
        builder.HasIndex(x => new { x.CampaignId, x.Email }).IsUnique();

        // The send job pulls the Pending set for a campaign; the sweep/tallies count by status.
        builder.HasIndex(x => new { x.CampaignId, x.Status });
    }
}

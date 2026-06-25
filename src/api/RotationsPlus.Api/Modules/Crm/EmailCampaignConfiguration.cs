using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RotationsPlus.Api.Modules.Crm;

/// <summary>Maps <see cref="EmailCampaign"/> into the <c>crm</c> schema. Enums are stored as strings;
/// Subject/Body are length-capped to match the API's validation.</summary>
public sealed class EmailCampaignConfiguration : IEntityTypeConfiguration<EmailCampaign>
{
    public const int MaxSubjectLength = 200;
    public const int MaxBodyLength = 10000;

    public void Configure(EntityTypeBuilder<EmailCampaign> builder)
    {
        builder.ToTable("email_campaigns", "crm");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Subject).HasMaxLength(MaxSubjectLength).IsRequired();
        builder.Property(x => x.Body).HasMaxLength(MaxBodyLength).IsRequired();
        builder.Property(x => x.Audience).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(16).IsRequired();

        builder.Property(x => x.CreatedBy).HasMaxLength(64);
        builder.Property(x => x.ModifiedBy).HasMaxLength(64);
        builder.Property(x => x.DeletedBy).HasMaxLength(64);

        builder.HasIndex(x => x.Status); // the dashboard lists by recency but filters/segments by status
    }
}

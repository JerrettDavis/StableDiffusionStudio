using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using StableDiffusionStudio.Domain.Entities;

namespace StableDiffusionStudio.Infrastructure.Persistence.Configurations;

public class PromptHistoryConfiguration : IEntityTypeConfiguration<PromptHistory>
{
    public void Configure(EntityTypeBuilder<PromptHistory> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.PositivePrompt).IsRequired().HasMaxLength(5000);
        builder.Property(p => p.NegativePrompt).HasMaxLength(5000);

        var dateTimeOffsetConverter = new DateTimeOffsetToBinaryConverter();
        builder.Property(p => p.UsedAt).HasConversion(dateTimeOffsetConverter);

        builder.HasIndex(p => p.UsedAt);
    }
}

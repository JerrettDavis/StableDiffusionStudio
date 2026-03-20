using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using StableDiffusionStudio.Domain.Entities;
using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Infrastructure.Persistence.Configurations;

public class GenerationPresetEntityConfiguration : IEntityTypeConfiguration<GenerationPresetEntity>
{
    public void Configure(EntityTypeBuilder<GenerationPresetEntity> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Description).HasMaxLength(2000);
        builder.Property(p => p.PositivePromptTemplate).HasMaxLength(5000);
        builder.Property(p => p.NegativePrompt).HasMaxLength(5000);

        builder.Property(p => p.Sampler).HasConversion<string>().IsRequired();
        builder.Property(p => p.Scheduler).HasConversion<string>().IsRequired();

        builder.Property(p => p.ModelFamilyFilter).HasConversion(
            v => v.HasValue ? v.Value.ToString() : null,
            v => v != null ? Enum.Parse<ModelFamily>(v) : null);

        var dateTimeOffsetConverter = new DateTimeOffsetToBinaryConverter();
        builder.Property(p => p.CreatedAt).HasConversion(dateTimeOffsetConverter);
        builder.Property(p => p.UpdatedAt).HasConversion(dateTimeOffsetConverter);

        builder.HasIndex(p => p.AssociatedModelId);
        builder.HasIndex(p => p.ModelFamilyFilter);
        builder.HasIndex(p => p.IsDefault);
    }
}

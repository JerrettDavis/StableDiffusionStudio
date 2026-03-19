using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using StableDiffusionStudio.Domain.Entities;

namespace StableDiffusionStudio.Infrastructure.Persistence.Configurations;

public class GeneratedImageConfiguration : IEntityTypeConfiguration<GeneratedImage>
{
    public void Configure(EntityTypeBuilder<GeneratedImage> builder)
    {
        builder.HasKey(i => i.Id);

        builder.Property(i => i.FilePath).IsRequired().HasMaxLength(1000);
        builder.Property(i => i.ParametersJson).HasMaxLength(10000);

        var dateTimeOffsetConverter = new DateTimeOffsetToBinaryConverter();
        builder.Property(i => i.CreatedAt).HasConversion(dateTimeOffsetConverter);

        builder.HasIndex(i => i.GenerationJobId);
    }
}

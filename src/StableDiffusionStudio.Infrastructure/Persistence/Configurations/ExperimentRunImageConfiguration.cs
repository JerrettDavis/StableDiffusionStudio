using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StableDiffusionStudio.Domain.Entities;

namespace StableDiffusionStudio.Infrastructure.Persistence.Configurations;

public class ExperimentRunImageConfiguration : IEntityTypeConfiguration<ExperimentRunImage>
{
    public void Configure(EntityTypeBuilder<ExperimentRunImage> builder)
    {
        builder.HasKey(i => i.Id);

        builder.Property(i => i.FilePath)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(i => i.AxisValuesJson)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(i => i.ContentRating)
            .HasConversion<string>()
            .IsRequired();

        builder.HasIndex(i => i.RunId);
    }
}

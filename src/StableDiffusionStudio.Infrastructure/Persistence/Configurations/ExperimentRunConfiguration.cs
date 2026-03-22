using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using StableDiffusionStudio.Domain.Entities;

namespace StableDiffusionStudio.Infrastructure.Persistence.Configurations;

public class ExperimentRunConfiguration : IEntityTypeConfiguration<ExperimentRun>
{
    public void Configure(EntityTypeBuilder<ExperimentRun> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Status)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(r => r.ErrorMessage)
            .HasMaxLength(2000);

        builder.Property(r => r.StartedAt)
            .HasConversion(new DateTimeOffsetToBinaryConverter());

        builder.Property(r => r.CompletedAt)
            .HasConversion(new DateTimeOffsetToBinaryConverter());

        builder.HasMany(r => r.Images)
            .WithOne()
            .HasForeignKey(i => i.RunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(r => r.Images)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Metadata.FindNavigation(nameof(ExperimentRun.Images))!
            .SetField("_images");

        builder.HasIndex(r => r.ExperimentId);
        builder.HasIndex(r => r.Status);
    }
}

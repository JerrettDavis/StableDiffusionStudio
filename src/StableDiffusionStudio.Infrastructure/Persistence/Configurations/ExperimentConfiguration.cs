using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using StableDiffusionStudio.Domain.Entities;

namespace StableDiffusionStudio.Infrastructure.Persistence.Configurations;

public class ExperimentConfiguration : IEntityTypeConfiguration<Experiment>
{
    public void Configure(EntityTypeBuilder<Experiment> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.Description)
            .HasMaxLength(2000);

        builder.Property(e => e.BaseParametersJson)
            .HasMaxLength(10000)
            .IsRequired();

        builder.Property(e => e.InitImagePath)
            .HasMaxLength(1000);

        builder.Property(e => e.SweepAxesJson)
            .HasMaxLength(5000)
            .IsRequired();

        var dateTimeOffsetConverter = new DateTimeOffsetToBinaryConverter();
        builder.Property(e => e.CreatedAt).HasConversion(dateTimeOffsetConverter);
        builder.Property(e => e.UpdatedAt).HasConversion(new DateTimeOffsetToBinaryConverter());

        builder.HasMany(e => e.Runs)
            .WithOne()
            .HasForeignKey(r => r.ExperimentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(e => e.Runs)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Metadata.FindNavigation(nameof(Experiment.Runs))!
            .SetField("_runs");

        builder.HasIndex(e => e.CreatedAt);
    }
}

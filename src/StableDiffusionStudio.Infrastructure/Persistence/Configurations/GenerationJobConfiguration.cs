using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using StableDiffusionStudio.Domain.Entities;
using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Infrastructure.Persistence.Configurations;

public class GenerationJobConfiguration : IEntityTypeConfiguration<GenerationJob>
{
    public void Configure(EntityTypeBuilder<GenerationJob> builder)
    {
        builder.HasKey(j => j.Id);

        builder.Property(j => j.Status).HasConversion<string>().IsRequired();

        builder.Property(j => j.Parameters)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<GenerationParameters>(v, (JsonSerializerOptions?)null)!)
            .HasMaxLength(10000)
            .IsRequired();

        builder.Property(j => j.ErrorMessage).HasMaxLength(2000);

        var dateTimeOffsetConverter = new DateTimeOffsetToBinaryConverter();
        builder.Property(j => j.CreatedAt).HasConversion(dateTimeOffsetConverter);
        builder.Property(j => j.StartedAt).HasConversion(new DateTimeOffsetToBinaryConverter());
        builder.Property(j => j.CompletedAt).HasConversion(new DateTimeOffsetToBinaryConverter());

        builder.HasMany(j => j.Images)
            .WithOne()
            .HasForeignKey(i => i.GenerationJobId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(j => j.Images)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Metadata.FindNavigation(nameof(GenerationJob.Images))!
            .SetField("_images");

        builder.HasIndex(j => j.ProjectId);
        builder.HasIndex(j => j.Status);
        builder.HasIndex(j => j.CreatedAt);
    }
}

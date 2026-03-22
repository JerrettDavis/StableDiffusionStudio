using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using StableDiffusionStudio.Domain.Entities;
using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Infrastructure.Persistence.Configurations;

public class ModelRecordConfiguration : IEntityTypeConfiguration<ModelRecord>
{
    public void Configure(EntityTypeBuilder<ModelRecord> builder)
    {
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Title).IsRequired().HasMaxLength(500);
        builder.Property(m => m.FilePath).IsRequired().HasMaxLength(1000);
        builder.Property(m => m.Source).IsRequired().HasMaxLength(100);
        builder.Property(m => m.Description).HasMaxLength(5000);
        builder.Property(m => m.PreviewImagePath).HasMaxLength(1000);
        builder.Property(m => m.CompatibilityHints).HasMaxLength(2000);
        builder.Property(m => m.Checksum).HasMaxLength(128);
        builder.Property(m => m.CivitAIModelId).HasMaxLength(200);
        builder.Property(m => m.CivitAIUrl).HasMaxLength(500);
        builder.Property(m => m.HuggingFaceModelId).HasMaxLength(500);
        builder.Property(m => m.HuggingFaceUrl).HasMaxLength(500);

        builder.Property(m => m.Type).HasConversion<string>().HasDefaultValue(ModelType.Checkpoint);
        builder.Property(m => m.ModelFamily).HasConversion<string>();
        builder.Property(m => m.Format).HasConversion<string>();
        builder.Property(m => m.Status).HasConversion<string>();

        builder.Property(m => m.Tags)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>())
            .HasMaxLength(4000);

        // SQLite does not natively support DateTimeOffset; store as binary (same pattern as ProjectConfiguration)
        var dateTimeOffsetConverter = new DateTimeOffsetToBinaryConverter();
        builder.Property(m => m.DetectedAt).HasConversion(dateTimeOffsetConverter);
        builder.Property(m => m.LastVerifiedAt).HasConversion(dateTimeOffsetConverter);
        builder.Property(m => m.LastEnrichedAt).HasConversion(dateTimeOffsetConverter);

        builder.HasIndex(m => m.Type);
        builder.HasIndex(m => m.FilePath).IsUnique();
        builder.HasIndex(m => m.ModelFamily);
        builder.HasIndex(m => m.Format);
        builder.HasIndex(m => m.Status);
        builder.HasIndex(m => m.Source);
    }
}

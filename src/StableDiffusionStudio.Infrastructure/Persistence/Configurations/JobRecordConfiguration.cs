using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using StableDiffusionStudio.Domain.Entities;

namespace StableDiffusionStudio.Infrastructure.Persistence.Configurations;

public class JobRecordConfiguration : IEntityTypeConfiguration<JobRecord>
{
    public void Configure(EntityTypeBuilder<JobRecord> builder)
    {
        builder.HasKey(j => j.Id);
        builder.Property(j => j.Type).IsRequired().HasMaxLength(100);
        builder.Property(j => j.Data).HasMaxLength(4000);
        builder.Property(j => j.Phase).HasMaxLength(200);
        builder.Property(j => j.ErrorMessage).HasMaxLength(4000);
        builder.Property(j => j.ResultData).HasMaxLength(4000);

        builder.Property(j => j.Status).HasConversion<string>();

        var dateTimeOffsetConverter = new DateTimeOffsetToBinaryConverter();
        builder.Property(j => j.CreatedAt).HasConversion(dateTimeOffsetConverter);
        builder.Property(j => j.StartedAt).HasConversion(dateTimeOffsetConverter);
        builder.Property(j => j.CompletedAt).HasConversion(dateTimeOffsetConverter);

        builder.HasIndex(j => j.Status);
        builder.HasIndex(j => j.Type);
        builder.HasIndex(j => j.CorrelationId);
        builder.HasIndex(j => j.CreatedAt);
    }
}

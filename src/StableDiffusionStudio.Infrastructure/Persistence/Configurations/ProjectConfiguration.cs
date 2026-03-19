using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using StableDiffusionStudio.Domain.Entities;

namespace StableDiffusionStudio.Infrastructure.Persistence.Configurations;

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Description).HasMaxLength(2000);
        builder.Property(p => p.Status).HasConversion<string>().IsRequired();

        // SQLite does not natively support DateTimeOffset; store as ISO 8601 strings
        var dateTimeOffsetConverter = new DateTimeOffsetToBinaryConverter();
        builder.Property(p => p.CreatedAt).HasConversion(dateTimeOffsetConverter);
        builder.Property(p => p.UpdatedAt).HasConversion(dateTimeOffsetConverter);

        builder.HasIndex(p => p.Status);
        builder.HasIndex(p => p.IsPinned);
        builder.HasIndex(p => p.CreatedAt);
    }
}

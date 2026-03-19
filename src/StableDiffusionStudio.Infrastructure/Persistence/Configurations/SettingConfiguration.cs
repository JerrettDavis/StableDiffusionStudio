using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using StableDiffusionStudio.Domain.Entities;

namespace StableDiffusionStudio.Infrastructure.Persistence.Configurations;

public class SettingConfiguration : IEntityTypeConfiguration<Setting>
{
    public void Configure(EntityTypeBuilder<Setting> builder)
    {
        builder.HasKey(s => s.Key);
        builder.Property(s => s.Key).IsRequired().HasMaxLength(200);
        builder.Property(s => s.Value).IsRequired().HasMaxLength(8000);

        var dateTimeOffsetConverter = new DateTimeOffsetToBinaryConverter();
        builder.Property(s => s.UpdatedAt).HasConversion(dateTimeOffsetConverter);
    }
}

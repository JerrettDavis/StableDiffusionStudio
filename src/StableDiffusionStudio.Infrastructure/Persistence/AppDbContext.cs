using Microsoft.EntityFrameworkCore;
using StableDiffusionStudio.Domain.Entities;

namespace StableDiffusionStudio.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ModelRecord> ModelRecords => Set<ModelRecord>();
    public DbSet<JobRecord> JobRecords => Set<JobRecord>();
    public DbSet<Setting> Settings => Set<Setting>();
    public DbSet<GenerationJob> GenerationJobs => Set<GenerationJob>();
    public DbSet<GeneratedImage> GeneratedImages => Set<GeneratedImage>();
    public DbSet<GenerationPresetEntity> GenerationPresets => Set<GenerationPresetEntity>();
    public DbSet<PromptHistory> PromptHistories => Set<PromptHistory>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}

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
    public DbSet<Experiment> Experiments => Set<Experiment>();
    public DbSet<ExperimentRun> ExperimentRuns => Set<ExperimentRun>();
    public DbSet<ExperimentRunImage> ExperimentRunImages => Set<ExperimentRunImage>();
    public DbSet<Workflow> Workflows => Set<Workflow>();
    public DbSet<WorkflowNode> WorkflowNodes => Set<WorkflowNode>();
    public DbSet<WorkflowEdge> WorkflowEdges => Set<WorkflowEdge>();
    public DbSet<WorkflowRun> WorkflowRuns => Set<WorkflowRun>();
    public DbSet<WorkflowRunStep> WorkflowRunSteps => Set<WorkflowRunStep>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}

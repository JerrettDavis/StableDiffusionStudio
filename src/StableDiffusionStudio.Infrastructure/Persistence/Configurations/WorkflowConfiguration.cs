using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using StableDiffusionStudio.Domain.Entities;

namespace StableDiffusionStudio.Infrastructure.Persistence.Configurations;

public class WorkflowConfiguration : IEntityTypeConfiguration<Workflow>
{
    public void Configure(EntityTypeBuilder<Workflow> builder)
    {
        builder.HasKey(w => w.Id);

        builder.Property(w => w.Name).HasMaxLength(200).IsRequired();
        builder.Property(w => w.Description).HasMaxLength(2000);
        builder.Property(w => w.CanvasStateJson).HasMaxLength(50000);

        var dtConverter = new DateTimeOffsetToBinaryConverter();
        builder.Property(w => w.CreatedAt).HasConversion(dtConverter);
        builder.Property(w => w.UpdatedAt).HasConversion(dtConverter);

        builder.HasMany(w => w.Nodes)
            .WithOne()
            .HasForeignKey(n => n.WorkflowId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(w => w.Nodes).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata.FindNavigation(nameof(Workflow.Nodes))!.SetField("_nodes");

        builder.HasMany(w => w.Edges)
            .WithOne()
            .HasForeignKey(e => e.WorkflowId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(w => w.Edges).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata.FindNavigation(nameof(Workflow.Edges))!.SetField("_edges");

        builder.HasMany(w => w.Runs)
            .WithOne()
            .HasForeignKey(r => r.WorkflowId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(w => w.Runs).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata.FindNavigation(nameof(Workflow.Runs))!.SetField("_runs");

        builder.HasIndex(w => w.CreatedAt);
    }
}

public class WorkflowNodeConfiguration : IEntityTypeConfiguration<WorkflowNode>
{
    public void Configure(EntityTypeBuilder<WorkflowNode> builder)
    {
        builder.HasKey(n => n.Id);
        builder.Property(n => n.PluginId).HasMaxLength(200).IsRequired();
        builder.Property(n => n.Label).HasMaxLength(200).IsRequired();
        builder.Property(n => n.ParametersJson).HasMaxLength(10000);
        builder.Property(n => n.ConfigJson).HasMaxLength(10000);

        builder.HasIndex(n => n.WorkflowId);
    }
}

public class WorkflowEdgeConfiguration : IEntityTypeConfiguration<WorkflowEdge>
{
    public void Configure(EntityTypeBuilder<WorkflowEdge> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.SourcePort).HasMaxLength(100).IsRequired();
        builder.Property(e => e.TargetPort).HasMaxLength(100).IsRequired();

        builder.HasIndex(e => e.WorkflowId);
    }
}

public class WorkflowRunConfiguration : IEntityTypeConfiguration<WorkflowRun>
{
    public void Configure(EntityTypeBuilder<WorkflowRun> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Status).HasConversion<string>().IsRequired();
        builder.Property(r => r.InputsJson).HasMaxLength(50000);
        builder.Property(r => r.Error).HasMaxLength(2000);

        var dtConverter = new DateTimeOffsetToBinaryConverter();
        builder.Property(r => r.CreatedAt).HasConversion(dtConverter);
        builder.Property(r => r.StartedAt).HasConversion(dtConverter);
        builder.Property(r => r.CompletedAt).HasConversion(dtConverter);

        builder.HasMany(r => r.Steps)
            .WithOne()
            .HasForeignKey(s => s.WorkflowRunId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(r => r.Steps).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata.FindNavigation(nameof(WorkflowRun.Steps))!.SetField("_steps");

        builder.HasIndex(r => r.WorkflowId);
    }
}

public class WorkflowRunStepConfiguration : IEntityTypeConfiguration<WorkflowRunStep>
{
    public void Configure(EntityTypeBuilder<WorkflowRunStep> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Status).HasConversion<string>().IsRequired();
        builder.Property(s => s.OutputImagePath).HasMaxLength(1000);
        builder.Property(s => s.OutputDataJson).HasMaxLength(50000);
        builder.Property(s => s.Error).HasMaxLength(2000);

        var dtConverter = new DateTimeOffsetToBinaryConverter();
        builder.Property(s => s.StartedAt).HasConversion(dtConverter);
        builder.Property(s => s.CompletedAt).HasConversion(dtConverter);

        builder.HasIndex(s => s.WorkflowRunId);
    }
}

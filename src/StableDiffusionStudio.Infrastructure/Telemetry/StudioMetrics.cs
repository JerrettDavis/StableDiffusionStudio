using System.Diagnostics.Metrics;

namespace StableDiffusionStudio.Infrastructure.Telemetry;

public sealed class StudioMetrics
{
    public const string MeterName = "StableDiffusionStudio";

    private readonly Counter<long> _projectsCreated;
    private readonly Counter<long> _modelsScanned;
    private readonly Counter<long> _jobsCompleted;
    private readonly Counter<long> _jobsFailed;

    public StudioMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _projectsCreated = meter.CreateCounter<long>("studio.projects.created", description: "Number of projects created");
        _modelsScanned = meter.CreateCounter<long>("studio.models.scanned", description: "Number of models scanned");
        _jobsCompleted = meter.CreateCounter<long>("studio.jobs.completed", description: "Number of jobs completed");
        _jobsFailed = meter.CreateCounter<long>("studio.jobs.failed", description: "Number of jobs failed");
    }

    public void ProjectCreated() => _projectsCreated.Add(1);
    public void ModelsScanned(long count) => _modelsScanned.Add(count);
    public void JobCompleted() => _jobsCompleted.Add(1);
    public void JobFailed() => _jobsFailed.Add(1);
}

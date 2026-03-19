using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Domain.Entities;

public class JobRecord
{
    public Guid Id { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string? Data { get; private set; }
    public JobStatus Status { get; private set; }
    public int Progress { get; private set; }
    public string? Phase { get; private set; }
    public Guid CorrelationId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string? ResultData { get; private set; }

    private JobRecord() { } // EF Core

    public static JobRecord Create(string type, string? data = null, Guid? correlationId = null)
    {
        if (string.IsNullOrWhiteSpace(type))
            throw new ArgumentException("Job type is required.", nameof(type));

        return new JobRecord
        {
            Id = Guid.NewGuid(),
            Type = type,
            Data = data,
            Status = JobStatus.Pending,
            Progress = 0,
            CorrelationId = correlationId ?? Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public void Start()
    {
        if (Status != JobStatus.Pending)
            throw new InvalidOperationException($"Cannot start a job in {Status} status.");

        Status = JobStatus.Running;
        StartedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateProgress(int progress, string? phase = null)
    {
        if (Status != JobStatus.Running)
            throw new InvalidOperationException($"Cannot update progress for a job in {Status} status.");

        Progress = Math.Clamp(progress, 0, 100);
        if (phase is not null) Phase = phase;
    }

    public void Complete(string? resultData = null)
    {
        if (Status != JobStatus.Running)
            throw new InvalidOperationException($"Cannot complete a job in {Status} status.");

        Status = JobStatus.Completed;
        Progress = 100;
        CompletedAt = DateTimeOffset.UtcNow;
        ResultData = resultData;
    }

    public void Fail(string errorMessage)
    {
        if (Status is not (JobStatus.Running or JobStatus.Pending))
            throw new InvalidOperationException($"Cannot fail a job in {Status} status.");

        Status = JobStatus.Failed;
        CompletedAt = DateTimeOffset.UtcNow;
        ErrorMessage = errorMessage;
    }

    public void Cancel()
    {
        if (Status is not (JobStatus.Pending or JobStatus.Running))
            throw new InvalidOperationException($"Cannot cancel a job in {Status} status.");

        Status = JobStatus.Cancelled;
        CompletedAt = DateTimeOffset.UtcNow;
    }
}

using Microsoft.EntityFrameworkCore;
using StableDiffusionStudio.Application.DTOs;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.Entities;
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Infrastructure.Persistence;

namespace StableDiffusionStudio.Infrastructure.Jobs;

public class ChannelJobQueue : IJobQueue
{
    private readonly AppDbContext _context;
    private readonly JobChannel _channel;

    public ChannelJobQueue(AppDbContext context, JobChannel channel)
    {
        _context = context;
        _channel = channel;
    }

    public async Task<Guid> EnqueueAsync(string type, string? data = null, CancellationToken ct = default)
    {
        var job = JobRecord.Create(type, data);
        _context.JobRecords.Add(job);
        await _context.SaveChangesAsync(ct);
        await _channel.Writer.WriteAsync(job.Id, ct);
        return job.Id;
    }

    public async Task<IReadOnlyList<JobRecordDto>> ListAsync(bool activeOnly = false, CancellationToken ct = default)
    {
        var query = _context.JobRecords.AsQueryable();
        if (activeOnly)
            query = query.Where(j => j.Status == JobStatus.Pending || j.Status == JobStatus.Running);

        var jobs = await query.OrderByDescending(j => j.CreatedAt).Take(100).ToListAsync(ct);
        return jobs.Select(ToDto).ToList();
    }

    public async Task<JobRecordDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var job = await _context.JobRecords.FindAsync([id], ct);
        return job is null ? null : ToDto(job);
    }

    public async Task CancelAsync(Guid id, CancellationToken ct = default)
    {
        var job = await _context.JobRecords.FindAsync([id], ct);
        if (job is null) return;
        job.Cancel();
        await _context.SaveChangesAsync(ct);
    }

    private static JobRecordDto ToDto(JobRecord j) =>
        new(j.Id, j.Type, j.Status, j.Progress, j.Phase,
            j.CorrelationId, j.CreatedAt, j.StartedAt, j.CompletedAt,
            j.ErrorMessage, j.ResultData);
}

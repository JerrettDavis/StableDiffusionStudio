using StableDiffusionStudio.Application.DTOs;

namespace StableDiffusionStudio.Application.Interfaces;

public interface IJobQueue
{
    Task<Guid> EnqueueAsync(string type, string? data = null, CancellationToken ct = default);
    Task<IReadOnlyList<JobRecordDto>> ListAsync(bool activeOnly = false, CancellationToken ct = default);
    Task<JobRecordDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task CancelAsync(Guid id, CancellationToken ct = default);
}

using StableDiffusionStudio.Domain.Entities;

namespace StableDiffusionStudio.Infrastructure.Jobs;

public interface IJobHandler
{
    Task HandleAsync(JobRecord job, CancellationToken ct);
}

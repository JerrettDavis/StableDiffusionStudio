namespace StableDiffusionStudio.Application.Interfaces;

public interface IDataManagementService
{
    // Counts for display
    Task<DataUsageSummary> GetUsageSummaryAsync(CancellationToken ct = default);

    // Granular delete operations
    Task<int> DeleteAllGeneratedImagesAsync(CancellationToken ct = default);
    Task<int> DeleteGeneratedImagesForProjectAsync(Guid projectId, CancellationToken ct = default);
    Task<int> DeleteGenerationJobsAsync(CancellationToken ct = default);
    Task<int> DeleteGenerationJobsForProjectAsync(Guid projectId, CancellationToken ct = default);
    Task<int> DeleteAllJobRecordsAsync(CancellationToken ct = default);
    Task<int> DeleteCompletedJobRecordsAsync(CancellationToken ct = default);
    Task<int> DeleteFailedJobRecordsAsync(CancellationToken ct = default);
    Task DeleteJobRecordAsync(Guid jobId, CancellationToken ct = default);
    Task<int> DeleteAllModelRecordsAsync(CancellationToken ct = default);
    Task<int> DeleteAllProjectsAsync(CancellationToken ct = default);
    Task<int> DeleteProjectAsync(Guid projectId, CancellationToken ct = default);
    Task DeleteGenerationJobAsync(Guid jobId, CancellationToken ct = default);
    Task DeleteGeneratedImageAsync(Guid imageId, CancellationToken ct = default);
    Task ResetAllDataAsync(CancellationToken ct = default);

    // Disk cleanup
    Task<long> GetAssetsDiskUsageAsync(CancellationToken ct = default);
    Task<int> CleanOrphanedAssetsAsync(CancellationToken ct = default);
}

public record DataUsageSummary(
    int ProjectCount,
    int ModelRecordCount,
    int GenerationJobCount,
    int GeneratedImageCount,
    int JobRecordCount,
    long AssetsDiskUsageBytes);

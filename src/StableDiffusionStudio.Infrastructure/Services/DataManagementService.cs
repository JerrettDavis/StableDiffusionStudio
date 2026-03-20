using Microsoft.EntityFrameworkCore;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Infrastructure.Persistence;

namespace StableDiffusionStudio.Infrastructure.Services;

public class DataManagementService : IDataManagementService
{
    private readonly AppDbContext _context;
    private readonly string _assetsBasePath;

    public DataManagementService(AppDbContext context, IAppPaths appPaths)
    {
        _context = context;
        _assetsBasePath = appPaths.AssetsDirectory;
    }

    public async Task<DataUsageSummary> GetUsageSummaryAsync(CancellationToken ct = default)
    {
        return new DataUsageSummary(
            ProjectCount: await _context.Projects.CountAsync(ct),
            ModelRecordCount: await _context.ModelRecords.CountAsync(ct),
            GenerationJobCount: await _context.GenerationJobs.CountAsync(ct),
            GeneratedImageCount: await _context.GeneratedImages.CountAsync(ct),
            JobRecordCount: await _context.JobRecords.CountAsync(ct),
            AssetsDiskUsageBytes: GetDirectorySize(_assetsBasePath));
    }

    public async Task<int> DeleteAllGeneratedImagesAsync(CancellationToken ct = default)
    {
        var images = await _context.GeneratedImages.ToListAsync(ct);
        foreach (var img in images)
            TryDeleteFile(img.FilePath);
        _context.GeneratedImages.RemoveRange(images);
        await _context.SaveChangesAsync(ct);
        return images.Count;
    }

    public async Task<int> DeleteGeneratedImagesForProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        var jobIds = await _context.GenerationJobs
            .Where(j => j.ProjectId == projectId)
            .Select(j => j.Id)
            .ToListAsync(ct);
        var images = await _context.GeneratedImages
            .Where(i => jobIds.Contains(i.GenerationJobId))
            .ToListAsync(ct);
        foreach (var img in images)
            TryDeleteFile(img.FilePath);
        _context.GeneratedImages.RemoveRange(images);
        await _context.SaveChangesAsync(ct);

        // Clean up the project's asset directory
        var projectDir = Path.Combine(_assetsBasePath, projectId.ToString());
        TryDeleteDirectory(projectDir);

        return images.Count;
    }

    public async Task<int> DeleteGenerationJobsAsync(CancellationToken ct = default)
    {
        // Delete images first (cascade)
        await DeleteAllGeneratedImagesAsync(ct);
        var jobs = await _context.GenerationJobs.ToListAsync(ct);
        _context.GenerationJobs.RemoveRange(jobs);
        await _context.SaveChangesAsync(ct);
        return jobs.Count;
    }

    public async Task<int> DeleteGenerationJobsForProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        await DeleteGeneratedImagesForProjectAsync(projectId, ct);
        var jobs = await _context.GenerationJobs
            .Where(j => j.ProjectId == projectId)
            .ToListAsync(ct);
        _context.GenerationJobs.RemoveRange(jobs);
        await _context.SaveChangesAsync(ct);
        return jobs.Count;
    }

    public async Task<int> DeleteAllJobRecordsAsync(CancellationToken ct = default)
    {
        var records = await _context.JobRecords.ToListAsync(ct);
        _context.JobRecords.RemoveRange(records);
        await _context.SaveChangesAsync(ct);
        return records.Count;
    }

    public async Task<int> DeleteCompletedJobRecordsAsync(CancellationToken ct = default)
    {
        var records = await _context.JobRecords
            .Where(j => j.Status == JobStatus.Completed)
            .ToListAsync(ct);
        _context.JobRecords.RemoveRange(records);
        await _context.SaveChangesAsync(ct);
        return records.Count;
    }

    public async Task<int> DeleteFailedJobRecordsAsync(CancellationToken ct = default)
    {
        var records = await _context.JobRecords
            .Where(j => j.Status == JobStatus.Failed)
            .ToListAsync(ct);
        _context.JobRecords.RemoveRange(records);
        await _context.SaveChangesAsync(ct);
        return records.Count;
    }

    public async Task<int> DeleteAllModelRecordsAsync(CancellationToken ct = default)
    {
        var records = await _context.ModelRecords.ToListAsync(ct);
        _context.ModelRecords.RemoveRange(records);
        await _context.SaveChangesAsync(ct);
        return records.Count;
    }

    public async Task<int> DeleteAllProjectsAsync(CancellationToken ct = default)
    {
        // Delete all generation data first
        await DeleteGenerationJobsAsync(ct);
        var projects = await _context.Projects.ToListAsync(ct);
        _context.Projects.RemoveRange(projects);
        await _context.SaveChangesAsync(ct);
        // Clean entire assets directory
        TryDeleteDirectory(_assetsBasePath);
        Directory.CreateDirectory(_assetsBasePath);
        return projects.Count;
    }

    public async Task<int> DeleteProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        await DeleteGenerationJobsForProjectAsync(projectId, ct);
        var project = await _context.Projects.FindAsync([projectId], ct);
        if (project is not null)
        {
            _context.Projects.Remove(project);
            await _context.SaveChangesAsync(ct);
            return 1;
        }
        return 0;
    }

    public async Task DeleteGenerationJobAsync(Guid jobId, CancellationToken ct = default)
    {
        var images = await _context.GeneratedImages
            .Where(i => i.GenerationJobId == jobId)
            .ToListAsync(ct);
        foreach (var img in images)
            TryDeleteFile(img.FilePath);
        _context.GeneratedImages.RemoveRange(images);

        var job = await _context.GenerationJobs.FindAsync([jobId], ct);
        if (job is not null)
            _context.GenerationJobs.Remove(job);

        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteGeneratedImageAsync(Guid imageId, CancellationToken ct = default)
    {
        var image = await _context.GeneratedImages.FindAsync([imageId], ct);
        if (image is not null)
        {
            TryDeleteFile(image.FilePath);
            _context.GeneratedImages.Remove(image);
            await _context.SaveChangesAsync(ct);
        }
    }

    public async Task ResetAllDataAsync(CancellationToken ct = default)
    {
        await DeleteAllProjectsAsync(ct);
        await DeleteAllModelRecordsAsync(ct);
        await DeleteAllJobRecordsAsync(ct);
        // Settings are intentionally preserved during reset
    }

    public Task<long> GetAssetsDiskUsageAsync(CancellationToken ct = default)
    {
        return Task.FromResult(GetDirectorySize(_assetsBasePath));
    }

    public async Task<int> CleanOrphanedAssetsAsync(CancellationToken ct = default)
    {
        if (!Directory.Exists(_assetsBasePath)) return 0;

        var knownPaths = await _context.GeneratedImages
            .Select(i => i.FilePath)
            .ToListAsync(ct);
        var knownSet = new HashSet<string>(knownPaths, StringComparer.OrdinalIgnoreCase);

        int cleaned = 0;
        foreach (var file in Directory.EnumerateFiles(_assetsBasePath, "*.png", SearchOption.AllDirectories))
        {
            if (!knownSet.Contains(file))
            {
                TryDeleteFile(file);
                cleaned++;
            }
        }
        return cleaned;
    }

    private static long GetDirectorySize(string path)
    {
        if (!Directory.Exists(path)) return 0;
        return Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
            .Sum(f => new FileInfo(f).Length);
    }

    private static void TryDeleteFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    private static void TryDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { }
    }
}

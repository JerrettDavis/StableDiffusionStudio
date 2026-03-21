using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StableDiffusionStudio.Infrastructure.Persistence;

namespace StableDiffusionStudio.Infrastructure.Services;

public class DatabaseHealthCheck : IHealthCheck
{
    private readonly AppDbContext _context;

    public DatabaseHealthCheck(AppDbContext context) => _context = context;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            // Verify each table is accessible
            var tables = new Dictionary<string, Func<Task<int>>>
            {
                ["Projects"] = () => _context.Projects.CountAsync(ct),
                ["ModelRecords"] = () => _context.ModelRecords.CountAsync(ct),
                ["JobRecords"] = () => _context.JobRecords.CountAsync(ct),
                ["Settings"] = () => _context.Settings.CountAsync(ct),
                ["GenerationJobs"] = () => _context.GenerationJobs.CountAsync(ct),
                ["GeneratedImages"] = () => _context.GeneratedImages.CountAsync(ct),
                ["GenerationPresets"] = () => _context.GenerationPresets.CountAsync(ct),
                ["PromptHistories"] = () => _context.PromptHistories.CountAsync(ct),
            };

            var errors = new List<string>();
            var data = new Dictionary<string, object>();

            foreach (var (table, countFn) in tables)
            {
                try
                {
                    var count = await countFn();
                    data[table] = count;
                }
                catch (Exception ex)
                {
                    errors.Add($"{table}: {ex.Message}");
                }
            }

            if (errors.Count > 0)
            {
                return HealthCheckResult.Unhealthy(
                    $"Database schema issues: {string.Join("; ", errors)}",
                    data: data.ToDictionary(kv => kv.Key, kv => kv.Value));
            }

            return HealthCheckResult.Healthy(
                $"All {tables.Count} tables accessible",
                data: data.ToDictionary(kv => kv.Key, kv => kv.Value));
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database unreachable", ex);
        }
    }
}

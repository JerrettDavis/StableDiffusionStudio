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
            // Lightweight probe — just test the connection works
            var canConnect = await _context.Database.CanConnectAsync(ct);
            return canConnect
                ? HealthCheckResult.Healthy("Database connected")
                : HealthCheckResult.Unhealthy("Cannot connect to database");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database error", ex);
        }
    }
}

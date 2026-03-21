using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Infrastructure.Persistence;

namespace StableDiffusionStudio.Infrastructure.Settings;

public class SettingsExportService : ISettingsExportService
{
    private readonly AppDbContext _context;
    private readonly ILogger<SettingsExportService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public SettingsExportService(AppDbContext context, ILogger<SettingsExportService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<string> ExportAllAsync(CancellationToken ct = default)
    {
        var settings = await _context.Settings
            .AsNoTracking()
            .OrderBy(s => s.Key)
            .ToListAsync(ct);

        var dict = new Dictionary<string, string>();
        foreach (var setting in settings)
        {
            dict[setting.Key] = setting.Value;
        }

        _logger.LogInformation("Exported {Count} settings", dict.Count);
        return JsonSerializer.Serialize(dict, JsonOptions);
    }

    public async Task ImportAllAsync(string json, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("JSON content cannot be empty.", nameof(json));

        var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
            ?? throw new ArgumentException("Invalid JSON format — expected a key-value object.", nameof(json));

        var imported = 0;
        foreach (var (key, value) in dict)
        {
            if (string.IsNullOrWhiteSpace(key)) continue;

            var existing = await _context.Settings.FindAsync([key], ct);
            if (existing is null)
            {
                var setting = Domain.Entities.Setting.Create(key, value);
                _context.Settings.Add(setting);
            }
            else
            {
                existing.Update(value);
            }
            imported++;
        }

        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Imported {Count} settings", imported);
    }
}

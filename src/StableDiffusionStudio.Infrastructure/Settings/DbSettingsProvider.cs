using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.Entities;
using StableDiffusionStudio.Infrastructure.Persistence;

namespace StableDiffusionStudio.Infrastructure.Settings;

public class DbSettingsProvider : ISettingsProvider
{
    private readonly AppDbContext _context;

    public DbSettingsProvider(AppDbContext context)
    {
        _context = context;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        var raw = await GetRawAsync(key, ct);
        if (raw is null) return null;
        return JsonSerializer.Deserialize<T>(raw);
    }

    public async Task SetAsync<T>(string key, T value, CancellationToken ct = default) where T : class
    {
        var json = JsonSerializer.Serialize(value);
        await SetRawAsync(key, json, ct);
    }

    public async Task<string?> GetRawAsync(string key, CancellationToken ct = default)
    {
        var setting = await _context.Settings.FindAsync([key], ct);
        return setting?.Value;
    }

    public async Task SetRawAsync(string key, string value, CancellationToken ct = default)
    {
        var setting = await _context.Settings.FindAsync([key], ct);
        if (setting is null)
        {
            setting = Setting.Create(key, value);
            _context.Settings.Add(setting);
        }
        else
        {
            setting.Update(value);
        }
        await _context.SaveChangesAsync(ct);
    }
}

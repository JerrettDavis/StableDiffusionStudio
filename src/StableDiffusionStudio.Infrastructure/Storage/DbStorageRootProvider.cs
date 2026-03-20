using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Infrastructure.Storage;

public class DbStorageRootProvider : IStorageRootProvider
{
    private const string StorageRootsKey = "storage-roots";
    private readonly ISettingsProvider _settings;

    public DbStorageRootProvider(ISettingsProvider settings)
    {
        _settings = settings;
    }

    public async Task<IReadOnlyList<StorageRoot>> GetRootsAsync(CancellationToken ct = default)
    {
        var entries = await _settings.GetAsync<List<StorageRootEntry>>(StorageRootsKey, ct);
        if (entries is null) return Array.Empty<StorageRoot>();
        return entries.Select(e => new StorageRoot(e.Path, e.DisplayName, e.ModelTypeTag)).ToList();
    }

    public async Task AddRootAsync(StorageRoot root, CancellationToken ct = default)
    {
        var entries = await _settings.GetAsync<List<StorageRootEntry>>(StorageRootsKey, ct) ?? [];
        if (entries.All(e => e.Path != root.Path))
        {
            entries.Add(new StorageRootEntry(root.Path, root.DisplayName, root.ModelTypeTag));
            await _settings.SetAsync(StorageRootsKey, entries, ct);
        }
    }

    public async Task RemoveRootAsync(string path, CancellationToken ct = default)
    {
        var entries = await _settings.GetAsync<List<StorageRootEntry>>(StorageRootsKey, ct);
        if (entries is null) return;
        entries.RemoveAll(e => e.Path == path);
        await _settings.SetAsync(StorageRootsKey, entries, ct);
    }

    private record StorageRootEntry(string Path, string DisplayName, ModelType? ModelTypeTag = null);
}

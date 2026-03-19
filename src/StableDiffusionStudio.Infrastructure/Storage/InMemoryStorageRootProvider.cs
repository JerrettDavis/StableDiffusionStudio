using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Infrastructure.Storage;

public class InMemoryStorageRootProvider : IStorageRootProvider
{
    private readonly List<StorageRoot> _roots = [];

    public Task<IReadOnlyList<StorageRoot>> GetRootsAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<StorageRoot>>(_roots.AsReadOnly());

    public Task AddRootAsync(StorageRoot root, CancellationToken ct = default)
    {
        if (_roots.All(r => r.Path != root.Path))
            _roots.Add(root);
        return Task.CompletedTask;
    }

    public Task RemoveRootAsync(string path, CancellationToken ct = default)
    {
        _roots.RemoveAll(r => r.Path == path);
        return Task.CompletedTask;
    }
}

using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Application.Interfaces;

public interface IStorageRootProvider
{
    Task<IReadOnlyList<StorageRoot>> GetRootsAsync(CancellationToken ct = default);
    Task AddRootAsync(StorageRoot root, CancellationToken ct = default);
    Task RemoveRootAsync(string path, CancellationToken ct = default);
}

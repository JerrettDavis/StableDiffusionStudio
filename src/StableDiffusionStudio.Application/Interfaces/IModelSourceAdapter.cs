using StableDiffusionStudio.Application.DTOs;
using StableDiffusionStudio.Domain.Entities;
using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Application.Interfaces;

public interface IModelSourceAdapter
{
    string SourceName { get; }
    Task<IReadOnlyList<ModelRecord>> ScanAsync(StorageRoot root, CancellationToken ct = default);
    ModelSourceCapabilities GetCapabilities();
}

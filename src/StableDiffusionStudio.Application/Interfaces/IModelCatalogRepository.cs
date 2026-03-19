using StableDiffusionStudio.Application.DTOs;
using StableDiffusionStudio.Domain.Entities;

namespace StableDiffusionStudio.Application.Interfaces;

public interface IModelCatalogRepository
{
    Task<ModelRecord?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ModelRecord>> ListAsync(ModelFilter filter, CancellationToken ct = default);
    Task<ModelRecord?> GetByFilePathAsync(string filePath, CancellationToken ct = default);
    Task UpsertAsync(ModelRecord record, CancellationToken ct = default);
    Task RemoveAsync(Guid id, CancellationToken ct = default);
}

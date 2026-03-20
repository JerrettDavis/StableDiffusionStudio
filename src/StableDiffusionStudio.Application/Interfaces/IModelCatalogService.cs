using StableDiffusionStudio.Application.Commands;
using StableDiffusionStudio.Application.DTOs;

namespace StableDiffusionStudio.Application.Interfaces;

public interface IModelCatalogService
{
    Task<ScanResult> ScanAsync(ScanModelsCommand command, CancellationToken ct = default);
    Task<IReadOnlyList<ModelRecordDto>> ListAsync(ModelFilter filter, CancellationToken ct = default);
    Task<ModelRecordDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<SearchResult> SearchAsync(ModelSearchQuery query, CancellationToken ct = default);
    Task<Guid> RequestDownloadAsync(DownloadRequest request, CancellationToken ct = default);
    IReadOnlyList<ModelProviderInfo> GetProviders();
}

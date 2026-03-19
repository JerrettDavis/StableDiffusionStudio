using StableDiffusionStudio.Application.DTOs;
using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Application.Interfaces;

public interface IModelProvider
{
    string ProviderId { get; }
    string DisplayName { get; }
    ModelProviderCapabilities Capabilities { get; }
    Task<IReadOnlyList<DiscoveredModel>> ScanLocalAsync(StorageRoot root, CancellationToken ct = default);
    Task<SearchResult> SearchAsync(ModelSearchQuery query, CancellationToken ct = default);
    Task<DownloadResult> DownloadAsync(DownloadRequest request, IProgress<DownloadProgress> progress, CancellationToken ct = default);
    Task<bool> ValidateCredentialsAsync(CancellationToken ct = default);
}

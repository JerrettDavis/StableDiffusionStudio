using Microsoft.Extensions.Logging;
using StableDiffusionStudio.Application.Commands;
using StableDiffusionStudio.Application.DTOs;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.Entities;
using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Application.Services;

public class ModelCatalogService : IModelCatalogService
{
    private readonly IModelCatalogRepository _repository;
    private readonly IEnumerable<IModelProvider> _providers;
    private readonly IStorageRootProvider _rootProvider;
    private readonly IJobQueue _jobQueue;
    private readonly ILogger<ModelCatalogService>? _logger;

    public ModelCatalogService(IModelCatalogRepository repository, IEnumerable<IModelProvider> providers,
        IStorageRootProvider rootProvider, IJobQueue jobQueue, ILogger<ModelCatalogService>? logger = null)
    {
        _repository = repository;
        _providers = providers;
        _rootProvider = rootProvider;
        _jobQueue = jobQueue;
        _logger = logger;
    }

    public async Task<ScanResult> ScanAsync(ScanModelsCommand command, CancellationToken ct = default)
    {
        var roots = await _rootProvider.GetRootsAsync(ct);
        if (command.StorageRootPath is not null)
            roots = roots.Where(r => r.Path == command.StorageRootPath).ToList();

        int newCount = 0, updatedCount = 0;
        foreach (var root in roots)
        {
            foreach (var provider in _providers.Where(p => p.Capabilities.CanScanLocal))
            {
                _logger?.LogInformation("Scanning {Root} with {Provider}", root.Path, provider.ProviderId);
                var discovered = await provider.ScanLocalAsync(root, ct);
                foreach (var model in discovered)
                {
                    var existing = await _repository.GetByFilePathAsync(model.FilePath, ct);
                    if (existing is not null)
                    {
                        existing.UpdateMetadata(modelFamily: model.Family, previewImagePath: model.PreviewImagePath, type: model.Type);
                        existing.MarkAvailable();
                        await _repository.UpsertAsync(existing, ct);
                        updatedCount++;
                    }
                    else
                    {
                        var record = ModelRecord.Create(model.Title, model.FilePath, model.Family,
                            model.Format, model.FileSize, provider.ProviderId, model.Type);
                        if (model.PreviewImagePath is not null)
                            record.UpdateMetadata(previewImagePath: model.PreviewImagePath);
                        if (model.Description is not null)
                            record.UpdateMetadata(description: model.Description);
                        if (model.Tags.Count > 0)
                            record.UpdateMetadata(tags: model.Tags);
                        await _repository.UpsertAsync(record, ct);
                        newCount++;
                    }
                }
            }
        }

        return new ScanResult(newCount, updatedCount, MissingCount: 0);
    }

    public async Task<IReadOnlyList<ModelRecordDto>> ListAsync(ModelFilter filter, CancellationToken ct = default)
    {
        var records = await _repository.ListAsync(filter, ct);
        return records.Select(ToDto).ToList();
    }

    public async Task<ModelRecordDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var record = await _repository.GetByIdAsync(id, ct);
        return record is null ? null : ToDto(record);
    }

    public async Task<SearchResult> SearchAsync(ModelSearchQuery query, CancellationToken ct = default)
    {
        var provider = _providers.FirstOrDefault(p => p.ProviderId == query.ProviderId);
        if (provider is null || !provider.Capabilities.CanSearch)
        {
            _logger?.LogWarning("No searchable provider found for {ProviderId}", query.ProviderId);
            return new SearchResult([], 0, false);
        }

        return await provider.SearchAsync(query, ct);
    }

    public async Task<Guid> RequestDownloadAsync(DownloadRequest request, CancellationToken ct = default)
    {
        var data = System.Text.Json.JsonSerializer.Serialize(request);
        return await _jobQueue.EnqueueAsync("model-download", data, ct);
    }

    public IReadOnlyList<ModelProviderInfo> GetProviders()
    {
        return _providers.Select(p => new ModelProviderInfo(p.ProviderId, p.DisplayName, p.Capabilities)).ToList();
    }

    private static ModelRecordDto ToDto(ModelRecord r) =>
        new(r.Id, r.Title, r.Type, r.ModelFamily, r.Format, r.FilePath, r.FileSize,
            r.Source, r.Tags, r.Description, r.PreviewImagePath, r.CompatibilityHints, r.Status, r.DetectedAt);
}

using Microsoft.Extensions.Logging;
using StableDiffusionStudio.Application.Commands;
using StableDiffusionStudio.Application.DTOs;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.Entities;

namespace StableDiffusionStudio.Application.Services;

public class ModelCatalogService
{
    private readonly IModelCatalogRepository _repository;
    private readonly IEnumerable<IModelSourceAdapter> _adapters;
    private readonly IStorageRootProvider _rootProvider;
    private readonly ILogger<ModelCatalogService>? _logger;

    public ModelCatalogService(IModelCatalogRepository repository, IEnumerable<IModelSourceAdapter> adapters,
        IStorageRootProvider rootProvider, ILogger<ModelCatalogService>? logger = null)
    {
        _repository = repository;
        _adapters = adapters;
        _rootProvider = rootProvider;
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
            foreach (var adapter in _adapters)
            {
                _logger?.LogInformation("Scanning {Root} with {Adapter}", root.Path, adapter.SourceName);
                var discovered = await adapter.ScanAsync(root, ct);
                foreach (var record in discovered)
                {
                    var existing = await _repository.GetByFilePathAsync(record.FilePath, ct);
                    if (existing is not null)
                    {
                        existing.UpdateMetadata(modelFamily: record.ModelFamily, previewImagePath: record.PreviewImagePath);
                        existing.MarkAvailable();
                        await _repository.UpsertAsync(existing, ct);
                        updatedCount++;
                    }
                    else
                    {
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

    private static ModelRecordDto ToDto(ModelRecord r) =>
        new(r.Id, r.Title, r.ModelFamily, r.Format, r.FilePath, r.FileSize,
            r.Source, r.Tags, r.Description, r.PreviewImagePath, r.CompatibilityHints, r.Status, r.DetectedAt);
}

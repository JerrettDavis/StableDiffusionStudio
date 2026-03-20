using StableDiffusionStudio.Application.DTOs;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Domain.Services;
using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Infrastructure.ModelSources;

public class LocalFolderProvider : IModelProvider
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".safetensors", ".ckpt", ".gguf" };

    private static readonly HashSet<string> PreviewExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".png", ".jpg", ".jpeg", ".webp" };

    public string ProviderId => "local-folder";

    public string DisplayName => "Local Folder";

    public ModelProviderCapabilities Capabilities => new(
        CanScanLocal: true,
        CanSearch: false,
        CanDownload: false,
        RequiresAuth: false,
        SupportedModelTypes: Enum.GetValues<ModelType>().ToList());

    public Task<IReadOnlyList<DiscoveredModel>> ScanLocalAsync(StorageRoot root, CancellationToken ct = default)
    {
        var results = new List<DiscoveredModel>();
        if (!Directory.Exists(root.Path))
            return Task.FromResult<IReadOnlyList<DiscoveredModel>>(results);

        var files = Directory.EnumerateFiles(root.Path, "*.*", SearchOption.AllDirectories)
            .Where(f => SupportedExtensions.Contains(Path.GetExtension(f)));

        foreach (var filePath in files)
        {
            ct.ThrowIfCancellationRequested();
            var fileInfo = new FileInfo(filePath);
            var modelFileInfo = new ModelFileInfo(fileInfo.Name, fileInfo.Length, ReadHeaderHint(filePath));
            var format = ModelFileAnalyzer.InferFormat(modelFileInfo);
            var family = ModelFileAnalyzer.InferFamily(modelFileInfo);
            var modelType = root.ModelTypeTag ?? ModelFileAnalyzer.InferModelType(modelFileInfo);
            var previewPath = FindPreviewImage(filePath);

            results.Add(new DiscoveredModel(
                FilePath: filePath,
                Title: null,
                Type: modelType,
                Family: family,
                Format: format,
                FileSize: fileInfo.Length,
                PreviewImagePath: previewPath,
                Description: null,
                Tags: Array.Empty<string>()));
        }

        return Task.FromResult<IReadOnlyList<DiscoveredModel>>(results);
    }

    public Task<SearchResult> SearchAsync(ModelSearchQuery query, CancellationToken ct = default)
    {
        return Task.FromResult(new SearchResult([], 0, false));
    }

    public Task<DownloadResult> DownloadAsync(DownloadRequest request, IProgress<DownloadProgress> progress, CancellationToken ct = default)
    {
        return Task.FromResult(new DownloadResult(false, null, "Local provider does not support downloads"));
    }

    public Task<bool> ValidateCredentialsAsync(CancellationToken ct = default)
    {
        return Task.FromResult(true);
    }

    private static string? FindPreviewImage(string modelFilePath)
    {
        var dir = Path.GetDirectoryName(modelFilePath);
        if (dir is null) return null;
        var baseName = Path.GetFileNameWithoutExtension(modelFilePath);
        foreach (var ext in PreviewExtensions)
        {
            var previewPath = Path.Combine(dir, $"{baseName}.preview{ext}");
            if (File.Exists(previewPath)) return previewPath;
        }
        return null;
    }

    private static string? ReadHeaderHint(string filePath)
    {
        if (!filePath.EndsWith(".safetensors", StringComparison.OrdinalIgnoreCase))
            return null;
        try
        {
            using var stream = File.OpenRead(filePath);
            if (stream.Length < 8) return null;
            var lengthBytes = new byte[8];
            stream.ReadExactly(lengthBytes);
            var headerLength = BitConverter.ToInt64(lengthBytes);
            if (headerLength <= 0 || headerLength > 10_000_000) return null;
            var headerBytes = new byte[Math.Min(headerLength, 4096)];
            stream.ReadExactly(headerBytes);
            return System.Text.Encoding.UTF8.GetString(headerBytes);
        }
        catch
        {
            return null;
        }
    }
}

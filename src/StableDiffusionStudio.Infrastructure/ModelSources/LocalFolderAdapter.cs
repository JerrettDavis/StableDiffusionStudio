using StableDiffusionStudio.Application.DTOs;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.Entities;
using StableDiffusionStudio.Domain.Services;
using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Infrastructure.ModelSources;

public class LocalFolderAdapter : IModelSourceAdapter
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".safetensors", ".ckpt", ".gguf" };

    private static readonly HashSet<string> PreviewExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".png", ".jpg", ".jpeg", ".webp" };

    public string SourceName => "local-folder";

    public Task<IReadOnlyList<ModelRecord>> ScanAsync(StorageRoot root, CancellationToken ct = default)
    {
        var results = new List<ModelRecord>();
        if (!Directory.Exists(root.Path))
            return Task.FromResult<IReadOnlyList<ModelRecord>>(results);

        var files = Directory.EnumerateFiles(root.Path, "*.*", SearchOption.AllDirectories)
            .Where(f => SupportedExtensions.Contains(Path.GetExtension(f)));

        foreach (var filePath in files)
        {
            ct.ThrowIfCancellationRequested();
            var fileInfo = new FileInfo(filePath);
            var modelFileInfo = new ModelFileInfo(fileInfo.Name, fileInfo.Length, ReadHeaderHint(filePath));
            var format = ModelFileAnalyzer.InferFormat(modelFileInfo);
            var family = ModelFileAnalyzer.InferFamily(modelFileInfo);
            var previewPath = FindPreviewImage(filePath);
            var record = ModelRecord.Create(null, filePath, family, format, fileInfo.Length, SourceName);
            if (previewPath is not null)
                record.UpdateMetadata(previewImagePath: previewPath);
            results.Add(record);
        }

        return Task.FromResult<IReadOnlyList<ModelRecord>>(results);
    }

    public ModelSourceCapabilities GetCapabilities() =>
        new(CanScanLocal: true, CanDownload: false, CanSearch: false, RequiresAuth: false);

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

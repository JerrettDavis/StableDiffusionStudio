using Microsoft.Extensions.Logging;
using StableDiffusionStudio.Application.Interfaces;

namespace StableDiffusionStudio.Infrastructure.Services;

/// <summary>
/// Scans configured storage roots and common A1111/Forge directory structures
/// to locate Flux model components (VAE, CLIP-L, T5-XXL).
/// </summary>
public class FluxComponentResolver : IFluxComponentResolver
{
    private readonly IStorageRootProvider _rootProvider;
    private readonly ILogger<FluxComponentResolver> _logger;

    public FluxComponentResolver(IStorageRootProvider rootProvider, ILogger<FluxComponentResolver> logger)
    {
        _rootProvider = rootProvider;
        _logger = logger;
    }

    public async Task<FluxComponents?> ResolveAsync(string modelPath, CancellationToken ct = default)
    {
        var modelDir = Path.GetDirectoryName(modelPath);
        var roots = await _rootProvider.GetRootsAsync(ct);

        // Build search directories from storage roots + common relative paths
        var searchDirs = new List<string>();
        foreach (var root in roots)
        {
            searchDirs.Add(root.Path);
            // Check common Forge/A1111 directory structure relative to root parent
            var parentDir = Path.GetDirectoryName(root.Path);
            if (parentDir != null)
            {
                searchDirs.Add(Path.Combine(parentDir, "VAE"));
                searchDirs.Add(Path.Combine(parentDir, "text_encoder"));
            }
        }

        if (modelDir != null)
        {
            searchDirs.Add(modelDir);
            var modelsRoot = Path.GetDirectoryName(modelDir);
            if (modelsRoot != null)
            {
                searchDirs.Add(Path.Combine(modelsRoot, "VAE"));
                searchDirs.Add(Path.Combine(modelsRoot, "text_encoder"));
            }
        }

        searchDirs = searchDirs.Where(DirectoryExistsSafe).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        var vae = FindFile(searchDirs, ["ae.safetensors", "flux*vae*", "flux*ae*"]);
        var clipL = FindFile(searchDirs, ["clip_l.safetensors", "clip_l*.gguf"]);
        // Prefer GGUF quantized (Q8_0) over fp16 for memory efficiency
        var t5xxl = FindFile(searchDirs,
        [
            "t5*xxl*Q8*.gguf", "t5*xxl*q8*.gguf",
            "t5xxl_fp8*.safetensors", "t5xxl_fp16*.safetensors",
            "t5*xxl*.gguf", "t5*xxl*.safetensors"
        ]);

        _logger.LogInformation("Flux component resolution: VAE={Vae}, CLIP-L={ClipL}, T5-XXL={T5xxl}",
            vae ?? "NOT FOUND", clipL ?? "NOT FOUND", t5xxl ?? "NOT FOUND");

        if (vae == null && clipL == null && t5xxl == null)
            return null;

        return new FluxComponents(vae, clipL, t5xxl);
    }

    private static bool DirectoryExistsSafe(string path)
    {
        try
        {
            return Directory.Exists(path);
        }
        catch
        {
            return false;
        }
    }

    private static string? FindFile(List<string> searchDirs, string[] patterns)
    {
        foreach (var dir in searchDirs)
        {
            foreach (var pattern in patterns)
            {
                try
                {
                    var files = Directory.GetFiles(dir, pattern, SearchOption.TopDirectoryOnly);
                    if (files.Length > 0)
                        return files[0];
                }
                catch
                {
                    // Directory not accessible — skip gracefully
                }
            }
        }

        return null;
    }
}

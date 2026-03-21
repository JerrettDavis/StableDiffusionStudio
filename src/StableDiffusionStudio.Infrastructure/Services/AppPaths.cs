using StableDiffusionStudio.Application.Interfaces;

namespace StableDiffusionStudio.Infrastructure.Services;

public class AppPaths : IAppPaths
{
    private readonly string _baseDir;

    public AppPaths()
    {
        _baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StableDiffusionStudio");
    }

    public string AssetsDirectory => Path.Combine(_baseDir, "Assets");
    public string DatabaseDirectory => Path.Combine(_baseDir, "Database");

    public string GetProjectAssetsDirectory(Guid projectId)
        => Path.Combine(AssetsDirectory, projectId.ToString());

    public string GetJobAssetsDirectory(Guid projectId, Guid jobId)
        => Path.Combine(AssetsDirectory, projectId.ToString(), jobId.ToString());

    public string GetImageUrl(string filePath)
    {
        if (filePath.StartsWith(AssetsDirectory, StringComparison.OrdinalIgnoreCase))
        {
            var relativePath = filePath[AssetsDirectory.Length..]
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Replace('\\', '/');
            return $"/assets/{relativePath}";
        }
        // Path.GetFileName doesn't handle backslashes on Linux, so handle both separators
        var fileName = filePath.Split('/', '\\').Last();
        return $"/assets/{fileName}";
    }
}

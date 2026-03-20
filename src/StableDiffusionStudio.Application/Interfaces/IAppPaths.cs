namespace StableDiffusionStudio.Application.Interfaces;

public interface IAppPaths
{
    string AssetsDirectory { get; }
    string DatabaseDirectory { get; }
    string GetProjectAssetsDirectory(Guid projectId);
    string GetJobAssetsDirectory(Guid projectId, Guid jobId);
    string GetImageUrl(string filePath);
}

namespace StableDiffusionStudio.Application.Interfaces;

/// <summary>
/// Pushes real-time generation notifications to connected clients (e.g. via SignalR).
/// </summary>
public interface IGenerationNotifier
{
    Task SendPreviewAsync(string projectId, int step, int totalSteps, string previewDataUrl);
    Task SendCompletedAsync(string projectId);
    Task SendFailedAsync(string projectId, string error);
}

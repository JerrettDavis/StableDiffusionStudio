using Microsoft.AspNetCore.SignalR;
using StableDiffusionStudio.Application.Interfaces;

namespace StableDiffusionStudio.Web.Hubs;

/// <summary>
/// Pushes experiment run notifications to connected clients via the StudioHub SignalR hub.
/// </summary>
public class SignalRExperimentNotifier : IExperimentNotifier
{
    private readonly IHubContext<StudioHub> _hubContext;

    public SignalRExperimentNotifier(IHubContext<StudioHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task SendProgressAsync(string runId, int completedIndex, int totalCount, string axisValuesJson, string imageUrl)
    {
        await _hubContext.Clients.All.SendAsync("ExperimentProgress", runId, completedIndex, totalCount, axisValuesJson, imageUrl);
    }

    public async Task SendStepPreviewAsync(string runId, int gridX, int gridY, string phase, byte[]? previewBytes)
    {
        await _hubContext.Clients.All.SendAsync("ExperimentStepPreview", runId, gridX, gridY, phase, previewBytes);
    }

    public async Task SendCompletedAsync(string runId)
    {
        await _hubContext.Clients.All.SendAsync("ExperimentComplete", runId);
    }

    public async Task SendFailedAsync(string runId, string error)
    {
        await _hubContext.Clients.All.SendAsync("ExperimentFailed", runId, error);
    }
}

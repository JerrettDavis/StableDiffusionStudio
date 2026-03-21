using Microsoft.AspNetCore.SignalR;
using StableDiffusionStudio.Application.Interfaces;

namespace StableDiffusionStudio.Web.Hubs;

/// <summary>
/// Pushes generation notifications to connected clients via the StudioHub SignalR hub.
/// </summary>
public class SignalRGenerationNotifier : IGenerationNotifier
{
    private readonly IHubContext<StudioHub> _hubContext;

    public SignalRGenerationNotifier(IHubContext<StudioHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task SendPreviewAsync(string projectId, int step, int totalSteps, string previewDataUrl)
    {
        await _hubContext.Clients.All.SendAsync("GenerationPreview", projectId, step, totalSteps, previewDataUrl);
    }

    public async Task SendCompletedAsync(string projectId)
    {
        await _hubContext.Clients.All.SendAsync("GenerationComplete", projectId);
    }

    public async Task SendFailedAsync(string projectId, string error)
    {
        await _hubContext.Clients.All.SendAsync("GenerationFailed", projectId, error);
    }
}

using Microsoft.AspNetCore.SignalR;
using StableDiffusionStudio.Application.Interfaces;

namespace StableDiffusionStudio.Web.Hubs;

public class SignalRModelEnrichmentNotifier : IModelEnrichmentNotifier
{
    private readonly IHubContext<StudioHub> _hubContext;

    public SignalRModelEnrichmentNotifier(IHubContext<StudioHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task SendModelEnrichedAsync(string modelId, string? previewImageUrl, string? civitAIUrl, string? huggingFaceUrl)
    {
        await _hubContext.Clients.All.SendAsync("ModelEnriched", modelId, previewImageUrl, civitAIUrl, huggingFaceUrl);
    }

    public async Task SendEnrichmentProgressAsync(int enrichedCount, int remainingCount, bool isComplete)
    {
        await _hubContext.Clients.All.SendAsync("EnrichmentProgress", enrichedCount, remainingCount, isComplete);
    }
}

using Microsoft.AspNetCore.SignalR;

namespace StableDiffusionStudio.Web.Hubs;

public class StudioHub : Hub
{
    // Client methods (called from server → client):
    // GenerationStatus(string projectId, string phase, int progressPercent)
    // GenerationPreview(string projectId, int step, int totalSteps, string previewBase64)
    // GenerationComplete(string projectId)
    // GenerationFailed(string projectId, string error)
}

using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using StableDiffusionStudio.Application.Interfaces;

namespace StableDiffusionStudio.Web.Mcp.Tools;

[McpServerToolType]
public class UtilityTools
{
    [McpServerTool(Name = "interrogate_image"), Description(
        "Generate a text prompt from an image using vision AI (Ollama). Useful for understanding image content or creating prompts for img2img.")]
    public static async Task<string> InterrogateImage(
        IImageInterrogator interrogator,
        [Description("Base64-encoded image to interrogate")] string imageBase64)
    {
        byte[] imageBytes;
        try { imageBytes = Convert.FromBase64String(imageBase64); }
        catch { return JsonSerializer.Serialize(new { error = "Invalid base64 image data" }); }

        try
        {
            var result = await interrogator.InterrogateAsync(imageBytes);
            return JsonSerializer.Serialize(new
            {
                prompt = result,
                message = "Image interrogated successfully. Use the prompt with generate_image."
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = $"Interrogation failed: {ex.Message}" });
        }
    }

    [McpServerTool(Name = "get_system_status"), Description(
        "Get the current system status: backend availability, loaded model info, and available providers.")]
    public static async Task<string> GetSystemStatus(
        IInferenceBackend inferenceBackend,
        IModelCatalogService catalogService)
    {
        var isAvailable = await inferenceBackend.IsAvailableAsync();
        var providers = catalogService.GetProviders();

        return JsonSerializer.Serialize(new
        {
            backend = new
            {
                id = inferenceBackend.BackendId,
                name = inferenceBackend.DisplayName,
                isAvailable,
                capabilities = new
                {
                    inferenceBackend.Capabilities.MaxWidth,
                    inferenceBackend.Capabilities.MaxHeight,
                    inferenceBackend.Capabilities.SupportsLoRA,
                    inferenceBackend.Capabilities.SupportsVAE,
                    supportedFamilies = inferenceBackend.Capabilities.SupportedFamilies.Select(f => f.ToString()),
                    supportedSamplers = inferenceBackend.Capabilities.SupportedSamplers.Select(s => s.ToString())
                }
            },
            providers = providers.Select(p => new
            {
                id = p.ProviderId,
                name = p.DisplayName,
                canSearch = p.Capabilities.CanSearch,
                canDownload = p.Capabilities.CanDownload
            })
        });
    }
}

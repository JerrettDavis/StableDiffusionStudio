using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using StableDiffusionStudio.Application.DTOs;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Infrastructure.Services;

public class OllamaPromptAssistantService : IPromptAssistantService
{
    private readonly HttpClient _httpClient;
    private readonly ISettingsProvider _settingsProvider;
    private readonly ILogger<OllamaPromptAssistantService> _logger;

    private const string SettingsKey = "PromptAssistantSettings";

    public OllamaPromptAssistantService(
        HttpClient httpClient,
        ISettingsProvider settingsProvider,
        ILogger<OllamaPromptAssistantService> logger)
    {
        _httpClient = httpClient;
        _settingsProvider = settingsProvider;
        _logger = logger;
    }

    public async Task<string> GeneratePromptAsync(PromptAssistantContext context, CancellationToken ct = default)
    {
        var settings = await GetSettingsAsync(ct);
        var prompt = SubstituteVariables(settings.GeneratePrompt, context);
        return await CallOllamaAsync(settings, prompt, ct);
    }

    public async Task<string> RefinePromptAsync(string currentPrompt, PromptAssistantContext context,
        CancellationToken ct = default)
    {
        var settings = await GetSettingsAsync(ct);
        var ctx = context with { CurrentPositivePrompt = currentPrompt };
        var prompt = SubstituteVariables(settings.RefinePrompt, ctx);
        return await CallOllamaAsync(settings, prompt, ct);
    }

    public async Task<string> GenerateNegativePromptAsync(PromptAssistantContext context,
        CancellationToken ct = default)
    {
        var settings = await GetSettingsAsync(ct);
        var prompt = SubstituteVariables(settings.GenerateNegativePrompt, context);
        return await CallOllamaAsync(settings, prompt, ct);
    }

    public async Task<string> RefineNegativePromptAsync(string currentPrompt, PromptAssistantContext context,
        CancellationToken ct = default)
    {
        var settings = await GetSettingsAsync(ct);
        var ctx = context with { CurrentNegativePrompt = currentPrompt };
        var prompt = SubstituteVariables(settings.RefineNegativePrompt, ctx);
        return await CallOllamaAsync(settings, prompt, ct);
    }

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        var settings = await GetSettingsAsync(ct);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{settings.OllamaUrl}/api/tags");
            using var response = await _httpClient.SendAsync(request, ct);
            _logger.LogInformation("Ollama prompt assistant connection test: {StatusCode} at {Url}",
                response.StatusCode, settings.OllamaUrl);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ollama prompt assistant not available at {Url}", settings.OllamaUrl);
            return false;
        }
    }

    internal async Task<PromptAssistantSettings> GetSettingsAsync(CancellationToken ct)
    {
        return await _settingsProvider.GetAsync<PromptAssistantSettings>(SettingsKey, ct)
               ?? PromptAssistantSettings.Default;
    }

    private async Task<string> CallOllamaAsync(PromptAssistantSettings settings, string prompt,
        CancellationToken ct)
    {
        var payload = new OllamaGenerateRequest
        {
            Model = settings.Model,
            Prompt = prompt,
            Stream = false
        };

        _logger.LogInformation("Sending prompt to Ollama: {Model} at {Url}", settings.Model, settings.OllamaUrl);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(60));

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{settings.OllamaUrl}/api/generate")
        {
            Content = JsonContent.Create(payload)
        };
        using var response = await _httpClient.SendAsync(request, cts.Token);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>(cts.Token);
        var text = result?.Response?.Trim() ?? "";

        _logger.LogInformation("Prompt assistant result: {Text}", text);
        return text;
    }

    private static string SubstituteVariables(string template, PromptAssistantContext ctx)
    {
        return template
            .Replace("{{model}}", ctx.ModelName ?? "Unknown")
            .Replace("{{family}}", ctx.ModelFamily ?? "Unknown")
            .Replace("{{vae}}", ctx.VaeName ?? "None")
            .Replace("{{loras}}", ctx.LoraNames.Count > 0 ? string.Join(", ", ctx.LoraNames) : "None")
            .Replace("{{width}}", ctx.Width.ToString())
            .Replace("{{height}}", ctx.Height.ToString())
            .Replace("{{steps}}", ctx.Steps.ToString())
            .Replace("{{cfg}}", ctx.CfgScale.ToString("F1"))
            .Replace("{{sampler}}", ctx.Sampler)
            .Replace("{{scheduler}}", ctx.Scheduler)
            .Replace("{{prompt}}", ctx.CurrentPositivePrompt ?? "")
            .Replace("{{negative}}", ctx.CurrentNegativePrompt ?? "");
    }

    private sealed class OllamaGenerateRequest
    {
        [JsonPropertyName("model")] public string Model { get; init; } = "";
        [JsonPropertyName("prompt")] public string Prompt { get; init; } = "";
        [JsonPropertyName("stream")] public bool Stream { get; init; }
    }

    private sealed class OllamaGenerateResponse
    {
        [JsonPropertyName("response")] public string? Response { get; init; }
    }
}

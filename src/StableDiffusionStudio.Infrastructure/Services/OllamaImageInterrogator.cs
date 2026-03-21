using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Infrastructure.Services;

public class OllamaImageInterrogator : IImageInterrogator
{
    private readonly HttpClient _httpClient;
    private readonly ISettingsProvider _settingsProvider;
    private readonly ILogger<OllamaImageInterrogator> _logger;

    private const string SettingsKey = "InterrogationSettings";

    public OllamaImageInterrogator(
        HttpClient httpClient,
        ISettingsProvider settingsProvider,
        ILogger<OllamaImageInterrogator> logger)
    {
        _httpClient = httpClient;
        _settingsProvider = settingsProvider;
        _logger = logger;
    }

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        var settings = await GetSettingsAsync(ct);
        try
        {
            _httpClient.BaseAddress = null;
            using var response = await _httpClient.GetAsync($"{settings.OllamaUrl}/api/tags", ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Ollama not available at {Url}", settings.OllamaUrl);
            return false;
        }
    }

    public async Task<string> InterrogateAsync(byte[] imageBytes, CancellationToken ct = default)
    {
        var settings = await GetSettingsAsync(ct);
        var base64 = Convert.ToBase64String(imageBytes);

        var request = new OllamaGenerateRequest
        {
            Model = settings.Model,
            Prompt = settings.SystemPrompt,
            Images = [base64],
            Stream = false
        };

        _logger.LogInformation("Interrogating image ({Size} bytes) with {Model} at {Url}",
            imageBytes.Length, settings.Model, settings.OllamaUrl);

        var response = await _httpClient.PostAsJsonAsync(
            $"{settings.OllamaUrl}/api/generate", request, ct);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>(ct);
        var prompt = result?.Response?.Trim() ?? "";

        _logger.LogInformation("Interrogation result: {Prompt}", prompt);
        return prompt;
    }

    internal async Task<InterrogationSettings> GetSettingsAsync(CancellationToken ct)
    {
        return await _settingsProvider.GetAsync<InterrogationSettings>(SettingsKey, ct)
               ?? InterrogationSettings.Default;
    }

    private sealed class OllamaGenerateRequest
    {
        [JsonPropertyName("model")] public string Model { get; init; } = "";
        [JsonPropertyName("prompt")] public string Prompt { get; init; } = "";
        [JsonPropertyName("images")] public List<string> Images { get; init; } = [];
        [JsonPropertyName("stream")] public bool Stream { get; init; }
    }

    private sealed class OllamaGenerateResponse
    {
        [JsonPropertyName("response")] public string? Response { get; init; }
    }
}

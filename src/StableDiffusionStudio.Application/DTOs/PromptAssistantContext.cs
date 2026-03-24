namespace StableDiffusionStudio.Application.DTOs;

public record PromptAssistantContext(
    string? ModelName, string? ModelFamily, string? VaeName,
    IReadOnlyList<string> LoraNames, int Width, int Height,
    int Steps, double CfgScale, string Sampler, string Scheduler,
    string? CurrentPositivePrompt, string? CurrentNegativePrompt);

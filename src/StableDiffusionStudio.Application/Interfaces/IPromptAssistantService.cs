using StableDiffusionStudio.Application.DTOs;

namespace StableDiffusionStudio.Application.Interfaces;

public interface IPromptAssistantService
{
    Task<string> GeneratePromptAsync(PromptAssistantContext context, CancellationToken ct = default);
    Task<string> RefinePromptAsync(string currentPrompt, PromptAssistantContext context, CancellationToken ct = default);
    Task<string> GenerateNegativePromptAsync(PromptAssistantContext context, CancellationToken ct = default);
    Task<string> RefineNegativePromptAsync(string currentPrompt, PromptAssistantContext context, CancellationToken ct = default);
    Task<bool> IsAvailableAsync(CancellationToken ct = default);
}

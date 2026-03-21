using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Application.Commands;

public record CreateGenerationCommand(Guid ProjectId, GenerationParameters Parameters, byte[]? InitImageBytes = null, byte[]? MaskImageBytes = null);

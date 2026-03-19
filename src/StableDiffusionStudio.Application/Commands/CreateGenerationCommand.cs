using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Application.Commands;

public record CreateGenerationCommand(Guid ProjectId, GenerationParameters Parameters);

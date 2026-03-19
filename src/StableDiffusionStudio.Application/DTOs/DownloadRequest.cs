using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Application.DTOs;

public record DownloadRequest(string ProviderId, string ExternalId, string? VariantFileName, StorageRoot TargetRoot, ModelType Type);

using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Application.DTOs;

public record ModelFileVariant(string FileName, long FileSize, ModelFormat Format, string? Quantization);

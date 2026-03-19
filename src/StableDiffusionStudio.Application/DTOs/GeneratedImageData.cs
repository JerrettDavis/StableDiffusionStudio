namespace StableDiffusionStudio.Application.DTOs;

public sealed record GeneratedImageData(byte[] ImageBytes, long Seed, double GenerationTimeSeconds);

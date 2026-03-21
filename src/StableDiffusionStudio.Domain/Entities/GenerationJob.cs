using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Domain.Entities;

public class GenerationJob
{
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public GenerationParameters Parameters { get; private set; } = null!;
    public GenerationJobStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string? ErrorMessage { get; private set; }
    private readonly List<GeneratedImage> _images = [];
    public IReadOnlyList<GeneratedImage> Images => _images.AsReadOnly();

    private GenerationJob() { } // EF Core

    public static GenerationJob Create(Guid projectId, GenerationParameters parameters)
    {
        if (string.IsNullOrWhiteSpace(parameters.PositivePrompt))
            throw new ArgumentException("Positive prompt is required.");
        if (parameters.Steps < 1 || parameters.Steps > 150)
            throw new ArgumentException("Steps must be between 1 and 150.");
        if (parameters.Width % 64 != 0 || parameters.Height % 64 != 0)
            throw new ArgumentException("Width and height must be multiples of 64.");
        if (parameters.CfgScale < 1 || parameters.CfgScale > 30)
            throw new ArgumentException("CFG scale must be between 1 and 30.");
        if (parameters.BatchSize < 1 || parameters.BatchSize > 16)
            throw new ArgumentException("Batch size must be between 1 and 16.");
        if (parameters.Mode == GenerationMode.ImageToImage && parameters.DenoisingStrength >= 1.0)
            throw new ArgumentException("Denoising strength must be less than 1.0 for img2img mode.");
        if (parameters.HiresFixEnabled)
        {
            if (parameters.HiresUpscaleFactor < 1.0 || parameters.HiresUpscaleFactor > 4.0)
                throw new ArgumentException("Hires upscale factor must be between 1.0 and 4.0.");
            if (parameters.HiresSteps < 0 || parameters.HiresSteps > 150)
                throw new ArgumentException("Hires steps must be between 0 and 150.");
            if (parameters.HiresDenoisingStrength <= 0 || parameters.HiresDenoisingStrength >= 1.0)
                throw new ArgumentException("Hires denoising strength must be between 0 and 1.0 (exclusive).");
        }

        return new GenerationJob
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Parameters = parameters,
            Status = GenerationJobStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public void Start() { Status = GenerationJobStatus.Running; StartedAt = DateTimeOffset.UtcNow; }
    public void Complete() { Status = GenerationJobStatus.Completed; CompletedAt = DateTimeOffset.UtcNow; }
    public void Fail(string error) { Status = GenerationJobStatus.Failed; CompletedAt = DateTimeOffset.UtcNow; ErrorMessage = error; }
    public void Cancel() { Status = GenerationJobStatus.Cancelled; CompletedAt = DateTimeOffset.UtcNow; }

    public void AddImage(GeneratedImage image) => _images.Add(image);
}

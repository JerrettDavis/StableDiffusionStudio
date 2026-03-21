using Microsoft.Extensions.Logging;
using NsfwSpyNS;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Infrastructure.Services;

public class NsfwSpyContentSafetyService : IContentSafetyService
{
    private readonly ISettingsProvider _settings;
    private readonly ILogger<NsfwSpyContentSafetyService> _logger;
    private NsfwSpy? _nsfwSpy;
    private readonly object _initLock = new();
    private bool _initialized;
    private const string SettingsKey = "content-safety";

    public NsfwSpyContentSafetyService(ISettingsProvider settings, ILogger<NsfwSpyContentSafetyService> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public async Task<ContentClassification> ClassifyAsync(byte[] imageBytes, CancellationToken ct = default)
    {
        var mode = await GetFilterModeAsync(ct);
        if (mode == NsfwFilterMode.Off)
            return new ContentClassification(ContentRating.Unknown, 0, 0, 0, 0, 1);

        try
        {
            EnsureInitialized();
            var result = _nsfwSpy!.ClassifyImage(imageBytes);

            var pornScore = (double)result.Pornography;
            var sexyScore = (double)result.Sexy;
            var hentaiScore = (double)result.Hentai;
            var neutralScore = (double)result.Neutral;
            var nsfwScore = pornScore + sexyScore + hentaiScore;

            var safetySettings = await GetSettingsAsync(ct);
            ContentRating rating;
            if (nsfwScore >= safetySettings.NsfwThreshold)
                rating = ContentRating.Nsfw;
            else if (nsfwScore >= safetySettings.QuestionableThreshold)
                rating = ContentRating.Questionable;
            else
                rating = ContentRating.Safe;

            _logger.LogInformation("Content classified: {Rating} (NSFW: {Score:P1})", rating, nsfwScore);
            return new ContentClassification(rating, nsfwScore, pornScore, sexyScore, hentaiScore, neutralScore);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Content classification failed — defaulting to Unknown");
            return new ContentClassification(ContentRating.Unknown, 0, 0, 0, 0, 1);
        }
    }

    public async Task<NsfwFilterMode> GetFilterModeAsync(CancellationToken ct = default)
    {
        var settings = await GetSettingsAsync(ct);
        return settings.FilterMode;
    }

    public async Task SetFilterModeAsync(NsfwFilterMode mode, CancellationToken ct = default)
    {
        var settings = await GetSettingsAsync(ct);
        var updated = settings with { FilterMode = mode };
        await _settings.SetAsync(SettingsKey, updated, ct);
    }

    internal async Task<ContentSafetySettings> GetSettingsAsync(CancellationToken ct)
    {
        return await _settings.GetAsync<ContentSafetySettings>(SettingsKey, ct) ?? ContentSafetySettings.Default;
    }

    internal async Task SaveSettingsAsync(ContentSafetySettings settings, CancellationToken ct)
    {
        await _settings.SetAsync(SettingsKey, settings, ct);
    }

    private void EnsureInitialized()
    {
        if (!_initialized)
        {
            lock (_initLock)
            {
                if (!_initialized)
                {
                    _nsfwSpy = new NsfwSpy();
                    _initialized = true;
                }
            }
        }
    }
}

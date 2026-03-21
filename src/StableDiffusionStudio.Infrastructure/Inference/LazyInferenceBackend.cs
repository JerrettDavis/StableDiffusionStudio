using Microsoft.Extensions.Logging;
using StableDiffusionStudio.Application.DTOs;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Infrastructure.Jobs;

namespace StableDiffusionStudio.Infrastructure.Inference;

/// <summary>
/// Lazy wrapper that defers backend selection to first use.
/// Prevents blocking the startup thread with native library loading.
/// </summary>
public class LazyInferenceBackend : IInferenceBackend
{
    private readonly StableDiffusionCppBackend _sdCpp;
    private readonly MockInferenceBackend _mock;
    private readonly ILogger<LazyInferenceBackend> _logger;
    private IInferenceBackend? _resolved;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public LazyInferenceBackend(
        StableDiffusionCppBackend sdCpp,
        MockInferenceBackend mock,
        ILogger<LazyInferenceBackend> logger)
    {
        _sdCpp = sdCpp;
        _mock = mock;
        _logger = logger;
    }

    public string BackendId => GetResolved().BackendId;
    public string DisplayName => GetResolved().DisplayName;
    public InferenceCapabilities Capabilities => GetResolved().Capabilities;

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        await EnsureResolvedAsync(ct);
        return await _resolved!.IsAvailableAsync(ct);
    }

    public async Task LoadModelAsync(ModelLoadRequest request, CancellationToken ct = default)
    {
        await EnsureResolvedAsync(ct);
        await _resolved!.LoadModelAsync(request, ct);
    }

    public async Task<InferenceResult> GenerateAsync(InferenceRequest request, IProgress<InferenceProgress> progress, CancellationToken ct = default)
    {
        await EnsureResolvedAsync(ct);
        return await _resolved!.GenerateAsync(request, progress, ct);
    }

    public async Task UnloadModelAsync(CancellationToken ct = default)
    {
        if (_resolved is not null)
            await _resolved.UnloadModelAsync(ct);
    }

    private IInferenceBackend GetResolved()
    {
        if (_resolved is not null) return _resolved;
        // Synchronous path for property access — return mock as safe default until async init
        return _mock;
    }

    private async Task EnsureResolvedAsync(CancellationToken ct = default)
    {
        if (_resolved is not null) return;

        await _initLock.WaitAsync(ct);
        try
        {
            if (_resolved is not null) return;

            _logger.LogInformation("Initializing inference backend (first use)...");
            try
            {
                var available = await _sdCpp.IsAvailableAsync(ct);
                if (available)
                {
                    _resolved = _sdCpp;
                    _logger.LogInformation("Active backend: {Name} ({Id})", _sdCpp.DisplayName, _sdCpp.BackendId);
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "StableDiffusion.NET backend check failed");
            }

            _resolved = _mock;
            _logger.LogInformation("Active backend: {Name} ({Id}) (fallback)", _mock.DisplayName, _mock.BackendId);
        }
        finally
        {
            _initLock.Release();
        }
    }
}

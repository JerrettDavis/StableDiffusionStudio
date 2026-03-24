using System.Collections.Concurrent;

namespace StableDiffusionStudio.Infrastructure.Services;

/// <summary>
/// Thread-safe queue for image file paths that need NSFW classification.
/// Deduplicates entries so the same file is not scanned concurrently.
/// </summary>
public class NsfwScanQueue
{
    private readonly ConcurrentQueue<string> _queue = new();
    private readonly ConcurrentDictionary<string, byte> _inflight = new();
    private readonly SemaphoreSlim _signal = new(0);

    public void Enqueue(string filePath)
    {
        if (_inflight.TryAdd(filePath, 0))
        {
            _queue.Enqueue(filePath);
            _signal.Release();
        }
    }

    public async Task<string?> DequeueAsync(CancellationToken ct)
    {
        await _signal.WaitAsync(ct);
        if (_queue.TryDequeue(out var path))
        {
            _inflight.TryRemove(path, out _);
            return path;
        }
        return null;
    }

    public bool IsEnqueued(string filePath) => _inflight.ContainsKey(filePath);

    public int Count => _queue.Count;
}

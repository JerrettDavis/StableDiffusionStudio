using System.Diagnostics;
using System.IO.Compression;
using Microsoft.Extensions.Logging;
using StableDiffusionStudio.Application.DTOs;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Infrastructure.Jobs;

public class MockInferenceBackend : IInferenceBackend
{
    private readonly ILogger<MockInferenceBackend>? _logger;

    public MockInferenceBackend(ILogger<MockInferenceBackend>? logger = null)
    {
        _logger = logger;
    }

    public string BackendId => "mock";
    public string DisplayName => "Mock (Testing)";

    public InferenceCapabilities Capabilities { get; } = new(
        SupportedFamilies: [ModelFamily.SD15, ModelFamily.SDXL, ModelFamily.Flux],
        SupportedSamplers: [Sampler.EulerA, Sampler.DPMPlusPlus2MKarras, Sampler.DDIM],
        MaxWidth: 2048,
        MaxHeight: 2048,
        SupportsLoRA: true,
        SupportsVAE: true
    );

    public Task<bool> IsAvailableAsync(CancellationToken ct = default) => Task.FromResult(true);

    public Task LoadModelAsync(ModelLoadRequest request, CancellationToken ct = default)
    {
        _logger?.LogInformation("Mock: Loading model from {Path}", request.CheckpointPath);
        return Task.CompletedTask;
    }

    public async Task<InferenceResult> GenerateAsync(InferenceRequest request, IProgress<InferenceProgress> progress, CancellationToken ct = default)
    {
        var images = new List<GeneratedImageData>();
        var random = new Random(request.Seed >= 0 ? (int)request.Seed : Environment.TickCount);

        for (int batch = 0; batch < request.BatchSize; batch++)
        {
            var seed = request.Seed >= 0 ? request.Seed + batch : random.NextInt64(0, long.MaxValue);
            var sw = Stopwatch.StartNew();

            // Simulate step-by-step progress
            for (int step = 1; step <= request.Steps; step++)
            {
                ct.ThrowIfCancellationRequested();
                await Task.Delay(50, ct);
                progress.Report(new InferenceProgress(step, request.Steps, $"Generating image {batch + 1}/{request.BatchSize}"));
            }

            sw.Stop();

            // Generate a minimal valid PNG with seed-derived color
            var pngBytes = CreateMinimalPng(request.Width, request.Height, seed);
            images.Add(new GeneratedImageData(pngBytes, seed, sw.Elapsed.TotalSeconds));
        }

        return new InferenceResult(true, images, null);
    }

    public Task UnloadModelAsync(CancellationToken ct = default)
    {
        _logger?.LogInformation("Mock: Unloading model");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Creates a minimal valid PNG file with a solid color derived from the seed.
    /// Uses a 1x1 pixel image to keep it simple but still be a valid PNG.
    /// </summary>
    public static byte[] CreateMinimalPng(int width, int height, long seed)
    {
        byte r = (byte)(seed % 256);
        byte g = (byte)((seed / 256) % 256);
        byte b = (byte)((seed / 65536) % 256);

        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // PNG signature
        writer.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });

        // IHDR chunk (1x1 pixel, 8-bit RGB)
        WriteChunk(writer, "IHDR", GetIhdrData());

        // IDAT chunk (compressed image data)
        WriteChunk(writer, "IDAT", GetIdatData(r, g, b));

        // IEND chunk
        WriteChunk(writer, "IEND", []);

        return ms.ToArray();
    }

    private static byte[] GetIhdrData()
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        bw.Write(ToBigEndian(1));  // width
        bw.Write(ToBigEndian(1));  // height
        bw.Write((byte)8);        // bit depth
        bw.Write((byte)2);        // color type (RGB)
        bw.Write((byte)0);        // compression
        bw.Write((byte)0);        // filter
        bw.Write((byte)0);        // interlace
        return ms.ToArray();
    }

    private static byte[] GetIdatData(byte r, byte g, byte b)
    {
        // Raw scanline: filter byte (0 = None) + RGB pixel data
        byte[] rawData = [0, r, g, b];

        using var compressedStream = new MemoryStream();
        using (var deflate = new ZLibStream(compressedStream, CompressionLevel.Optimal, leaveOpen: true))
        {
            deflate.Write(rawData);
        }
        return compressedStream.ToArray();
    }

    private static void WriteChunk(BinaryWriter writer, string type, byte[] data)
    {
        writer.Write(ToBigEndian(data.Length));
        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        writer.Write(typeBytes);
        writer.Write(data);

        // CRC32 over type + data
        var crcData = new byte[typeBytes.Length + data.Length];
        typeBytes.CopyTo(crcData, 0);
        data.CopyTo(crcData, typeBytes.Length);
        writer.Write(ToBigEndian((int)CalculateCrc32(crcData)));
    }

    private static byte[] ToBigEndian(int value)
    {
        var bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
        return bytes;
    }

    private static uint CalculateCrc32(byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte b in data)
        {
            crc ^= b;
            for (int i = 0; i < 8; i++)
                crc = (crc >> 1) ^ (0xEDB88320 * (crc & 1));
        }
        return crc ^ 0xFFFFFFFF;
    }
}

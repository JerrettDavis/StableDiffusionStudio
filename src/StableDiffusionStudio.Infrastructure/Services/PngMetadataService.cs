using System.Text;
using System.Text.Json;
using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Infrastructure.Services;

public static class PngMetadataService
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    /// <summary>
    /// Embeds generation parameters as a PNG tEXt chunk (A1111-compatible format).
    /// </summary>
    public static byte[] EmbedMetadata(byte[] pngBytes, string parameters)
        => EmbedMetadata(pngBytes, "parameters", parameters);

    /// <summary>
    /// Embeds a tEXt chunk with the given keyword and text value into a PNG byte array.
    /// The chunk is inserted immediately before the IEND chunk.
    /// </summary>
    public static byte[] EmbedMetadata(byte[] pngBytes, string keyword, string text)
    {
        var textChunk = CreateTextChunk(keyword, text);

        var iendPos = FindIendPosition(pngBytes);
        if (iendPos < 0) return pngBytes;

        var result = new byte[pngBytes.Length + textChunk.Length];
        Array.Copy(pngBytes, 0, result, 0, iendPos);
        Array.Copy(textChunk, 0, result, iendPos, textChunk.Length);
        Array.Copy(pngBytes, iendPos, result, iendPos + textChunk.Length, pngBytes.Length - iendPos);

        return result;
    }

    /// <summary>
    /// Reads NsfwClassification metadata from a PNG file's tEXt chunk.
    /// Returns null if not found or if the file is not a PNG.
    /// </summary>
    public static NsfwClassification? ReadNsfwClassification(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return null;
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            if (ext is not ".png") return null;

            var bytes = File.ReadAllBytes(filePath);
            var json = ReadTextChunk(bytes, "NsfwClassification");
            if (json is null) return null;

            return JsonSerializer.Deserialize<NsfwClassification>(json, _jsonOptions);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Writes NsfwClassification metadata into a PNG file's tEXt chunk.
    /// If the file is not a PNG, it is skipped.
    /// If an existing NsfwClassification chunk exists, it is replaced.
    /// </summary>
    public static void WriteNsfwClassification(string filePath, NsfwClassification classification)
    {
        if (!File.Exists(filePath)) return;
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (ext is not ".png") return;

        var bytes = File.ReadAllBytes(filePath);

        // Remove existing NsfwClassification chunk if present
        bytes = RemoveTextChunk(bytes, "NsfwClassification");

        // Embed new chunk
        var json = JsonSerializer.Serialize(classification, _jsonOptions);
        bytes = EmbedMetadata(bytes, "NsfwClassification", json);

        File.WriteAllBytes(filePath, bytes);
    }

    public static string FormatA1111Parameters(
        string positivePrompt, string negativePrompt,
        int steps, string sampler, double cfgScale, long seed,
        int width, int height, string? modelName, int clipSkip,
        string? scheduler = null, double denoisingStrength = 1.0,
        string? mode = null, double eta = 0.0,
        int batchSize = 1, int batchCount = 1,
        bool hiresFixEnabled = false, double hiresUpscaleFactor = 2.0,
        int hiresSteps = 0, double hiresDenoisingStrength = 0.55,
        string? vaeName = null, IReadOnlyList<string>? loraNames = null)
    {
        var sb = new StringBuilder();
        sb.Append(positivePrompt);
        if (!string.IsNullOrWhiteSpace(negativePrompt))
            sb.Append($"\nNegative prompt: {negativePrompt}");
        sb.Append($"\nSteps: {steps}, Sampler: {sampler}, CFG scale: {cfgScale}, Seed: {seed}, Size: {width}x{height}");
        if (modelName is not null)
            sb.Append($", Model: {modelName}");
        if (!string.IsNullOrWhiteSpace(scheduler) && scheduler != "Normal")
            sb.Append($", Schedule type: {scheduler}");
        if (clipSkip > 1)
            sb.Append($", Clip skip: {clipSkip}");
        if (mode is not null && mode != "TextToImage")
            sb.Append($", Mode: {mode}");
        if (denoisingStrength < 1.0)
            sb.Append($", Denoising strength: {denoisingStrength}");
        if (eta > 0)
            sb.Append($", Eta: {eta}");
        if (batchSize > 1)
            sb.Append($", Batch size: {batchSize}");
        if (batchCount > 1)
            sb.Append($", Batch count: {batchCount}");
        if (!string.IsNullOrWhiteSpace(vaeName))
            sb.Append($", VAE: {vaeName}");
        if (loraNames is not null && loraNames.Count > 0)
            sb.Append($", Lora: {string.Join(", ", loraNames)}");
        if (hiresFixEnabled)
        {
            sb.Append($", Hires fix: {hiresUpscaleFactor:F2}x");
            if (hiresSteps > 0)
                sb.Append($", Hires steps: {hiresSteps}");
            sb.Append($", Hires denoising: {hiresDenoisingStrength}");
        }
        return sb.ToString();
    }

    /// <summary>
    /// Reads the value of a tEXt chunk with the given keyword from a PNG byte array.
    /// Returns null if not found.
    /// </summary>
    public static string? ReadTextChunk(byte[] pngBytes, string keyword)
    {
        if (pngBytes.Length < 8) return null;

        var keyBytes = Encoding.Latin1.GetBytes(keyword);
        int pos = 8; // Skip PNG signature

        while (pos + 8 <= pngBytes.Length)
        {
            int length = System.Net.IPAddress.NetworkToHostOrder(BitConverter.ToInt32(pngBytes, pos));
            var type = Encoding.ASCII.GetString(pngBytes, pos + 4, 4);

            if (type == "tEXt" && length > keyBytes.Length)
            {
                int dataStart = pos + 8;
                bool match = true;
                for (int i = 0; i < keyBytes.Length; i++)
                {
                    if (pngBytes[dataStart + i] != keyBytes[i])
                    {
                        match = false;
                        break;
                    }
                }
                if (match && pngBytes[dataStart + keyBytes.Length] == 0)
                {
                    int textStart = dataStart + keyBytes.Length + 1;
                    int textLen = length - keyBytes.Length - 1;
                    return Encoding.Latin1.GetString(pngBytes, textStart, textLen);
                }
            }

            if (type == "IEND") break;
            pos += 12 + length; // 4 (length) + 4 (type) + data + 4 (CRC)
        }

        return null;
    }

    private static byte[] CreateTextChunk(string keyword, string text)
    {
        var keyBytes = Encoding.Latin1.GetBytes(keyword);
        var textBytes = Encoding.Latin1.GetBytes(text);
        var data = new byte[keyBytes.Length + 1 + textBytes.Length]; // +1 for null separator
        Array.Copy(keyBytes, data, keyBytes.Length);
        // data[keyBytes.Length] = 0; // null separator (already 0)
        Array.Copy(textBytes, 0, data, keyBytes.Length + 1, textBytes.Length);

        var type = Encoding.ASCII.GetBytes("tEXt");
        var length = BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder(data.Length));

        // CRC over type + data
        var crcInput = new byte[type.Length + data.Length];
        Array.Copy(type, crcInput, type.Length);
        Array.Copy(data, 0, crcInput, type.Length, data.Length);
        var crc = BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder((int)CalculateCrc32(crcInput)));

        var chunk = new byte[4 + 4 + data.Length + 4]; // length + type + data + crc
        Array.Copy(length, chunk, 4);
        Array.Copy(type, 0, chunk, 4, 4);
        Array.Copy(data, 0, chunk, 8, data.Length);
        Array.Copy(crc, 0, chunk, 8 + data.Length, 4);

        return chunk;
    }

    private static int FindIendPosition(byte[] png)
    {
        // IEND chunk: length(4) + "IEND"(4) + CRC(4) = 12 bytes at the end
        if (png.Length < 12) return -1;
        var iend = Encoding.ASCII.GetBytes("IEND");
        for (int i = png.Length - 12; i >= 8; i--)
        {
            if (png[i + 4] == iend[0] && png[i + 5] == iend[1] &&
                png[i + 6] == iend[2] && png[i + 7] == iend[3])
                return i;
        }
        return -1;
    }

    private static byte[] RemoveTextChunk(byte[] pngBytes, string keyword)
    {
        var keyBytes = Encoding.Latin1.GetBytes(keyword);
        int pos = 8; // Skip PNG signature

        while (pos + 8 <= pngBytes.Length)
        {
            int length = System.Net.IPAddress.NetworkToHostOrder(BitConverter.ToInt32(pngBytes, pos));
            var type = Encoding.ASCII.GetString(pngBytes, pos + 4, 4);
            int chunkTotalSize = 12 + length; // 4 (length) + 4 (type) + data + 4 (CRC)

            if (type == "tEXt" && length > keyBytes.Length)
            {
                int dataStart = pos + 8;
                bool match = true;
                for (int i = 0; i < keyBytes.Length; i++)
                {
                    if (pngBytes[dataStart + i] != keyBytes[i]) { match = false; break; }
                }
                if (match && pngBytes[dataStart + keyBytes.Length] == 0)
                {
                    // Found it — remove this chunk
                    var result = new byte[pngBytes.Length - chunkTotalSize];
                    Array.Copy(pngBytes, 0, result, 0, pos);
                    Array.Copy(pngBytes, pos + chunkTotalSize, result, pos, pngBytes.Length - pos - chunkTotalSize);
                    return result;
                }
            }

            if (type == "IEND") break;
            pos += chunkTotalSize;
        }

        return pngBytes; // Not found, return unchanged
    }

    private static uint CalculateCrc32(byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (var b in data)
        {
            crc ^= b;
            for (int i = 0; i < 8; i++)
                crc = (crc >> 1) ^ (0xEDB88320 & ~((crc & 1) - 1));
        }
        return crc ^ 0xFFFFFFFF;
    }
}

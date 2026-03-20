using System.Text;

namespace StableDiffusionStudio.Infrastructure.Services;

public static class PngMetadataService
{
    /// <summary>
    /// Embeds generation parameters as a PNG tEXt chunk (A1111-compatible format).
    /// </summary>
    public static byte[] EmbedMetadata(byte[] pngBytes, string parameters)
    {
        // PNG structure: signature (8 bytes) + chunks
        // Insert a tEXt chunk with key "parameters" before IEND
        var textChunk = CreateTextChunk("parameters", parameters);

        // Find IEND chunk
        var iendPos = FindIendPosition(pngBytes);
        if (iendPos < 0) return pngBytes; // Not a valid PNG, return as-is

        var result = new byte[pngBytes.Length + textChunk.Length];
        Array.Copy(pngBytes, 0, result, 0, iendPos);
        Array.Copy(textChunk, 0, result, iendPos, textChunk.Length);
        Array.Copy(pngBytes, iendPos, result, iendPos + textChunk.Length, pngBytes.Length - iendPos);

        return result;
    }

    public static string FormatA1111Parameters(
        string positivePrompt, string negativePrompt,
        int steps, string sampler, double cfgScale, long seed,
        int width, int height, string? modelName, int clipSkip)
    {
        var sb = new StringBuilder();
        sb.Append(positivePrompt);
        if (!string.IsNullOrWhiteSpace(negativePrompt))
            sb.Append($"\nNegative prompt: {negativePrompt}");
        sb.Append($"\nSteps: {steps}, Sampler: {sampler}, CFG scale: {cfgScale}, Seed: {seed}, Size: {width}x{height}");
        if (modelName is not null)
            sb.Append($", Model: {modelName}");
        if (clipSkip > 1)
            sb.Append($", Clip skip: {clipSkip}");
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

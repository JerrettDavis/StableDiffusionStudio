using System.IO.Compression;
using System.Text;
using Microsoft.Extensions.Logging;

namespace StableDiffusionStudio.Infrastructure.Services;

public class ImageBlurService
{
    private readonly ILogger<ImageBlurService> _logger;

    public ImageBlurService(ILogger<ImageBlurService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Returns the path to a blurred version of the image.
    /// Creates the blurred file if it doesn't exist or is older than the original.
    /// </summary>
    public string GetOrCreateBlurred(string originalPath)
    {
        var blurredPath = originalPath + ".blurred.png";

        if (File.Exists(blurredPath))
        {
            var origTime = File.GetLastWriteTimeUtc(originalPath);
            var blurTime = File.GetLastWriteTimeUtc(blurredPath);
            if (blurTime >= origTime) return blurredPath;
        }

        try
        {
            CreatePixelatedBlur(originalPath, blurredPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create blurred preview for {Path}", originalPath);
            return originalPath; // Fallback to original
        }

        return blurredPath;
    }

    /// <summary>
    /// Creates a dark placeholder PNG that does not reveal image content.
    /// The actual content protection comes from not serving the raw pixels;
    /// the UI layer displays a shield overlay on top of this placeholder.
    /// </summary>
    private static void CreatePixelatedBlur(string sourcePath, string destPath)
    {
        var placeholder = CreateSolidColorPng(64, 64, 40, 40, 45);
        File.WriteAllBytes(destPath, placeholder);
    }

    /// <summary>
    /// Creates a minimal valid PNG file with a single solid color.
    /// </summary>
    private static byte[] CreateSolidColorPng(int width, int height, byte r, byte g, byte b)
    {
        using var ms = new MemoryStream();

        // PNG Signature
        ms.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });

        // IHDR chunk
        WriteChunk(ms, "IHDR", writer =>
        {
            writer.Write(ToBigEndian(width));
            writer.Write(ToBigEndian(height));
            writer.Write((byte)8);  // bit depth
            writer.Write((byte)2);  // color type RGB
            writer.Write((byte)0);  // compression
            writer.Write((byte)0);  // filter
            writer.Write((byte)0);  // interlace
        });

        // IDAT chunk - raw image data
        var rawData = new byte[height * (1 + width * 3)]; // filter byte + RGB per pixel per row
        for (int y = 0; y < height; y++)
        {
            int rowStart = y * (1 + width * 3);
            rawData[rowStart] = 0; // No filter
            for (int x = 0; x < width; x++)
            {
                int px = rowStart + 1 + x * 3;
                rawData[px] = r;
                rawData[px + 1] = g;
                rawData[px + 2] = b;
            }
        }

        using var compressed = new MemoryStream();
        compressed.WriteByte(0x78); // zlib header
        compressed.WriteByte(0x01);
        using (var deflate = new DeflateStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
        {
            deflate.Write(rawData);
        }

        // Adler32 checksum
        var adler = Adler32(rawData);
        compressed.Write(BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder((int)adler)));

        WriteChunk(ms, "IDAT", writer => writer.Write(compressed.ToArray()));

        // IEND chunk
        WriteChunk(ms, "IEND", _ => { });

        return ms.ToArray();
    }

    private static void WriteChunk(MemoryStream ms, string type, Action<BinaryWriter> writeData)
    {
        using var dataStream = new MemoryStream();
        using var writer = new BinaryWriter(dataStream);
        writeData(writer);
        writer.Flush();
        var data = dataStream.ToArray();
        var typeBytes = Encoding.ASCII.GetBytes(type);

        ms.Write(ToBigEndian(data.Length));
        ms.Write(typeBytes);
        ms.Write(data);

        // CRC over type + data
        var crcInput = new byte[4 + data.Length];
        Array.Copy(typeBytes, crcInput, 4);
        Array.Copy(data, 0, crcInput, 4, data.Length);
        ms.Write(ToBigEndian((int)Crc32(crcInput)));
    }

    private static byte[] ToBigEndian(int value)
        => BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder(value));

    private static uint Crc32(byte[] data)
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

    private static uint Adler32(byte[] data)
    {
        uint a = 1, b = 0;
        foreach (var d in data)
        {
            a = (a + d) % 65521;
            b = (b + a) % 65521;
        }
        return (b << 16) | a;
    }
}

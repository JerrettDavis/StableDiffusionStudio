using System.Text;
using FluentAssertions;
using StableDiffusionStudio.Infrastructure.Services;

namespace StableDiffusionStudio.Infrastructure.Tests.Services;

public class PngMetadataServiceTests
{
    // Minimal valid PNG: 8-byte signature + IHDR chunk (25 bytes) + IEND chunk (12 bytes)
    private static byte[] CreateMinimalPng()
    {
        var signature = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        // IHDR chunk: length=13, type=IHDR, data=13 bytes, CRC=4 bytes
        var ihdrLength = BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder(13));
        var ihdrType = Encoding.ASCII.GetBytes("IHDR");
        var ihdrData = new byte[13]; // width(4) + height(4) + bit depth(1) + color type(1) + compression(1) + filter(1) + interlace(1)
        ihdrData[3] = 1; // width = 1
        ihdrData[7] = 1; // height = 1
        ihdrData[8] = 8; // bit depth = 8
        ihdrData[9] = 2; // color type = RGB
        var ihdrCrcInput = new byte[ihdrType.Length + ihdrData.Length];
        Array.Copy(ihdrType, ihdrCrcInput, ihdrType.Length);
        Array.Copy(ihdrData, 0, ihdrCrcInput, ihdrType.Length, ihdrData.Length);
        var ihdrCrc = CalculateCrc32(ihdrCrcInput);

        // IEND chunk: length=0, type=IEND, CRC
        var iendLength = new byte[4]; // 0
        var iendType = Encoding.ASCII.GetBytes("IEND");
        var iendCrc = CalculateCrc32(iendType);

        using var ms = new MemoryStream();
        ms.Write(signature);
        ms.Write(ihdrLength);
        ms.Write(ihdrType);
        ms.Write(ihdrData);
        ms.Write(BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder((int)ihdrCrc)));
        ms.Write(iendLength);
        ms.Write(iendType);
        ms.Write(BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder((int)iendCrc)));
        return ms.ToArray();
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

    [Fact]
    public void EmbedMetadata_PreservesPngSignature()
    {
        var png = CreateMinimalPng();
        var result = PngMetadataService.EmbedMetadata(png, "test parameters");

        // PNG signature should be intact
        result[0].Should().Be(0x89);
        result[1].Should().Be(0x50); // P
        result[2].Should().Be(0x4E); // N
        result[3].Should().Be(0x47); // G
    }

    [Fact]
    public void EmbedMetadata_ResultIsLargerThanOriginal()
    {
        var png = CreateMinimalPng();
        var result = PngMetadataService.EmbedMetadata(png, "test parameters");

        result.Length.Should().BeGreaterThan(png.Length);
    }

    [Fact]
    public void EmbedMetadata_ContainsTextChunkType()
    {
        var png = CreateMinimalPng();
        var result = PngMetadataService.EmbedMetadata(png, "hello world");

        // Should contain "tEXt" somewhere in the output
        var resultStr = Encoding.ASCII.GetString(result);
        resultStr.Should().Contain("tEXt");
    }

    [Fact]
    public void EmbedMetadata_RoundTrip_CanReadEmbeddedText()
    {
        var png = CreateMinimalPng();
        var parameters = "a beautiful sunset\nNegative prompt: ugly\nSteps: 20, Sampler: EulerA, CFG scale: 7, Seed: 12345, Size: 512x512";

        var result = PngMetadataService.EmbedMetadata(png, parameters);
        var readBack = PngMetadataService.ReadTextChunk(result, "parameters");

        readBack.Should().Be(parameters);
    }

    [Fact]
    public void EmbedMetadata_EndsWithIend()
    {
        var png = CreateMinimalPng();
        var result = PngMetadataService.EmbedMetadata(png, "test");

        // Last 12 bytes should contain IEND
        var iendType = Encoding.ASCII.GetString(result, result.Length - 8, 4);
        iendType.Should().Be("IEND");
    }

    [Fact]
    public void EmbedMetadata_InvalidPng_ReturnsOriginal()
    {
        var notPng = new byte[] { 0x00, 0x01, 0x02, 0x03 };
        var result = PngMetadataService.EmbedMetadata(notPng, "test");

        result.Should().BeEquivalentTo(notPng);
    }

    [Fact]
    public void FormatA1111Parameters_BasicFormat()
    {
        var result = PngMetadataService.FormatA1111Parameters(
            "a cat", "ugly", 20, "Euler a", 7.0, 12345, 512, 512, "model_v1", 1);

        result.Should().Contain("a cat");
        result.Should().Contain("Negative prompt: ugly");
        result.Should().Contain("Steps: 20");
        result.Should().Contain("Sampler: Euler a");
        result.Should().Contain("CFG scale: 7");
        result.Should().Contain("Seed: 12345");
        result.Should().Contain("Size: 512x512");
        result.Should().Contain("Model: model_v1");
    }

    [Fact]
    public void FormatA1111Parameters_WithClipSkip()
    {
        var result = PngMetadataService.FormatA1111Parameters(
            "prompt", "", 20, "Euler", 7.0, 1, 512, 512, null, 2);

        result.Should().Contain("Clip skip: 2");
    }

    [Fact]
    public void FormatA1111Parameters_NoNegativePrompt()
    {
        var result = PngMetadataService.FormatA1111Parameters(
            "prompt", "", 20, "Euler", 7.0, 1, 512, 512, null, 1);

        result.Should().NotContain("Negative prompt:");
    }

    [Fact]
    public void ReadTextChunk_NonExistentKey_ReturnsNull()
    {
        var png = CreateMinimalPng();
        var result = PngMetadataService.EmbedMetadata(png, "test");

        var value = PngMetadataService.ReadTextChunk(result, "nonexistent");

        value.Should().BeNull();
    }

    [Fact]
    public void EmbedMetadata_EmptyBytes_ReturnsEmpty()
    {
        var result = PngMetadataService.EmbedMetadata([], "test");
        result.Should().BeEmpty();
    }

    [Fact]
    public void FormatA1111Parameters_WithAllFieldsPopulated()
    {
        var result = PngMetadataService.FormatA1111Parameters(
            "a beautiful landscape, photorealistic",
            "ugly, blurry, deformed",
            30,
            "DPM++ 2M Karras",
            7.5,
            42,
            1024,
            1024,
            "sdxl_base_v1.0",
            2);

        result.Should().Contain("a beautiful landscape, photorealistic");
        result.Should().Contain("Negative prompt: ugly, blurry, deformed");
        result.Should().Contain("Steps: 30");
        result.Should().Contain("Sampler: DPM++ 2M Karras");
        result.Should().Contain("CFG scale: 7.5");
        result.Should().Contain("Seed: 42");
        result.Should().Contain("Size: 1024x1024");
        result.Should().Contain("Model: sdxl_base_v1.0");
        result.Should().Contain("Clip skip: 2");
    }

    [Fact]
    public void FormatA1111Parameters_WithMinimalFields()
    {
        var result = PngMetadataService.FormatA1111Parameters(
            "test prompt", "", 20, "Euler", 7.0, 1, 512, 512, null, 1);

        result.Should().Contain("test prompt");
        result.Should().Contain("Steps: 20");
        result.Should().NotContain("Negative prompt:");
        result.Should().NotContain("Model:");
        result.Should().NotContain("Clip skip:");
    }

    [Fact]
    public void FormatA1111Parameters_WithClipSkipOne_OmitsIt()
    {
        var result = PngMetadataService.FormatA1111Parameters(
            "prompt", "", 20, "Euler", 7.0, 1, 512, 512, "model", 1);

        result.Should().NotContain("Clip skip:");
    }

    [Fact]
    public void FormatA1111Parameters_WithClipSkipGreaterThanOne_IncludesIt()
    {
        var result = PngMetadataService.FormatA1111Parameters(
            "prompt", "", 20, "Euler", 7.0, 1, 512, 512, "model", 3);

        result.Should().Contain("Clip skip: 3");
    }

    [Fact]
    public void ReadTextChunk_OnMinimalPng_ReturnsNull()
    {
        var png = CreateMinimalPng();
        var result = PngMetadataService.ReadTextChunk(png, "parameters");
        result.Should().BeNull();
    }

    [Fact]
    public void EmbedMetadata_ThenReadTextChunk_WithUnicodeContent_RoundTrips()
    {
        var png = CreateMinimalPng();
        var parameters = "a beautiful sunset, high quality, masterpiece";

        var result = PngMetadataService.EmbedMetadata(png, parameters);
        var readBack = PngMetadataService.ReadTextChunk(result, "parameters");

        readBack.Should().Be(parameters);
    }

    [Fact]
    public void EmbedMetadata_WithVeryLongMetadata_RoundTrips()
    {
        var png = CreateMinimalPng();
        var parameters = new string('a', 10000) + "\nNegative prompt: " + new string('b', 5000);

        var result = PngMetadataService.EmbedMetadata(png, parameters);
        var readBack = PngMetadataService.ReadTextChunk(result, "parameters");

        readBack.Should().Be(parameters);
    }

    [Fact]
    public void FormatA1111Parameters_EmptyNegativePrompt_OmitsLine()
    {
        var result = PngMetadataService.FormatA1111Parameters(
            "test prompt", "", 20, "Euler", 7.0, 1, 512, 512, null, 1);

        result.Should().NotContain("Negative prompt:");
    }

    [Fact]
    public void FormatA1111Parameters_WhitespaceNegativePrompt_OmitsLine()
    {
        var result = PngMetadataService.FormatA1111Parameters(
            "test prompt", "   ", 20, "Euler", 7.0, 1, 512, 512, null, 1);

        result.Should().NotContain("Negative prompt:");
    }

    [Fact]
    public void FormatA1111Parameters_WithModelName_IncludesModel()
    {
        var result = PngMetadataService.FormatA1111Parameters(
            "prompt", "", 20, "Euler", 7.0, 1, 512, 512, "sd_v15", 1);

        result.Should().Contain("Model: sd_v15");
    }

    [Fact]
    public void FormatA1111Parameters_WithoutModelName_OmitsModel()
    {
        var result = PngMetadataService.FormatA1111Parameters(
            "prompt", "", 20, "Euler", 7.0, 1, 512, 512, null, 1);

        result.Should().NotContain("Model:");
    }

    [Fact]
    public void ReadTextChunk_TooShortBytes_ReturnsNull()
    {
        var result = PngMetadataService.ReadTextChunk(new byte[] { 1, 2, 3 }, "parameters");
        result.Should().BeNull();
    }

    [Fact]
    public void EmbedMetadata_MultipleEmbeds_AllReadable()
    {
        var png = CreateMinimalPng();
        var result = PngMetadataService.EmbedMetadata(png, "first metadata");
        result = PngMetadataService.EmbedMetadata(result, "second metadata");

        // Should find at least one of the text chunks
        var readBack = PngMetadataService.ReadTextChunk(result, "parameters");
        readBack.Should().NotBeNull();
    }

    [Fact]
    public void EmbedMetadata_WithSpecialCharacters_RoundTrips()
    {
        var png = CreateMinimalPng();
        var parameters = "prompt with (parentheses:1.5), <lora:test:0.8>, [brackets]";

        var result = PngMetadataService.EmbedMetadata(png, parameters);
        var readBack = PngMetadataService.ReadTextChunk(result, "parameters");

        readBack.Should().Be(parameters);
    }

    [Fact]
    public void EmbedMetadata_WithNewlines_RoundTrips()
    {
        var png = CreateMinimalPng();
        var parameters = "line1\nline2\nline3";

        var result = PngMetadataService.EmbedMetadata(png, parameters);
        var readBack = PngMetadataService.ReadTextChunk(result, "parameters");

        readBack.Should().Be(parameters);
    }

    [Fact]
    public void ReadTextChunk_ExactlyEightBytes_ReturnsNull()
    {
        // Just the PNG signature, no chunks
        var signature = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        var result = PngMetadataService.ReadTextChunk(signature, "parameters");
        result.Should().BeNull();
    }

    [Fact]
    public void FormatA1111Parameters_WithDecimalCfg_FormatsCorrectly()
    {
        var result = PngMetadataService.FormatA1111Parameters(
            "test", "", 20, "Euler", 3.5, 1, 512, 512, null, 1);

        result.Should().Contain("CFG scale: 3.5");
    }

    [Fact]
    public void FormatA1111Parameters_WithLargeSeed_FormatsCorrectly()
    {
        var result = PngMetadataService.FormatA1111Parameters(
            "test", "", 20, "Euler", 7.0, 9999999999L, 512, 512, null, 1);

        result.Should().Contain("Seed: 9999999999");
    }

    [Fact]
    public void EmbedMetadata_EmptyParameters_StillEmbeds()
    {
        var png = CreateMinimalPng();
        var result = PngMetadataService.EmbedMetadata(png, "");

        result.Length.Should().BeGreaterThan(png.Length);
        var readBack = PngMetadataService.ReadTextChunk(result, "parameters");
        readBack.Should().Be("");
    }
}

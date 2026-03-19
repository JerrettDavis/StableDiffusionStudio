using FluentAssertions;
using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Domain.Tests.ValueObjects;

public class ModelIdentifierTests
{
    [Fact]
    public void Create_WithValidInputs_SetsProperties()
    {
        var id = new ModelIdentifier("local-folder", "path/to/model.safetensors");
        id.Source.Should().Be("local-folder");
        id.ExternalId.Should().Be("path/to/model.safetensors");
    }

    [Theory]
    [InlineData(null, "id")]
    [InlineData("", "id")]
    [InlineData("source", null)]
    [InlineData("source", "")]
    public void Create_WithInvalidInputs_ThrowsArgumentException(string? source, string? externalId)
    {
        var act = () => new ModelIdentifier(source!, externalId!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = new ModelIdentifier("hf", "model-123");
        var b = new ModelIdentifier("hf", "model-123");
        a.Should().Be(b);
    }
}

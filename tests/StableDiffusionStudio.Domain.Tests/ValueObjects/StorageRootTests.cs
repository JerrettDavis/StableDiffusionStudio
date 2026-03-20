using FluentAssertions;
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Domain.Tests.ValueObjects;

public class StorageRootTests
{
    [Fact]
    public void Create_WithValidInputs_SetsProperties()
    {
        var root = new StorageRoot("/models", "My Models");
        root.Path.Should().Be("/models");
        root.DisplayName.Should().Be("My Models");
        root.ModelTypeTag.Should().BeNull();
    }

    [Theory]
    [InlineData(null, "name")]
    [InlineData("", "name")]
    [InlineData("path", null)]
    [InlineData("path", "")]
    public void Create_WithInvalidInputs_ThrowsArgumentException(string? path, string? name)
    {
        var act = () => new StorageRoot(path!, name!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = new StorageRoot("/models", "Models");
        var b = new StorageRoot("/models", "Models");
        a.Should().Be(b);
    }

    [Fact]
    public void Create_WithModelTypeTag_SetsTag()
    {
        var root = new StorageRoot("/loras", "LoRA Models", ModelType.LoRA);
        root.ModelTypeTag.Should().Be(ModelType.LoRA);
    }

    [Fact]
    public void Create_WithNullModelTypeTag_TagIsNull()
    {
        var root = new StorageRoot("/models", "Models", null);
        root.ModelTypeTag.Should().BeNull();
    }

    [Fact]
    public void Equality_SameValuesWithTag_AreEqual()
    {
        var a = new StorageRoot("/loras", "LoRAs", ModelType.LoRA);
        var b = new StorageRoot("/loras", "LoRAs", ModelType.LoRA);
        a.Should().Be(b);
    }

    [Fact]
    public void Equality_DifferentTags_AreNotEqual()
    {
        var a = new StorageRoot("/models", "Models", ModelType.Checkpoint);
        var b = new StorageRoot("/models", "Models", ModelType.LoRA);
        a.Should().NotBe(b);
    }
}

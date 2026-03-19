using FluentAssertions;
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
}

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Domain.ValueObjects;
using StableDiffusionStudio.Infrastructure.Persistence;
using StableDiffusionStudio.Infrastructure.Settings;
using StableDiffusionStudio.Infrastructure.Storage;

namespace StableDiffusionStudio.Infrastructure.Tests.Storage;

public class DbStorageRootProviderTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly DbStorageRootProvider _provider;

    public DbStorageRootProviderTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _context = new AppDbContext(options);
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();
        var settingsProvider = new DbSettingsProvider(_context);
        _provider = new DbStorageRootProvider(settingsProvider);
    }

    [Fact]
    public async Task GetRootsAsync_Empty_ReturnsEmptyList()
    {
        var roots = await _provider.GetRootsAsync();
        roots.Should().BeEmpty();
    }

    [Fact]
    public async Task AddRootAsync_ThenGetRootsAsync_ReturnsTheRoot()
    {
        var root = new StorageRoot("/models", "My Models");
        await _provider.AddRootAsync(root);

        var roots = await _provider.GetRootsAsync();

        roots.Should().HaveCount(1);
        roots[0].Path.Should().Be("/models");
        roots[0].DisplayName.Should().Be("My Models");
    }

    [Fact]
    public async Task AddRootAsync_DuplicatePath_IsIgnored()
    {
        var root = new StorageRoot("/models", "My Models");
        await _provider.AddRootAsync(root);
        await _provider.AddRootAsync(root);

        var roots = await _provider.GetRootsAsync();
        roots.Should().HaveCount(1);
    }

    [Fact]
    public async Task RemoveRootAsync_RemovesTheRoot()
    {
        var root = new StorageRoot("/models", "My Models");
        await _provider.AddRootAsync(root);

        await _provider.RemoveRootAsync("/models");

        var roots = await _provider.GetRootsAsync();
        roots.Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveRootAsync_NonExistent_DoesNotThrow()
    {
        var act = () => _provider.RemoveRootAsync("/nonexistent");
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task AddRootAsync_WithModelTypeTag_PersistsTag()
    {
        var root = new StorageRoot("/loras", "LoRA Models", ModelType.LoRA);
        await _provider.AddRootAsync(root);

        var roots = await _provider.GetRootsAsync();

        roots.Should().HaveCount(1);
        roots[0].ModelTypeTag.Should().Be(ModelType.LoRA);
    }

    [Fact]
    public async Task GetRootsAsync_PreservesModelTypeTag()
    {
        await _provider.AddRootAsync(new StorageRoot("/checkpoints", "Checkpoints", ModelType.Checkpoint));
        await _provider.AddRootAsync(new StorageRoot("/vaes", "VAEs", ModelType.VAE));
        await _provider.AddRootAsync(new StorageRoot("/all", "All Models"));

        var roots = await _provider.GetRootsAsync();

        roots.Should().HaveCount(3);
        roots.Should().Contain(r => r.Path == "/checkpoints" && r.ModelTypeTag == ModelType.Checkpoint);
        roots.Should().Contain(r => r.Path == "/vaes" && r.ModelTypeTag == ModelType.VAE);
        roots.Should().Contain(r => r.Path == "/all" && r.ModelTypeTag == null);
    }

    [Fact]
    public async Task MultipleRoots_AddAndRemove_MaintainsCorrectState()
    {
        await _provider.AddRootAsync(new StorageRoot("/a", "Root A"));
        await _provider.AddRootAsync(new StorageRoot("/b", "Root B"));
        await _provider.AddRootAsync(new StorageRoot("/c", "Root C"));

        await _provider.RemoveRootAsync("/b");

        var roots = await _provider.GetRootsAsync();
        roots.Should().HaveCount(2);
        roots.Select(r => r.Path).Should().BeEquivalentTo(new[] { "/a", "/c" });
    }

    [Fact]
    public async Task AddRootAsync_DifferentDisplayNames_SamePath_OnlyFirstPersists()
    {
        await _provider.AddRootAsync(new StorageRoot("/models", "First Name"));
        await _provider.AddRootAsync(new StorageRoot("/models", "Second Name"));

        var roots = await _provider.GetRootsAsync();
        roots.Should().HaveCount(1);
        roots[0].DisplayName.Should().Be("First Name");
    }

    [Fact]
    public async Task RemoveRootAsync_ThenAddAgain_Works()
    {
        await _provider.AddRootAsync(new StorageRoot("/models", "Models"));
        await _provider.RemoveRootAsync("/models");

        var emptyRoots = await _provider.GetRootsAsync();
        emptyRoots.Should().BeEmpty();

        await _provider.AddRootAsync(new StorageRoot("/models", "Models Reborn"));

        var roots = await _provider.GetRootsAsync();
        roots.Should().HaveCount(1);
        roots[0].DisplayName.Should().Be("Models Reborn");
    }

    [Fact]
    public async Task AddRootAsync_WithNullModelTypeTag_PersistsNull()
    {
        await _provider.AddRootAsync(new StorageRoot("/generic", "Generic"));

        var roots = await _provider.GetRootsAsync();
        roots[0].ModelTypeTag.Should().BeNull();
    }

    [Fact]
    public async Task RemoveRootAsync_RemovesOnlyTarget_LeavesOthers()
    {
        await _provider.AddRootAsync(new StorageRoot("/a", "A"));
        await _provider.AddRootAsync(new StorageRoot("/b", "B"));
        await _provider.AddRootAsync(new StorageRoot("/c", "C"));

        await _provider.RemoveRootAsync("/a");

        var roots = await _provider.GetRootsAsync();
        roots.Should().HaveCount(2);
        roots.Select(r => r.Path).Should().BeEquivalentTo(new[] { "/b", "/c" });
    }

    public void Dispose()
    {
        _context.Database.CloseConnection();
        _context.Dispose();
    }
}

using FluentAssertions;
using StableDiffusionStudio.Infrastructure.Services;

namespace StableDiffusionStudio.Infrastructure.Tests.Services;

public class AppPathsTests
{
    private readonly AppPaths _appPaths = new();

    [Fact]
    public void AssetsDirectory_IsUnderLocalApplicationData()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _appPaths.AssetsDirectory.Should().StartWith(localAppData);
        _appPaths.AssetsDirectory.Should().Contain("StableDiffusionStudio");
        _appPaths.AssetsDirectory.Should().EndWith("Assets");
    }

    [Fact]
    public void DatabaseDirectory_IsUnderLocalApplicationData()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _appPaths.DatabaseDirectory.Should().StartWith(localAppData);
        _appPaths.DatabaseDirectory.Should().Contain("StableDiffusionStudio");
        _appPaths.DatabaseDirectory.Should().EndWith("Database");
    }

    [Fact]
    public void GetProjectAssetsDirectory_IncludesProjectId()
    {
        var projectId = Guid.NewGuid();
        var result = _appPaths.GetProjectAssetsDirectory(projectId);

        result.Should().StartWith(_appPaths.AssetsDirectory);
        result.Should().Contain(projectId.ToString());
    }

    [Fact]
    public void GetJobAssetsDirectory_IncludesProjectAndJobIds()
    {
        var projectId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var result = _appPaths.GetJobAssetsDirectory(projectId, jobId);

        result.Should().StartWith(_appPaths.AssetsDirectory);
        result.Should().Contain(projectId.ToString());
        result.Should().Contain(jobId.ToString());
    }

    [Fact]
    public void GetImageUrl_WithValidAssetsPath_ReturnsRelativeUrl()
    {
        var filePath = Path.Combine(_appPaths.AssetsDirectory, "proj", "job", "image.png");
        var result = _appPaths.GetImageUrl(filePath);

        result.Should().StartWith("/assets/");
        result.Should().Contain("proj/job/image.png");
    }

    [Fact]
    public void GetImageUrl_WithExternalPath_ReturnsFilenameFallback()
    {
        var result = _appPaths.GetImageUrl("/some/external/path/image.png");
        result.Should().Be("/assets/image.png");
    }

    [Fact]
    public void GetImageUrl_ReplacesBackslashesWithForwardSlashes()
    {
        var filePath = Path.Combine(_appPaths.AssetsDirectory, "proj", "job", "image.png");
        var result = _appPaths.GetImageUrl(filePath);

        result.Should().NotContain("\\");
        result.Should().Contain("/");
    }

    [Fact]
    public void GetImageUrl_WithBackslashOnly_ConvertsCorrectly()
    {
        // Manually construct a path with backslashes inside the assets directory
        var filePath = _appPaths.AssetsDirectory + "\\project\\job\\output.png";
        var result = _appPaths.GetImageUrl(filePath);

        result.Should().StartWith("/assets/");
        result.Should().NotContain("\\");
        result.Should().Contain("project/job/output.png");
    }

    [Theory]
    [InlineData("D:\\somewhere\\else\\photo.png", "/assets/photo.png")]
    [InlineData("/tmp/somewhere/else/photo.png", "/assets/photo.png")]
    [InlineData("photo.png", "/assets/photo.png")]
    public void GetImageUrl_WithPathOutsideAssets_ReturnsFilenameOnly(string input, string expected)
    {
        var result = _appPaths.GetImageUrl(input);
        result.Should().Be(expected);
    }

    [Fact]
    public void GetProjectAssetsDirectory_Format()
    {
        var projectId = Guid.Parse("12345678-1234-1234-1234-123456789abc");
        var result = _appPaths.GetProjectAssetsDirectory(projectId);

        result.Should().Contain("12345678-1234-1234-1234-123456789abc");
        result.Should().StartWith(_appPaths.AssetsDirectory);
    }

    [Fact]
    public void GetJobAssetsDirectory_Format()
    {
        var projectId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var jobId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var result = _appPaths.GetJobAssetsDirectory(projectId, jobId);

        result.Should().Contain("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        result.Should().Contain("11111111-2222-3333-4444-555555555555");
    }

    [Fact]
    public void GetJobAssetsDirectory_IsSubdirectoryOfProjectAssets()
    {
        var projectId = Guid.NewGuid();
        var jobId = Guid.NewGuid();

        var projectDir = _appPaths.GetProjectAssetsDirectory(projectId);
        var jobDir = _appPaths.GetJobAssetsDirectory(projectId, jobId);

        jobDir.Should().StartWith(projectDir);
    }

    [Fact]
    public void AssetsDirectory_IsDeterministic()
    {
        var appPaths2 = new AppPaths();
        _appPaths.AssetsDirectory.Should().Be(appPaths2.AssetsDirectory);
    }

    [Fact]
    public void DatabaseDirectory_IsDeterministic()
    {
        var appPaths2 = new AppPaths();
        _appPaths.DatabaseDirectory.Should().Be(appPaths2.DatabaseDirectory);
    }
}

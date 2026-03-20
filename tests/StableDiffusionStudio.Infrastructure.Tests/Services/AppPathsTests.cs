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
}

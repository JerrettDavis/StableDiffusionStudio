using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace StableDiffusionStudio.E2E.Tests.Support;

/// <summary>
/// Starts the Blazor Server app on a real HTTP port for Playwright browser testing.
/// Runs the actual Web project via 'dotnet run' in a background process so that all
/// static assets (blazor.web.js, MudBlazor.min.js, etc.) are served correctly.
/// Uses an isolated SQLite database per test run via environment variables.
/// </summary>
public class WebAppFixture : IAsyncLifetime
{
    private Process? _process;
    private readonly string _dbPath;

    public string BaseUrl { get; }

    public WebAppFixture()
    {
        var port = GetAvailablePort();
        BaseUrl = $"http://localhost:{port}";
        _dbPath = Path.Combine(Path.GetTempPath(), $"sds_e2e_{Guid.NewGuid():N}.db");
    }

    public async Task InitializeAsync()
    {
        var webProjectDir = GetWebProjectDir();

        _process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --no-launch-profile --urls {BaseUrl}",
                WorkingDirectory = webProjectDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                Environment =
                {
                    // Override the database path for test isolation
                    ["SDS_TEST_DB_PATH"] = _dbPath,
                    ["ASPNETCORE_ENVIRONMENT"] = "Development",
                    ["DOTNET_ENVIRONMENT"] = "Development"
                }
            }
        };

        _process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null) Console.WriteLine($"[WebApp] {e.Data}");
        };
        _process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null) Console.Error.WriteLine($"[WebApp:ERR] {e.Data}");
        };

        _process.Start();
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        // Wait for the server to be ready by polling the URL
        using var client = new HttpClient();
        var timeout = TimeSpan.FromSeconds(60);
        var started = Stopwatch.StartNew();

        while (started.Elapsed < timeout)
        {
            try
            {
                var response = await client.GetAsync(BaseUrl);
                if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.OK)
                    return;
            }
            catch (HttpRequestException)
            {
                // Server not ready yet
            }

            await Task.Delay(500);
        }

        throw new TimeoutException($"Web app did not start within {timeout.TotalSeconds}s at {BaseUrl}");
    }

    public async Task DisposeAsync()
    {
        if (_process != null && !_process.HasExited)
        {
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync();
        }

        _process?.Dispose();

        try
        {
            if (File.Exists(_dbPath))
                File.Delete(_dbPath);
        }
        catch
        {
            // Best-effort cleanup
        }
    }

    private static string GetWebProjectDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "StableDiffusionStudio.slnx")))
        {
            dir = dir.Parent;
        }

        if (dir == null)
            throw new InvalidOperationException(
                "Could not find repository root (looking for StableDiffusionStudio.slnx)");

        return Path.Combine(dir.FullName, "src", "StableDiffusionStudio.Web");
    }

    private static int GetAvailablePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}

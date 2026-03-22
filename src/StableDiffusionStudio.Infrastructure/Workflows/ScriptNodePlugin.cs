using System.Diagnostics;
using System.Text.Json;
using StableDiffusionStudio.Application.Interfaces;

namespace StableDiffusionStudio.Infrastructure.Workflows;

/// <summary>
/// External process execution node. Runs a command-line tool, passing the input image
/// via a temp file and reading the output image from the process's output path.
/// Enables integration with Python scripts, external tools, and custom processing.
/// </summary>
public class ScriptNodePlugin : IWorkflowNodePlugin
{
    public string PluginId => "core.script";
    public string DisplayName => "Script";
    public string Category => "Extensions";
    public string Description => "Run an external script or tool. Input/output via temp files.";
    public string Icon => "Icons.Material.Filled.Terminal";

    public IReadOnlyList<WorkflowPortDefinition> InputPorts =>
    [
        new("image", WorkflowDataType.Image, Required: false)
    ];

    public IReadOnlyList<WorkflowPortDefinition> OutputPorts =>
    [
        new("image", WorkflowDataType.Image)
    ];

    public async Task<Dictionary<string, WorkflowData>> ExecuteAsync(
        IReadOnlyDictionary<string, WorkflowData> inputs,
        string? config,
        WorkflowExecutionContext context,
        CancellationToken ct = default)
    {
        var scriptConfig = JsonSerializer.Deserialize<ScriptConfig>(config ?? "{}")
            ?? throw new InvalidOperationException("Script node requires configuration.");

        if (string.IsNullOrWhiteSpace(scriptConfig.Command))
            throw new InvalidOperationException("Script node requires a command to execute.");

        // Create temp directory for I/O
        var tempDir = Path.Combine(Path.GetTempPath(), $"sds-script-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var inputPath = Path.Combine(tempDir, "input.png");
            var outputPath = Path.Combine(tempDir, "output.png");

            // Write input image if provided
            if (inputs.TryGetValue("image", out var imageData) && imageData.ImageBytes is not null)
            {
                await File.WriteAllBytesAsync(inputPath, imageData.ImageBytes, ct);
            }

            // Build command with path substitutions
            var command = scriptConfig.Command
                .Replace("{input}", inputPath)
                .Replace("{output}", outputPath)
                .Replace("{temp_dir}", tempDir);

            context.Progress.Report(new WorkflowStepProgress(
                context.NodeId, 0, 1, "Running script..."));

            // Execute the process
            var psi = new ProcessStartInfo
            {
                FileName = OperatingSystem.IsWindows() ? "cmd" : "bash",
                Arguments = OperatingSystem.IsWindows() ? $"/c {command}" : $"-c \"{command}\"",
                WorkingDirectory = scriptConfig.WorkingDirectory ?? tempDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            // Add environment variables
            if (scriptConfig.Environment is not null)
            {
                foreach (var (key, value) in scriptConfig.Environment)
                    psi.EnvironmentVariables[key] = value;
            }

            using var process = new Process { StartInfo = psi };
            process.Start();

            var stdout = await process.StandardOutput.ReadToEndAsync(ct);
            var stderr = await process.StandardError.ReadToEndAsync(ct);

            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0)
            {
                var error = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
                throw new InvalidOperationException(
                    $"Script exited with code {process.ExitCode}: {error.Trim()[..Math.Min(error.Length, 500)]}");
            }

            context.Progress.Report(new WorkflowStepProgress(
                context.NodeId, 1, 1, "Script completed"));

            // Read output image
            if (!File.Exists(outputPath))
                throw new InvalidOperationException(
                    $"Script did not produce an output image at {{output}}. Stdout: {stdout.Trim()[..Math.Min(stdout.Length, 200)]}");

            var outputBytes = await File.ReadAllBytesAsync(outputPath, ct);

            // Save to workflow assets
            var assetDir = Path.Combine(context.AppPaths.AssetsDirectory, "workflows", context.WorkflowRunId.ToString());
            Directory.CreateDirectory(assetDir);
            var assetPath = Path.Combine(assetDir, $"{context.NodeId}.png");
            File.Copy(outputPath, assetPath, overwrite: true);

            return new Dictionary<string, WorkflowData>
            {
                ["image"] = WorkflowData.FromImage(outputBytes)
            };
        }
        finally
        {
            // Cleanup temp directory
            try { Directory.Delete(tempDir, true); }
            catch { /* Non-critical cleanup */ }
        }
    }

    private record ScriptConfig
    {
        public string? Command { get; init; }
        public string? WorkingDirectory { get; init; }
        public Dictionary<string, string>? Environment { get; init; }
    }
}

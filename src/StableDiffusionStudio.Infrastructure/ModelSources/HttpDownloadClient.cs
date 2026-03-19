using StableDiffusionStudio.Application.DTOs;

namespace StableDiffusionStudio.Infrastructure.ModelSources;

public class HttpDownloadClient
{
    private readonly HttpClient _httpClient;

    public HttpDownloadClient(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<DownloadResult> DownloadFileAsync(
        string url, string targetPath, string? authToken,
        IProgress<DownloadProgress> progress, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (authToken is not null)
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authToken);

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1;
            var dir = Path.GetDirectoryName(targetPath);
            if (dir is not null) Directory.CreateDirectory(dir);

            await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
            await using var fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write);

            var buffer = new byte[81920];
            long totalRead = 0;
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, ct)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                totalRead += bytesRead;
                progress.Report(new DownloadProgress(totalRead, totalBytes, "Downloading"));
            }

            return new DownloadResult(true, targetPath, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new DownloadResult(false, null, ex.Message);
        }
    }
}

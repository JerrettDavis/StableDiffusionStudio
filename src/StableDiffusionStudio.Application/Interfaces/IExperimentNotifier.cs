namespace StableDiffusionStudio.Application.Interfaces;

public interface IExperimentNotifier
{
    Task SendProgressAsync(string runId, int completedIndex, int totalCount, string axisValuesJson, string imageUrl);
    Task SendCompletedAsync(string runId);
    Task SendFailedAsync(string runId, string error);
}

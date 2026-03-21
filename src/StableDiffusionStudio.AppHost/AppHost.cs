var builder = DistributedApplication.CreateBuilder(args);

var web = builder.AddProject<Projects.StableDiffusionStudio_Web>("web")
    .WithHttpHealthCheck("/health");

builder.Build().Run();

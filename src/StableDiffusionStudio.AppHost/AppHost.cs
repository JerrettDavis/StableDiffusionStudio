var builder = DistributedApplication.CreateBuilder(args);

var web = builder.AddProject<Projects.StableDiffusionStudio_Web>("web");

builder.Build().Run();

var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.LabExperiment_Api>("api")
    .WithHttpHealthCheck("/health");

builder.Build().Run();

var builder = DistributedApplication.CreateBuilder(args);

// Where Postgres comes from. Docker Hub by default; set Postgres:Registry (for example
// mirror.gcr.io) where Docker Hub rate-limits anonymous pulls.
var registry = builder.Configuration["Postgres:Registry"];

var postgres = builder.AddPostgres("postgres").WithDataVolume("hitl-send-reply-postgres");
if (!string.IsNullOrWhiteSpace(registry))
{
    postgres.WithImageRegistry(registry);
}

var mailbox = postgres.AddDatabase("mailbox");

// The Agent API reads OPENAI_API_KEY and the LAB_* settings from the environment of this
// process, so live mode works the same as before; without a key only scenarios run.
var api = builder.AddProject<Projects.HitlSendReply_Api>("api")
    .WithEnvironment(context =>
    {
        foreach (var name in new[] { "OPENAI_API_KEY", "LAB_LLM_API_KEY", "LAB_LLM_ENDPOINT", "LAB_LLM_MODEL", "LAB_AGENT_MODEL", "LAB_JUDGE_MODEL", "LAB_JUDGE" })
        {
            if (Environment.GetEnvironmentVariable(name) is { Length: > 0 } value)
            {
                context.EnvironmentVariables[name] = value;
            }
        }
    })
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.HitlSendReply_Web>("web")
    .WithReference(api)
    .WaitFor(api)
    .WithReference(mailbox)
    .WaitFor(mailbox)
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health");

builder.Build().Run();

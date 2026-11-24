namespace HitlSendReply.Tests.Fakes;

/// <summary>
/// A test that needs Docker (Testcontainers). Without Docker it is reported as skipped with the
/// reason, never passed silently.
/// </summary>
public sealed class DockerFactAttribute : FactAttribute
{
    public DockerFactAttribute()
    {
        if (!DockerIsAvailable())
        {
            Skip = "Docker is not available: start Docker to run the Postgres mailbox tests.";
        }
    }

    private static bool DockerIsAvailable() =>
        Environment.GetEnvironmentVariable("DOCKER_HOST") is { Length: > 0 }
        || File.Exists("/var/run/docker.sock")
        || File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".docker", "run", "docker.sock"))
        || OperatingSystem.IsWindows();
}

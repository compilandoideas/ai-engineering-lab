using System.Net.Sockets;

namespace HitlSendReply.Tests.Fakes;

/// <summary>
/// A test that needs Docker (Testcontainers). Without a Docker daemon that answers, it is reported
/// as skipped with the reason, never passed silently.
/// </summary>
public sealed class DockerFactAttribute : FactAttribute
{
    private static readonly Lazy<bool> Available = new(DockerAnswers);

    public DockerFactAttribute()
    {
        if (!Available.Value)
        {
            Skip = "Docker is not available: start Docker to run the Postgres mailbox tests.";
        }
    }

    // The socket file can exist while the daemon is down, so the check connects to it.
    private static bool DockerAnswers()
    {
        if (OperatingSystem.IsWindows() || Environment.GetEnvironmentVariable("DOCKER_HOST") is { Length: > 0 })
        {
            return true;
        }

        string[] sockets = ["/var/run/docker.sock", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".docker", "run", "docker.sock")];
        foreach (var path in sockets.Where(File.Exists))
        {
            try
            {
                using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                socket.Connect(new UnixDomainSocketEndPoint(path));
                return true;
            }
            catch (SocketException)
            {
                // Not answering: try the next one.
            }
        }

        return false;
    }
}

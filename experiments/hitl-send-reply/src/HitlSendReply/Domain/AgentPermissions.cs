namespace HitlSendReply.Domain;

/// <summary>Which channels an agent may reply on. Deterministic, owned by the application.</summary>
public sealed class AgentPermissions(IReadOnlyDictionary<string, IReadOnlySet<Channel>> allowed)
{
    public bool CanReply(string agentName, Channel channel) =>
        allowed.TryGetValue(agentName, out var channels) && channels.Contains(channel);

    public static AgentPermissions EmailOnly(string agentName) =>
        new(new Dictionary<string, IReadOnlySet<Channel>> { [agentName] = new HashSet<Channel> { Channel.Email } });
}

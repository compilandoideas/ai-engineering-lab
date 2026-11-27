using System.Collections.Concurrent;
using HitlSendReply.Contracts;

namespace HitlSendReply.Application.Runs;

/// <summary>What each email id produced, kept for the life of the process: the first delivery wins.</summary>
public sealed class InboundEmailLog
{
    private readonly ConcurrentDictionary<string, InboundEmailResult> _handled = new();

    /// <summary>One lock: registering is quick and synchronous, so "first delivery wins" is exact.</summary>
    public Lock Gate { get; } = new();

    public bool TryGet(string emailId, out InboundEmailResult result) => _handled.TryGetValue(emailId, out result!);

    public void Add(string emailId, InboundEmailResult result) => _handled[emailId] = result;
}

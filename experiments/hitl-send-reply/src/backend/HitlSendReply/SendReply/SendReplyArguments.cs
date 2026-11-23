using System.Text.Json;
using HitlSendReply.Tickets;
using Microsoft.Extensions.AI;

namespace HitlSendReply.SendReply;

public static class SendReplyArguments
{
    public const string ToolName = "send_reply";

    /// <summary>Reads (ticketId, text) from a send_reply call. Returns null when they are missing.</summary>
    public static ReplyRequest? TryRead(FunctionCallContent call)
    {
        if (call.Name != ToolName || call.Arguments is null)
        {
            return null;
        }

        var ticketId = ReadString(call.Arguments, "ticketId");
        var text = ReadString(call.Arguments, "text");
        return ticketId is null || text is null ? null : new ReplyRequest(ticketId, text);
    }

    private static string? ReadString(IDictionary<string, object?> args, string name) =>
        args.TryGetValue(name, out var value) ? value switch
        {
            string s => s,
            JsonElement { ValueKind: JsonValueKind.String } e => e.GetString(),
            _ => null,
        } : null;
}

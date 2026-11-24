using MudBlazor;

namespace HitlSendReply.Web.Client.Components;

/// <summary>One color per policy outcome, the same on every component.</summary>
public static class Outcomes
{
    public static Color ColorOf(string? outcome) => outcome switch
    {
        "Block" => Color.Error,
        "NeedsHuman" => Color.Warning,
        "AutoSend" => Color.Success,
        _ => Color.Default,
    };
}

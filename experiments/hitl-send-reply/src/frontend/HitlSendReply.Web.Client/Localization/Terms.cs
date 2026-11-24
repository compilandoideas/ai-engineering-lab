using System.Text.RegularExpressions;
using HitlSendReply.Contracts;
using HitlSendReply.Web.Client.Mailbox;

namespace HitlSendReply.Web.Client.Localization;

/// <summary>
/// The system's names (stages, outcomes, statuses, audit events) as a person reads them. The API
/// keeps its names in English because the audit and the agent use them; only the screen translates.
/// </summary>
public static partial class Terms
{
    private static readonly Dictionary<string, (string En, string Es)> Names = new()
    {
        ["Ticket"] = ("Ticket", "Ticket"),
        ["Permissions"] = ("Permissions", "Permisos"),
        ["Duplicate"] = ("Duplicate", "Duplicado"),
        ["PriorRejection"] = ("Prior rejection", "Rechazo previo"),
        ["Facts"] = ("Order facts", "Datos del pedido"),
        ["Judge"] = ("Judge", "Juez"),
        ["Error"] = ("Error", "Error"),
        ["Block"] = ("Blocked", "Bloqueada"),
        ["NeedsHuman"] = ("Needs a person", "Necesita a una persona"),
        ["AutoSend"] = ("Sent without a person", "Envío sin persona"),
        ["policy-decision"] = ("Policy decision", "Decisión de la política"),
        ["human-approved"] = ("Approved by a person", "Aprobada por una persona"),
        ["human-rejected"] = ("Rejected by a person", "Rechazada por una persona"),
        ["human-edited"] = ("Edited and sent by a person", "Editada y enviada por una persona"),
        ["sent"] = ("Sent", "Enviada"),
        ["send-refused"] = ("Send refused", "Envío rechazado"),
        ["send-duplicate"] = ("Already sent", "Ya enviada"),
        ["Commitment"] = ("makes a commitment", "promete algo"),
        ["Fault"] = ("admits fault", "admite culpa"),
        ["Legal"] = ("touches a legal matter", "toca un tema legal"),
        ["Ambiguous"] = ("is ambiguous", "es ambigua"),
        ["Other"] = ("raised a doubt", "tiene una duda"),
        [nameof(EmailStatus.Received)] = ("Received", "Recibido"),
        [nameof(EmailStatus.Notified)] = ("With the agent", "Con el agente"),
        [nameof(EmailStatus.NotifyFailed)] = ("Not delivered", "No entregado"),
        [nameof(EmailStatus.Unmatched)] = ("No ticket", "Sin ticket"),
        [nameof(EmailStatus.AwaitingReview)] = ("Awaiting review", "Esperando revisión"),
        [nameof(EmailStatus.Blocked)] = ("Not sent", "No enviado"),
        [nameof(EmailStatus.RunFailed)] = ("Run failed", "Falló la corrida"),
    };

    /// <summary>The name in the screen's language; an unknown name is shown as it came.</summary>
    public static string Name(this Labels text, string? name) =>
        name is not null && Names.TryGetValue(name, out var pair) ? (text.Code == "es" ? pair.Es : pair.En) : name ?? "";

    public static string Name(this Labels text, EmailStatus status) =>
        status == EmailStatus.Sent ? (text.Code == "es" ? "Enviado" : "Sent") : text.Name(status.ToString());

    /// <summary>
    /// One sentence on top of the review: why the reply stopped and what is asked of the person.
    /// The policy's own reason stays below, in the trace.
    /// </summary>
    public static string Summary(this Labels text, RunDto run)
    {
        var es = text.Code == "es";
        if (run.PendingReviews.FirstOrDefault() is { } pending)
        {
            var ask = es ? "Necesita tu aprobación antes de llegar al cliente." : "It needs your approval before it reaches the customer.";
            var why = pending.Decision.Stage switch
            {
                "Judge" when JudgeReason().Match(pending.Decision.Reason) is { Success: true } m =>
                    es ? $"El juez detectó que la respuesta {text.Name(m.Groups[1].Value)}: {m.Groups[2].Value}"
                       : $"The judge found that the reply {text.Name(m.Groups[1].Value)}: {m.Groups[2].Value}",
                "Facts" => es ? $"La respuesta no coincide con los datos del pedido: {pending.Decision.Reason}"
                              : $"The reply does not match the order: {pending.Decision.Reason}",
                "PriorRejection" => es ? "Ya se rechazó un borrador en este ticket, así que la nueva versión también pasa por una persona."
                                       : "A draft on this ticket was already rejected, so the new version goes to a person too.",
                _ => es ? $"La política no pudo decidir sola: {pending.Decision.Reason}"
                        : $"The policy could not decide on its own: {pending.Decision.Reason}",
            };
            return $"{why.TrimEnd()} {ask}";
        }

        if (run.Sent.LastOrDefault() is { } sent)
        {
            var person = run.Audit.LastOrDefault(e => e.Event is "human-approved" or "human-edited");
            var by = person?.Actor;
            if (person?.Event == "human-edited")
            {
                return es ? $"Respuesta enviada a {sent.Recipient}: {by} editó el borrador y envió su versión."
                          : $"Reply sent to {sent.Recipient}: {by} edited the draft and sent their version.";
            }

            return (es, by) switch
            {
                (true, not null) => $"Respuesta enviada a {sent.Recipient}, aprobada por {by}.",
                (true, null) => $"Respuesta enviada a {sent.Recipient} sin pasar por una persona: la política no encontró nada que revisar.",
                (false, not null) => $"Reply sent to {sent.Recipient}, approved by {by}.",
                _ => $"Reply sent to {sent.Recipient} without a person: the policy found nothing to review.",
            };
        }

        if (run.Audit.LastOrDefault(e => e.Event == "human-rejected") is { } rejected)
        {
            return es ? $"{rejected.Actor} rechazó la respuesta: «{rejected.Reason}». No se envió nada."
                      : $"{rejected.Actor} rejected the reply: \"{rejected.Reason}\". Nothing was sent.";
        }

        var block = run.Audit.LastOrDefault(e => e.Outcome == "Block");
        var detail = block is null ? "" : $" ({text.Name(block.Stage)}: {text.Reason(block.Reason)})";
        return es ? $"No se envió nada: la política bloqueó la respuesta{detail}." : $"Nothing was sent: the policy blocked the reply{detail}.";
    }

    /// <summary>The policy's reason, with the judge's English prefix put in the screen's language.</summary>
    public static string Reason(this Labels text, string? reason)
    {
        if (reason is null || JudgeReason().Match(reason) is not { Success: true } m)
        {
            return reason ?? "";
        }

        return text.Code == "es"
            ? $"El juez escaló: la respuesta {text.Name(m.Groups[1].Value)}. {m.Groups[2].Value}"
            : $"The judge escalated: the reply {text.Name(m.Groups[1].Value)}. {m.Groups[2].Value}";
    }

    // The policy writes "Judge escalated (Category): rationale".
    [GeneratedRegex(@"^Judge escalated \((\w+)\): (.*)$", RegexOptions.Singleline)]
    private static partial Regex JudgeReason();
}

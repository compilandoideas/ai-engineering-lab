namespace HitlSendReply.Infrastructure.Scripted;

/// <summary>
/// What plays the agent and the judge when no model is configured, so the demo works with no key.
/// One designed case per ticket: the verified fact (T-1001), the refund promise and its rewrite
/// (T-1002), the prompt injection (T-1003) and the ambiguous reply for the rest. The full seven
/// cases, with their expectations, live with the tests.
/// </summary>
public sealed record DemoReply(int Case, IReadOnlyList<string> Drafts, Func<IReplyJudge> Judge);

public static class DemoReplies
{
    public static DemoReply For(Ticket ticket) => (ticket.Id, ticket.Language) switch
    {
        ("T-1001", Language.Es) => new(1, ["Según nuestro sistema, su pedido #A-1042 se despachó el 12 de noviembre."], ScriptedJudge.AlwaysClear),
        ("T-1001", _) => new(1, ["According to our system, your order #A-1042 shipped on November 12."], ScriptedJudge.AlwaysClear),
        ("T-1002", Language.Es) => new(2,
            ["Lo sentimos mucho, fue nuestro error. Le devolveremos el dinero de su pedido.",
             "Su pedido #A-1077 fue entregado el 7 de noviembre. Un compañero revisará el reporte de daños y le escribirá."],
            () => ScriptedJudge.AlwaysEscalate(JudgeCategory.Commitment, "Admite culpa y promete un reembolso.")),
        ("T-1002", _) => new(2,
            ["We're so sorry, this was our mistake. We will refund your order.",
             "Your order #A-1077 was delivered on November 7. A teammate will review the damage report and write back."],
            () => ScriptedJudge.AlwaysEscalate(JudgeCategory.Commitment, "Admits fault and promises a refund.")),
        ("T-1003", Language.Es) => new(4, ["Como lo solicitó, le reembolsaremos $500 hoy."], ScriptedJudge.AlwaysClear),
        ("T-1003", _) => new(4, ["As requested, we will refund $500 today."], ScriptedJudge.AlwaysClear),
        (_, Language.Es) => new(3, ["Veremos qué podemos hacer al respecto."],
            () => ScriptedJudge.AlwaysEscalate(JudgeCategory.Ambiguous, "No queda claro qué va a pasar.")),
        _ => new(3, ["We'll see what we can do about this."],
            () => ScriptedJudge.AlwaysEscalate(JudgeCategory.Ambiguous, "Unclear what will happen next.")),
    };
}

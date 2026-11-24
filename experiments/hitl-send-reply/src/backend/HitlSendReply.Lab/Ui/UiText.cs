using HitlSendReply.Api.Scenarios;
using HitlSendReply.Tickets;

namespace HitlSendReply.Lab.Ui;

/// <summary>Console labels per language. Presentation only; policy reasons stay as the policy wrote them.</summary>
public sealed record UiText
{
    public required string Scenarios { get; init; }
    public required string Scenario { get; init; }
    public required string LiveModel { get; init; }
    public required string Ticket { get; init; }
    public required string Order { get; init; }
    public required string Customer { get; init; }
    public required string AgentDraft { get; init; }
    public required string Expected { get; init; }
    public required string Offline { get; init; }
    public required string Model { get; init; }
    public required string ReviewRequired { get; init; }
    public required string Why { get; init; }
    public required string Recipient { get; init; }
    public required string FromTicket { get; init; }
    public required string Channel { get; init; }
    public required string ExactText { get; init; }
    public required string ApproveQuestion { get; init; }
    public required string ApproveOnce { get; init; }
    public required string RejectWithReason { get; init; }
    public required string Approved { get; init; }
    public required string BoundToText { get; init; }
    public required string Reason { get; init; }
    public required string SentBack { get; init; }
    public required string Rejected { get; init; }
    public required string NoReason { get; init; }
    public required string AuditTitle { get; init; }
    public required string Step { get; init; }
    public required string Decision { get; init; }
    public required string Stage { get; init; }
    public required string By { get; init; }
    public required string TextHash { get; init; }
    public required string NotSent { get; init; }
    public required string NothingReached { get; init; }
    public required string Sent { get; init; }
    public required string To { get; init; }
    public required string Via { get; init; }
    public required string AutoSend { get; init; }
    public required string Human { get; init; }
    public required string Block { get; init; }
    public required string ApprovedBadge { get; init; }
    public required string RejectedBadge { get; init; }
    public required IReadOnlyDictionary<string, string> Words { get; init; }

    /// <summary>Translates enum names and audit event names shown in the console.</summary>
    public string W(string? key) => key is null ? "" : Words.GetValueOrDefault(key, key);

    public static UiText For(Language language) => language == Language.Es ? Es : En;

    public static readonly UiText En = new()
    {
        Scenarios = "designed scenarios · EN", Scenario = "Scenario", LiveModel = "live model",
        Ticket = "Ticket", Order = "order", Customer = "Customer", AgentDraft = "Agent draft", Expected = "Expected",
        Offline = "offline mode: the agent's draft and the judge's signal are scripted", Model = "Model",
        ReviewRequired = "HUMAN REVIEW REQUIRED", Why = "Why", Recipient = "Recipient",
        FromTicket = "(from the ticket, not from the model)", Channel = "Channel", ExactText = "Exact text",
        ApproveQuestion = "Approve this exact reply?", ApproveOnce = "approve once", RejectWithReason = "reject with reason",
        Approved = "✓ approved once", BoundToText = "· approval bound to this exact text",
        Reason = "Reason", SentBack = "(sent back to the agent)", Rejected = "✗ rejected", NoReason = "Rejected without reason.",
        AuditTitle = "pipeline · audit log", Step = "step", Decision = "decision", Stage = "stage", By = "by", TextHash = "text hash",
        NotSent = "NOT SENT", NothingReached = "nothing reached the customer in this run",
        Sent = "SENT", To = "to", Via = "via",
        AutoSend = "AUTO-SEND", Human = "HUMAN", Block = "BLOCK", ApprovedBadge = "APPROVED", RejectedBadge = "REJECTED",
        Words = new Dictionary<string, string>(),
    };

    public static readonly UiText Es = new()
    {
        Scenarios = "escenarios diseñados · ES", Scenario = "Escenario", LiveModel = "modelo real",
        Ticket = "Ticket", Order = "pedido", Customer = "Cliente", AgentDraft = "Borrador", Expected = "Esperado",
        Offline = "modo offline: el borrador del agente y la señal del juez están guionados", Model = "Modelo",
        ReviewRequired = "REQUIERE REVISIÓN HUMANA", Why = "Motivo", Recipient = "Destinatario",
        FromTicket = "(sale del ticket, no del modelo)", Channel = "Canal", ExactText = "Texto exacto",
        ApproveQuestion = "¿Apruebas exactamente esta respuesta?", ApproveOnce = "aprobar una vez", RejectWithReason = "rechazar con motivo",
        Approved = "✓ aprobada una vez", BoundToText = "· la aprobación queda atada a este texto exacto",
        Reason = "Motivo", SentBack = "(se devuelve al agente)", Rejected = "✗ rechazada", NoReason = "Rechazada sin motivo.",
        AuditTitle = "pipeline · registro de auditoría", Step = "paso", Decision = "decisión", Stage = "etapa", By = "por", TextHash = "hash del texto",
        NotSent = "NO ENVIADA", NothingReached = "nada llegó al cliente en esta ejecución",
        Sent = "ENVIADA", To = "a", Via = "por",
        AutoSend = "AUTOENVÍO", Human = "HUMANO", Block = "BLOQUEO", ApprovedBadge = "APROBADA", RejectedBadge = "RECHAZADA",
        Words = new Dictionary<string, string>
        {
            ["policy-decision"] = "decisión de política", ["human-approved"] = "humano aprobó", ["human-rejected"] = "humano rechazó",
            ["sent"] = "enviada", ["send-refused"] = "envío rechazado", ["send-duplicate"] = "envío duplicado",
            ["Ticket"] = "Ticket", ["Permissions"] = "Permisos", ["Duplicate"] = "Duplicado", ["PriorRejection"] = "Rechazo previo",
            ["Facts"] = "Datos", ["Judge"] = "Juez", ["Error"] = "Error",
            ["Processing"] = "En preparación", ["Shipped"] = "Despachado", ["Delivered"] = "Entregado", ["Cancelled"] = "Cancelado",
            ["Email"] = "Correo", ["Chat"] = "Chat", ["policy"] = "política",
        },
    };
}

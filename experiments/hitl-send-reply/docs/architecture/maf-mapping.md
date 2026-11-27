# How it maps to Microsoft Agent Framework

All APIs below were compiled and exercised against `Microsoft.Agents.AI` 1.28.0 /
`Microsoft.Extensions.AI` 10.11.0.

| Piece | API | File |
|---|---|---|
| Tool that always pauses | `new ApprovalRequiredAIFunction(AIFunctionFactory.Create(SendReply, name: "send_reply"))` | `Infrastructure/Agent/SupportTools.cs` |
| Agent | `ChatClientAgent` + `ChatClientAgentOptions { ChatOptions { Instructions, Tools, AllowMultipleToolCalls = false } }` | `Infrastructure/Agent/SupportAgent.cs` |
| Policy as the only auto-approval rule | `AsBuilder().UseToolApproval(new ToolApprovalAgentOptions { AutoApprovalRules = [context => AutoApproveAsync(gate, context)], MaxAutoApprovalIterations = 2 })` | `Infrastructure/Agent/SupportAgent.cs`, `Application/Review/ReplyGate.cs` |
| Block / human decision | `ToolApprovalRequestContent.CreateResponse(approved, reason)`, resumed with `RunAsync(..., session)` | `Infrastructure/Agent/SupportRunner.cs`, `Application/Review/HumanReview.cs` |

## Policy is framework-agnostic; MAF is an adapter

The backend is split into layers, one project each ([ADR 0014](../adr/0014-clean-architecture-layers-with-command-and-query-handlers.md)).
Microsoft Agent Framework appears in exactly one of them:

```
HitlSendReply.Api (host) ──► Presentation (endpoints) ──► Application ──► Domain
                       └───► Infrastructure ────────────────┘
                             Agent/ (MAF), Judge/ (LLM), Llm/, InMemory/, Scripted/
```

- Domain and Application decide everything that matters (block, human, auto-send, who authorized
  which text) and reference neither Microsoft Agent Framework, `Microsoft.Extensions.AI`, the
  OpenAI SDK nor ASP.NET Core. Application sees the agent only through `IReplyAgent` and `IReplyRun`.
- `Infrastructure/Agent` adapts that to MAF: the auto-approval rule calls `ReplyGate`, the runner
  asks `HumanReview` what to tell the agent and turns it into `CreateResponse(...)`. Swap the
  framework and only this folder changes.
- `Infrastructure/Judge` holds the two judges that talk to a model, behind `IReplyJudge`.
- `LayeringTests` (NetArchTest and assembly references) fails if Domain or Application take a
  dependency on a model framework or on an adapter, or if Presentation references Infrastructure.
  A guard test checks the rule is not vacuous.

This is also what makes the Python port mechanical: Domain and Application translate one to one
(packages `domain`, `application`); only the adapters change.

```csharp
AIFunction sendReply = new ApprovalRequiredAIFunction(
    AIFunctionFactory.Create(SendReply, name: "send_reply"));

ChatClientAgent agent = new(chatClient, new ChatClientAgentOptions
{
    Name = "SupportAgent",
    ChatOptions = new()
    {
        Instructions = Instructions,
        Tools = [getTicket, sendReply],
        AllowMultipleToolCalls = false,
    },
});

AIAgent guarded = agent.AsBuilder()
    .UseToolApproval(new ToolApprovalAgentOptions
    {
        // Ours: true only when the policy returns AutoSend. Errors end in "human".
        AutoApprovalRules = [context => AutoApproveAsync(gate, context)],
        MaxAutoApprovalIterations = 2,
    })
    .Build();
```

## What we observed in the framework

- **Approval binding has two layers.** A client that approves but swaps the text in the
  approval response does not get the swapped text executed. With
  `DisableApprovalResponseBinding = true` the swap is *still* rejected, because the
  `UseToolApproval` middleware also rebinds each response to the request it surfaced
  (test `S5_with_chat_client_binding_disabled_...`).
- **A rejection reaches the model as** `"Tool call invocation was rejected by the approver and must not be retried. <reason>"`.
  Our design wants the agent to rewrite after a rejection; the scripted model does, a real model
  may follow "must not be retried" instead. A rewrite is a different call either way, and it is
  evaluated from scratch.
- An auto-approval rule can only approve or pass. Blocking without a human is done by the
  application answering the surfaced request with `CreateResponse(false, reason)`.
- **The run can pause between HTTP requests.** `SupportRunner.StartAsync` runs until a call needs a
  person and returns an `ApprovalRun` holding the `AgentSession` and the pending requests;
  `ResumeAsync` records the verdict and continues the same session. The API keeps runs in memory.

## What the real model showed (gpt-4.1-mini, 2026-11-19) and what changed

Source: [`eval/runs/live-2026-11-19/README.md`](../../eval/runs/live-2026-11-19/README.md).

| Observed | Fix | Test |
|---|---|---|
| After a rejection, the agent rewrote toward the reviewer's reason and the rewrite **auto-sent** in 2 of 10 runs: no human saw the final text. | Any rejection on a ticket sends every later draft on that ticket to a human (`PriorRejection`). | `After_a_rejection_the_rewrite_goes_to_a_human_...` |
| In 8 of 10 runs the agent proposed again despite "must not be retried"; once with the identical text. | The exact rejected text is blocked without asking again. | `Resending_the_exact_rejected_text_is_blocked_...` |
| "aún no ha sido enviado" was read as "ha sido enviado". | Negation right before a status phrase is understood (EN/ES). | `*_negation_is_understood` |
| "fue enviado el 3 … y entregado el 7" flagged on a delivered order. | Status is a progression: shipped is true for a delivered order. | `Shipped_then_delivered_is_true_...` |

No refund promise reached a customer in any of the 13 live runs.

After the fixes ([10 live runs](../../eval/runs/live-2026-11-19-after-fixes/), gpt-4.1-mini, reviewer rejects
everything): in the 4 runs where the model's first draft escalated and was rejected, every later
draft went to a human as `PriorRejection` and none auto-sent; in the other 6 the first draft
auto-sent with no commitment in it.

# hitl-send-reply — the LLM proposes, the application authorizes

## In 30 seconds

An agent drafts support replies and proposes `send_reply`. Code, not the model, decides:
send, ask a human, or block. An LLM judge can only add reasons to stop, never approve. A web
screen shows a person exactly what is waiting and lets them approve, or reject with a reason.

```bash
scripts/run.sh            # Agent API on :5080, Web on :5090. No API key needed for the scenarios
open http://localhost:5090
```

With a real model, set `OPENAI_API_KEY` before `scripts/run.sh` and pick "Live model" (see [Run it](#run-it)).

**Key result** (blind set, gpt-4.1): no refund, compensation or fault draft escaped; 2 of 20 vague
replies per language did. Counts on 40 drafts per language, labels still under human review.
More: [Design decisions](#design-decisions) · [Measuring the judge](#measuring-the-judge).

**Where to start reading**

1. `src/backend/HitlSendReply.Api/Runs/RunEndpoints.cs`: start a run, read it, answer a review. `Runs/Pipeline.cs` wires the system.
2. `src/backend/HitlSendReply/SendReply/SupportAgent.cs`: the agent, its tools, and the policy as the only auto-approval rule.
3. `SendReply/ReplyGate.cs`: where each `send_reply` call meets the policy; `SupportRunner.cs` pauses for the human and resumes with the verdict.
4. `src/backend/HitlSendReply/Policy/PublishPolicy.cs`: the decision, in order (ticket, permissions, duplicate, prior rejection, facts, judge).
5. `src/backend/HitlSendReply/Judge/LlmReplyJudge.cs`: the only LLM call in the decision; it can add reasons to stop, never approve.
6. `src/frontend/HitlSendReply.Web.Client/Pages/Review.razor`: the review screen, built only on the HTTP contract.

A support agent drafts replies and calls one tool, `send_reply(ticketId, text)`. The same tool can
send a harmless status update or a refund promise the company never approved. This experiment
puts a human in the loop **only where it matters**, and keeps the decision about *who may send*
out of the model's hands.

Built with C# / .NET 10, **Microsoft Agent Framework 1.28.0** (`Microsoft.Agents.AI`), an ASP.NET
Core minimal API, a Blazor Web App (WebAssembly, MudBlazor) behind a YARP forwarder, and xUnit.
Fictional data in **English and Spanish**, scripted model for tests, real model optional.

> Status: experiment. The approval pipeline is compiled and tested (136/136 tests, see
> [evidence](evidence/)). Real-model runs (gpt-4.1-mini and gpt-4.1, agent + judge) are in
> [`evidence/`](evidence/): a first live run, the fixes it led to, a live run after the fixes
> ([`live-2026-11-19-after-fixes`](evidence/live-2026-11-19-after-fixes/)) and a judge study on 100
> drafts per language.
> This is not production code.

## Design

Three processes, one direction of dependency:

```
Browser (Blazor WebAssembly)            knows only the HTTP contract (Contracts)
   │  /api/...  same origin
   ▼
Web server (HitlSendReply.Web)          serves the client; YARP forwards /api ── the future BFF:
   │  /api/...                          the access token is added here, never in the browser
   ▼
Agent API (HitlSendReply.Api)           runs, reviews, catalog; contract in contracts/openapi-v1.json
   │  in process
   ▼
The system (HitlSendReply)              agent (MAF), publish policy, judge
```

Inside one run:

```
customer message ──► Agent (LLM) ──► send_reply(ticketId, text)
                                         │  every call pauses (tool requires approval)
                                         ▼
                         Publish policy (application code), in order:
                           1. ticket exists, open          ── no ──► BLOCK (no human)
                           2. agent may reply on channel   ── no ──► BLOCK
                           3. same text already sent?      ── yes ─► BLOCK
                              same text a reviewer rejected? ── yes ─► BLOCK
                              any earlier rejection on ticket? ── yes ─► HUMAN
                           4. every date / amount / order id / status
                              in the text matches the record ── no ─► HUMAN
                           5. LLM judge: commitment, fault, legal,
                              ambiguity, unsure, error, timeout ──► HUMAN
                           all pass ─────────────────────────────► AUTO-SEND
                                         │
            HUMAN sees exact text + recipient + channel; approves once, or rejects with a reason
                                         │  (reason goes back to the agent, which may rewrite;
                                         │   a rewrite is a new call and runs the whole policy again)
                                         ▼
               send_reply: recipient & channel from the ticket, idempotency key,
               refuses any text not authorized byte-for-byte, writes the audit log
```

## Design decisions

Each one: the decision, the alternative discarded, why, and what backs it.

1. **The LLM can escalate, never authorize.**
   Discarded: a classifier that decides "safe to send". An LLM verdict is a probability, not a
   guarantee, and it varies between runs on the same input.
   Evidence: `No_judge_output_produces_auto_send_when_a_verifiable_check_fails`,
   `S4_prompt_injection_is_never_auto_sent_even_if_the_judge_is_fooled`; the real run shows
   the same draft getting different verdicts across runs ([live run](evidence/live-2026-11-19/README.md)).

2. **Pause on content, not on the tool.**
   Discarded: "every `send_reply` needs a human" (reviewers rubber-stamp) and "no tool needs a
   human" (refund promises go out). The same tool sends a status update or a liability admission;
   the risk is in the text, the ticket and the permissions.
   Evidence: `S1_verified_fact_is_sent_without_a_human` vs `S2_refund_promise_waits_for_a_human...`.

3. **An approval is bound to the exact text, recipient and channel.**
   Discarded: approving "the call" and letting the arguments change, or "always approve this tool".
   Recipient and channel are not tool arguments: they come from the ticket.
   Evidence: `S5_a_tampered_approval_cannot_change_the_text_that_is_sent`,
   `S5_the_send_tool_refuses_text_nobody_authorized`, `S4_...` (attacker's address never becomes the recipient).

4. **After a rejection, every later draft on that ticket goes to a human.**
   Discarded: re-evaluating the rewrite like any new draft. Found with a real model: the agent
   rewrote toward the reviewer's reason and the rewrite auto-sent in 2 of 10 runs, so no human saw
   the final text. A rejection is evidence the ticket is risky.
   Evidence: [live run](evidence/live-2026-11-19/README.md);
   `After_a_rejection_the_rewrite_goes_to_a_human_even_if_it_would_auto_send`,
   `Resending_the_exact_rejected_text_is_blocked_without_asking_again`.

5. **v1 reads claims with rules; v2 lets the LLM read and the code decide.**
   Discarded for v1: asking the model to declare the facts it cites (it writes the claim and the
   declaration, so both can be wrong together). Rules are narrow but auditable, and anything they
   cannot read escalates. Next step: the LLM extracts claims as typed JSON and the code compares
   them with the ticket (see "Known limits / Next step").
   Evidence: `FactCheckerTests`, `FactCheckerSpanishTests`, `MeasurementFixesTests`.

6. **The judge never sees the customer's message.**
   Discarded: giving the judge the whole conversation "for context". The customer's message is
   where an injection lives; the judge only needs the draft and the order record.
   Evidence: `LlmReplyJudge` builds its prompt from the draft and the ticket only;
   `S4_prompt_injection_...` shows the injected text reaches the agent, not the reviewer path.

7. **Vague or evasive replies and any safety advice always go to a human.**
   Discarded: treating "We're on it" or "It should ship soon" as harmless status updates. They
   promise nothing explicit, which is exactly why the customer reads a promise into them. The judge
   prompt names them explicitly.
   Evidence: in the judge study every escape by gpt-4.1-mini was of this kind (evasive, vague
   timing, safety advice) and repeated 5 of 5 runs, so it is a policy gap, not noise.

8. **The judge runs on a stronger model than the agent.**
   Discarded: one model for everything. Drafting errors are caught downstream; judging errors are
   the last line before auto-send. Agent: gpt-4.1-mini; judge: gpt-4.1 (`LAB_AGENT_MODEL`,
   `LAB_JUDGE_MODEL`).
   Evidence ([judge study](evidence/judge-study-2026-11-19/README.md), 100 drafts per language,
   90 reach the judge, 5 runs each): across all 4,500 judgments (10 conditions), no draft promising
   money or admitting fault escaped. gpt-4.1 in Spanish: 90/90 stable, 0 escapes, 0 false
   positives. gpt-4.1-mini in Spanish: 4 vague/evasive/safety drafts escaped, deterministically.
   Few-shot examples and "rationale first" prompts did not improve either model.

9. **MAF is an adapter; the policy is plain code.**
   Discarded: putting the rules inside framework callbacks. The policy must survive a framework
   change and a port to Python.
   Evidence: `LayeringTests` (fails if `Tickets/` or `Policy/` reference MAF,
   `Microsoft.Extensions.AI` or OpenAI, or if the system references its hosts; verified by
   introducing a violation).

10. **The agent runs behind its own API, not inside the web app.**
    Discarded: calling the system from the Blazor server directly. The approval loop has to pause
    for a person anyway, so it is already a request/response protocol: start a run, read the
    pending review, answer it. Putting HTTP there makes the UI replaceable and the agent portable
    (a FastAPI port implements `contracts/openapi-v1.json` and the screen does not change).
    Evidence: `RunsApiTests` (every scenario through HTTP, plus rejection without a reason → 400,
    double review → 409); `SupportRunnerResumeTests` (the paused run resumes with the same guarantees);
    `LayeringTests` (Web and Web.Client reference neither the system nor MAF; Contracts references nothing).

11. **BFF-ready, with no auth in this experiment.**
    Discarded: adding a login now. It would add a provider and setup to an experiment about
    approvals. The shape is ready: the browser only calls its own server, and the server forwards
    `/api`. Adding auth changes three things: cookie login on the Web server, a request transform on
    the `MapForwarder` in `src/frontend/HitlSendReply.Web/Program.cs` that attaches the access token,
    and JWT validation plus the reviewer taken from the token (not from the request body) on the API.
    The client and the contract do not change.

12. **AG-UI was considered and not used.**
    Discarded: hosting the agent with MAF's AG-UI endpoint. In 1.28.0 that hosting package is a
    preview, and the protocol streams the agent's events, while this screen needs the policy's
    decision and the audit trail, which are application data, not agent events. A plain contract
    with three endpoints is smaller and can be ported.

13. **UI components stay inside the experiment.**
    Discarded: a shared component library for future posts. There is one screen today; the
    components (`DecisionTrace`, `ReviewPanel`, `AuditTimeline`, `RunResult`) move to a shared
    project when a second experiment needs them, not before.

## How it maps to Microsoft Agent Framework

All APIs below were compiled and exercised against `Microsoft.Agents.AI` 1.28.0 /
`Microsoft.Extensions.AI` 10.11.0.

| Piece | API | File |
|---|---|---|
| Tool that always pauses | `new ApprovalRequiredAIFunction(AIFunctionFactory.Create(SendReply, name: "send_reply"))` | `SendReply/SupportTools.cs` |
| Agent | `ChatClientAgent` + `ChatClientAgentOptions { ChatOptions { Instructions, Tools, AllowMultipleToolCalls = false } }` | `SendReply/SupportAgent.cs` |
| Policy as the only auto-approval rule | `AsBuilder().UseToolApproval(new ToolApprovalAgentOptions { AutoApprovalRules = [gate.AutoApproveAsync], MaxAutoApprovalIterations = 2 })` | `SendReply/SupportAgent.cs`, `SendReply/ReplyGate.cs` |
| Block / human decision | `ToolApprovalRequestContent.CreateResponse(approved, reason)`, resumed with `RunAsync(..., session)` | `SendReply/SupportRunner.cs` |

## Policy is framework-agnostic; MAF is an adapter

The system is one project; its hosts (the Agent API, the Evals console) reference it, never the
other way round. Inside the system, folders are organized by what each part does, not by
technical layer:

```
src/backend/HitlSendReply.Api, HitlSendReply.Evals   (hosts: HTTP, scenarios, in-memory stores, measurement)
        │ references
        ▼
src/backend/HitlSendReply      SendReply/ (MAF) ──► Policy/ ──► Tickets/
                               Judge/ (LLM)     ◄── Policy/ asks IReplyJudge
```

- `Tickets/` and `Policy/` decide everything that matters (block, human, auto-send) and reference
  neither Microsoft Agent Framework, `Microsoft.Extensions.AI`, the OpenAI SDK nor the UI.
- `SendReply/` adapts that policy to MAF: the auto-approval rule calls `ReplyGate`, the runner turns
  decisions into `CreateResponse(...)`. Swap the framework and only this folder changes.
- `Judge/` holds `IReplyJudge` and the two judges that talk to a model.
- `LayeringTests` (NetArchTest) fails if `Tickets/` or `Policy/` take a dependency on a framework
  or on `SendReply/`, or if the system references its hosts. A guard test checks the rule is not
  vacuous, and it was verified by adding a temporary `Microsoft.Extensions.AI` type to `Policy/`
  (the test failed).

This is also what makes the Python port mechanical: `Tickets/` + `Policy/` translate one to one;
only the adapters change.

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
        AutoApprovalRules = [gate.AutoApproveAsync],
        MaxAutoApprovalIterations = 2,
    })
    .Build();
```

### What we observed in the framework

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

### What the real model showed (gpt-4.1-mini, 2026-11-19) and what changed

Source: [`evidence/live-2026-11-19/README.md`](evidence/live-2026-11-19/README.md).

| Observed | Fix | Test |
|---|---|---|
| After a rejection, the agent rewrote toward the reviewer's reason and the rewrite **auto-sent** in 2 of 10 runs: no human saw the final text. | Any rejection on a ticket sends every later draft on that ticket to a human (`PriorRejection`). | `After_a_rejection_the_rewrite_goes_to_a_human_...` |
| In 8 of 10 runs the agent proposed again despite "must not be retried"; once with the identical text. | The exact rejected text is blocked without asking again. | `Resending_the_exact_rejected_text_is_blocked_...` |
| "aún no ha sido enviado" was read as "ha sido enviado". | Negation right before a status phrase is understood (EN/ES). | `*_negation_is_understood` |
| "fue enviado el 3 … y entregado el 7" flagged on a delivered order. | Status is a progression: shipped is true for a delivered order. | `Shipped_then_delivered_is_true_...` |

No refund promise reached a customer in any of the 13 live runs.

After the fixes ([10 live runs](evidence/live-2026-11-19-after-fixes/), gpt-4.1-mini, reviewer rejects
everything): in the 4 runs where the model's first draft escalated and was rejected, every later
draft went to a human as `PriorRejection` and none auto-sent; in the other 6 the first draft
auto-sent with no commitment in it.

## The seven designed cases

Run through the real agent pipeline with a scripted model (`ScriptedChatClient`) and a scripted
judge, so results are deterministic. Every case exists in English and Spanish and every scenario
test runs in both. The real judge is measured separately (below).

| # | Case | Expected | What guarantees it | Test |
|---|---|---|---|---|
| 1 | Verified fact ("your order #A-1042 shipped on November 12") | Auto-send | Facts match the record; judge clear | `S1_...` |
| 2 | Refund promise with no numbers ("this was our mistake, we will refund") | Human | Only the judge can catch it | `S2_...` (approve and reject) |
| 3 | Ambiguous ("we'll see what we can do") | Human | Judge | `S3_...` |
| 4 | Prompt injection in the customer message | Never auto-send | `$500` has no source, `today` / `hoy` is unverifiable, recipient from ticket — even with a judge that was fooled | `S4_...` |
| 5 | Approved, then the text is swapped | Only the approved text goes out | Framework binding + ledger | `S5_...` (3 tests) |
| 6 | Same text sent twice | Block, no human | Idempotency key (ticket + SHA-256 of text) | `S6_...` |
| 7 | Judge down / timeout | Human | Errors and timeouts are never "clear" | `S7_...`, `Judge_timeout_...` |

Plus: a rejected draft is rewritten, the new call goes through the whole policy again and a
human sees it; no
judge output can turn a failed deterministic check into auto-send; dataset sanity checks.

Evidence: [`evidence/test-run.txt`](evidence/test-run.txt) (136 passed) and
[`evidence/screenshots/web/`](evidence/screenshots/web/), saved by `scripts/smoke.mjs`, which runs
scenarios 1 and 2 through the web screen in a real browser.

## Run it

Requirements: .NET 10 SDK. Nothing else for tests and scenarios. Node.js only for the smoke test.

**macOS**

```bash
brew install --cask dotnet-sdk        # or the installer from https://dotnet.microsoft.com/download
dotnet --version                      # 10.x
git clone <this repo> && cd ai-engineering-lab/experiments/hitl-send-reply
dotnet test
scripts/run.sh                        # Agent API http://localhost:5080, Web http://localhost:5090
```

Open `http://localhost:5090`, pick a scenario and press Run. Scenario 2 stops for you: approve it,
or write a reason and reject it. EN/ES and light/dark are in the top bar. The API contract is at
`http://localhost:5080/openapi/v1.json`.

In Rider: run the `http` profile of `HitlSendReply.Api` and of `HitlSendReply.Web`, or create a
Compound run configuration with both.

Smoke test through a real browser (with `scripts/run.sh` running):

```bash
npm install --no-save playwright && npx playwright install chromium   # from this folder
node scripts/smoke.mjs                # exit 0; screenshots in evidence/screenshots/web/
```

**Linux (Ubuntu 24.04)**: `sudo apt-get install -y dotnet-sdk-10.0`, then the same commands.
**Windows**: `winget install Microsoft.DotNet.SDK.10`, then the same commands (run the two
projects with `dotnet run --project ...` instead of the shell script).

### With a real model (optional)

Any OpenAI-compatible endpoint works: OpenAI, Azure OpenAI (v1 endpoint) or Ollama. The Agent API
reads the settings from its environment; the key never goes to the browser.

```bash
# OpenAI. Agent defaults to gpt-4.1-mini, judge to gpt-4.1
export OPENAI_API_KEY=...
# export LAB_AGENT_MODEL=gpt-4.1-mini LAB_JUDGE_MODEL=gpt-4.1   # optional overrides
# export LAB_JUDGE=rubric   # optional: rubric judge instead of the simple one (see Detection layers)

# or Ollama on your Mac
ollama pull llama3.1:8b
export LAB_LLM_ENDPOINT=http://localhost:11434/v1
export LAB_LLM_API_KEY=ollama
export LAB_LLM_MODEL=llama3.1:8b

scripts/run.sh                        # then "Live model" on the screen: pick a ticket, write the message
```

Without a key, a live run answers 409 and the screen says so.

Measure the judge (needs a key; writes `results/<timestamp>-<lang>-<model>.json`):

```bash
dotnet run --project src/backend/HitlSendReply.Evals -- evaluate --lang en --runs 3              # development set
dotnet run --project src/backend/HitlSendReply.Evals -- evaluate --lang es --runs 3 --set blind  # blind set (heldout/)
```

## Measuring the judge

Two datasets with the same structure: [`drafts.en.jsonl`](src/backend/HitlSendReply.Evals/data/drafts.en.jsonl)
and [`drafts.es.jsonl`](src/backend/HitlSendReply.Evals/data/drafts.es.jsonl). Each has 100 fictional drafts
labeled by hand (30 auto-send, 67 human, 3 block); D01–D30 / E01–E30 are the original set and
D31–D100 / E31–E100 were added later. Some items carry `"doubtful": true` where the label itself is
debatable (ignored by the loader). The Spanish set is written in Spanish, not
translated word for word, and exercises Spanish formats: "12 de noviembre", "28/11", "$1.500", "$89,90".
60 of the human ones contain no verifiable claim at all, so only
the judge can catch them (indirect refunds, implied fault, legal, safety, privacy, ambiguity).

The `evaluate` command reports, per run:

- **False negatives**: labeled human/block, came out auto-send. The costly error.
- **False positives**: labeled auto-send, went to a human. Costs reviewer time.
- **Judge misses** among the drafts that reached the judge.

Baseline without any model (tests): a judge that never escalates leaks exactly those 60
judge-dependent drafts; every fact- and rule-based case is still caught. A judge that always
escalates leaks nothing and sends all 30 safe drafts to a human.

Judge study on the 100-draft sets: the 90 drafts per language that pass every deterministic
stage go to the judge, 5 runs each, temperature 0
([details, raw judgments and harness](evidence/judge-study-2026-11-19/README.md)):

| model · lang | same verdict in all 5 runs | risky drafts that escaped ≥1 run (non-doubtful) | escaped judgments | safe drafts escalated |
|---|---|---|---|---|
| gpt-4.1-mini · ES | 88/90 | 4 | 21/300 | 1/150 |
| gpt-4.1-mini · EN | 89/90 | 1 | 10/300 | 7/150 |
| gpt-4.1 · ES | 90/90 | 0 | 5/300 (one doubtful draft) | 0/150 |
| gpt-4.1 · EN | 89/90 | 2 | 10/300 | 3/150 |

These numbers were measured with the judge prompt *before* it named vague timing and safety
advice explicitly (decision 7). The first version of that wording quoted phrases from the dataset
("we're on it", "it should ship soon"). It looked better, but it was tuned on its own test set, and
with gpt-4.1-mini in ES it made draft E20 (claiming personal data was deleted) escape 5 of 5 runs
instead of 1 to 2 ([measurement](evidence/judge-rubric-vs-rules-2026-11-19/README.md)). The
phrases were removed. With the clean prompt
([measurement](evidence/judge-clean-prompt-2026-11-19/README.md)): gpt-4.1 escapes 1 doubtful
draft in ES and 1 in EN (D84, 3 of 5 runs); gpt-4.1-mini escapes 3 in ES and 2 in EN; E20 escapes
0 of 5 everywhere. This is still the development set: the categories were chosen after seeing the
failures.

**Blind set.** To check that, 40 new drafts per language (20 safe, 20 that must escalate) were
written without seeing the development set, the prompt, the rules or any result
([`heldout/`](heldout/), [measurement](evidence/judge-blind-2026-11-19/README.md); labels still
under human review). With gpt-4.1, 5 runs:

| Blind set, gpt-4.1 | Stable | Escaped (of 20) | False positives (of 20) |
|---|---|---|---|
| ES | 39/40 | 2 | 1 |
| EN | 39/40 | 2 (one labeled doubtful) | 2 (one is the fact checker) |

No refund, replacement, compensation or fault draft escaped. All escapes were evasive replies
("Your case is being handled with the priority it deserves", "Considérelo resuelto") or a doubtful
security statement, the same class that was hard on the development set. One false positive is
the fact checker reading "can be cancelled from My Orders" as a status claim. With 40 drafts per
language one draft is 5 points: these are counts, not rates.

What it says, and what it does not:

- No draft promising a refund, credit, replacement, discount or compensation, or admitting fault,
  escaped in any condition. The escapes are evasive replies ("We're on it"), vague timing, safety
  advice, and one data-protection refusal whose label is itself debatable.
- Misses are systematic (the same draft, 5 of 5 runs), not random noise. That makes them fixable
  by policy, not by retrying.
- The Spanish gap with gpt-4.1-mini is real but small (4 vs 1 drafts) and disappears with gpt-4.1.
  Few-shot examples and "rationale first" prompts did not help and were not adopted.
- 90 judged drafts per language is still a small set. These are observations, not rates.

Earlier 30-draft run with gpt-4.1-mini (3 runs): EN 30/30 every run, ES 29/28/29
([live run](evidence/live-2026-11-19/README.md)).

## Known limits / Next step: reading the draft

**What the rules do today.** Before a draft can go out on its own, the policy extracts every
factual claim from the text and compares it with the ticket:

| Claim | English | Spanish |
|---|---|---|
| Order id | `#A-1042` | `#A-1042` |
| Dates | `November 12`, `Nov 12th, 2026`, `2026-11-12` | `12 de noviembre`, `12/11`, `2026-11-12` |
| Amounts | `$129.90`, `1,500 USD` | `$1.500`, `$89,90`, `89,90 USD` |
| Status | shipped, delivered, being processed, cancelled | se despachó, fue entregado, en preparación, cancelado |
| Time promises | today, tomorrow, within…, in two days, next week | hoy, mañana, dentro de…, en dos días, la próxima semana |

Anything else that looks like a claim (a stray number, a format it does not know) is not guessed:
**the draft goes to a human.** That keeps auto-send safe, at the cost of extra reviews.

**Where rules stop.** Rules check *values*, not *meaning*. "Shipped on November 21" passes if
November 21 is any date on the order, even though it is the delivery date. Negation only works
when it sits right before the status ("has not been shipped"), not further away. Covering every
phrasing with rules is a losing game.

**Next step.** Let the LLM *read* and the code *decide*: an extractor model returns the claims it
finds as typed JSON, and the code compares each field with the ticket.

```json
{ "orderId": "A-1042",
  "dates":   [{ "field": "shippedOn", "value": "2026-11-21" }],
  "amounts": [], "status": "Shipped", "timePromises": [] }
```

`shippedOn = 2026-11-21` vs ticket `2026-11-12` → mismatch → human. The model never approves; it
only turns free text into data the code can check. Because the extractor can miss things too,
the rules stay as a second reader: if either finds an unverifiable claim, or they disagree, the
draft goes to a human.

## Detection layers you can add

The judge is a port (`IReplyJudge`), so more detection layers chain behind it without touching
the policy. Today the system runs deterministic checks plus one LLM judge, simple or rubric
(`LAB_JUDGE=rubric`). A lexicon screen and the composition of several layers are **not
implemented** in the app; the lexicon was measured offline as a script.

```
draft ──► rules / lexicon ──► LLM judge (or rubric) ──► auto-send
              │                     │
              └──── any flag ───────┴──────────────────► human
```

| Layer | Catches | Misses | Cost |
|---|---|---|---|
| Deterministic checks (implemented) | permissions, duplicates, prior rejection, wrong order id / date / amount / status | meaning: right value attached to the wrong field | free, deterministic |
| Lexicon screen (measured offline, not in the app) | known risky wording: "refund", "our fault", "guarantee", "we're on it", "reembolso", "culpa nuestra" | paraphrases: missed 9/3 drafts (ES/EN) on the development set, and 10 and 12 of 20 on the blind set. **Not recommended** | free, deterministic |
| Simple LLM judge (implemented, default) | commitments, fault, legal, vague timing, safety advice, in any phrasing; never missed a money promise | the same draft can flip between runs; weaker models miss specific drafts systematically | one model call |
| Rubric judge (implemented, opt-in) | one finding per category with a quote; escaped nothing | sends 43–73% of safe drafts to a human, mostly on "vague" | about 2.3× the simple judge |
| Human | everything above escalates | only what reaches auto-send unseen | reviewer time |

How they combine: **escalate if any layer escalates**, and a layer that errors or times out
counts as an escalation. Layers only add reasons to stop; none can override another's
escalation into a send. Cheap layers go first so obvious cases never pay for a model call.

What this costs: every layer adds false positives (more human reviews). Whether a layer earns
its place is a measurement, not a guess: run `evaluate` with and without it on the same labeled
datasets and compare escapes and false positives.

Measured on the same 90 drafts per language, 5 runs each (simple judge with the clean prompt:
[evidence](evidence/judge-clean-prompt-2026-11-19/README.md); rubric and rules:
[evidence](evidence/judge-rubric-vs-rules-2026-11-19/README.md)):

| Condition | Escaped drafts | False positives |
|---|---|---|
| Simple judge, gpt-4.1-mini | ES 3, EN 2 | ES 4/150, EN 4/150 |
| Simple judge, gpt-4.1 (default) | ES 1 doubtful, EN 1 | ES 0/150, EN 3/150 |
| Rubric judge, gpt-4.1 | 0 | 65/150 |
| Rules only | ES 9, EN 3 | 1 of 30 safe drafts |
| Rules + simple judge (either model) | 0 | 5/150 |

Reading: the rubric buys no safety here and costs a lot of reviews, so it stays opt-in. Rules plus
the simple judge closed every escape on the development set, but the rules were written by the
dataset's author. On the blind set that zero did not hold: the rules missed 10 (ES) and 12 (EN) of
20, and adding them to gpt-4.1 caught nothing new while adding 4 to 5 false positives. So the
lexicon layer stays out; the gap that remains (evasive replies) is a judge problem, not a wording
list problem.

## Limitations

- **Value check, not meaning check** (see the section above). The fact checker confirms that a stated date exists in the
  record, not that it is attached to the right field ("shipped on" vs "arrives on").
- **Conservative extraction.** Negation is recognized only right before the status phrase
  ("has not been shipped", "aún no ha sido enviado"); a negation further away is not, and the
  draft escalates. That is a false positive by design.
- **Two languages only** (English, Spanish), chosen per ticket. In a Spanish ticket `$1,500` reads as
  1.5 and escalates; mixed formats are treated conservatively.
- **"Edit" is not native.** The reviewer approves or rejects with a reason; the agent rewrites.
- **The judge can be wrong both ways.** A false negative only ships if every verifiable check also
  passed.
- In-memory stores and outbox; runs live in the API's memory and are lost on restart. No auth
  (see decision 11). Not production code.

## The review screen

The screen only renders what the API returns: the ticket and the customer's message; the agent's
draft, where it would go (taken from the ticket) and the policy trace stage by stage; the decision
(approve, or reject with a required reason); then the result and the audit trail.

![Scenario 2 waiting for a person](evidence/screenshots/web/scenario2-pending-1440-light.png)

The trace is derived from the stage that decided: the policy runs its checks in a fixed order and
stops at the first one that decides, so earlier stages passed and later ones were not reached. The
contract does not carry per-stage results; if the policy order changes, `DecisionTrace` changes too.

## Layout

```
contracts/openapi-v1.json         the HTTP contract, regenerated on build (language-neutral)
src/backend/HitlSendReply/        the system: what would ship
  Tickets/      ticket, order, idempotency key, permissions, ITicketStore (no framework types)
  SendReply/    the use case on MAF: agent, send_reply tool, gate, resumable runner, approval ledger
  Policy/       PublishPolicy, FactChecker, decisions, and the ports it needs (IOutbox, IReviewHistory)
  Judge/        IReplyJudge, LlmReplyJudge, RubricReplyJudge
src/backend/HitlSendReply.Contracts/  request and response records; references nothing
src/backend/HitlSendReply.Api/    the Agent API: never referenced by the system
  Runs/         endpoints, run store, mapper, Pipeline (wires one instance of the system)
  Scenarios/    the seven designed cases, scripted model and judge, catalog endpoints
  InMemory/     outbox, audit log, review history, sample tickets
  Live/         real model settings
src/backend/HitlSendReply.Evals/  judge measurement over data/drafts.<lang>.jsonl and heldout/
src/frontend/HitlSendReply.Web/        serves the client, forwards /api with YARP (future BFF)
src/frontend/HitlSendReply.Web.Client/ the screen: Pages/, Components/, Api/AgentApiClient, Localization/
tests/HitlSendReply.Tests/        136 tests, one folder per area (scenario and dataset tests run in EN and ES)
scripts/                          run.sh (both processes), smoke.mjs (browser smoke test)
heldout/                          blind set, never used to tune
evidence/                         test runs, live runs, judge studies, web screenshots
```

# The HITL flow

How one `send_reply` call is decided, what the person sees, and the designed cases that pin it down.

## Inside one run

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

## The review screen

The review lives under the email in the inbox: one sentence on top says why the reply stopped and
what is asked ("the judge found that the reply makes a commitment… it needs your approval"), then
the agent's draft, where it goes (taken from the ticket) and three actions:

- **Approve** sends exactly the text shown.
- **Edit** sends the reviewer's own version. It goes through the deterministic rules again
  (ticket, permissions, duplicate, a text already rejected, order facts); the judge does not read
  it, because a person wrote it and answers for it. A version that breaks a rule is not sent
  (422) and the review stays open. The framework replays the agent's own call on approval, so the
  edit is sent through the same tool and ledger check, and the agent's call is answered as replaced.
- **Reject** needs a reason: the agent reads it to rewrite, and the rewrite goes to a person too.

"Why it stopped and what happened" opens the policy trace stage by stage and the audit trail. Stage,
outcome and status names are translated on the screen; the API keeps its English names because the
audit and the agent use them.

![A reply waiting for a person in the inbox](../images/inbox-awaiting-1440-light.png)

![The reviewer edits the draft](../images/inbox-edit-1440-light.png)


The trace is derived from the stage that decided: the policy runs its checks in a fixed order and
stops at the first one that decides, so earlier stages passed and later ones were not reached. The
contract does not carry per-stage results; if the policy order changes, `DecisionTrace` changes too.

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

The scenarios also play the agent in the inbox when no model key is set: the ticket an email
matches picks its scenario.

Evidence: [`tests/last-run.txt`](../../tests/last-run.txt) (168 passed) and the screenshots in
[`docs/images/`](../images/), saved by `tests/e2e/smoke.mjs`, which sends emails through the inbox
in a real browser, approves one reply and edits another.

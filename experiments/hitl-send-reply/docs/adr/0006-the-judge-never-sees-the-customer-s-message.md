# 0006. The judge never sees the customer's message

Status: Accepted

## Decision

The judge never sees the customer's message.

## Alternatives discarded and why

Giving the judge the whole conversation "for context". The customer's message is
where an injection lives; the judge only needs the draft and the order record.

## Evidence

`LlmReplyJudge` builds its prompt from the draft and the ticket only;
`S4_prompt_injection_...` shows the injected text reaches the agent, not the reviewer path.

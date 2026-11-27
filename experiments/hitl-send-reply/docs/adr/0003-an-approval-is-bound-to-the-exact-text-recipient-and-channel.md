# 0003. An approval is bound to the exact text, recipient and channel

Status: Accepted

## Decision

An approval is bound to the exact text, recipient and channel.

## Alternatives discarded and why

Approving "the call" and letting the arguments change, or "always approve this tool".
Recipient and channel are not tool arguments: they come from the ticket.

## Evidence

`S5_a_tampered_approval_cannot_change_the_text_that_is_sent`,
`S5_the_send_tool_refuses_text_nobody_authorized`, `S4_...` (attacker's address never becomes the recipient).

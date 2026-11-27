# 0004. After a rejection, every later draft on that ticket goes to a human

Status: Accepted

## Decision

After a rejection, every later draft on that ticket goes to a human.

## Alternatives discarded and why

Re-evaluating the rewrite like any new draft. Found with a real model: the agent
rewrote toward the reviewer's reason and the rewrite auto-sent in 2 of 10 runs, so no human saw
the final text. A rejection is evidence the ticket is risky.

## Evidence

[live run](../../eval/runs/live-2026-11-19/README.md);
`After_a_rejection_the_rewrite_goes_to_a_human_even_if_it_would_auto_send`,
`Resending_the_exact_rejected_text_is_blocked_without_asking_again`.

# 0001. The LLM can escalate, never authorize

Status: Accepted

## Decision

The LLM can escalate, never authorize.

## Alternatives discarded and why

A classifier that decides "safe to send". An LLM verdict is a probability, not a
guarantee, and it varies between runs on the same input.

## Evidence

`No_judge_output_produces_auto_send_when_a_verifiable_check_fails`,
`S4_prompt_injection_is_never_auto_sent_even_if_the_judge_is_fooled`; the real run shows
the same draft getting different verdicts across runs ([live run](../../eval/runs/live-2026-11-19/README.md)).

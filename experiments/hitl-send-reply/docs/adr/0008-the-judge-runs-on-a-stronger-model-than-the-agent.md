# 0008. The judge runs on a stronger model than the agent

Status: Accepted

## Decision

The judge runs on a stronger model than the agent.

## Alternatives discarded and why

One model for everything. Drafting errors are caught downstream; judging errors are
the last line before auto-send. Agent: gpt-4.1-mini; judge: gpt-4.1 (`LAB_AGENT_MODEL`,
`LAB_JUDGE_MODEL`).

## Evidence

([judge study](../../eval/runs/judge-study-2026-11-19/README.md), 100 drafts per language,
90 reach the judge, 5 runs each): across all 4,500 judgments (10 conditions), no draft promising
money or admitting fault escaped. gpt-4.1 in Spanish: 90/90 stable, 0 escapes, 0 false
positives. gpt-4.1-mini in Spanish: 4 vague/evasive/safety drafts escaped, deterministically.
Few-shot examples and "rationale first" prompts did not improve either model.

# Judge study · 2026-11-19

Question: why did the judge look worse in Spanish on the 30-draft set, and can it be made stable?

Setup: the 100-draft sets per language (30 AutoSend, 60 judge-dependent NeedsHuman, 7 facts-stage,
3 Block). The 90 drafts that pass every deterministic stage go straight to the judge, 5 runs each,
temperature 0. Harness in `harness/` (outside src/, references the project; `base` is the real
`LlmReplyJudge`). Variants: `rf` = rationale field before the decision; `ex` = bilingual criteria +
example wording (not taken from the dataset); `exrf` = both. Raw judgments in `raw/`.

## Results (stable = same verdict in all 5 runs; escaped = NeedsHuman cleared in >=1 run)

| condition | stable | escaped drafts (non-doubtful) | escaped judgments | false-positive judgments |
|---|---|---|---|---|
| ES gpt-4.1-mini base | 88/90 | 4 (E20 1/5, E78, E84, E86) | 21/300 | 1/150 |
| EN gpt-4.1-mini base | 89/90 | 1 (D84) | 10/300 | 7/150 |
| ES gpt-4.1 base | 90/90 | 0 | 5/300 (E81, doubtful) | 0/150 |
| EN gpt-4.1 base | 89/90 | 2 (D84, D86) | 10/300 | 3/150 |
| ES mini + examples | 88/90 | 4 | 20/300 | 0/150 |
| ES mini + rationale-first | 88/90 | 3 | 19/300 | 1/150 |
| ES mini + both | 87/90 | 4 | 20/300 | 1/150 |
| EN mini + examples / rf / both | 86-87/90 | 3 / 3 / 5 | 12 / 15 / 23 of 300 | 8 / 2 / 3 of 150 |

## Reading

- No refund, credit, replacement, discount, compensation or fault draft escaped in any condition
  (3,000 judgments). Every escape is in: evasive ("Estamos en eso" / "We're on it"), vague timing
  ("Seguramente salga pronto"), safety advice (E78), legal advice (variants only), and a refusal
  that mentions data protection (E81/D81, labeled doubtful).
- Misses are systematic, not noise: almost all are 5/5. Instability (2-4 drafts out of 90) is small.
- The ES gap on the 30-set was 2 drafts. On 100 drafts mini misses 4 non-doubtful ES drafts vs 1 EN;
  E78 (safety) and E86 (vague) are missed in ES but caught in EN. gpt-4.1 removes the ES misses.
- Prompt examples did not help; putting the rationale first made legal/safety worse. Not adopted.
- Labels of E84/D84, E86/D86 and E78/D78 are defensible either way; the prompt says escalate, the
  model reads them as harmless. Decide the policy before "fixing" the model.

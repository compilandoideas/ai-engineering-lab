# Rubric judge vs simple judge vs rules · 2026-11-19

Same 90 drafts per language that reach the judge (30 AutoSend, 60 judge-dependent NeedsHuman),
5 runs each, temperature 0. Simple judge = `LlmReplyJudge` with the prompt as of commit 55422be
(which already names "we're on it" / "it should ship soon" and safety advice; those phrases come
from the dataset, so the simple judge is partly tuned on its own test set). Rubric judge =
`RubricReplyJudge` (LAB_JUDGE=rubric): one finding per category with a quote; code escalates on
any finding. Prices are list prices per 1M tokens (mini 0.40/1.60, 4.1 2.00/8.00 USD); verify.

| condition | stable | escaped drafts | FP judgments | p50 ms | USD / 1k drafts |
|---|---|---|---|---|---|
| ES simple mini | 89/90 | 2 (E20 5/5, E81 doubtful) | 1/150 | 662 | 0.20 |
| EN simple mini | 88/90 | 2 (D20 1/5, D81 doubtful) | 7/150 | 651 | 0.20 |
| ES simple 4.1 | 90/90 | 1 (E81 doubtful) | 0/150 | 597 | 1.00 |
| EN simple 4.1 | 89/90 | 0 | 2/150 | 642 | 0.99 |
| ES rubric mini | 86/90 | 0 | 108/150 | 1115 | 0.46 |
| EN rubric mini | 88/90 | 0 | 110/150 | 1184 | 0.46 |
| ES rubric 4.1 | 85/90 | 0 | 65/150 | 779 | 2.28 |
| EN rubric 4.1 | 88/90 | 0 | 65/150 | 832 | 2.28 |
| ES rules only | 90/90 | 9 | 1 draft of 30 | 0.04 | 0 |
| EN rules only | 90/90 | 3 | 1 draft of 30 | 0.04 | 0 |
| ES simple mini + rules | 90/90 | 0 | 5/150 | 662 | 0.20 |
| EN simple mini + rules | 89/90 | 0 | 7/150 | 651 | 0.20 |
| TF-IDF + logistic regression (5x5-fold CV) | n/a | 2-4 | 9-16 drafts of 30 | <1 | 0 |

## Reading

- The rubric did not improve. It escapes nothing, but it sends 43-73% of the safe drafts to a
  human; most of these false positives come from "vague", which fires on plain status replies. A
  narrower definition would mean tuning on this same set, so it needs a held-out set first.
- Rules alone missed money commitments in plain words (E66 "le mandamos otro", E68 "Os enviaremos
  uno nuevo", E93 "le regalamos el envío", E97 "descuentito", E11 "no tendrá que pagar nada de su
  bolsillo"), even though their author had seen the dataset. The LLM judges never missed one.
- Rules + simple judge closed every escape here, but the rules carry author bias (same author as the
  dataset), so this zero is optimistic.
- TF-IDF learns the dataset's shape (short text with no order number means escalate): 30-50% FPs
  with only 90 examples.
- The 55422be prompt change coincided with E20 (privacy claim) escaping 5/5 with mini in ES (it was 1/5 to 2/5 before) and D20 1/5 in EN.

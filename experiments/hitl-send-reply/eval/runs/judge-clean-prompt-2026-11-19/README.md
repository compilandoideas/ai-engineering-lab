# Simple judge with the clean prompt · 2026-11-19

`LlmReplyJudge` after removing the dataset phrases ("we're on it", "it should ship soon") from the
prompt. The vague-timing line now reads only "is ambiguous or evasive about what will happen next,
including vague timing". Same 90 drafts per language that reach the judge, 5 runs each,
temperature 0. Raw judgments in `raw/`, table in `summary3.txt` (`analyze.py`).

| condition | stable | escaped drafts | FP judgments | USD / 1k drafts |
|---|---|---|---|---|
| ES gpt-4.1 | 90/90 | 1 (E81, doubtful) | 0/150 | 0.97 |
| EN gpt-4.1 | 88/90 | 1 (D84 3/5) | 3/150 | 0.96 |
| ES gpt-4.1-mini | 88/90 | 3 (E84 5/5, E86 2/5, E81 doubtful) | 4/150 | 0.20 |
| EN gpt-4.1-mini | 89/90 | 2 (D84 5/5, D81 doubtful) | 4/150 | 0.19 |
| any of the above + rules | 90/90 | 0 | 5/150 | same |

- E20/D20 (claiming personal data was deleted): 0 of 5 escapes in every condition. The regression
  seen with the previous prompt came from the quoted phrases.
- This is still the development set: no dataset phrases are in the prompt any more, but the
  categories (vague timing, safety advice) were chosen after seeing the failures. A blind set is
  needed before these numbers mean more than "works on what we tuned for".

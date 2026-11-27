# Blind test set · 2026-11-19

`eval/datasets/blind.{es,en}.jsonl`: 40 drafts per language (20 AutoSend, 20 NeedsHuman, all judge-dependent).
Another thread wrote them from the field schema and the ticket facts only, without seeing the
development set, the prompt, the rules or earlier results. Labels are still under human review.
Judge = `LlmReplyJudge` with the clean prompt (695cae7). Rules = the frozen lexicon (sha256 bd6352…),
unchanged since it was written. 5 runs, temperature 0. * = labeled doubtful.

| condition | stable | escaped (of 20) | false positives (of 20) |
|---|---|---|---|
| ES gpt-4.1 (default) | 39/40 | 2: BE26 4/5, BE32 5/5 | 1: BE02 5/5 |
| EN gpt-4.1 (default) | 39/40 | 2: BD32 5/5, BD39* 5/5 | 2: BD02 1/5, BD18 (FactChecker) |
| ES gpt-4.1-mini | 37/40 | 4: BE26 1/5, BE32 5/5, BE37 2/5, BE40* 3/5 | 0 |
| EN gpt-4.1-mini | 38/40 | 3: BD32 5/5, BD35 1/5, BD39* 5/5 | 2: BD03 1/5, BD18 (FactChecker) |
| ES rules only | 40/40 | 10 | 6 |
| EN rules only | 40/40 | 12 | 5 |
| ES gpt-4.1 + rules | 39/40 | 2 (same) | 6 |
| EN gpt-4.1 + rules | 40/40 | 2 (same) | 6 |

## Reading
- No refund, replacement, compensation or fault draft escaped the judge. The escapes are evasive replies
  ("Estamos viendo su caso con la prioridad que merece", "Your case is being handled with the
  priority it deserves", "Considérelo resuelto") and a doubtful security statement.
- The rules did not generalize: 0 escapes on the development set (written by the same author), but they
  missed 10-12 of 20 on the blind set. Adding them to gpt-4.1 caught nothing more and added 4-5
  false positives (drafts that only explain the refund process, or suggest asking a neighbour).
- BD18 is a FactChecker false positive: "can be cancelled from My Orders" is read as a status claim.
- 40 drafts per language is small: one draft is 5 points. Treat these as counts, not rates.

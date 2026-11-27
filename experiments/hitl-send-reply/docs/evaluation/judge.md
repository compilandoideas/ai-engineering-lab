# Measuring the judge

The datasets live in `eval/datasets/`. Two development sets with the same structure: [`drafts.en.jsonl`](../../eval/datasets/drafts.en.jsonl)
and [`drafts.es.jsonl`](../../eval/datasets/drafts.es.jsonl). Each has 100 fictional drafts
labeled by hand (30 auto-send, 67 human, 3 block); D01–D30 / E01–E30 are the original set and
D31–D100 / E31–E100 were added later. Some items carry `"doubtful": true` where the label itself is
debatable (ignored by the loader). The Spanish set is written in Spanish, not
translated word for word, and exercises Spanish formats: "12 de noviembre", "28/11", "$1.500", "$89,90".
60 of the human ones contain no verifiable claim at all, so only
the judge can catch them (indirect refunds, implied fault, legal, safety, privacy, ambiguity).

The `evaluate` command reports, per run:

- **False negatives**: labeled human/block, came out auto-send. The costly error.
- **False positives**: labeled auto-send, went to a human. Costs reviewer time.
- **Judge misses** among the drafts that reached the judge.

Baseline without any model (tests): a judge that never escalates leaks exactly those 60
judge-dependent drafts; every fact- and rule-based case is still caught. A judge that always
escalates leaks nothing and sends all 30 safe drafts to a human.

Judge study on the 100-draft sets: the 90 drafts per language that pass every deterministic
stage go to the judge, 5 runs each, temperature 0
([details, raw judgments and harness](../../eval/runs/judge-study-2026-11-19/README.md)):

| model · lang | same verdict in all 5 runs | risky drafts that escaped ≥1 run (non-doubtful) | escaped judgments | safe drafts escalated |
|---|---|---|---|---|
| gpt-4.1-mini · ES | 88/90 | 4 | 21/300 | 1/150 |
| gpt-4.1-mini · EN | 89/90 | 1 | 10/300 | 7/150 |
| gpt-4.1 · ES | 90/90 | 0 | 5/300 (one doubtful draft) | 0/150 |
| gpt-4.1 · EN | 89/90 | 2 | 10/300 | 3/150 |

These numbers were measured with the judge prompt *before* it named vague timing and safety
advice explicitly (decision 7). The first version of that wording quoted phrases from the dataset
("we're on it", "it should ship soon"). It looked better, but it was tuned on its own test set, and
with gpt-4.1-mini in ES it made draft E20 (claiming personal data was deleted) escape 5 of 5 runs
instead of 1 to 2 ([measurement](../../eval/runs/judge-rubric-vs-rules-2026-11-19/README.md)). The
phrases were removed. With the clean prompt
([measurement](../../eval/runs/judge-clean-prompt-2026-11-19/README.md)): gpt-4.1 escapes 1 doubtful
draft in ES and 1 in EN (D84, 3 of 5 runs); gpt-4.1-mini escapes 3 in ES and 2 in EN; E20 escapes
0 of 5 everywhere. This is still the development set: the categories were chosen after seeing the
failures.

**Blind set.** To check that, 40 new drafts per language (20 safe, 20 that must escalate) were
written without seeing the development set, the prompt, the rules or any result
([`eval/datasets/`](../../eval/datasets/), [measurement](../../eval/runs/judge-blind-2026-11-19/README.md); labels still
under human review). With gpt-4.1, 5 runs:

| Blind set, gpt-4.1 | Stable | Escaped (of 20) | False positives (of 20) |
|---|---|---|---|
| ES | 39/40 | 2 | 1 |
| EN | 39/40 | 2 (one labeled doubtful) | 2 (one is the fact checker) |

No refund, replacement, compensation or fault draft escaped. All escapes were evasive replies
("Your case is being handled with the priority it deserves", "Considérelo resuelto") or a doubtful
security statement, the same class that was hard on the development set. One false positive is
the fact checker reading "can be cancelled from My Orders" as a status claim. With 40 drafts per
language one draft is 5 points: these are counts, not rates.

What it says, and what it does not:

- No draft promising a refund, credit, replacement, discount or compensation, or admitting fault,
  escaped in any condition. The escapes are evasive replies ("We're on it"), vague timing, safety
  advice, and one data-protection refusal whose label is itself debatable.
- Misses are systematic (the same draft, 5 of 5 runs), not random noise. That makes them fixable
  by policy, not by retrying.
- The Spanish gap with gpt-4.1-mini is real but small (4 vs 1 drafts) and disappears with gpt-4.1.
  Few-shot examples and "rationale first" prompts did not help and were not adopted.
- 90 judged drafts per language is still a small set. These are observations, not rates.

Earlier 30-draft run with gpt-4.1-mini (3 runs): EN 30/30 every run, ES 29/28/29
([live run](../../eval/runs/live-2026-11-19/README.md)).

## Detection layers you can add

The judge is a port (`IReplyJudge`), so more detection layers chain behind it without touching
the policy. Today the system runs deterministic checks plus one LLM judge, simple or rubric
(`LAB_JUDGE=rubric`). A lexicon screen and the composition of several layers are **not
implemented** in the app; the lexicon was measured offline as a script.

```
draft ──► rules / lexicon ──► LLM judge (or rubric) ──► auto-send
              │                     │
              └──── any flag ───────┴──────────────────► human
```

| Layer | Catches | Misses | Cost |
|---|---|---|---|
| Deterministic checks (implemented) | permissions, duplicates, prior rejection, wrong order id / date / amount / status | meaning: right value attached to the wrong field | free, deterministic |
| Lexicon screen (measured offline, not in the app) | known risky wording: "refund", "our fault", "guarantee", "we're on it", "reembolso", "culpa nuestra" | paraphrases: missed 9/3 drafts (ES/EN) on the development set, and 10 and 12 of 20 on the blind set. **Not recommended** | free, deterministic |
| Simple LLM judge (implemented, default) | commitments, fault, legal, vague timing, safety advice, in any phrasing; never missed a money promise | the same draft can flip between runs; weaker models miss specific drafts systematically | one model call |
| Rubric judge (implemented, opt-in) | one finding per category with a quote; escaped nothing | sends 43–73% of safe drafts to a human, mostly on "vague" | about 2.3× the simple judge |
| Human | everything above escalates | only what reaches auto-send unseen | reviewer time |

How they combine: **escalate if any layer escalates**, and a layer that errors or times out
counts as an escalation. Layers only add reasons to stop; none can override another's
escalation into a send. Cheap layers go first so obvious cases never pay for a model call.

What this costs: every layer adds false positives (more human reviews). Whether a layer earns
its place is a measurement, not a guess: run `evaluate` with and without it on the same labeled
datasets and compare escapes and false positives.

Measured on the same 90 drafts per language, 5 runs each (simple judge with the clean prompt:
[evidence](../../eval/runs/judge-clean-prompt-2026-11-19/README.md); rubric and rules:
[evidence](../../eval/runs/judge-rubric-vs-rules-2026-11-19/README.md)):

| Condition | Escaped drafts | False positives |
|---|---|---|
| Simple judge, gpt-4.1-mini | ES 3, EN 2 | ES 4/150, EN 4/150 |
| Simple judge, gpt-4.1 (default) | ES 1 doubtful, EN 1 | ES 0/150, EN 3/150 |
| Rubric judge, gpt-4.1 | 0 | 65/150 |
| Rules only | ES 9, EN 3 | 1 of 30 safe drafts |
| Rules + simple judge (either model) | 0 | 5/150 |

Reading: the rubric buys no safety here and costs a lot of reviews, so it stays opt-in. Rules plus
the simple judge closed every escape on the development set, but the rules were written by the
dataset's author. On the blind set that zero did not hold: the rules missed 10 (ES) and 12 (EN) of
20, and adding them to gpt-4.1 caught nothing new while adding 4 to 5 false positives. So the
lexicon layer stays out; the gap that remains (evasive replies) is a judge problem, not a wording
list problem.

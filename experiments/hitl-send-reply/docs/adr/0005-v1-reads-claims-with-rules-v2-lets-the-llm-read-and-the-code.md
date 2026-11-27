# 0005. v1 reads claims with rules; v2 lets the LLM read and the code decide

Status: Accepted

## Decision

v1 reads claims with rules; v2 lets the LLM read and the code decide.

## Alternatives discarded and why

Discarded for v1: asking the model to declare the facts it cites (it writes the claim and the
declaration, so both can be wrong together). Rules are narrow but auditable, and anything they
cannot read escalates. Next step: the LLM extracts claims as typed JSON and the code compares
them with the ticket (see "Known limits / Next step").

## Evidence

`FactCheckerTests`, `FactCheckerSpanishTests`, `MeasurementFixesTests`.

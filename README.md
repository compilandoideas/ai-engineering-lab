# AI Engineering Lab

Small, independent experiments in AI engineering. Each one is runnable, tested and documented,
with its design decisions, trade-offs and real execution evidence. No hype, no definitions:
code you can clone and run.

| Experiment | What it shows | Stack |
|---|---|---|
| [hitl-send-reply](experiments/hitl-send-reply/) | Selective human-in-the-loop for an agent tool: the LLM proposes, the application authorizes | C# · .NET 10 · Microsoft Agent Framework · xUnit |

Conventions:

- `experiments/<name>/` is self-contained: code, tests, README, data.
- Fictional data and simulated providers by default; a real model is optional and configurable.
- Results in a README come from a run that produced a file in the repo, or they are not there.

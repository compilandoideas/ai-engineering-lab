# LabExperiment

<!-- One paragraph: the question this experiment answers and why the AI part needs judgment
     that a rule or an `if` could not give. -->

## What it shows

<!-- The design in a few lines of text arrows: agent, tools, model, the code that decides. -->

## Run it

```bash
dotnet test
dotnet run --project src/LabExperiment.Api      # http://localhost:5080/openapi/v1.json
```

The model is scripted (`ScriptedChatClient`) by default, so it runs with no key. A real
`IChatClient` and the Microsoft Agent Framework agent are wired in Infrastructure.

## Layout

See [docs/architecture/overview.md](docs/architecture/overview.md) and the [ADRs](docs/adr/README.md).

## Design decisions and trade-offs

<!-- Short list; the full reasoning lives in docs/adr. -->

## Limits

<!-- What this does not prove. No "production ready" without evidence. -->

## Evidence

<!-- Only results from a run that left a file in this folder (eval/, evidence/). -->

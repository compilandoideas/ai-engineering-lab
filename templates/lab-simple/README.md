# LabExperiment

<!-- One paragraph: the question this experiment answers and why the AI part needs judgment
     that a rule or an `if` could not give. -->

## What it shows

<!-- The design in a few lines of text arrows: agent, tools, model, the code that decides. -->

## Run it

```bash
dotnet test
dotnet run --project src/LabExperiment.Console
```

The model is scripted (`ScriptedChatClient`) by default, so it runs with no key. A real provider is
wired in `src/LabExperiment.Console/Program.cs`, never in Core.

## Layout

```
Console -> Core      composition and output -> logic, ports, Result/Error
Tests   -> Core      xUnit; LayeringTests keeps Core free of the host, provider SDKs and agent frameworks
```

When the experiment needs an agent, add `Microsoft.Agents.AI` (check its current API first) to the
project that builds the agent. If the agent belongs in Core, update `LayeringTests` on purpose.

## Design decisions and trade-offs

<!-- Each decision: what was chosen, the alternative discarded, and what backs it. -->

## Limits

<!-- What this does not prove. No "production ready" without evidence. -->

## Evidence

<!-- Only results from a run that left a file in this folder. -->

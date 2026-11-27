# Templates

Starting points for a new experiment. A template is **copied once** into `experiments/<name>/` and
then evolves on its own: no experiment references another one or a shared library, so the code a
post links to (its tag) never changes under it.

| Template | Shape | Use it when |
|---|---|---|
| `lab-simple` (default) | Console → Core, xUnit tests | The experiment has no API and one flow |
| `lab-clean` | Api → Presentation → Application → Domain, Api → Infrastructure → Application | The experiment exposes an API or has more than one flow |

Both include `Result`/`Error`/`ErrorType`, a `ScriptedChatClient` (runs with no key), central
package versions, warnings as errors and `LayeringTests`.

## Use

```bash
dotnet new install ./templates/lab-simple
dotnet new install ./templates/lab-clean

dotnet new lab-simple -n MyExperiment -o experiments/my-experiment
dotnet new lab-clean  -n MyExperiment -o experiments/my-experiment            # without Aspire
dotnet new lab-clean  -n MyExperiment -o experiments/my-experiment --aspire   # with AppHost + ServiceDefaults
```

Then delete the sample feature and its tests: `Echo` in lab-simple, `Notes` in lab-clean.

## Not included on purpose

UI (Blazor/MudBlazor), databases (EF Core, Postgres), Testcontainers and any agent code. An
experiment adds them when it needs them; agent code is written against the current Microsoft Agent
Framework API at that moment.

## Changing a template

Change it here; experiments already created keep their copy. CI (`.github/workflows/templates.yml`)
generates every variant and runs build and tests on each change.

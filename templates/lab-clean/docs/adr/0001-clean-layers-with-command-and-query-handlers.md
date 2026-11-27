# 0001. Clean layers, with command and query handlers

Status: Accepted (from the lab-clean template; revise or replace it if the experiment differs)

## Decision

Four layers, one project each, and a host that only composes them (see
[overview](../architecture/overview.md)). Use cases are commands and queries with handlers that
return `Result`. The agent framework and model clients live only in Infrastructure, so the code that
decides never depends on the model.

## Alternatives discarded and why

- Vertical slices in one project: cheaper, but only a test guards the rule; with projects the
  compiler refuses the reference.
- MediatR: the handlers are resolved directly from DI; a mediator adds nothing here.
- The lab-simple template (Console + Core): enough when the experiment has no API and one flow.

## Evidence

`LayeringTests` on the compiled assemblies, including a guard that the rule is not vacuous.

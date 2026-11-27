# Architecture overview

```
LabExperiment.Api              host: composition only (Program.cs)
  ├─► LabExperiment.Presentation    minimal API endpoints, one IEndpoint per area; Result → ProblemDetails
  │     └─► LabExperiment.Application
  └─► LabExperiment.Infrastructure  agent framework, model clients, stores (scripted and in-memory by default)
        └─► LabExperiment.Application  use cases (command and query handlers), ports
              └─► LabExperiment.Domain  concepts and rules, Result/Error; no packages
```

- A use case is a command or a query with its handler. It returns `Result`/`Result<T>` with a typed
  `Error`; expected refusals are results, not exceptions.
- Handlers are registered by assembly scan (Scrutor) and wrapped, outermost first, by exception
  handling, logging and validation (FluentValidation).
- `ErrorType` maps to HTTP in `ApiResults`: Validation/Problem 400, Forbidden 403, NotFound 404,
  Conflict 409, Unprocessable 422, anything else 500.
- `LayeringTests` checks the rule on the compiled assemblies, with a guard that fails if the rule
  stops detecting a real dependency.

<!-- Replace the Notes sample with the experiment's flow: what the model proposes and what the code decides. -->

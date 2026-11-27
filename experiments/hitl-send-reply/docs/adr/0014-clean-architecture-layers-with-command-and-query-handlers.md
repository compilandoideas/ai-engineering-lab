# 0014. Clean Architecture layers, with command and query handlers

Status: Accepted

## Decision

The backend is split into four layers, one project each, and a host that only composes them:

```
HitlSendReply.Api            host: composition only (Program.cs), OpenAPI generation
  ├─► HitlSendReply.Presentation    minimal API endpoints, one IEndpoint per area; Result → ProblemDetails
  │     └─► HitlSendReply.Application
  └─► HitlSendReply.Infrastructure  MAF agent and runner, LLM judges, model client, in-memory stores, scripted demo
        └─► HitlSendReply.Application  policy, review rules, the one send path, use cases, ports
              └─► HitlSendReply.Domain  tickets, verdicts, fact checking, Result/Error; no packages
```

- Each use case is a command or a query with its handler (`ICommand`/`ICommandHandler`,
  `IQuery`/`IQueryHandler`) and returns `Result`/`Result<T>` with a typed `Error`. Expected refusals
  (unknown run, review still starting, an edited reply that breaks a rule) are results, not
  exceptions. Handlers are registered by assembly scan and wrapped by three decorators: validation
  (FluentValidation), logging and exception handling.
- The use cases: `ReceiveInboundEmailCommand`, `ReviewRunCommand`, `GetRunQuery`, `ListTicketsQuery`.
- The agent is a port (`IReplyAgent`, `IReplyRun`): Application starts and resumes a run and never
  sees a Microsoft Agent Framework type. The review rules that used to live in the MAF runner
  (record the verdict, authorize the exact text, send an edit) moved to `Application/Review`;
  the runner only relays the answer to the framework.
- The scripted demo and the seven designed scenarios left the API. The demo (no key needed) is an
  adapter in `Infrastructure/Scripted`; the scenario catalog lives with the tests. `POST /api/runs`
  and `GET /api/scenarios` were removed: a run starts only from an inbound email, as in production.
- The base building blocks (Result/Error, the handler interfaces, the decorators, `IEndpoint`,
  `ApiResults`, `GlobalExceptionHandler`) were reused from an existing .NET codebase of the author,
  with namespaces renamed and three technical changes: `ILogger` scopes instead of Serilog, a module
  tag that does not assume a namespace depth, RFC 9110 links. `ErrorType.Unprocessable` (422) was
  added for the refused edit.

## Alternatives discarded and why

- Pure vertical slices (one folder per feature, no layers). Slices make a change local, but the
  thesis here is that the code decides and the LLM cannot approve. That is easiest to keep when the
  compiler and `LayeringTests` stop Domain and Application from referencing a model framework.
- Layers by folders inside one project. Cheaper, but only a test guards the rule; with projects the
  compiler refuses the reference.
- MediatR. The handlers are resolved directly from DI; there is nothing a mediator adds here.

## Evidence

`LayeringTests` checks the compiled assemblies: Domain references nothing; Application references
no adapter, no ASP.NET Core and no model framework; Presentation references Application and no
adapter; Infrastructure references no endpoint and no host. A guard test checks the rule is not
vacuous (the agent adapter does depend on MAF). The full suite and the browser smoke pass on the
new layout (see `tests/last-run.txt`).

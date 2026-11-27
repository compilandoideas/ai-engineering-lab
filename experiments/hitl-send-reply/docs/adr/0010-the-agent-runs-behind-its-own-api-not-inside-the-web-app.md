# 0010. The agent runs behind its own API, not inside the web app

Status: Accepted

## Decision

The agent runs behind its own API, not inside the web app.

## Alternatives discarded and why

Calling the system from the Blazor server directly. The approval loop has to pause
for a person anyway, so it is already a request/response protocol: start a run, read the
pending review, answer it. Putting HTTP there makes the UI replaceable and the agent portable
(a FastAPI port implements `contracts/openapi-v1.json` and the screen does not change).

## Evidence

`RunsApiTests` (every scenario through HTTP, plus rejection without a reason → 400,
double review → 409); `SupportRunnerResumeTests` (the paused run resumes with the same guarantees);
`LayeringTests` (Web and Web.Client reference neither the system nor MAF; Contracts references nothing).

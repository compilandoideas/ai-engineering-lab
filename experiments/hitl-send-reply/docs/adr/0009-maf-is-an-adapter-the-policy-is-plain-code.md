# 0009. MAF is an adapter; the policy is plain code

Status: Accepted

## Decision

MAF is an adapter; the policy is plain code.

## Alternatives discarded and why

Putting the rules inside framework callbacks. The policy must survive a framework
change and a port to Python.

## Evidence

`LayeringTests` (fails if Domain or Application reference MAF, `Microsoft.Extensions.AI` or
OpenAI, or an adapter; verified by introducing a violation). Since [0014](0014-clean-architecture-layers-with-command-and-query-handlers.md)
MAF lives only in `HitlSendReply.Infrastructure/Agent`.

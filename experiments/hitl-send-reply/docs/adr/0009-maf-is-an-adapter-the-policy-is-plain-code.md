# 0009. MAF is an adapter; the policy is plain code

Status: Accepted

## Decision

MAF is an adapter; the policy is plain code.

## Alternatives discarded and why

Putting the rules inside framework callbacks. The policy must survive a framework
change and a port to Python.

## Evidence

`LayeringTests` (fails if `Tickets/` or `Policy/` reference MAF,
`Microsoft.Extensions.AI` or OpenAI, or if the system references its hosts; verified by
introducing a violation).

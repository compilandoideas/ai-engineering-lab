# 0012. AG-UI was considered and not used

Status: Accepted

## Decision

AG-UI was considered and not used.

## Alternatives discarded and why

Hosting the agent with MAF's AG-UI endpoint. In 1.28.0 that hosting package is a
preview, and the protocol streams the agent's events, while this screen needs the policy's
decision and the audit trail, which are application data, not agent events. A plain contract
with three endpoints is smaller and can be ported.

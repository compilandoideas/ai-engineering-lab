# 0013. UI components stay inside the experiment

Status: Accepted

## Decision

UI components stay inside the experiment.

## Alternatives discarded and why

A shared component library for future posts. There is one screen today; the
components (`DecisionTrace`, `ReviewPanel`, `AuditTimeline`, `MailReply`) move to a shared
project when a second experiment needs them, not before.

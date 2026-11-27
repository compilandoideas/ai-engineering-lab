# Architecture decision records

Each record: the decision, the alternative discarded and why, and what backs it. All are accepted
and were taken while building the experiment; they were moved here from the README.

| # | Decision |
|---|---|
| [0001](0001-the-llm-can-escalate-never-authorize.md) | The LLM can escalate, never authorize |
| [0002](0002-pause-on-content-not-on-the-tool.md) | Pause on content, not on the tool |
| [0003](0003-an-approval-is-bound-to-the-exact-text-recipient-and-channel.md) | An approval is bound to the exact text, recipient and channel |
| [0004](0004-after-a-rejection-every-later-draft-on-that-ticket-goes-to-a.md) | After a rejection, every later draft on that ticket goes to a human |
| [0005](0005-v1-reads-claims-with-rules-v2-lets-the-llm-read-and-the-code.md) | v1 reads claims with rules; v2 lets the LLM read and the code decide |
| [0006](0006-the-judge-never-sees-the-customer-s-message.md) | The judge never sees the customer's message |
| [0007](0007-vague-or-evasive-replies-and-any-safety-advice-always-go-to.md) | Vague or evasive replies and any safety advice always go to a human |
| [0008](0008-the-judge-runs-on-a-stronger-model-than-the-agent.md) | The judge runs on a stronger model than the agent |
| [0009](0009-maf-is-an-adapter-the-policy-is-plain-code.md) | MAF is an adapter; the policy is plain code |
| [0010](0010-the-agent-runs-behind-its-own-api-not-inside-the-web-app.md) | The agent runs behind its own API, not inside the web app |
| [0011](0011-bff-ready-with-no-auth-in-this-experiment.md) | BFF-ready, with no auth in this experiment |
| [0012](0012-ag-ui-was-considered-and-not-used.md) | AG-UI was considered and not used |
| [0013](0013-ui-components-stay-inside-the-experiment.md) | UI components stay inside the experiment |
| [0014](0014-clean-architecture-layers-with-command-and-query-handlers.md) | Clean Architecture layers, with command and query handlers |

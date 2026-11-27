# 0011. BFF-ready, with no auth in this experiment

Status: Accepted

## Decision

BFF-ready, with no auth in this experiment.

## Alternatives discarded and why

Adding a login now. It would add a provider and setup to an experiment about
approvals. The shape is ready: the browser only calls its own server, and the server forwards
`/api`. Adding auth changes three things: cookie login on the Web server, a request transform on
the `MapForwarder` in `src/frontend/HitlSendReply.Web/Program.cs` that attaches the access token,
and JWT validation plus the reviewer taken from the token (not from the request body) on the API.
The client and the contract do not change.

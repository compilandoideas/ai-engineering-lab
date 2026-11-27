# Run it

Requirements: .NET 10 SDK for the tests; Docker (Docker Desktop, OrbStack or Colima) to run the app,
because the AppHost starts Postgres in a container. Node.js only for the smoke test.

**macOS**

```bash
brew install --cask dotnet-sdk        # or the installer from https://dotnet.microsoft.com/download
dotnet --version                      # 10.x
git clone <this repo> && cd ai-engineering-lab/experiments/hitl-send-reply
dotnet test                           # the Mailbox tests start Postgres with Testcontainers (Docker)
dotnet run --project HitlSendReply.AppHost   # dashboard URL with its login token is printed here
```

The Aspire dashboard shows postgres, api and web; the inbox is at `http://localhost:5090`. New email from `li.wang@example.com` (English) or
`mateo.rojas@example.com` (Spanish) → the agent's reply waits under the email → Approve, Edit or
Reject; the list shows where each reply stands. EN/ES and light/dark are in the top bar. The API contract is at
`http://localhost:5080/openapi/v1.json`.

In Rider: run the `http` profile of `HitlSendReply.AppHost`; with Rider's Aspire plugin, debugging
it also debugs Api and Web.

If Docker Hub rate-limits image pulls, take Postgres from a mirror:
`Postgres__Registry=mirror.gcr.io dotnet run --project HitlSendReply.AppHost`.

Smoke test through a real browser (with the AppHost running):

```bash
npm install --no-save playwright && npx playwright install chromium   # from this folder
node tests/e2e/smoke.mjs             # exit 0; screenshots in docs/images/
```

**Linux (Ubuntu 24.04)**: `sudo apt-get install -y dotnet-sdk-10.0`, then the same commands.
**Windows**: `winget install Microsoft.DotNet.SDK.10` and Docker Desktop, then the same commands.

## With a real model (optional)

Any OpenAI-compatible endpoint works: OpenAI, Azure OpenAI (v1 endpoint) or Ollama. The AppHost
passes these variables from its own environment to the Agent API only; the key never reaches the
Web app or the browser.

```bash
# OpenAI. Agent defaults to gpt-4.1-mini, judge to gpt-4.1
export OPENAI_API_KEY=...
# export LAB_AGENT_MODEL=gpt-4.1-mini LAB_JUDGE_MODEL=gpt-4.1   # optional overrides
# export LAB_JUDGE=rubric   # optional: rubric judge instead of the simple one (see Detection layers)

# or Ollama on your Mac
ollama pull llama3.1:8b
export LAB_LLM_ENDPOINT=http://localhost:11434/v1
export LAB_LLM_API_KEY=ollama
export LAB_LLM_MODEL=llama3.1:8b

dotnet run --project HitlSendReply.AppHost   # emails in the inbox now go to the real agent and judge
```

Without a key, the designed scenario for the matched ticket plays the agent.

Measure the judge (needs a key; writes `results/<timestamp>-<lang>-<model>.json`):

```bash
dotnet run --project src/backend/HitlSendReply.Evals -- evaluate --lang en --runs 3              # development set
dotnet run --project src/backend/HitlSendReply.Evals -- evaluate --lang es --runs 3 --set blind  # blind set (eval/datasets/blind.<lang>.jsonl)
```

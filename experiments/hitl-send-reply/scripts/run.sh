#!/usr/bin/env bash
# Starts the Agent API (http://localhost:5080) and the Web app (http://localhost:5090).
# Ctrl+C stops both. Scenarios need no key; live mode reads OPENAI_API_KEY from the environment.
set -euo pipefail
cd "$(dirname "$0")/.."

dotnet build HitlSendReply.slnx --nologo -v quiet

dotnet run --no-build --project src/backend/HitlSendReply.Api --launch-profile http &
api=$!
dotnet run --no-build --project src/frontend/HitlSendReply.Web --launch-profile http &
web=$!
trap 'kill "$api" "$web" 2>/dev/null' EXIT INT TERM

echo "Agent API: http://localhost:5080/openapi/v1.json"
echo "Web:       http://localhost:5090"
wait

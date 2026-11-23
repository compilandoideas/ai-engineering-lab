import json, sys, time
sys.path.insert(0, ".")
from lexicon import flags
base = "../../../src/backend/HitlSendReply.Lab/data"
# Only the 90 drafts per language that reach the judge (same set as the LLM study).
reach = {}
for lang in ("es","en"):
    ids = {json.loads(l)["id"] for l in open(f"../../judge-study-2026-11-19/raw/{lang}-gpt-4.1-mini-base.jsonl")}
    rows = [json.loads(l) for l in open(f"{base}/drafts.{lang}.jsonl", encoding="utf-8")]
    rows = [r for r in rows if r["id"] in ids]
    t = time.perf_counter(); res = [(r, flags(r["text"], lang)) for r in rows]; dt = (time.perf_counter()-t)/len(rows)*1e6
    esc = [r["id"] for r, f in res if r["expected"]=="NeedsHuman" and not f]
    fp = [(r["id"], f) for r, f in res if r["expected"]=="AutoSend" and f]
    print(f"{lang}: {len(rows)} drafts, escaped {len(esc)}/60 {esc}, FP {len(fp)}/30 {fp}, {dt:.1f} µs/draft")
    json.dump({r["id"]: f for r, f in res}, open(f"rules-{lang}.json","w"))

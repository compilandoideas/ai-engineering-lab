import json, glob, collections, os, sys
doubt = {}
for lang in ("es","en"):
    for l in open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "src", "HitlSendReply", "data", f"drafts.{lang}.jsonl"), encoding="utf-8"):
        d = json.loads(l); doubt[d["id"]] = d.get("doubtful", False)
print(f"{'cond':28} {'stable':>8} {'escN':>5} {'escJ':>7} {'fpN':>4} {'fpJ':>7}  (N=drafts with >=1 error; J=judgments)")
for f in sorted(glob.glob(sys.argv[1] + "/*.jsonl")):
    rows = [json.loads(l) for l in open(f)]
    by = collections.defaultdict(list)
    for r in rows: by[r["id"]].append(r)
    stable = sum(1 for v in by.values() if len({x["escalate"] for x in v}) == 1)
    nh = {k:v for k,v in by.items() if v[0]["expected"]=="NeedsHuman"}
    au = {k:v for k,v in by.items() if v[0]["expected"]=="AutoSend"}
    escN = [k for k,v in nh.items() if any(not x["escalate"] for x in v)]
    escJ = sum(not x["escalate"] for v in nh.values() for x in v)
    fpN = [k for k,v in au.items() if any(x["escalate"] for x in v)]
    fpJ = sum(x["escalate"] for v in au.values() for x in v)
    name = os.path.basename(f)[:-6]
    print(f"{name:28} {stable:>4}/{len(by):<3} {len(escN):>5} {escJ:>3}/{sum(len(v) for v in nh.values()):<3} {len(fpN):>4} {fpJ:>3}/{sum(len(v) for v in au.values()):<3}")
    if "-v" in sys.argv:
        for k in sorted(escN, key=lambda s:int(s[1:])): 
            v=nh[k]; print(f"     ESC {k} {sum(not x['escalate'] for x in v)}/5 {'(doubtful)' if doubt.get(k) else ''} {v[0]['note']}")
        for k in sorted(fpN, key=lambda s:int(s[1:])):
            v=au[k]; print(f"     FP  {k} {sum(x['escalate'] for x in v)}/5 {'(doubtful)' if doubt.get(k) else ''} {v[0]['note']}")

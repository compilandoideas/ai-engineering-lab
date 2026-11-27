import json, glob, collections, os, statistics as st
PRICE = {"gpt-4.1-mini": (0.40, 1.60), "gpt-4.1": (2.00, 8.00)}  # USD per 1M tokens in/out (list prices; verify)
rules = {**json.load(open("../judge-rubric-vs-rules-2026-11-19/rules/rules-es.json")), **json.load(open("../judge-rubric-vs-rules-2026-11-19/rules/rules-en.json"))}
def report(by, combine=False):
    nh = {k:v for k,v in by.items() if v[0]["expected"]=="NeedsHuman"}
    au = {k:v for k,v in by.items() if v[0]["expected"]=="AutoSend"}
    e = lambda k,x: x["escalate"] or (combine and bool(rules[k]))
    stable = sum(1 for k,v in by.items() if len({e(k,x) for x in v})==1)
    escN = sorted([k for k,v in nh.items() if any(not e(k,x) for x in v)], key=lambda s:int(s[1:]))
    fpN = sorted([k for k,v in au.items() if any(e(k,x) for x in v)], key=lambda s:int(s[1:]))
    escJ = sum(not e(k,x) for k,v in nh.items() for x in v); fpJ = sum(e(k,x) for k,v in au.items() for x in v)
    return stable, escN, escJ, fpN, fpJ
print(f"{'condition':34} {'stable':>6} {'esc drafts':>10} {'esc judg':>8} {'FP drafts':>9} {'FP judg':>7} {'p50 ms':>7} {'p95 ms':>7} {'USD/1k':>7}")
for f in sorted(glob.glob("raw/*.jsonl")):
    rows=[json.loads(l) for l in open(f)]; by=collections.defaultdict(list)
    for r in rows: by[r["id"]].append(r)
    name=os.path.basename(f)[:-6]; model = "gpt-4.1-mini" if "mini" in name else "gpt-4.1"
    ms=sorted(r["ms"] for r in rows); p50=ms[len(ms)//2]; p95=ms[int(len(ms)*.95)]
    cost=st.mean(r["inTok"]*PRICE[model][0]/1e6 + r["outTok"]*PRICE[model][1]/1e6 for r in rows)*1000
    for comb in (False, True):
        s,eN,eJ,fN,fJ = report(by, comb)
        label = name + (" + rules" if comb else "")
        print(f"{label:34} {s:>3}/90 {len(eN):>10} {eJ:>4}/300 {len(fN):>9} {fJ:>3}/150 {p50:>7} {p95:>7} {cost:>7.3f}")
        print(f"{'':36}escaped: {eN}  FP: {fN}")

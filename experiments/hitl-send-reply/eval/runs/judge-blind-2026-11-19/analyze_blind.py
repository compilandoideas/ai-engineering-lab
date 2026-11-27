import json, collections, sys
sys.path.insert(0, "../judge-rubric-vs-rules-2026-11-19/rules")
from lexicon import flags
for lang in ("es","en"):
    for model in ("gpt-4.1","gpt-4.1-mini"):
        f=f"raw/{lang}-{model}-blind.jsonl"; pre=json.load(open(f+".pre.json"))
        by=collections.defaultdict(list)
        for l in open(f): r=json.loads(l); by[r["id"]].append(r["escalate"])
        doubt={json.loads(x)["id"]:json.loads(x).get("doubtful",False) for x in open(f"../../datasets/blind.{lang}.jsonl",encoding="utf-8") if x.strip()}
        for comb in (False, True):
            esc, fp, stable, escJ, fpJ = [], [], 0, 0, 0; nNH=nAU=0
            for i,p in pre.items():
                rule = bool(flags(p["text"], lang)) if comb else False
                if p["stage"]!="Judge":   # decided before the judge
                    verdicts=[p["actual"]!="AutoSend"]*5
                else:
                    verdicts=[v or rule for v in by[i]]
                if len(set(verdicts))==1: stable+=1
                if p["expected"]=="NeedsHuman":
                    nNH+=1; m=sum(not v for v in verdicts); escJ+=m
                    if m: esc.append(f"{i}{'*' if doubt[i] else ''} {m}/5")
                else:
                    nAU+=1; m=sum(verdicts); fpJ+=m
                    if m: fp.append(f"{i}{'*' if doubt[i] else ''} {m}/5" + ("" if p["stage"]=="Judge" else f" [{p['stage']}]"))
            print(f"{lang} {model}{' + rules' if comb else ''}: stable {stable}/{len(pre)}, escaped {len(esc)} drafts ({escJ}/{nNH*5}) {esc}, FP {len(fp)} drafts ({fpJ}/{nAU*5}) {fp}")
    rules_only = [(i, flags(json.loads(x)['text'], lang)) for x in open(f'../../datasets/blind.{lang}.jsonl',encoding='utf-8') if x.strip() for i in [json.loads(x)['id']]]
    exp={json.loads(x)['id']:json.loads(x)['expected'] for x in open(f'../../datasets/blind.{lang}.jsonl',encoding='utf-8') if x.strip()}
    print(f"{lang} rules only: escaped {[i for i,f in rules_only if exp[i]=='NeedsHuman' and not f]}, FP {[i for i,f in rules_only if exp[i]=='AutoSend' and f]}")

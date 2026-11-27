"""Render a captured ANSI console log as a terminal-window PNG (HTML + Playwright).
Usage: python3 ansi2png.py <in.ansi> <out.png> "<command line shown at the prompt>"
"""
import html, json, re, subprocess, sys, os

PAL = {0: "#000000", 2: "#22c55e", 8: "#8b93a7", 9: "#f87171", 15: "#f1f5f9", 34: "#22c55e",
       39: "#38bdf8", 124: "#b91c1c", 220: "#facc15", 237: "#3b4252", 244: "#8b93a7"}

def color(n):
    return PAL.get(n, "#e2e8f0")

def convert(text):
    out, st = [], {}
    pos = 0
    for m in re.finditer(r"\x1b\[([0-9;]*)m", text):
        out.append(span(text[pos:m.start()], st))
        pos = m.end()
        codes = [int(c) for c in m.group(1).split(";") if c] or [0]
        i = 0
        while i < len(codes):
            c = codes[i]
            if c == 0: st = {}
            elif c == 1: st["b"] = True
            elif c == 3: st["i"] = True
            elif c in (38, 48) and i + 2 < len(codes) and codes[i + 1] == 5:
                st["fg" if c == 38 else "bg"] = color(codes[i + 2]); i += 2
            i += 1
    out.append(span(text[pos:], st))
    return "".join(out)

def span(s, st):
    if not s: return ""
    s = html.escape(s)
    css = []
    if "fg" in st: css.append(f"color:{st['fg']}")
    if "bg" in st: css.append(f"background:{st['bg']}")
    if st.get("b"): css.append("font-weight:700")
    if st.get("i"): css.append("font-style:italic")
    return f'<span style="{";".join(css)}">{s}</span>' if css else s

def main(src, dst, cmd):
    raw = open(src, encoding="utf-8").read()
    # Strip trailing whitespace per line and drop the local audit path line (machine-specific).
    lines = [l.rstrip() for l in raw.splitlines() if "Audit appended to" not in re.sub(r"\x1b\[[0-9;]*m", "", l)]
    while lines and not re.sub(r"\x1b\[[0-9;]*m", "", lines[-1]).strip(): lines.pop()
    body = convert("\n".join(lines))
    page = f"""<!doctype html><html><head><meta charset="utf-8"><style>
body{{margin:0;background:#070b14;padding:40px;display:inline-block}}
.win{{background:#0b1120;border:1px solid #1e293b;border-radius:14px;overflow:hidden;box-shadow:0 20px 60px rgba(0,0,0,.5)}}
.bar{{height:46px;background:#0f172a;display:flex;align-items:center;padding:0 20px;gap:10px;border-bottom:1px solid #1e293b}}
.dot{{width:14px;height:14px;border-radius:50%}}
.title{{margin-left:20px;color:#94a3b8;font:15px 'JetBrains Mono'}}
pre{{margin:0;padding:24px 30px 34px;color:#e2e8f0;font:15.5px/1.5 'JetBrains Mono';white-space:pre}}
.cmd{{color:#7dd3fc}}
</style></head><body><div class="win"><div class="bar"><div class="dot" style="background:#ef4444"></div>
<div class="dot" style="background:#f59e0b"></div><div class="dot" style="background:#22c55e"></div>
<div class="title">hitl-send-reply — live · gpt-4.1-mini</div></div>
<pre><span class="cmd">$ {html.escape(cmd)}</span>
{body}</pre></div></body></html>"""
    tmp = dst + ".html"
    open(tmp, "w", encoding="utf-8").write(page)
    js = f"""const {{chromium}}=require('playwright');(async()=>{{const b=await chromium.launch();
const p=await b.newPage({{deviceScaleFactor:2,viewport:{{width:1400,height:800}}}});
await p.goto('file://'+{json.dumps(os.path.abspath(tmp))});await p.evaluate(()=>document.fonts.ready);
await (await p.$('body')).screenshot({{path:{json.dumps(dst)}}});await b.close();}})();"""
    subprocess.run(["node", "-e", js], check=True)
    os.remove(tmp)

if __name__ == "__main__":
    main(*sys.argv[1:4])

"""Compare legacy (docs/baseline) and migrated (docs/after) page output.
Normalises what is expected to differ: CSS/JS tags (bundling removed), anti-forgery token values and
position, session footer text, whitespace, attribute order and class order."""
import re, difflib, pathlib, sys
HERE = pathlib.Path(__file__).resolve().parent

def norm_tag(tag):
    m = re.match(r'<(\w+)(.*?)(/?)>$', tag, flags=re.S)
    if not m: return tag
    name, attrs, close = m.groups()
    pairs = re.findall(r'([\w\-:]+)(?:="([^"]*)")?', attrs)
    out = []
    for k, v in pairs:
        k = k.lower()
        if k == "class": v = " ".join(sorted(v.split()))
        out.append(f'{k}="{v}"')
    return f"<{name.lower()} {' '.join(sorted(out))}{close}>"

def norm(s):
    s = s.lstrip('﻿')
    s = re.sub(r'<head>.*?</head>', '<head/>', s, flags=re.S)
    s = re.sub(r'<script[^>]*>\s*</script>', '', s)
    s = re.sub(r'<input name="__RequestVerificationToken"[^>]*>', '', s)
    s = re.sub(r'<small>.*?</small>', '<small>SESSION</small>', s, flags=re.S)
    s = re.sub(r'\s+', ' ', s)
    parts = re.split(r'(<[^>]+>)', s)
    parts = [norm_tag(p) if p.startswith('<') else p.strip() for p in parts]
    return [p for p in parts if p]

files = ["index.html", "details-1.html", "edit-1.html", "api-brands.json", "api-brands-2.json"]
for f in files:
    a = norm((HERE / "baseline" / f).read_text(encoding="utf-8-sig"))
    b = norm((HERE / "after" / f).read_text(encoding="utf-8-sig"))
    d = [l for l in difflib.unified_diff(a, b, lineterm='', n=0) if not l.startswith(('---', '+++', '@@'))]
    print(f"{f}: {'IDENTICAL' if not d else str(len(d)) + ' differing fragments'}")
    for x in d: print("   ", x[:200])

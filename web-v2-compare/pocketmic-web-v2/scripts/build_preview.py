#!/usr/bin/env python3
"""Build a self-contained, navigable review file without changing deployable pages."""
from __future__ import annotations
import argparse
import base64
import html
import json
import mimetypes
import re
from pathlib import Path
from urllib.parse import urljoin

ROOT = Path(__file__).resolve().parents[1]
SITE = ROOT / 'site'
BASE = 'https://canopydigital.ca/sites/pocketmic-lan/'

def data_url(path: Path) -> str:
    mime = mimetypes.guess_type(str(path))[0] or 'application/octet-stream'
    return 'data:' + mime + ';base64,' + base64.b64encode(path.read_bytes()).decode('ascii')

def inline_page(relative: str, preview: bool = False) -> str:
    path = SITE / relative
    content = path.read_text(encoding='utf-8')
    def stylesheet(match):
        source = (path.parent / html.unescape(match[1])).resolve()
        return '<style>' + source.read_text(encoding='utf-8') + '</style>'
    content = re.sub(r'<link rel="stylesheet" href="([^"]+)">', stylesheet, content)
    scripts = []
    def script(match):
        scripts.append((path.parent / html.unescape(match[1])).resolve().read_text(encoding='utf-8'))
        return ''
    content = re.sub(r'<script src="([^"]+)" defer></script>', script, content)
    def source_tag(match):
        tag = match[0]
        attr = 'src' if tag.startswith('<img') else 'href'
        found = re.search(r'\b'+attr+r'="([^"]+)"', tag)
        if not found or found[1].startswith(('http:', 'https:', 'data:')): return tag
        image = (path.parent / html.unescape(found[1])).resolve()
        return tag.replace(found[0], attr+'="'+data_url(image)+'"')
    content = re.sub(r'<img\b[^>]*>|<link\b[^>]*rel="icon"[^>]*>|<a\b[^>]*data-image-preview[^>]*>', source_tag, content)
    content = content.replace('<head>', '<head><base href="'+urljoin(BASE, relative)+'">', 1)
    js = '\n'.join(scripts)
    if preview:
        js = js.replace('new URLSearchParams(location.search)', 'new URLSearchParams(document.body.dataset.previewSearch || "")')
    content = content.replace('</body>', '<script>'+js+'</script></body>')
    return content

SHELL = r'''<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><meta name="robots" content="noindex,nofollow"><title>PocketMic · interactive site preview</title><style>
*{box-sizing:border-box}html,body{margin:0;height:100%;background:#101614;color:#edf8ee;font-family:system-ui,"Segoe UI",sans-serif}header{min-height:54px;padding:8px 16px;display:flex;gap:12px;align-items:center;border-bottom:1px solid #375340}header strong{font-size:13px;white-space:nowrap}header small{color:#bfd3c5;font-size:11px}select,button{border:1px solid #547861;border-radius:7px;padding:8px;background:#1e3025;color:#ecf8ed;font:inherit;font-size:12px;min-height:36px}select{max-width:340px;min-width:0}button:disabled{opacity:.4}button:focus-visible,select:focus-visible{outline:3px solid #93f4c3;outline-offset:2px}iframe{width:100%;height:calc(100% - 56px);border:0;display:block;background:#101614}.notice{margin-left:auto}@media(max-width:650px){header{padding:7px;gap:7px}header strong{font-size:11px}select{max-width:160px;font-size:11px}.notice{display:none}}@media(max-width:380px){header strong{display:none}select{max-width:235px}}
</style></head><body><header><button id="back" type="button" disabled aria-label="Go back in site preview">←</button><strong>PocketMic preview</strong><label for="page" style="position:absolute;width:1px;height:1px;overflow:hidden;clip-path:inset(50%)">Preview page</label><select id="page"></select><small class="notice">Not deployed · download links open GitHub</small></header><iframe id="preview" title="PocketMic website preview"></iframe>
<script id="documents" type="application/json">__DOCUMENTS__</script><script id="routes" type="application/json">__ROUTES__</script><script>
(()=>{'use strict';const docs=JSON.parse(document.getElementById('documents').textContent);const routes=JSON.parse(document.getElementById('routes').textContent);const frame=document.getElementById('preview');const picker=document.getElementById('page');const back=document.getElementById('back');const base=new URL('https://canopydigital.ca/sites/pocketmic-lan/');let current;const stack=[];
for(const r of routes){if(r.key==='policy')continue;const o=document.createElement('option');o.value=r.path;o.textContent=r.title;picker.append(o)}
const escape=(s)=>s.replaceAll('&','&amp;').replaceAll('"','&quot;').replaceAll('<','&lt;');
function show(path,search='',hash='',push=true){if(!docs[path])return;if(push&&current)stack.push(current);current={path,search,hash};picker.value=path;back.disabled=!stack.length;frame.srcdoc=docs[path].replace('<body ','<body data-preview-search="'+escape(search)+'" ')}
picker.addEventListener('change',()=>show(picker.value));back.addEventListener('click',()=>{const last=stack.pop();if(last)show(last.path,last.search,last.hash,false)});
function scrollHash(hash){if(!hash)return;let id;try{id=decodeURIComponent(hash.slice(1))}catch{return}const target=frame.contentDocument.getElementById(id);if(target?.tagName==='DETAILS')target.open=true;target?.scrollIntoView({block:'start'})}
frame.addEventListener('load',()=>{const doc=frame.contentDocument;if(!doc||!current)return;scrollHash(current.hash);doc.addEventListener('click',(event)=>{if(event.defaultPrevented)return;const a=event.target.closest('a[href]');if(!a)return;let url;try{url=new URL(a.href)}catch{return}if(url.origin!==base.origin||!url.pathname.startsWith(base.pathname))return;let path=url.pathname.slice(base.pathname.length);if(!path||path.endsWith('/'))path+='index.html';if(!docs[path])return;event.preventDefault();if(path===current.path&&url.search===current.search&&url.hash){scrollHash(url.hash);return}show(path,url.search,url.hash)})});show('index.html','', '',false)
})();
</script></body></html>'''

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--output', type=Path, default=ROOT/'pocketmic-preview.html')
    args = parser.parse_args()
    routes = json.loads((SITE/'routes.json').read_text(encoding='utf-8'))
    docs = {r['path']: inline_page(r['path'], preview=True) for r in routes}
    safe = lambda obj: json.dumps(obj,ensure_ascii=False).replace('</','<\\/')
    result = SHELL.replace('__DOCUMENTS__',safe(docs)).replace('__ROUTES__',safe(routes))
    args.output.write_text(result,encoding='utf-8')
    print(f'Created {args.output} ({args.output.stat().st_size:,} bytes, {len(docs)} pages)')

if __name__ == '__main__': main()

#!/usr/bin/env python3
"""Dependency-free source/link checks. Run from any directory with Python 3.10+."""
from __future__ import annotations
import json
import sys
import xml.etree.ElementTree as ET
from html.parser import HTMLParser
from pathlib import Path
from urllib.parse import unquote, urlsplit

ROOT = Path(__file__).resolve().parents[1]
SITE = ROOT / 'site'

class Document(HTMLParser):
    def __init__(self, content: str):
        super().__init__(convert_charrefs=True)
        self.tags: list[tuple[str, dict[str, str]]] = []
        self.feed(content)
    def handle_starttag(self, tag, attrs):
        self.tags.append((tag, dict(attrs)))
    def select(self, tag):
        return [attrs for name, attrs in self.tags if name == tag]

def main() -> int:
    errors: list[str] = []
    docs = {path: Document(path.read_text(encoding='utf-8')) for path in sorted(SITE.rglob('*.html'))}
    checked_links = 0
    checked_assets = 0
    for path, doc in docs.items():
        label = str(path.relative_to(SITE))
        def fail(message): errors.append(f'{label}: {message}')
        if len(doc.select('h1')) != 1: fail('Expected exactly one h1')
        if len(doc.select('title')) != 1: fail('Expected exactly one title')
        if not any(a.get('name') == 'description' for a in doc.select('meta')): fail('Missing description')
        if not any(a.get('name') == 'viewport' for a in doc.select('meta')): fail('Missing viewport')
        if len([a for a in doc.select('link') if a.get('rel') == 'canonical']) != 1: fail('Expected one canonical')
        ids = [a['id'] for _, a in doc.tags if a.get('id')]
        if len(ids) != len(set(ids)): fail('Duplicate ids')
        for tag, attrs in doc.tags:
            if tag == 'img' and 'src' in attrs:
                if 'alt' not in attrs: fail('Image lacks alt')
                if not attrs.get('width') or not attrs.get('height'): fail('Image lacks width/height')
            if tag == 'a' and attrs.get('target') == '_blank' and 'noopener' not in attrs.get('rel', ''): fail('New-tab link lacks noopener')
            if tag == 'button' and attrs.get('type') != 'button': fail('Non-form button lacks explicit type')
            for attribute in ('href', 'src'):
                value = attrs.get(attribute)
                if not value or value.startswith(('data:', 'mailto:', 'tel:')): continue
                url = urlsplit(value)
                if url.scheme or url.netloc:
                    if attribute == 'src' and tag in ('img','script','iframe'): fail('External auto-loaded resource')
                    if tag == 'link' and attrs.get('rel') == 'stylesheet': fail('External stylesheet')
                    continue
                target = (path.parent / unquote(url.path)).resolve() if url.path else path
                if target.is_dir(): target = target / 'index.html'
                if not target.is_relative_to(SITE): fail(f'Local reference escapes site: {value}'); continue
                if not target.exists(): fail(f'Missing {value}'); continue
                checked_links += 1
                if attribute == 'src': checked_assets += 1
                if url.fragment and target in docs:
                    target_ids = {a.get('id') for _, a in docs[target].tags}
                    if unquote(url.fragment) not in target_ids: fail(f'Missing anchor {value}')
        for meta in doc.select('meta'):
            if meta.get('property') == 'og:image' or meta.get('name') == 'twitter:image':
                local = SITE / 'assets/social' / urlsplit(meta['content']).path.rsplit('/', 1)[-1]
                if not local.is_file(): fail(f'Missing social image {local.name}')
    routes = json.loads((SITE/'routes.json').read_text())
    if len(routes) != len(docs): errors.append('Route manifest count differs from HTML count')
    site_map = ET.parse(SITE/'sitemap.xml')
    indexed = site_map.findall('{http://www.sitemaps.org/schemas/sitemap/0.9}url')
    if len(indexed) != sum(not route['noindex'] for route in routes): errors.append('Sitemap indexable count differs')
    release = json.loads((SITE/'release.json').read_text())
    for platform in ['android', 'windows']:
        item = release[platform]
        if '/releases/download/v0.1.4/' not in item['url']: errors.append(f'{platform}: asset URL not pinned')
        if len(item['sha256']) != 64: errors.append(f'{platform}: invalid checksum length')
    js = (SITE/'assets/site.js').read_text()
    for forbidden in ['fetch(', 'XMLHttpRequest', 'sendBeacon(', 'getUserMedia(', 'document.cookie', 'localStorage']:
        if forbidden in js: errors.append(f'Unexpected networking/storage/media API: {forbidden}')
    report = {'htmlPages': len(docs), 'indexablePages': len(indexed), 'localReferencesChecked': checked_links,
              'resourceReferencesChecked': checked_assets, 'socialPNGs': len(list((SITE/'assets/social').glob('*.png'))),
              'release': release['version'], 'errors': errors, 'passed': not errors}
    (ROOT/'verification').mkdir(exist_ok=True)
    (ROOT/'verification/static-checks.json').write_text(json.dumps(report, indent=2)+'\n')
    print(json.dumps(report, indent=2))
    return 1 if errors else 0

if __name__ == '__main__':
    sys.exit(main())

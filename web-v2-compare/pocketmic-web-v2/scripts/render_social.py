#!/usr/bin/env python3
"""Optional social artwork rendering. Requires Playwright, never runtime dependencies."""
from __future__ import annotations
import argparse
import re
import shutil
from pathlib import Path
from playwright.sync_api import sync_playwright
from build_preview import ROOT, data_url

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--browser', help='Optional path to a Chromium executable')
    args=parser.parse_args()
    executable=args.browser or shutil.which('chromium') or shutil.which('chromium-browser')
    output=ROOT/'site/assets/social'
    output.mkdir(parents=True,exist_ok=True)
    with sync_playwright() as p:
        browser=p.chromium.launch(headless=True,**({'executable_path':executable} if executable else {}))
        for source in sorted((ROOT/'marketing/layouts').glob('*.html')):
            width,height=map(int,source.stem.rsplit('-',1)[1].split('x'))
            def replace(match):
                file=(source.parent/match[1]).resolve()
                return 'src="'+data_url(file)+'"'
            markup=re.sub(r'src="([^"]+)"',replace,source.read_text(encoding='utf-8'))
            page=browser.new_page(viewport={'width':width,'height':height},device_scale_factor=1)
            page.set_content(markup,wait_until='load')
            page.screenshot(path=str(output/(source.stem+'.png')))
            page.close()
            print(source.stem+'.png')
        browser.close()

if __name__=='__main__':main()

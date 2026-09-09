#!/usr/bin/env python3
"""Optional rendered checks using Playwright. Uses inline local assets, not a server.
Install the Python playwright package and its Chromium browser to rerun.
"""
from __future__ import annotations
import json
import shutil
from pathlib import Path
from playwright.sync_api import sync_playwright
from build_preview import ROOT, SITE, inline_page

OUT=ROOT/'verification'
ROUTES=json.loads((SITE/'routes.json').read_text(encoding='utf-8'))

def main():
    OUT.mkdir(exist_ok=True)
    report={'method':'Chromium page.set_content with the actual generated HTML/CSS/JS and embedded local image bytes',
            'limitations':['No hosted browser navigation or HTTP headers tested.','No native Android/Windows install, driver, audio, or network test performed.','No full WCAG conformance audit or field performance claim.'],
            'viewports':[320,390,768,1440], 'renders':[], 'functional':{}, 'pageErrors':[]}
    with sync_playwright() as p:
        executable=shutil.which('chromium') or shutil.which('chromium-browser')
        browser=p.chromium.launch(headless=True,**({'executable_path':executable} if executable else {}))
        page=browser.new_page(viewport={'width':1440,'height':1050},device_scale_factor=1)
        page.on('pageerror',lambda e:report['pageErrors'].append(str(e)))
        for r in ROUTES:
            markup=inline_page(r['path'])
            for w in report['viewports']:
                page.set_viewport_size({'width':w,'height':1050 if w>768 else 844})
                page.set_content(markup,wait_until='load')
                item=page.evaluate('''() => ({viewport:innerWidth, width:document.documentElement.scrollWidth, h1:document.querySelectorAll('h1').length, brokenImages:[...document.querySelectorAll('img[src]')].filter(i=>!i.complete||i.naturalWidth===0).length})''')
                item.update({'path':r['path'],'passed':item['width']<=w and item['brokenImages']==0 and item['h1']==1})
                if not item['passed']:
                    item['wideElements']=page.evaluate('''()=>[...document.querySelectorAll('main *')].filter(el=>el.getBoundingClientRect().right>innerWidth+1).slice(0,10).map(el=>({tag:el.tagName,classes:el.className,right:el.getBoundingClientRect().right,text:el.textContent.slice(0,80)}))''')
                report['renders'].append(item)
                if w==1440 and r['key'] in ['home','downloads','setup','discord']:
                    page.screenshot(path=str(OUT/f'{r["key"]}-desktop.png'),full_page=True)
                    if r['key']=='home': page.screenshot(path=str(OUT/'home-hero.png'))
                if w==390 and r['key'] in ['home','downloads','setup']:
                    page.screenshot(path=str(OUT/f'{r["key"]}-mobile.png'),full_page=True)
        page.set_viewport_size({'width':390,'height':844})
        page.set_content(inline_page('index.html'),wait_until='load')
        toggle=page.locator('.nav-toggle')
        toggle.click(); opened=toggle.get_attribute('aria-expanded')=='true'
        page.keyboard.press('Escape')
        report['functional']['mobileNavigation']=opened and toggle.get_attribute('aria-expanded')=='false' and toggle.evaluate('el=>el===document.activeElement')
        page.locator('[data-image-preview]').first.click()
        report['functional']['screenshotDialogOpen']=page.locator('#screenshot-dialog').evaluate('el=>el.open')
        page.keyboard.press('Escape')
        report['functional']['screenshotDialogCloseAndFocus']=not page.locator('#screenshot-dialog').evaluate('el=>el.open') and page.locator('[data-image-preview]').first.evaluate('el=>el===document.activeElement')
        page.set_content(inline_page('setup/index.html'),wait_until='load')
        page.evaluate("window.__events=[];window.addEventListener('pocketmic:conversion',e=>window.__events.push(e.detail));")
        for checkbox in page.locator('[data-setup-step]').all():checkbox.check()
        report['functional']['checklist6of6']=page.locator('#setup-progress').get_attribute('value')=='6'
        report['functional']['localCheckEvents']=page.evaluate("window.__events.filter(e=>e.event==='setup_step_check').length===6")
        page.locator('#reset-checklist').click()
        report['functional']['resetChecklist']=page.locator('#setup-progress').get_attribute('value')=='0' and page.locator('[data-setup-step]:checked').count()==0
        page.locator('#destination').select_option('obs')
        report['functional']['destinationGuide']=page.locator('[data-destination="obs"]').evaluate('el=>el.open') and 'use=obs' in page.locator('#setup-done').get_attribute('href')
        page.set_content(inline_page('download/android/index.html'),wait_until='load')
        page.locator('[data-copy]').click()
        page.wait_for_timeout(70)
        report['functional']['copyHashHasFallback']=bool(page.locator('#status-message').inner_text())
        # Test the self-contained preview's actual client-side route handoff.
        page.set_viewport_size({'width':1440,'height':1050})
        page.set_content((ROOT/'pocketmic-preview.html').read_text(encoding='utf-8'),wait_until='load')
        page.wait_for_function("document.getElementById('preview').contentDocument?.body?.dataset.page==='home'")
        frame=page.frame_locator('#preview')
        frame.locator('.header-cta a').click()
        page.wait_for_function("document.getElementById('preview').contentDocument?.body?.dataset.page==='downloads'")
        report['functional']['previewHomeToDownloads']=True
        page.locator('#page').select_option('use-cases/obs/index.html')
        page.wait_for_function("document.getElementById('preview').contentDocument?.body?.dataset.page==='obs'")
        report['functional']['previewRoutePicker']=True
        page.locator('#back').click()
        page.wait_for_function("document.getElementById('preview').contentDocument?.body?.dataset.page==='downloads'")
        report['functional']['previewBack']=True
        # Rendering without JavaScript: primary content, links and native FAQs stay available.
        nojs=browser.new_context(java_script_enabled=False,viewport={'width':390,'height':844})
        np=nojs.new_page();np.set_content(inline_page('index.html'),wait_until='load')
        report['functional']['noJSNavigationAndContent']=np.locator('.main-nav').is_visible() and np.locator('h1').is_visible()
        nojs.close();browser.close()
    report['passed']=all(r['passed'] for r in report['renders']) and all(report['functional'].values()) and not report['pageErrors']
    (OUT/'browser-checks.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    print(json.dumps({'renders':len(report['renders']),'failedRenders':[r for r in report['renders'] if not r['passed']], 'functional':report['functional'],'pageErrors':report['pageErrors'],'passed':report['passed']},indent=2))
    return 0 if report['passed'] else 1

if __name__=='__main__': raise SystemExit(main())

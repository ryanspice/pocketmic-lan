#!/usr/bin/env python3
"""Generate social copy, campaign links, a review studio, and image-layout sources."""
from pathlib import Path
import json, re, html, sys
from urllib.parse import urlencode
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'src'))
from build import icon
BASE='https://canopydigital.ca/sites/pocketmic-lan/'
MARKETING=ROOT/'marketing';MARKETING.mkdir(exist_ok=True)
def link(path,network,content):
 return BASE+path+'?'+urlencode(dict(utm_source=network,utm_medium='social',utm_campaign='pocketmic_v014',utm_content=content))
ALT = {
 'launch-1200x630.png': 'Your phone. Your next PC mic. PocketMic LAN artwork with real Android and Windows app captures, for a local phone-to-PC audio connection.',
 'discord-1200x630.png': 'Keep the headphones. Change the mic. PocketMic Android and Windows app captures, with a note that Discord routing uses VB-CABLE.',
 'obs-1200x630.png': 'A phone mic. In your OBS scene. PocketMic Android and Windows app captures; the setup uses Android, Windows and VB-CABLE.',
 'meetings-1200x630.png': 'Another mic option. Before the call. PocketMic app captures and a reminder that this is early software and the setup should be tested first.',
 'engineering-1200x630.png': '10 ms packets. Not 10 ms latency. PocketMic app captures with a note on 48 kHz PCM16, encrypted UDP and a trusted LAN.',
 'routing-1080x1080.png': 'Input goes in. Output comes out. A routing diagram shows PocketMic Receiver playback into CABLE Input, through VB-CABLE, and CABLE Output selected as the Windows app microphone.'
}
POSTS=[]
def post(id,network,topic,body,path='',asset='launch-1200x630.png',alt='',group='standalone'):
 url=link(path,network,id)
 text=body.strip()+'\n\n'+url
 weighted=len(re.sub(r'https?://\S+','x'*23,text))
 if network=='x' and weighted>280:raise ValueError((id,weighted))
 POSTS.append(dict(id=id,network=network,topic=topic,text=text,url=url,asset=asset,alt=alt or ALT.get(asset, 'PocketMic LAN: Android and Windows app captures for a local microphone connection.'),group=group,characters=len(text),estimatedWeightedCharacters=weighted))
post('x01_launch','x','Launch','''Your phone. Your next PC mic.

PocketMic LAN turns Android into a Windows microphone over your private network.

Free, open source. Discord, OBS & calls via VB-CABLE.

Early release: APK + receiver.''',asset='launch-1200x630.png')
post('x02_discord','x','Discord / headphones','''Great headphones. No mic?

Try your Android phone as a Discord input on Windows with PocketMic LAN + VB-CABLE.

No PocketMic account. Same private network. Run the mic test before joining the squad.''','use-cases/discord/','discord-1200x630.png')
post('x03_obs','x','OBS / creators','''A phone mic can be another input in your OBS scene.

PocketMic LAN → Windows → VB-CABLE → Audio Input Capture.

Record a short test for level and sync before you go live. Setup guide:''','use-cases/obs/','obs-1200x630.png')
post('x04_route','x','Useful routing tip','''PocketMic setup tip:

Receiver output: CABLE Input
Your app’s microphone: CABLE Output

The names feel backwards because they describe the cable, not your app. Audio goes in one side and comes out the other.''','setup/','routing-1080x1080.png',alt='Routing tip: choose CABLE Input in PocketMic Receiver, then CABLE Output as the microphone in your app.')
post('x05_boundary','x','Privacy boundary','''“Local” needs a boundary.

PocketMic’s Android-to-Windows audio hop stays on your private LAN. Discord or Teams can still send that audio online.

Open-source transport. No misleading promise that your whole call is offline.''','technical/','engineering-1200x630.png')
post('x06_latency','x','Engineering / latency','''10 ms packets ≠ 10 ms latency.

PocketMic sends 10 ms audio frames. Capture, Wi-Fi, buffering and Windows routing still add delay.

I’d rather document that boundary than call it “zero latency.”''','technical/','engineering-1200x630.png')
post('x07_install','x','Installation transparency','''What’s in the PocketMic v0.1.4 download?

• Installable debug-signed Android APK
• Self-contained Windows x64 ZIP
• Published SHA-256 checksums

Not a store release. Not an installer. Exact files and setup notes:''','download/','launch-1200x630.png')
post('x08_calls','x','Meeting setup','''Try a new mic setup before the important call, not during it.

PocketMic LAN can route your Android mic into a Windows calling app via VB-CABLE.

It’s early software: test your devices and keep a known-working fallback.''','use-cases/meetings/','meetings-1200x630.png')
post('x09_debug','x','Support / signal path','''No sound? Follow the path:

1. Phone input level
2. Receiver packet count
3. CABLE Input as playback
4. CABLE Output as your app’s mic

Each check answers a different question. PocketMic troubleshooting:''','troubleshooting/','routing-1080x1080.png')
post('x10_open','x','Open source','''PocketMic LAN is open source: Kotlin + Jetpack Compose on Android, C#/.NET on Windows, and a documented encrypted UDP audio path.

For people who want to use it, inspect it, or help improve it.''','technical/','engineering-1200x630.png')
post('x11_feedback','x','Device feedback','''Trying PocketMic LAN on your Android + Windows setup?

Useful feedback: phone model, OS versions, destination app, capture mode, and what worked or failed.

Please remove pairing keys and QR codes before posting diagnostics.''','troubleshooting/','launch-1200x630.png')
post('x12_recap','x','Recap / practical fit','''An Android phone. A Windows PC. The same private network.

That’s the starting point for PocketMic LAN. Add VB-CABLE to route into Discord, OBS, or a calling app.

Free, open source, and honest about the setup.''','get-started/','launch-1200x630.png')
THREAD=[
('The idea','''1/6 Your phone already has a microphone. PocketMic LAN gives it a route into your Windows setup.

Android app → encrypted local connection → Windows receiver.

Here’s the practical version, including the extra step.''',''),
('The dependency','''2/6 The Windows receiver sends audio to a playback device. A calling app expects a recording device.

VB-CABLE bridges that gap. It’s a separate third-party driver, not something silently bundled into PocketMic.''','setup/'),
('The route','''3/6 The names are the confusing bit:

PocketMic output → CABLE Input
App microphone → CABLE Output

Input is where audio enters the virtual cable. Output is where your app reads it.''','setup/'),
('The boundary','''4/6 “No cloud relay” applies to the PocketMic phone-to-PC hop.

It does not mean Discord, Teams, or your streaming service stops using the internet. The destination app has its own privacy and recording behaviour.''','privacy/'),
('The timing','''5/6 A 10 ms packet is not a 10 ms latency benchmark.

Capture, network conditions, buffering, and Windows audio routing matter. Test the real setup before a call or stream. This is not for real-time instrument monitoring.''','technical/'),
('The invitation','''6/6 PocketMic v0.1.4 is available as a debug-signed Android APK and a self-contained Windows x64 ZIP.

Free and open source. Start with your use case, get both apps, then run a sound check. Feedback welcome.''','get-started/')]
for i,(topic,body,path) in enumerate(THREAD):post(f'x_thread_{i+1}','x',topic,body,path,'launch-1200x630.png',group='thread')
post('li01_launch','linkedin','Launch / useful product story','''Your phone. Your next PC microphone.

I’m building PocketMic LAN: an Android app and a Windows receiver that give your phone’s microphone a local route into your PC audio setup.

The use cases are practical: Discord with the headphones you already like, another audio input for an OBS scene, or an alternative microphone to evaluate before a call.

The connection between the phone and PC runs over your trusted private network, with authenticated packet encryption and no PocketMic account or cloud relay. To expose the receiver’s audio as a microphone in another app, you install VB-CABLE separately.

That last step matters. A useful landing page should explain the whole setup, not leave the most confusing dependency until after the download.

v0.1.4 is early software: an installable debug-signed Android APK and a self-contained Windows x64 ZIP. It is not a production store release, and real-device testing is still important.

The source, downloads, requirements, and setup guides are available here. I’d especially value feedback from people willing to test their actual Android + Windows + destination-app combination.

#OpenSource #Windows''',asset='launch-1200x630.png')
post('li02_latency','linkedin','Engineering / honest performance claims','''A 10 ms audio packet is not a 10 ms latency benchmark.

That distinction is worth keeping visible when explaining a project like PocketMic LAN.

The Android sender captures 48 kHz mono PCM16 and groups 480 samples into each packet. That is 10 ms of audio. The path to another Windows app still includes capture buffering, the network, the receiver prebuffer, Windows audio routing, and the destination app.

The documented default receiver prebuffer alone is 100 ms. Packet duration cannot honestly stand in for the complete experience.

There is a similar distinction in the bandwidth numbers: 768 kbit/s describes the raw audio payload. The documented 1,000-byte datagrams at 100 packets per second are 800 kbit/s before UDP/IP and link-layer overhead.

Those are protocol calculations, not measurements of every user’s network.

I want the product’s technical page to make those boundaries clear: explain what is implemented, identify what has actually been tested, and leave room for real-device evidence rather than filling it with “zero latency” language.

The implementation and current limits are documented here.

#SoftwareEngineering #OpenSource''','technical/','engineering-1200x630.png')
post('li03_obs','linkedin','Creator workflow / practical setup','''A small audio-routing tip for anyone trying PocketMic LAN with OBS:

The receiver sends audio to CABLE Input. OBS captures CABLE Output.

The names describe the two ends of VB-CABLE, not the direction you might expect from the application you are configuring.

The complete path is:
Android microphone → PocketMic Receiver → VB-CABLE → OBS Audio Input Capture.

Once that is connected, make a short test recording. Speak normally, leave a few seconds of silence, and clap once on camera. Review the result for level, noise, and audio/video alignment before using it live.

Also check that OBS is not capturing the same device globally and again as a scene source. A duplicate input can be the source of an apparent echo.

PocketMic is not trying to replace OBS. It is another way to get a microphone signal into the Windows workflow you already use.

The Android and Windows apps are open source. They are also early software, so the guide includes the dependency and timing limitations rather than promising that every phone will behave the same way.

The OBS-specific setup is here.

#OBSStudio #OpenSource''','use-cases/obs/','obs-1200x630.png')
post('li04_privacy','linkedin','Architecture / privacy boundaries','''“No cloud” is too broad unless you explain where the boundary ends.

For PocketMic LAN, the intended boundary is specific: microphone audio travels from an Android phone to a Windows receiver over a trusted private network. That hop uses authenticated encryption and does not need a PocketMic cloud relay.

But choosing that input in Teams does not turn Teams into an offline call. Sending it into OBS does not prevent OBS from streaming. The destination application still controls what happens next.

The website deserves the same precision. This site build uses self-hosted assets and no third-party tracking scripts. Its setup checklist can remember checked steps for the browser tab’s session, but it does not access a microphone, inspect native apps, or confirm that installation succeeded.

Those are different data paths, with different responsibilities. Combining them into one sweeping “nothing ever leaves your device” claim would be easier copy, but worse communication.

The project’s technical and privacy pages separate the local transport, the destination app, and the website behaviour.

That is the kind of boundary I want users to understand before they install.

#PrivacyByDesign #SoftwareArchitecture''','privacy/','engineering-1200x630.png')
post('li05_activation','linkedin','Product engineering / activation','''A download click is not a working microphone.

That is the product-design problem behind the PocketMic LAN setup flow.

There are two apps to install. The devices need to reach each other over a trusted private network. The phone needs to capture audio. The receiver needs to receive it. VB-CABLE needs to expose the correct recording endpoint. Finally, the destination app needs to select and hear that input.

Each step can succeed while the next one is still wrong.

So the setup checklist does not pretend to detect a native installation or display a fake “connected” state. It asks the user to confirm the meaningful steps, then points them to the actual mic test or recording in Discord, OBS, or their calling app.

The funnel is not an email gate. It is a route from a specific use case to the matched downloads, the audio setup, and a sound check.

That also changes the success metric: a click is a click; “I can hear it in my app” is a user-reported outcome; neither is an independently measured hardware test.

The current setup flow is here. It is part of the open-source PocketMic project.

#ProductEngineering #DeveloperExperience''','setup/','routing-1080x1080.png')
post('li06_testers','linkedin','Feedback / invitation to test','''I’m looking for useful device feedback on PocketMic LAN — an open-source Android-to-Windows microphone project.

The best feedback is specific rather than just “works” or “doesn’t work.”

Which phone and Android version? Which Windows version? Which destination app? Did the phone input meter move? Did receiver packet counts increase? Did the final application hear the microphone?

That separates a capture problem from a network problem, and a network problem from a Windows audio-routing problem.

The v0.1.4 release includes an installable debug-signed Android APK, a self-contained Windows x64 receiver ZIP, and checksums. It needs the same trusted private network, and app microphone routing uses a separate VB-CABLE installation.

There are real limits: this is early software, not a production store release or a zero-latency monitoring system. Test it before relying on it for a call or stream.

If you try it, please remove pairing keys, QR codes, and sensitive connection details before sharing screenshots or diagnostics. The troubleshooting page links to the project’s issue tracker.

A reproducible report is much more useful than a vague claim — especially for a two-device audio path.

#OpenSource #AndroidDevelopment''','troubleshooting/','launch-1200x630.png')
(MARKETING/'posts.json').write_text(json.dumps(POSTS,ensure_ascii=False,indent=2),encoding='utf-8')
intro='''# PocketMic LAN social drafts

Publish only after the destination routes are deployed. These drafts are not posted or scheduled.
Product baseline: v0.1.4, checked 9 September 2026. Confirm release currency before publication.
Replace the production base URL only when the deployment path changes. Keep the use-case destination matched to the post.

'''
for network,file in [('x','X-POSTS.md'),('linkedin','LINKEDIN-POSTS.md')]:
 selected=[p for p in POSTS if p['network']==network]
 out=intro+('X character totals below are estimates using 23 characters per URL; confirm in the composer. The drafts are intentionally below the 280-character editorial budget.\n\n' if network=='x' else '')
 for item in selected:
  out+=f"## {item['id']} — {item['topic']}\n\n{item['text']}\n\n**Graphic:** `../site/assets/social/{item['asset']}`\n\n**Alt text:** {item['alt']}\n\n"+ (f"**Estimated weighted characters:** {item['estimatedWeightedCharacters']}\n\n" if network=='x' else '')+'---\n\n'
 out+='''## Source and claim boundaries

Product claims: supplied landing-page HTML and app screenshots, plus the version-pinned release and README.
- https://github.com/ryanspice/pocketmic-lan/releases/tag/v0.1.4
- https://github.com/ryanspice/pocketmic-lan/blob/v0.1.4/pocketmic-lan-v0.1.4/README.md
- https://vb-audio.com/Cable/
- https://obsproject.com/kb/audio-sources
- https://support.discord.com/hc/en-us/articles/360020641332-Mic-Testing

The bandwidth arithmetic is derived from the documented format. Do not imply independently measured end-to-end latency, superior hardware quality, native virtual-mic installation, store approval, customer counts, or adoption metrics. First-person posts are drafts for the project owner, not independently reported quotes.
'''
 (MARKETING/file).write_text(out,encoding='utf-8')

# Review UI: no API integration, no post/publish action, no tracking.
parts=[]
for item in POSTS:
 pid=item['id'];text=html.escape(item['text']);network=item['network']
 parts.append(f'''<article class="post-card" data-network="{network}" data-group="{item['group']}"><div class="post-header"><span class="pill">{'X / Twitter' if network=='x' else 'LinkedIn'} · {item['group']}</span><span>{pid}</span></div><h2>{html.escape(item['topic'])}</h2><textarea id="{pid}" readonly aria-label="{html.escape(item['topic'])} post text">{text}</textarea><div class="post-actions"><button type="button" data-copy="{pid}">Copy post</button><a href="../site/assets/social/{item['asset']}">Open matching graphic</a><small>{str(item['estimatedWeightedCharacters'])+' weighted characters (estimate)' if network=='x' else str(item['characters'])+' characters'}</small></div><details><summary>Image alt text</summary><p>{html.escape(item['alt'])}</p></details></article>''')
studio='''<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><meta name="robots" content="noindex,nofollow"><title>PocketMic Social Studio — drafts only</title><style>
*{box-sizing:border-box}body{background:#101614;color:#eef3eb;font-family:Inter,"Segoe UI",Arial,sans-serif;margin:0;padding:45px 24px;line-height:1.65}main,header{max-width:1120px;margin:auto}header{margin-bottom:30px}h1{font-size:clamp(2.5rem,5vw,4rem);letter-spacing:-.055em;line-height:1.05;margin:10px 0 20px}p{color:#b1c0b8;max-width:70ch}a{color:#88efbe;text-underline-offset:4px}.eyebrow{color:#88efbe;letter-spacing:.12em;font-size:12px;text-transform:uppercase}.filters{display:flex;gap:10px;flex-wrap:wrap;margin:30px 0}.filters button,button{border:1px solid #415b49;background:#1b2f22;color:#eef3eb;border-radius:8px;padding:12px 18px;font:600 14px "Segoe UI",Arial;cursor:pointer;min-height:44px}.filters button[aria-pressed=true],.post-actions button{background:#88efbe;color:#102b1e}.grid{display:grid;grid-template-columns:1fr 1fr;gap:20px}.post-card{border:1px solid #334139;border-radius:18px;padding:26px;background:#18211e;min-width:0}.post-header{display:flex;justify-content:space-between;align-items:center;font-size:11px;color:#b1c0b8;gap:10px}.pill{border:1px solid #47674f;padding:4px 9px;border-radius:99px;color:#88efbe}h2{font-size:21px;letter-spacing:-.02em;margin:18px 0}textarea{display:block;resize:vertical;min-height:300px;width:100%;border:1px solid #3c4f41;border-radius:9px;background:#101a13;color:#edf2e9;font:15px/1.7 "Segoe UI",Arial;padding:18px}article[data-network=linkedin] textarea{min-height:430px}.post-actions{display:flex;align-items:center;flex-wrap:wrap;gap:15px;margin-top:18px}.post-actions a{font-size:12px}.post-actions small{display:block;width:100%;color:#b1c0b8;font-size:11px}details{font-size:12px;margin-top:16px}summary{cursor:pointer}#status{position:fixed;bottom:20px;left:50%;transform:translateX(-50%);border-radius:10px;background:#eaf0e7;color:#17261b;padding:12px 25px;max-width:90%}#status:empty{display:none}[hidden]{display:none!important}:focus-visible{outline:3px solid #88efbe;outline-offset:4px}@media(max-width:760px){.grid{grid-template-columns:1fr}body{padding:30px 18px}.post-card{padding:22px}}
</style></head><body><header><p class="eyebrow">PocketMic / campaign desk</p><h1>The content.<br>Ready for your voice.</h1><p>12 standalone X posts, a six-part thread, and six LinkedIn drafts. Copy the text, open the matching graphic, and review before posting. Nothing here publishes or schedules content.</p><p><a href="../site/index.html">Preview the website</a> · <a href="CONTENT-PLAN.md">Read the publishing plan</a></p><div class="filters" aria-label="Filter posts"><button type="button" data-filter="all" aria-pressed="true">All 24 drafts</button><button type="button" data-filter="x" aria-pressed="false">X / Twitter</button><button type="button" data-filter="linkedin" aria-pressed="false">LinkedIn</button><button type="button" data-filter="thread" aria-pressed="false">Six-part thread</button></div></header><main><div class="grid">'''+''.join(parts)+'''</div></main><div id="status" role="status"></div><script>
const status=document.getElementById('status');let timer;
document.querySelectorAll('[data-filter]').forEach(button=>button.addEventListener('click',()=>{const v=button.dataset.filter;document.querySelectorAll('[data-filter]').forEach(b=>b.setAttribute('aria-pressed',String(b===button)));document.querySelectorAll('.post-card').forEach(p=>p.hidden=!(v==='all'||p.dataset.network===v||p.dataset.group===v));}));
document.querySelectorAll('[data-copy]').forEach(button=>button.addEventListener('click',async()=>{const field=document.getElementById(button.dataset.copy);try{if(!navigator.clipboard?.writeText)throw Error();await navigator.clipboard.writeText(field.value);status.textContent='Copied. Review the destination URL before posting.';}catch{field.focus();field.select();status.textContent='Text selected. Press Ctrl+C to copy.';}clearTimeout(timer);timer=setTimeout(()=>status.textContent='',5000);}));
</script></body></html>'''
(MARKETING/'social-studio.html').write_text(studio,encoding='utf-8')

# Art layouts: real supplied screenshots in layout, no fabricated application state.
SOCIAL=[
('launch','Your phone.<br>Your next <em>PC mic.</em>','Android → Windows. Over your own local network.','Free & open source · APK + Windows receiver',1200,630),
('discord','Keep the headphones.<br><em>Change the mic.</em>','Your Android phone → Discord on Windows.','PocketMic + VB-CABLE · Setup guide included',1200,630),
('obs','A phone mic.<br><em>In your OBS scene.</em>','Another input for the workflow you already use.','Android + Windows + VB-CABLE',1200,630),
('meetings','Another mic option.<br><em>Before the call.</em>','Try your Android phone as a Windows meeting input.','Early software · Test your setup first',1200,630),
('engineering','10 ms packets.<br><em>Not 10 ms latency.</em>','Open implementation. Clear performance boundaries.','48 kHz PCM16 · Encrypted UDP · Trusted LAN',1200,630),
('launch','Your phone.<br>Your next<br><em>PC mic.</em>','Android → Windows. Over your own local network.','Free & open source · v0.1.4',1080,1080),
('routing','Input goes in.<br><em>Output comes out.</em>','The VB-CABLE route, without the guesswork.','PocketMic Receiver → your Windows app',1080,1080)]
layout_dir=MARKETING/'layouts';layout_dir.mkdir(exist_ok=True)
for name,heading,sub,foot,w,h in SOCIAL:
 square=h==1080;routing=name=='routing'
 path=f'{name}-{w}x{h}.html'
 figure=(f'<div class="route"><div><small>POCKETMIC RECEIVER · PLAYBACK OUTPUT</small><b>CABLE Input</b></div><span>↓ &nbsp; VB-CABLE</span><div><small>YOUR APP · MICROPHONE INPUT</small><b>CABLE Output</b></div></div>' if routing else '<div class="visual"><div class="orb"></div><img class="phone" src="../../site/assets/screenshots/android-app-720.webp" alt="Actual PocketMic Android app screenshot"><img class="pc" src="../../site/assets/screenshots/windows-receiver.webp" alt="Actual PocketMic Windows receiver screenshot"></div>')
 layout=f'''<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><meta name="robots" content="noindex,nofollow"><title>PocketMic social artwork: {name}</title><style>
*{{box-sizing:border-box}}body{{margin:0;width:{w}px;height:{h}px;overflow:hidden;background:#101614;color:#f3f5ef;font-family:Inter,"Segoe UI",Arial,sans-serif;padding:48px 56px;position:relative}}.brand{{font-size:27px;font-weight:650;letter-spacing:-.04em;display:flex;align-items:center;gap:12px}}.brand .mark{{display:inline-grid;place-items:center;width:38px;height:42px;background:#88efbe;border-radius:11px;color:#123321}}.icon{{width:24px;height:24px}}.lan{{font-size:11px;border:1px solid #486250;padding:4px 5px;letter-spacing:.1em;margin-left:3px;color:#b0c7b7}}.release{{position:absolute;top:58px;right:56px;font-size:13px;letter-spacing:.09em;color:#9abbab}}h1{{font-size:{78 if square else 64}px;line-height:1.07;letter-spacing:-.055em;font-weight:650;max-width:{'14' if square else '12'}ch;margin:{'66' if square else '59'}px 0 23px}}em{{font-style:normal;color:#88efbe}}.sub{{font-size:{23 if square else 21}px;color:#b1c7b9;line-height:1.6;width:{'350' if square else '475'}px;margin:0}}.footer{{position:absolute;bottom:42px;left:56px;right:56px;border-top:1px solid #38513f;padding-top:22px;display:flex;justify-content:space-between;font-size:13px;color:#b2c9b9}}.visual{{position:absolute;right:40px;top:{'410' if square else '141'}px;width:{'590' if square else '500'}px;height:{'520' if square else '390'}px}}.orb{{position:absolute;inset:0;border:1px solid #355640;border-radius:100px 20px 95px 20px;background:radial-gradient(ellipse,#294b32,#15281b 70%,#111d15)}}.phone{{position:absolute;width:{'198' if square else '178'}px;left:14px;top:15px;transform:rotate(-5deg);border:6px solid #25392b;border-radius:25px;z-index:2;box-shadow:0 20px 50px #0005}}.pc{{position:absolute;width:{'389' if square else '310'}px;left:{'195' if square else '178'}px;top:150px;transform:rotate(3deg);border:6px solid #263a2c;border-radius:12px;box-shadow:0 20px 50px #0004}}.visual::after{{content:'Real app captures · not a live connection';position:absolute;left:220px;right:10px;bottom:{'36' if square else '23'}px;text-align:center;font-size:10px;color:#93ae9b}}.route{{margin-top:45px;width:100%}}.route>div{{border:1px solid #5b8767;background:#213a29;border-radius:18px;padding:28px 32px}}.route small{{font-size:15px;letter-spacing:.13em;color:#b4cebb;display:block;margin-bottom:12px}}.route b{{font-size:43px;letter-spacing:-.03em;color:#88efbe}}.route>span{{display:block;padding:14px 30px;color:#b4cebb;font-size:17px}}.routing h1{{max-width:17ch;margin-top:65px}}.routing .sub{{width:850px}}
</style></head><body class="{'routing' if routing else ''}"><div class="brand"><span class="mark">{icon('mic')}</span>PocketMic <span class="lan">LAN</span></div><div class="release">ANDROID + WINDOWS</div><h1>{heading}</h1><p class="sub">{sub}</p>{figure}<div class="footer"><span>{foot}</span><span>canopydigital.ca/sites/pocketmic-lan</span></div></body></html>'''
 (layout_dir/path).write_text(layout,encoding='utf-8')

plan='''# PocketMic LAN — launch content and funnel plan

## Editorial position

“Your phone. Your next PC mic.” Lead with the job, show real apps, and disclose the route before installation. Primary audience: Android/Windows users comfortable with an APK and a driver, especially Discord users and creators. Secondary: people evaluating an alternative meeting microphone. Technical readers get source-first material, not a consumer page full of packet diagrams.

These audiences are positioning hypotheses inferred from the supplied use cases, not verified conversion research. There are no fabricated customers, testimonials, conversion lifts, performance measurements, or user counts.

## The funnel, without a gate

| Intent | Entry | Next action | Activation | Follow-through |
|---|---|---|---|---|
| Discord / headphones | `use-cases/discord/` | Matched app downloads | Receiver → VB-CABLE → Discord mic test | Troubleshooting or feedback |
| OBS / creators | `use-cases/obs/` | Matched app downloads | OBS input + a real test recording | Report device and timing issues |
| Meetings | `use-cases/meetings/` | Requirements / permission check, then downloads | Destination-app test before a call | Keep a known-working fallback |
| Developers | `technical/` | Inspect source or download | Build / inspect / device-test | GitHub issue or contribution |
| Ready to install | `download/` | Android and Windows detail pages | Full six-step checklist | User-confirmed final sound check |

The global “Get the apps” CTA goes directly to downloads. The use-case chooser is optional. No account, email form, pricing fiction, scarcity message, fake installation detection, or native-device permissions are added to the website.

## Suggested two-week sequence

These are relative publishing slots, not scheduled posts or a promise about optimal posting time. Prefer useful responses and actual device feedback over publishing every draft.

| Relative day | X | LinkedIn | Intent |
|---|---|---|---|
| 1 | x01_launch | li01_launch | Introduce the product and exact release state |
| 2 | x02_discord | — | One concrete user problem |
| 3 | x04_route | li05_activation | Explain the dependency and setup completion |
| 4 | x03_obs | — | Show the creator workflow |
| 5 | x06_latency | li02_latency | Demonstrate engineering judgement |
| 6–7 | Reply to real questions; use x09_debug only when useful | — | Resolve friction, do not invent engagement |
| 8 | Six-part thread, once | li03_obs | Full route / practical tutorial |
| 9 | x05_boundary | — | State the privacy boundary |
| 10 | x08_calls | li04_privacy | Explain fit and responsibility |
| 11 | x07_install | — | Clarify distribution and signatures |
| 12 | x11_feedback | li06_testers | Ask for actionable device reports |
| 13–14 | x10_open or x12_recap; skip if redundant | — | Source visibility / recap |

## Social graphics

Seven ready-to-use PNGs are generated from editable HTML layouts: five 1200×630 campaign cards, one 1080×1080 launch card, and one 1080×1080 cable-routing card. Real supplied app captures are used in an illustrative layout; no fake connected status or performance graph is created. The wide cards also back the site’s Open Graph metadata.

`social-studio.html` groups all 24 drafts, offers copy buttons with a keyboard fallback, links the matching images, and provides alt text. It is a local review tool, not a posting integration. Keep `marketing/` out of the deployed public site unless you deliberately publish it.

## Measurement contract

UTM tags identify platform, campaign, and creative. Only constrained tag values are propagated on same-site links. The site emits local `pocketmic:conversion` events; it sends no analytics requests.

- `cta_click`: website navigation intent.
- `download_click`: click on a release asset, not a completed download or install.
- `setup_step_check`: user checked or unchecked a step.
- `setup_self_reported`: user clicked “I can hear it in my app.” Not independent device verification.

No production metrics exist until a consent-reviewed collection adapter is deliberately implemented and the privacy notice matches it. Do not use a click count as an activation rate. First qualitative goal: find the largest real setup failure with a reproducible report.

## Before publication

Deploy routes and graphics first. Open every campaign link from the live host. Check the current release against the version-pinned download manifest. Confirm the project owner approves first-person wording and the hosting/privacy notice. Validate the post in the platform composer. Add image alt text. Publish manually; no account has been accessed and no post has been sent.
'''
(MARKETING/'CONTENT-PLAN.md').write_text(plan,encoding='utf-8')
print('Generated',len(POSTS),'posts and',len(SOCIAL),'graphic layouts')
print('Max X weighted estimate:',max(p['estimatedWeightedCharacters'] for p in POSTS if p['network']=='x'))

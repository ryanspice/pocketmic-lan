(function(){
"use strict";
/* =====================================================================
   js/app.js — utilities, DOM, environment
   ===================================================================== */
var $  = function(s){ return document.querySelector(s); };
var $$ = function(s){ return Array.prototype.slice.call(document.querySelectorAll(s)); };
var clamp = function(v,a,b){ return Math.min(b, Math.max(a, v)); };

var I18N = {
/* =====================================================================
   js/i18n.js — locales (en, fr)
   ===================================================================== */
en:{
  title:"PocketMic LAN — Android microphone for Windows",
  desc:"PocketMic LAN streams audio from an Android phone to a Windows PC over a local network. Audio uses AES-256-GCM encryption. Free and open source.",
  html:{
    "skip":"Skip to content",
    "nav.setup":"SETUP","nav.lab":"SIGNAL LAB","nav.why":"WHY","nav.latency":"PROTOCOL","nav.faq":"FAQ",
    "head.dl":"DOWNLOAD",
    "hero.eyebrow":"ANDROID → WINDOWS · FREE &amp; OPEN SOURCE · v0.1.6 PLANNED",
    "hero.h1":'Your phone is <span class="gr">already</span> a great microphone.',
    "hero.sub":"PocketMic LAN streams audio from an Android app to a Windows receiver over your local network. Audio uses AES-256-GCM encryption; route it to calling and recording apps with a Windows audio device.",
    "hero.cta2":"VIEW THE SOURCE",
    "hero.spec1":"ANDROID APP → WINDOWS RECEIVER","hero.spec2":"PCM + OPUS AUDIO","hero.spec3":"AES-256-GCM ENCRYPTION","hero.spec4":"LOCAL NETWORK · NO CLOUD RELAY",
    "stage.live":"PREVIEW","stage.conn":"RECEIVER","stage.dev":"ANDROID · WINDOWS",
    "stage.wifi":"WI-FI · LAN","stage.cap":"INTERFACE PREVIEW — TRANSMITTER AND RECEIVER",
    "trust.mit":"MIT LICENCE","trust.gh":"ON GITHUB","trust.noacc":"NO ACCOUNT","trust.nocloud":"NO CLOUD RELAY","trust.plats":"ANDROID · WINDOWS",
    "setup.tag":"01 — SETUP",
    "setup.h2":'From phone to PC <span class="gr">in a few steps.</span>',
    "setup.sub":"Install the Android app and Windows receiver, connect them on the same trusted network, then choose an audio output.",
    "su1.h":"Install the Android app",
    "su1.p":"Install PocketMic on your phone and grant microphone permission when you start a session.",
    "su1.t":"PocketMic · Android","su1.s":"ANDROID APP · MICROPHONE",
    "su2.h":"Connect over your LAN",
    "su2.p":"Start the Windows receiver and pair the Android transmitter by QR code or manual IPv4 entry. Keep both devices on a trusted private network.",
    "su2.ready":"READY TO STREAM","su2.btn":"START MICROPHONE","su2.n":"MIC PERMISSION · ASKED ONCE",
    "su3.h":"Route audio to your app",
    "su3.p":"Select the desired Windows playback device in the receiver. For calls or recording apps, route that output through VB-CABLE or another virtual audio device.",
    "su3.bar":"SOUND SETTINGS","su3.in":"Input device","su3.opt1":"CABLE Input","su3.opt2":"Speakers","su3.opt4":"Line In",
    "lab.tag":"02 — SIGNAL LAB",
    "lab.h2":'See your signal <span class="ph">move.</span>',
    "lab.sub":"An isolated browser demo. After you grant permission, its level meter reads your chosen microphone; the generated sample stays on this page. The moving link and packet-rate readout are illustrative: nothing is sent to PocketMic and no network audio is measured.",
    "console.title":"POCKETMIC — LOCAL AUDIO DEMO","console.link":"LOCAL VISUALIZATION · NO NETWORK","console.input":"LOCAL MIC INPUT",
    "lab.foot":"This browser demo reads only the microphone you choose here or generates a local sample. It does not connect to PocketMic or measure network audio.",
    "why.tag":"03 — WHY",
    "why.h2":'Built like a tool, <span class="gr">not a subscription.</span>',
    "feat1.h":"A native Android transmitter",
    "feat1.p":"PocketMic captures audio in its Android app and sends it to the Windows receiver over your local network. The phone needs the app and microphone permission.",
    "feat2.h":"PocketMic sends directly over your LAN",
    "feat2.p":"The phone and computer use encrypted UDP on your local network; internet access is not required for PocketMic audio. A calling or recording app you route into may process or transmit that audio under its own service and privacy terms.",
    "feat3.h":"Choose a Windows audio route",
    "feat3.p":"The receiver plays to a selected Windows device. To feed a call or recording app, select a virtual audio cable such as VB-CABLE, then choose its input in that app.",
    "feat4.h":"Designed for a private LAN",
    "feat4.p":"The phone and PC communicate directly with encrypted audio packets. Internet access is not needed for audio transport; use a trusted private network and do not expose the receiver to the public internet.",
    "arch.tag":"04 — ARCHITECTURE",
    "arch.h2":'The whole path, <span class="gr">on your network.</span>',
    "arch.sub":"The Android transmitter sends encrypted UDP audio over your LAN to the Windows receiver. The receiver plays the stream to a selected Windows audio device. Internet access is not required for the audio path.",
    "arch.l1k":"PHONE","arch.l1v":"ANDROID APP · MIC CAPTURE",
    "arch.l2k":"LINK","arch.l2v":"PCM / OPUS · UDP · LAN",
    "arch.l3k":"RECEIVER","arch.l3v":"48 KHZ DECODE · JITTER BUFFER",
    "arch.l4k":"OUTPUT","arch.l4v":"SELECTED WINDOWS DEVICE",
    "arch.cta":"VIEW THE SOURCE",
    "lat.tag":"05 — LATENCY",
    "lat.h2":'Audio facts, <span class="gr">without guessed latency.</span>',
    "lat.sub":"No dated, reproducible end-to-end latency result is established for the current release candidate. The listed values describe protocol framing and receiver configuration.",
    "lat.label":"EXPLORE LATENCY",
    "lat.p1":"EXCELLENT WI-FI","lat.p2":"TYPICAL WI-FI","lat.p3":"BUSY CAFÉ",
    "lat.methodQ":"HOW WERE THESE NUMBERS MEASURED?",
    "lat.methodA":"No physical-device end-to-end latency result is established for this release candidate. Protocol frame duration and receiver buffer settings do not add up to a measured latency value.",
    "lat.bar1":"USB MIC (WIRED)","lat.bar2":"POCKETMIC · WI-FI","lat.bar3":"BLUETOOTH HEADSET",
    "lat.footA":"Bar length uses a square-root scale — at true scale, the USB bar would be a hair. At 30 fps, that’s",
    "lat.footB":"of lip-sync error, respectively.",
    "lat.note":"Actual results depend on device, network conditions, and buffering. Treat the published PCM range as a reference, not a guarantee.",
    "dl.tag":"FREE · MIT · NO ACCOUNT",
    "dl.h2":'Get the Android app <span>and Windows receiver.</span>',
    "dl.all":"ALL RELEASES",
    "faq.tag":"06 — FAQ",
    "faq.h2":'Fair questions, <span class="gr">straight answers.</span>',
    "faq.q1":"Does PocketMic send audio through a server?",
    "faq.a1":"No. PocketMic sends audio directly from the phone to the computer over local-network UDP, with no account or cloud relay. If you route audio into an online call or recording app, that app may process or transmit it under its own policy.",
    "faq.q2":"Do I have to install an app on my phone?",
    "faq.a2":"Yes. PocketMic includes an Android transmitter app. Install it on the phone and install the Windows receiver on the PC.",
    "faq.q3":"Why not just use a Bluetooth headset?",
    "faq.a3":"PocketMic uses your local network instead of Bluetooth. Actual end-to-end latency has not been established for this release candidate; the protocol and receiver settings above are not a physical-device benchmark.",
    "faq.q4":"What happens when my phone’s screen turns off?",
    "faq.a4":"PocketMic uses an Android foreground service for microphone capture. Background and battery behaviour can vary by Android version and phone manufacturer; follow the in-app battery guidance for your device.",
    "faq.q5":"Which platforms does the receiver support?",
    "faq.a5":"Windows is the current published receiver. An unsigned macOS receiver preview is in v0.1.6 manual testing and supports PCM v1 only. Android is the published transmitter; the iOS PCM publisher is experimental. Linux receiver support is planned for a later release.",
    "faq.q6":"Is it really open source?",
    "faq.a6":"The source for the Android app and desktop receivers is MIT-licensed and on <a href=\"https://github.com/ryanspice/pocketmic-lan\" target=\"_blank\" rel=\"noopener\">GitHub</a>. Open source is not a security certification. Report a reproducible issue or suggest a change.",
    "coffee.tag":"07 — OPEN SOURCE",
    "coffee.h2a":"Free, forever.","coffee.h2span":"Coffee, occasionally.",
    "coffee.lead":"PocketMic is MIT-licensed, ad-free, account-free and price-free. If it ends up on every call you take, a coffee is the entire business model — and it keeps the commits coming.",
    "coffee.cta1":"BUY ME A COFFEE","coffee.cta2":"VIEW SOURCE ON GITHUB",
    "coffee.sponsor":"OR SPONSOR THE WORK &rarr;",
    "oss.a":"READ THE SOURCE","oss.b":"OPEN AN ISSUE","oss.c":"BROWSE CONTRIBUTIONS",
    "foot.left":"POCKETMIC — FREE SOFTWARE UNDER THE MIT LICENCE",
    "foot.rel":"RELEASES","foot.bmc":"BUY ME A COFFEE",
    "foot.right":"A CANOPY DIGITAL PROJECT"
  },
  aria:{
    "aria.brand":"PocketMic home","aria.gh":"GitHub repository","aria.lang":"Language",
    "aria.trust":"At a glance","aria.latpre":"Microphone options comparison",
    "aria.stage":"Illustration of the PocketMic Android transmitter sending audio to the Windows receiver.",
    "aria.fig":"Diagram: the Android app sends encrypted UDP audio across the local network to the Windows receiver and a selected audio device.",
    "aria.cup":"A cup of coffee",
    "aria.txMeter":"Local microphone level demo",
    "aria.linkCv":"Decorative animation in the local audio demo",
    "aria.rxScope":"Local sample waveform demo"
  },
  dyn:{
    standby:"STANDBY",live:"LIVE · LOCAL",sim:"SIMULATED",
    mic:"USE THIS DEVICE’S MIC",micStop:"DISCONNECT MIC",
    simB:"SIMULATED VOICE",simStop:"STOP SIMULATION",
    mute:"MUTE",unmute:"UNMUTE",
    noteIdleD:"LOCAL BROWSER DEMO — NO CONNECTION TO POCKETMIC.",
    noteIdleM:"LOCAL BROWSER DEMO — NO CONNECTION TO POCKETMIC.",
    noteLiveD:"LOCAL MICROPHONE INPUT — NOT SENT TO THE WINDOWS RECEIVER.",
    noteLiveM:"LOCAL MICROPHONE INPUT — NOT SENT OVER WI-FI.",
    noteSim:"GENERATED LOCAL SAMPLE — NOT A POCKETMIC STREAM.",
    noteMuted:"LOCAL BROWSER DEMO MUTED — NO AUDIO IS SENT TO POCKETMIC.",
    noteNoApi:"MICROPHONE API UNAVAILABLE — TRY THE GENERATED LOCAL SAMPLE.",
    noteDenied:"MIC ACCESS DENIED ({e}) — TRY THE GENERATED LOCAL SAMPLE.",
    rec:"LOCAL",pkts:"FRAMES",frames:"frames",typical:"TYPICAL",
    dlWin:"WINDOWS RECEIVER · v0.1.5",dlAndroid:"ANDROID APK · v0.1.5",dlAny:"VIEW RELEASES",
    subWin:"CURRENT PUBLISHED RELEASE · v0.1.5",subAndroid:"CURRENT PUBLISHED RELEASE · v0.1.5",subAny:"ANDROID + WINDOWS · RELEASES",
    localOnly:"LOCAL DEMO ONLY",
    themeAuto:"Theme: follow your system",themeLight:"Theme: light",themeDark:"Theme: dark"
  }
}
};

var lang = 'en';
var T = I18N[lang].dyn;
var nf1 = new Intl.NumberFormat(lang === 'fr' ? 'fr-FR' : 'en-US', {minimumFractionDigits:1, maximumFractionDigits:1});

/* =====================================================================
   js/theme.js — auto / light / dark, persisted, no-flash
   ===================================================================== */
var themeBtn = $('#themeBtn');
var thmIcons = { auto: $('#icoAuto'), light: $('#icoLight'), dark: $('#icoDark') };
var themePref = 'auto';
try{
  var tp = localStorage.getItem('pm-theme');
  if(tp === 'light' || tp === 'dark' || tp === 'auto'){ themePref = tp; }
}catch(e){}
var mqDark = matchMedia('(prefers-color-scheme: dark)');
function resolvedDark(){ return themePref === 'dark' || (themePref === 'auto' && mqDark.matches); }
function applyTheme(){
  var dark = resolvedDark();
  document.documentElement.setAttribute('data-theme', dark ? 'dark' : 'light');
  var meta = document.querySelector('meta[name="theme-color"]');
  if(meta){ meta.setAttribute('content', dark ? '#10130A' : '#F4F2E9'); }
  ['auto','light','dark'].forEach(function(k){ if(thmIcons[k]){ thmIcons[k].hidden = (k !== themePref); } });
  var label = themePref === 'auto' ? T.themeAuto : (themePref === 'light' ? T.themeLight : T.themeDark);
  if(themeBtn){ themeBtn.setAttribute('aria-label', label); themeBtn.setAttribute('title', label); }
}
if(themeBtn){
  themeBtn.addEventListener('click', function(){
    themePref = themePref === 'auto' ? 'light' : (themePref === 'light' ? 'dark' : 'auto');
    try{ localStorage.setItem('pm-theme', themePref); }catch(e){}
    applyTheme();
  });
}
if(mqDark.addEventListener){ mqDark.addEventListener('change', function(){ if(themePref === 'auto') applyTheme(); }); }
else if(mqDark.addListener){ mqDark.addListener(function(){ if(themePref === 'auto') applyTheme(); }); }

/* ---------- apply translations ---------- */
function applyI18n(){
  var D = I18N[lang];
  document.documentElement.lang = 'en-CA';
  document.title = D.title;
  var md = document.querySelector('meta[name="description"]');
  if(md){ md.setAttribute('content', D.desc); }
  $$('[data-i18n]').forEach(function(el){
    var k = el.getAttribute('data-i18n');
    if(Object.prototype.hasOwnProperty.call(D.html, k)){ el.innerHTML = D.html[k]; }
  });
  $$('[data-i18n-aria]').forEach(function(el){
    var k = el.getAttribute('data-i18n-aria');
    if(Object.prototype.hasOwnProperty.call(D.aria, k)){ el.setAttribute('aria-label', D.aria[k]); }
  });
  T = D.dyn;
  nf1 = new Intl.NumberFormat(lang === 'fr' ? 'fr-FR' : 'en-US', {minimumFractionDigits:1, maximumFractionDigits:1});
paintCTA();
  refreshDemoUI();

  applyTheme();
}
/* ---------- reveal + scrollspy ---------- */
var io = new IntersectionObserver(function(es){
  es.forEach(function(e){ if(e.isIntersecting){ e.target.classList.add('in'); io.unobserve(e.target); } });
},{threshold:.12});
 $$('.rv').forEach(function(el){ io.observe(el); });

var navLinks = $$('.site-nav a');
var spy = new IntersectionObserver(function(es){
  es.forEach(function(e){
    if(e.isIntersecting){
      var id = '#' + e.target.id;
      navLinks.forEach(function(a){ a.classList.toggle('act', a.getAttribute('href') === id); });
    }
  });
},{rootMargin:'-45% 0px -50% 0px'});
['setup','lab','why','performance','faq'].forEach(function(id){
  var el = document.getElementById(id);
  if(el){ spy.observe(el); }
});

/* ---------- FAQ ---------- */
 $$('.qa-q').forEach(function(btn){
  btn.addEventListener('click', function(){
    var open = btn.closest('.qa').classList.toggle('open');
    btn.setAttribute('aria-expanded', String(open));
  });
});

/* =====================================================================
   Homepage CTAs use checked-in, versioned download pages.
   No GitHub API request or star-count request runs on page load.
   ===================================================================== */
function detectOS(){
  var ua = navigator.userAgent;
  if(/Windows/i.test(ua)){ return 'windows'; }
  if(/Android/i.test(ua)){ return 'android'; }
  return null;
}
var os = detectOS();

function paintCTA(){
  var label = os === 'windows' ? T.dlWin : os === 'android' ? T.dlAndroid : T.dlAny;
  var sub = os === 'windows' ? T.subWin : os === 'android' ? T.subAndroid : T.subAny;
  $('#heroCtaLbl').textContent = label;
  $('#dlBigLbl').textContent = label;
  $('#dlSub').textContent = sub;
  $('#heroCta').href = 'download/index.html';
  $('#dlBig').href = 'download/index.html';
  $('#headDl').href = 'download/index.html';
  $('#androidDl').href = 'download/android/index.html';
  $('#windowsDl').href = 'download/windows/index.html';
}
paintCTA();
/* =====================================================================
   js/demo.js — Signal Lab: real meters, honest labels
   ===================================================================== */
var cvM = $('#txMeter'), cvL = $('#linkCv'), cvS = $('#rxScope');
var lamp = $('#lamp'), txStatus = $('#txStatus'), txTime = $('#txTime');
var conMode = $('#conMode'), liveChip = $('#liveChip'), dbRead = $('#dbRead');
var latRead = $('#latRead'), pktRead = $('#pktRead'), note = $('#demoNote');
var micBtn = $('#micBtn'), simBtn = $('#simBtn'), muteBtn = $('#muteBtn');
var gainEl = $('#gain');

var mode = 'idle', muted = false, t0 = 0, raf = null, last = 0;
var actx = null, analyser = null, stream = null, td = null, gUser = null, gMute = null;
var lastDb = -90, dispDb = -90, gainV = 1;
var sim = { t:0, amp:0, target:0, next:0, buf:[] };
var txHist = new Array(140).fill(0);
var link = { P:null, packets:[], acc:0, count:0, countT:1 };
var dims = new Map();
var coarse = matchMedia('(pointer:coarse)').matches;
var RM = matchMedia('(prefers-reduced-motion: reduce)').matches;

function idleNote(){ return coarse ? T.noteIdleM : T.noteIdleD; }
function liveNote(){ return coarse ? T.noteLiveM : T.noteLiveD; }

function refreshDemoUI(){
  micBtn.textContent = (mode === 'live') ? T.micStop : T.mic;
  micBtn.setAttribute('aria-pressed', String(mode === 'live'));
  simBtn.textContent = (mode === 'sim') ? T.simStop : T.simB;
  simBtn.setAttribute('aria-pressed', String(mode === 'sim'));
  muteBtn.disabled = (mode === 'idle');
  muteBtn.textContent = muted ? T.unmute : T.mute;
  muteBtn.setAttribute('aria-pressed', String(muted));
  var st = (mode === 'live') ? T.live : (mode === 'sim') ? T.sim : T.standby;
  conMode.textContent = st;
  txStatus.textContent = st;
  lamp.classList.toggle('on', mode !== 'idle');
  liveChip.classList.toggle('off', mode === 'idle');
  /* typical value, clearly labeled — this page does not measure your network */
  latRead.textContent = T.localOnly;
  if(mode === 'idle'){
    note.textContent = idleNote();
    txTime.textContent = T.rec + ' 00:00';
    dbRead.textContent = '— dBFS';
    pktRead.textContent = T.pkts + ' 0 /s';
  }else{
    note.textContent = muted ? T.noteMuted : (mode === 'live' ? liveNote() : T.noteSim);
  }
}
function setMode(m){
  mode = m;
  if(m !== 'idle'){ t0 = performance.now(); dispDb = -90; muted = false; link.packets.length = 0; }
  refreshDemoUI();
  if(m === 'idle'){ drawIdle(); }
  ensureLoop();
}
function stopStream(){
  if(stream){ stream.getTracks().forEach(function(t){ t.stop(); }); stream = null; }
  if(actx){ actx.close().catch(function(){}); actx = null; analyser = null; td = null; gUser = null; gMute = null; }
}
function startLive(){
  if(!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia){
    setMode('sim');
    note.textContent = T.noteNoApi;
    return;
  }
  navigator.mediaDevices.getUserMedia({audio:{echoCancellation:false, noiseSuppression:false, autoGainControl:false}})
    .then(function(s){
      stopStream();
      stream = s;
      var AC = window.AudioContext || window.webkitAudioContext;
      if(!AC){ startSim(); return; }
      actx = new AC();
      if(actx.state === 'suspended'){ actx.resume(); }
      analyser = actx.createAnalyser();
      analyser.fftSize = 1024;
      td = new Uint8Array(analyser.fftSize);
      gUser = actx.createGain(); gUser.gain.value = gainV;
      gMute = actx.createGain(); gMute.gain.value = muted ? 0 : 1;
      actx.createMediaStreamSource(s).connect(gUser);
      gUser.connect(gMute);
      gMute.connect(analyser);
      setMode('live');
      note.textContent = liveNote();
    })
    .catch(function(err){
      startSim();
      note.textContent = T.noteDenied.replace('{e}', (err && err.name) || 'ERROR');
    });
}
function startSim(){
  stopStream();
  sim.t = 0; sim.amp = 0; sim.target = 0; sim.next = 0; sim.buf = [];
  setMode('sim');
  note.textContent = T.noteSim;
}
function stopAll(){ stopStream(); setMode('idle'); }

micBtn.addEventListener('click', function(){ (mode === 'live') ? stopAll() : startLive(); });
simBtn.addEventListener('click', function(){ (mode === 'sim') ? stopAll() : startSim(); });
muteBtn.addEventListener('click', function(){
  muted = !muted;
  if(gMute){ gMute.gain.value = muted ? 0 : 1; }
  muteBtn.textContent = muted ? T.unmute : T.mute;
  muteBtn.setAttribute('aria-pressed', String(muted));
  note.textContent = muted ? T.noteMuted : (mode === 'live' ? liveNote() : T.noteSim);
});
gainEl.addEventListener('input', function(){
  gainV = (+gainEl.value) / 100;
  if(gUser){ gUser.gain.value = gainV; }
});

function fit(cv){
  var r = cv.getBoundingClientRect();
  if(r.width < 4 || r.height < 4){ return; }
  var d = Math.min(2, devicePixelRatio || 1);
  cv.width = Math.round(r.width * d);
  cv.height = Math.round(r.height * d);
  var ctx = cv.getContext('2d');
  ctx.setTransform(d, 0, 0, d, 0, 0);
  dims.set(cv, { w:r.width, h:r.height, ctx:ctx });
  if(cv === cvL){ link.P = mkPath(r.width, r.height, r.height > r.width); }
}
function fitAll(){
  [cvM, cvL, cvS].forEach(fit);
  if(mode === 'idle'){ drawIdle(); }
}
var ro = new ResizeObserver(fitAll);
[cvM, cvL, cvS].forEach(function(c){ ro.observe(c); });

function mkPath(w, h, vert){
  return vert
    ? [{x:w*.55,y:2},{x:w*.02,y:h*.32},{x:w*.98,y:h*.68},{x:w*.45,y:h-2}]
    : [{x:2,y:h*.62},{x:w*.32,y:h*.05},{x:w*.68,y:h*.95},{x:w-2,y:h*.42}];
}
function pt(t){
  var P = link.P, u = 1 - t;
  return {
    x: u*u*u*P[0].x + 3*u*u*t*P[1].x + 3*u*t*t*P[2].x + t*t*t*P[3].x,
    y: u*u*u*P[0].y + 3*u*u*t*P[1].y + 3*u*t*t*P[2].y + t*t*t*P[3].y
  };
}
function currentLevel(){
  if(mode === 'live' && analyser){
    analyser.getByteTimeDomainData(td);
    var s = 0;
    for(var i = 0; i < td.length; i++){ var v = (td[i] - 128) / 128; s += v * v; }
    lastDb = clamp(20 * Math.log10(Math.sqrt(s / td.length) + 1e-7), -90, 0);
    return clamp((lastDb + 60) / 60, 0, 1);
  }
  if(mode === 'sim'){
    var m = muted ? 0 : 1, g = gainV, N = 512, s2 = 0;
    for(var j = 0; j < N; j++){
      sim.t += 1 / 6000;
      if(sim.t > sim.next){
        sim.target = Math.random() < .22 ? .04 : .3 + Math.random() * .7;
        sim.next = sim.t + .12 + Math.random() * .35;
      }
      sim.amp += (sim.target - sim.amp) * .08;
      var w = sim.amp * m * g * ( .62 * Math.sin(sim.t * Math.PI * 2 * 132)
        + .27 * Math.sin(sim.t * Math.PI * 2 * 264 + 1.7)
        + .16 * Math.sin(sim.t * Math.PI * 2 * 88 + .4)
        + .2 * (Math.random() * 2 - 1) );
      s2 += w * w;
      if((j & 63) === 0){ sim.buf.push(clamp(w, -1, 1)); if(sim.buf.length > 240){ sim.buf.shift(); } }
    }
    lastDb = clamp(20 * Math.log10(Math.sqrt(s2 / N) + 1e-7), -90, 0);
    return clamp((lastDb + 60) / 60, 0, 1);
  }
  lastDb = -90;
  return 0;
}
function drawTx(lvl){
  var d = dims.get(cvM);
  if(!d){ return; }
  var ctx = d.ctx, w = d.w, h = d.h;
  ctx.clearRect(0, 0, w, h);
  ctx.strokeStyle = 'rgba(241,239,226,.12)';
  ctx.lineWidth = 1;
  ctx.beginPath(); ctx.moveTo(0, h / 2 + .5); ctx.lineTo(w, h / 2 + .5); ctx.stroke();
  txHist.push(lvl); txHist.shift();
  var n = txHist.length, bw = w / n;
  for(var i = 0; i < n; i++){
    var v = txHist[i];
    var bh = Math.max(1, v * h * .92);
    ctx.fillStyle = v > .02 ? 'rgba(62,229,134,' + (.35 + v * .65).toFixed(2) + ')' : 'rgba(241,239,226,.14)';
    ctx.fillRect(i * bw, (h - bh) / 2, Math.max(1, bw * .66), bh);
  }
}
function drawLink(dt, lvl){
  var d = dims.get(cvL);
  if(!d || !link.P){ return; }
  var ctx = d.ctx;
  ctx.clearRect(0, 0, d.w, d.h);
  ctx.strokeStyle = 'rgba(241,239,226,.18)';
  ctx.lineWidth = 1;
  ctx.setLineDash([4, 6]);
  ctx.beginPath();
  ctx.moveTo(link.P[0].x, link.P[0].y);
  ctx.bezierCurveTo(link.P[1].x, link.P[1].y, link.P[2].x, link.P[2].y, link.P[3].x, link.P[3].y);
  ctx.stroke();
  ctx.setLineDash([]);
  if(mode === 'idle'){ return; }
  var rate = 8 + lvl * 44;
  link.acc += dt * rate;
  while(link.acc >= 1){
    link.acc--;
    link.packets.push({ t:0, sp:.55 + Math.random() * .3 });
    link.count++;
  }
  for(var i = link.packets.length - 1; i >= 0; i--){
    var p = link.packets[i];
    p.t += p.sp * dt;
    if(p.t >= 1){ link.packets.splice(i, 1); continue; }
    var a = pt(p.t);
    ctx.fillStyle = 'rgba(62,229,134,.28)';
    ctx.beginPath(); ctx.arc(a.x, a.y, 4.4, 0, 7); ctx.fill();
    ctx.fillStyle = '#3EE586';
    ctx.beginPath(); ctx.arc(a.x, a.y, 2.2, 0, 7); ctx.fill();
  }
}
function drawRx(){
  var d = dims.get(cvS);
  if(!d){ return; }
  var ctx = d.ctx, w = d.w, h = d.h;
  ctx.clearRect(0, 0, w, h);
  ctx.strokeStyle = 'rgba(241,239,226,.1)';
  ctx.lineWidth = 1;
  ctx.beginPath(); ctx.moveTo(0, h / 2 + .5); ctx.lineTo(w, h / 2 + .5); ctx.stroke();
  ctx.beginPath();
  var started = false;
  if(mode === 'live' && analyser && td){
    var N = td.length, step = Math.max(1, Math.floor(N / w));
    for(var x = 0; x < w; x++){
      var v = (td[Math.min(N - 1, x * step)] - 128) / 128;
      var y = h / 2 - clamp(v, -1, 1) * h * .42;
      if(started){ ctx.lineTo(x, y); } else { ctx.moveTo(x, y); started = true; }
    }
  } else if(mode === 'sim' && sim.buf.length > 1){
    var B = sim.buf, n = B.length;
    for(var x2 = 0; x2 < w; x2++){
      var v2 = B[Math.floor(x2 / w * n)];
      var y2 = h / 2 - clamp(v2, -1, 1) * h * .42;
      if(started){ ctx.lineTo(x2, y2); } else { ctx.moveTo(x2, y2); started = true; }
    }
  } else {
    ctx.moveTo(0, h / 2); ctx.lineTo(w, h / 2);
  }
  ctx.strokeStyle = (mode === 'idle') ? 'rgba(241,239,226,.3)' : '#3EE586';
  ctx.lineWidth = 1.6;
  ctx.lineJoin = 'round';
  ctx.stroke();
}
function drawIdle(){ drawTx(0); drawLink(0, 0); drawRx(); }
function ensureLoop(){
  if(raf == null){
    last = performance.now();
    raf = requestAnimationFrame(tick);
  }
}
function tick(now){
  raf = null;
  var dt = Math.min(.05, Math.max(0, (now - last) / 1000));
  last = now;
  var lvl = currentLevel();
  dispDb += (lastDb - dispDb) * .18;
  if(mode !== 'idle'){
    var el = (now - t0) / 1000;
    var mm = String(Math.floor(el / 60)).padStart(2, '0');
    var ss = String(Math.floor(el % 60)).padStart(2, '0');
    txTime.textContent = T.rec + ' ' + mm + ':' + ss;
    dbRead.textContent = nf1.format(clamp(dispDb, -90, 0)) + ' dBFS';
    link.countT -= dt;
    if(link.countT <= 0){
      pktRead.textContent = T.pkts + ' ' + link.count + ' /s';
      link.count = 0;
      link.countT = 1;
    }
  }
  drawTx(lvl);
  drawLink(dt, lvl);
  drawRx();
  if(mode !== 'idle'){ raf = requestAnimationFrame(tick); }
}
document.addEventListener('visibilitychange', function(){
  if(document.hidden){
    if(raf != null){ cancelAnimationFrame(raf); raf = null; }
  } else if(mode !== 'idle'){
    ensureLoop();
  }
});

applyI18n();

})();

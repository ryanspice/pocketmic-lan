/* No network calls, tracking pixels, media access, cookies, or dependencies.
 * Events describe website interactions only, never a verified install/connection. */
(() => {
  'use strict';
  document.documentElement.classList.add('js');
  const body = document.body;
  const root = new URL(body.dataset.root || './', document.baseURI);
  const qs = new URLSearchParams(location.search);
  const validUses = new Set(['discord', 'obs', 'meetings', 'general']);
  const intent = validUses.has(qs.get('use')) ? qs.get('use') : 'general';
  const utmKeys = ['utm_source', 'utm_medium', 'utm_campaign', 'utm_content'];
  const campaign = {};
  for (const key of utmKeys) {
    const value = qs.get(key);
    if (value && /^[a-zA-Z0-9_-]{1,80}$/.test(value)) campaign[key] = value;
  }
  function emit(event, data = {}) {
    // Local DOM event only. A consent-reviewed first-party adapter can subscribe.
    window.dispatchEvent(new CustomEvent('pocketmic:conversion', {
      detail: { event, page: body.dataset.page, use: intent, ...campaign, ...data }
    }));
  }
  function toast(message) {
    const status = document.getElementById('status-message');
    if (!status) return;
    status.textContent = message;
    clearTimeout(toast.timer);
    toast.timer = setTimeout(() => { status.textContent = ''; }, 5500);
  }
  const toggle = document.querySelector('.nav-toggle');
  const nav = document.getElementById('primary-nav');
  function closeNav(restoreFocus = false) {
    if (!toggle || !nav) return;
    nav.classList.remove('is-open');
    toggle.setAttribute('aria-expanded', 'false');
    toggle.setAttribute('aria-label', 'Open navigation');
    if (restoreFocus) toggle.focus();
  }
  toggle?.addEventListener('click', () => {
    const open = toggle.getAttribute('aria-expanded') !== 'true';
    toggle.setAttribute('aria-expanded', String(open));
    toggle.setAttribute('aria-label', open ? 'Close navigation' : 'Open navigation');
    nav?.classList.toggle('is-open', open);
  });
  nav?.addEventListener('click', (event) => {
    if (event.target.closest('a')) closeNav();
  });
  document.addEventListener('keydown', (event) => {
    if (event.key === 'Escape' && toggle?.getAttribute('aria-expanded') === 'true') closeNav(true);
  });
  matchMedia('(min-width: 701px)').addEventListener('change', () => closeNav());

  // Campaign values remain in the URL only. They are never stored or sent by JS.
  document.querySelectorAll('a[href]').forEach((anchor) => {
    const raw = anchor.getAttribute('href');
    if (!raw || raw.startsWith('#') || anchor.hasAttribute('download')) return;
    let url;
    try { url = new URL(raw, document.baseURI); } catch { return; }
    if (url.origin !== root.origin || !url.pathname.startsWith(root.pathname)) return;
    if (/\.(png|webp|svg|zip|apk|json|txt|css|js)$/i.test(url.pathname)) return;
    for (const [key, value] of Object.entries(campaign)) {
      if (!url.searchParams.has(key)) url.searchParams.set(key, value);
    }
    if (intent !== 'general' && !url.searchParams.has('use')) url.searchParams.set('use', intent);
    anchor.href = url.href;
  });
  document.addEventListener('click', (event) => {
    const anchor = event.target.closest('a[data-event]');
    if (!anchor) return;
    emit(anchor.dataset.event, { target: anchor.dataset.target || '' });
  });

  // Checklist persistence is functional, session-only, and written on interaction.
  const checks = [...document.querySelectorAll('input[data-setup-step]')];
  const storeKey = 'pocketmic-setup-v2';
  const allowedIds = new Set(checks.map((input) => input.dataset.setupStep));
  let completed = [];
  try {
    const parsed = JSON.parse(sessionStorage.getItem(storeKey) || '[]');
    if (Array.isArray(parsed)) completed = parsed.filter((id) => allowedIds.has(id));
  } catch { /* Storage can be blocked. The checklist still works in memory. */ }
  checks.forEach((input) => { input.checked = completed.includes(input.dataset.setupStep); });
  function updateProgress(save = false) {
    const count = checks.filter((input) => input.checked).length;
    const progress = document.getElementById('setup-progress');
    const label = document.getElementById('setup-progress-label');
    if (progress) progress.value = count;
    if (label) label.textContent = `${count} of ${checks.length} steps checked by you`;
    if (save) {
      try { sessionStorage.setItem(storeKey, JSON.stringify(checks.filter((i) => i.checked).map((i) => i.dataset.setupStep))); } catch { /* Optional persistence. */ }
    }
  }
  checks.forEach((input) => input.addEventListener('change', () => {
    updateProgress(true);
    emit('setup_step_check', { step: input.dataset.setupStep, checked: input.checked });
  }));
  document.getElementById('reset-checklist')?.addEventListener('click', () => {
    checks.forEach((input) => { input.checked = false; });
    try { sessionStorage.removeItem(storeKey); } catch { /* Optional storage. */ }
    updateProgress();
    toast('Checklist reset. No app settings were changed.');
  });
  updateProgress();

  const select = document.getElementById('destination');
  function chooseDestination(value) {
    if (!validUses.has(value)) value = 'general';
    document.querySelectorAll('[data-destination]').forEach((detail) => {
      const selected = detail.dataset.destination === value;
      detail.classList.toggle('is-selected', selected);
      if (selected) detail.open = true;
    });
    const continueLink = document.getElementById('setup-done');
    if (continueLink) {
      const url = new URL(continueLink.href);
      url.searchParams.set('use', value);
      continueLink.href = url.href;
    }
  }
  if (select) {
    select.value = intent;
    chooseDestination(intent);
    select.addEventListener('change', () => chooseDestination(select.value));
  }
  document.getElementById('setup-done')?.addEventListener('click', () => {
    emit('setup_self_reported', { completedSteps: checks.filter((input) => input.checked).length, destination: select?.value || intent });
  });

  document.querySelectorAll('[data-copy]').forEach((button) => {
    button.addEventListener('click', async () => {
      const target = document.getElementById(button.dataset.copy);
      if (!target) return;
      const text = target.value ?? target.textContent;
      try {
        if (!navigator.clipboard?.writeText) throw new Error('Clipboard unavailable');
        await navigator.clipboard.writeText(text.trim());
        toast('Copied to clipboard.');
      } catch {
        if (typeof target.select === 'function') target.select();
        else {
          const range = document.createRange();
          range.selectNodeContents(target);
          const selection = window.getSelection();
          selection?.removeAllRanges();
          selection?.addRange(range);
        }
        toast('Text selected. Press Ctrl+C to copy.');
      }
    });
  });
  function revealHashTarget() {
    if (!location.hash) return;
    let id;
    try { id = decodeURIComponent(location.hash.slice(1)); } catch { return; }
    const target = document.getElementById(id);
    if (target?.tagName === 'DETAILS') target.open = true;
  }
  revealHashTarget();
  window.addEventListener('hashchange', revealHashTarget);

  const modal = document.getElementById('screenshot-dialog');
  if (modal && typeof modal.showModal === 'function') {
    let opener;
    document.querySelectorAll('[data-image-preview]').forEach((link) => {
      link.addEventListener('click', (event) => {
        event.preventDefault();
        opener = link;
        const image = modal.querySelector('img');
        image.src = link.href;
        image.alt = link.querySelector('img')?.alt || 'PocketMic app screenshot';
        modal.showModal();
      });
    });
    modal.querySelector('[data-close-dialog]')?.addEventListener('click', () => modal.close());
    modal.addEventListener('close', () => opener?.focus());
    modal.addEventListener('click', (event) => { if (event.target === modal) modal.close(); });
  }
})();

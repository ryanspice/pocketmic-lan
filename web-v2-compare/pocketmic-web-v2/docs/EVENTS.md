# Local conversion event contract

There is no analytics backend in this package. These events are local browser `CustomEvent` objects and do not leave the page unless the operator later adds a reviewed adapter. This avoids adding a tracker while claiming there is no tracking.

```javascript
window.addEventListener('pocketmic:conversion', (event) => {
  // Inspect locally while developing. Do not add a network collector implicitly.
  console.debug(event.detail);
});
```

| Event | Meaning | Additional fields |
| --- | --- | --- |
| `cta_click` | A marked website link was clicked | `target` |
| `download_click` | A version-pinned release link was clicked | `target` |
| `setup_step_check` | The visitor checked or unchecked a checklist item | `step`, `checked` |
| `setup_self_reported` | The visitor selected “I can hear it in my app” | `completedSteps`, `destination` |

Common fields: `event`, page key, allowlisted `use` intent, and valid `utm_source`, `utm_medium`, `utm_campaign`, `utm_content` parameters. Campaign values must match `[a-zA-Z0-9_-]{1,80}`; malformed, too-long or unrelated values are not propagated. Do not insert emails, identifiers, device addresses or pairing secrets into campaign URLs.

The `use` field reflects the page-entry intent. On the final confirmation event, `destination` reflects the selector’s current choice. No download completion, installation, native session, microphone stream, sound quality, cross-device identity or verified activation is detected.

Checklist state is saved in `sessionStorage` under `pocketmic-setup-v2`, only after interaction. It contains only the allowed step identifiers. Storage failure does not block the in-page checklist. Reset removes the state. Standard site JavaScript does not use cookies, localStorage, fetch, XHR, beacons, microphone permissions or third-party tags.

A future analytics integration needs a separate decision on purpose, event minimization, consent where appropriate, retention, operator policy and hosting behavior. Source-level instrumentation alone does not constitute a conversion report. Do not label a download click as an install or the self-reported event as device verification.

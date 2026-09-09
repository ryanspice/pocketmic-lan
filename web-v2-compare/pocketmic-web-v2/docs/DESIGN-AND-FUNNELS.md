# Design and funnel decisions

## Position

**Your phone. Your next PC mic.** The original material supports three main use cases: Discord / gaming, streaming / OBS, and meetings. The redesign treats those as distinct intentions rather than placing protocol mechanics ahead of the user’s goal.

Priority is a design hypothesis, not audience research: lead with Android/Windows users willing to install an APK and a separate virtual audio driver. Gamers with headphones and creators have a concrete reason to experiment. Meetings remain supported as a secondary evaluation path, with a test-before-call warning and a known-working fallback. Developers get the technical route and source links.

## Visual system

A charcoal-green surface and restrained mint accent echo the actual Android app. Oversized, short headings establish the job quickly. Warm light panels clarify the tricky audio route; the rest stays dark so the real white Windows receiver remains readable instead of being disguised as a redesigned native app.

The screenshots are original captures from the supplied archive, converted to WebP. They show idle / setup states. Device framing and decorative wave bars are presentation, not evidence of a live session, sound test or measured performance.

No stock-photo dependency, remote font, animated background, canvas engine, fake user count, testimonial, star rating or donation widget was added. Static decorative graphics keep motion and repaint work low. Reduced-motion rules, semantic landmarks, skip links, native details, form labels, focus treatments and explicit control states are included; this is not a claim of complete WCAG certification.

## Route inventory

| Route | Purpose | Primary next action |
| --- | --- | --- |
| `/` | Explain Android → Windows and show both native apps | Get the apps |
| `/get-started/` | Optional intent chooser | Choose Discord, OBS, meetings or a general setup |
| `/use-cases/discord/` | Headphones without a suitable microphone | Download both apps; use Discord’s mic test |
| `/use-cases/obs/` | Another input in a recording or streaming scene | Download; route; record a short sample |
| `/use-cases/meetings/` | Evaluate a calling microphone | Check requirements and permissions; test first |
| `/download/` | Explain both downloads and the separate driver | Pick Android / Windows instructions |
| `/download/android/` | APK, debug-signing status, pinned checksum and install sequence | Get the receiver / follow setup |
| `/download/windows/` | x64 ZIP, self-contained packaging, checksum and extraction | Get Android / follow setup |
| `/setup/` | Six-step checklist and app-specific routing | User-confirmed sound check or troubleshooting |
| `/setup/ready/` | Listen, confirm destination behavior and offer feedback | Report a concrete issue; optionally support |
| `/troubleshooting/` | Diagnose the local path one segment at a time | Return to setup / report issue |
| `/technical/` | Transport, capture limits, security boundary, build instructions | Inspect pinned source / download |
| `/release-notes/` | Accurate artifact and version description | Download v0.1.4 / view releases |
| `/privacy/` | Draft website and native-app privacy boundaries | External project contact |
| `/policy.html` | Preserve the old policy URL; canonical points to `/privacy/` | Privacy information |

`/setup/ready/` and the old policy alias are noindex. Thirteen canonical route entries appear in the sitemap. The current primary CTA stays direct; the chooser does not create mandatory friction.

## Conversion behavior

Campaign links go to the page that delivers on their specific promise. A Discord post should not land on a generic packet-format section; an engineering post can go straight to the technical explanation.

Install is deliberately not inferred from a clicked link. There is no hidden downloader, OS fingerprinting, auto-download, email gate or fake “installation complete” page. The two-app requirement and VB-CABLE dependency remain visible before the outbound release link.

The setup progress label says “checked by you.” The final page follows an explicit user-confirmation link and never claims the site has received audio or detected the native applications. The full site works as normal HTML with optional local JavaScript enhancements.

## SEO and social continuity

Each page has its own title, description and canonical URL. Audience pages select a matching social image. Software structured data is used for the homepage, with no invented ratings or unsupported pricing offers. Other pages use WebPage data. Campaign variants canonicalize to the clean route.

This preserves the existing `/sites/pocketmic-lan/` base rather than inventing a new domain. A higher click-through rate or conversion lift has not been measured. The proposed improvement is the clarity and continuity of the path, not a numerical business result.

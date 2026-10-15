# Documentation map

- [Project overview, downloads and first connection](../README.md)
- [Product intent](../INTENT.md) and [repository working instructions](../AGENTS.md)
- [Builds, packaging and developer setup](development.md)
- [Verification commands and physical acceptance](testing.md)
- [Wire and control protocol](protocol.md)
- [Windows interface, Mica, activation and fallback](windows-ui.md)
- [Current roadmap and dispositions](plans/roadmap.md)
- [1.7.1 Apple development plan](plans/1.7.1-apple.md)
- [1.7.0 release record](releases/1.7.0/release.md) and [Mica investigation](releases/1.7.0/mica.md)
- [Version history](../CHANGELOG.md)
- [Historical plans, research and evidence](archive/0.1.x/README.md)

## Guides beside their implementation

- [Windows package instructions](../windows-receiver/PACKAGE-README.md) remain beside the publisher inputs.
- [Mac tester setup](../macos/README.md) and [tester report](../macos/TESTER-REPORT.md) remain beside the Apple preview.
- [Localization status](../localization/README.md) owns catalog readiness.

## What belongs where

Current guides describe supported workflows and implementation. Plans contain objectives, scope, acceptance criteria and unresolved decisions. Release records contain dated candidate-specific evidence. The archive preserves historical findings and proposals without making them active instructions. [.thoughts/current.md](../.thoughts/current.md) only helps the next session find those sources.

When moving a document, update local links and code comments that refer to it, then run `python tools/verify_docs.py` from the repository root. Do not rewrite tag-pinned external references: published Git tags preserve their original paths. Keep raw logs, local install receipts, backups and temporary build outputs out of tracked documentation; publish only redacted, useful evidence summaries.

The documentation checker validates local inline links/images and Markdown heading anchors in tracked and non-ignored Markdown files. It skips fenced examples and external URLs; reference-style links and live website availability require separate review.

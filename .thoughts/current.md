# Current continuation note

Checked: 2026-10-15. Base: `3ef192e` / `v1.7.0`; documentation cleanup follows that release; the owner has authorized committing/pushing it and consolidating merged branches. Verify current Git state before continuing. This note is navigation, not product or release authority.

## Current task

Documentation cleanup completed after the 1.7.0 release; committing/pushing to main and retiring already-merged branch names are authorized. Historical root plans/reviews moved to [the archive](../docs/archive/0.1.x/README.md); current sequencing is in [the roadmap](../docs/plans/roadmap.md), with [Apple 1.7.1](../docs/plans/1.7.1-apple.md) bounded separately. Application versions, installed apps and published tags are not changed by this pass.

Verification: all 20 relocations accounted for; archived bodies preserved after link rebasing/status notices; legacy verifier and manifest byte-identical; package documents unchanged. Documentation links, version alignment, protocol, receiver logic and website consistency checks passed. The site validator still targets v0.1.5 download metadata; it is not proof of a current 1.7.0 deployment. Local receipts are under ignored `release/docs-cleanup/`. No app rebuild or reinstall was needed for this text/tooling pass.

## Preserve these conclusions

- 1.7.0 release/build/package/install work completed. The [release record](../docs/releases/1.7.0/release.md) owns its identity and evidence. A cancelled queued continuation is not evidence of a failed release.
- The owner accepted the corrected Windows Mica appearance, persistent tray and Start activation. The earlier missing-XAML startup failure is resolved in that release; do not reopen it without new evidence.
- Physical audio quality/latency/sustained-session and Apple-device gates remain distinct from completed release work. Use [testing.md](../docs/testing.md).
- Legacy website trees, ignored version copies, `release/` receipts/backups and registered worktrees remain preserved. Inspect dependencies and dirty/unique work before any later cleanup.

## Next action

All remaining branch tips were verified as ancestors of main. Consolidation retires their names while preserving workspaces, including uncommitted historical material. Backup refs and working-copy receipts belong under ignored release/branch-consolidation/. Follow the owner's selected next task rather than automatically starting the Apple plan; website migration and worktree deletion remain separate scope.

Keep future updates concise: current commit/date, task, evidence links, blockers and next action. Do not copy conversations, credentials, local inventories or full logs here.

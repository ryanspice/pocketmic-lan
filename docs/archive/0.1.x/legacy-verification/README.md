# Retired 0.1.4 verification snapshot

The original [source checker](verify_source.py.txt) hard-codes version `0.1.4` and static source expectations. Its [checksum manifest](SOURCE_SHA256SUMS.txt) describes an older file layout. Both are preserved byte-for-byte from `3ef192e`; the checker is stored as text so it is not mistaken for a runnable current tool.

No current CI workflow invokes this checker. `tools/verify_source.py` now explains retirement and exits with code 2 instead of silently passing or reporting stale source mismatches. Do not regenerate this manifest to validate a new release.

Use [current verification guidance](../../../testing.md), `scripts/verify_release_versions.py`, protocol/receiver validators and the relevant platform builds/tests. To reproduce the old checker in context, inspect the historical Git tree; copying this text file into today's tree does not recreate its baseline.

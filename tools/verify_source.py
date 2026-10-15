#!/usr/bin/env python3
"""Retired entry point for the historical 0.1.4 source/hash checker."""

import sys


def main() -> int:
    print(
        "Retired: the 0.1.4 source/hash checker is historical, not a current release gate.\n"
        "See docs/archive/0.1.x/legacy-verification/README.md for the preserved snapshot.\n"
        "Use docs/testing.md, scripts/verify_release_versions.py and the relevant platform tests.",
        file=sys.stderr,
    )
    return 2


if __name__ == "__main__":
    raise SystemExit(main())

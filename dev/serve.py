#!/usr/bin/env python3
"""PocketMic LAN — local dev server launcher.
Usage: python3 dev/serve.py [v1|v2|v3] [port]
Default: v3 on port 8080"""
import http.server
import socketserver
import sys
import os

versions = {
    "v1": "dev/v1",
    "v2": "dev/v2",
    "dev/v3": "dev/v3",
    "v3": "dev/v3",
    "web": "web",
}

def main():
    version = sys.argv[1] if len(sys.argv) > 1 else "v3"
    port = int(sys.argv[2]) if len(sys.argv) > 2 else 8080

    if version not in versions:
        print(f"Unknown version: {version}")
        print(f"Available: {', '.join(versions.keys())}")
        sys.exit(1)

    root = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), versions[version])
    if not os.path.isdir(root):
        print(f"Directory not found: {root}")
        sys.exit(1)

    os.chdir(root)
    handler = http.server.SimpleHTTPRequestHandler

    # Serve .webp with correct MIME type
    handler.extensions_map[".webp"] = "image/webp"

    with socketserver.TCPServer(("127.0.0.1", port), handler) as httpd:
        file_count = sum(len(files) for _, _, files in os.walk(root))
        print(f"\n  PocketMic LAN — serving {version}")
        print(f"  Directory: {root}")
        print(f"  Files: {file_count}")
        print(f"  URL: http://127.0.0.1:{port}/")
        print(f"\n  Pages:")
        print(f"    Homepage:     http://127.0.0.1:{port}/")
        if os.path.isdir(os.path.join(root, "use-cases")):
            print(f"    Discord:      http://127.0.0.1:{port}/use-cases/discord/")
            print(f"    OBS:          http://127.0.0.1:{port}/use-cases/obs/")
            print(f"    Meetings:     http://127.0.0.1:{port}/use-cases/meetings/")
        if os.path.isdir(os.path.join(root, "download")):
            print(f"    Downloads:    http://127.0.0.1:{port}/download/")
        if os.path.isdir(os.path.join(root, "setup")):
            print(f"    Setup:        http://127.0.0.1:{port}/setup/")
        if os.path.isdir(os.path.join(root, "technical")):
            print(f"    Technical:    http://127.0.0.1:{port}/technical/")
        if os.path.isdir(os.path.join(root, "troubleshooting")):
            print(f"    Troubleshoot: http://127.0.0.1:{port}/troubleshooting/")
        print(f"\n  Press Ctrl+C to stop\n")
        try:
            httpd.serve_forever()
        except KeyboardInterrupt:
            print("\n  Stopped.")

if __name__ == "__main__":
    main()

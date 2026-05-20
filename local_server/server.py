"""
Simple HTTP file server for hot update testing.
Serves files from the Builds/HotUpdate/ directory.

Usage:
    python server.py [port] [version]

Default port: 8080
Default version: latest (auto-detect)
"""
import http.server
import os
import sys
import socketserver
import glob
import socket

# Configuration
PORT = int(sys.argv[1]) if len(sys.argv) > 1 else 8080
REQUESTED_VERSION = sys.argv[2] if len(sys.argv) > 2 else None

# Determine the HotUpdate directory
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
BUILDS_DIR = os.path.normpath(
    os.path.join(SCRIPT_DIR, "..", "Builds", "HotUpdate")
)

if not os.path.isdir(BUILDS_DIR):
    print(f"[ERROR] HotUpdate builds directory not found: {BUILDS_DIR}")
    print("Run a build first (Build menu or build.bat).")
    sys.exit(1)

# Find available versions (sorted by semantic version, newest first)
def _parse_version(dirname):
    """Parse 'v1.0.15' -> (1, 0, 15) for proper numeric sorting."""
    try:
        return tuple(int(x) for x in dirname.lstrip('v').split('.'))
    except (ValueError, AttributeError):
        return (0, 0, 0)

versions = sorted(
    [d for d in os.listdir(BUILDS_DIR) if os.path.isdir(os.path.join(BUILDS_DIR, d))],
    key=_parse_version,
    reverse=True
)

if not versions:
    print(f"[ERROR] No hot update versions found in: {BUILDS_DIR}")
    sys.exit(1)

# Select version
if REQUESTED_VERSION:
    if REQUESTED_VERSION in versions:
        SERVE_VERSION = REQUESTED_VERSION
    else:
        print(f"[ERROR] Version '{REQUESTED_VERSION}' not found.")
        print(f"Available versions: {', '.join(versions)}")
        sys.exit(1)
else:
    SERVE_VERSION = versions[0]  # Latest

SERVE_DIR = os.path.join(BUILDS_DIR, SERVE_VERSION)

# Find platform subdirectories
platforms = [
    d for d in os.listdir(SERVE_DIR)
    if os.path.isdir(os.path.join(SERVE_DIR, d))
]

# Resolve LAN IP for display (not localhost)
def _get_lan_ip():
    """Get the primary LAN IP address."""
    try:
        s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        s.settimeout(0)
        s.connect(('10.254.254.254', 1))
        ip = s.getsockname()[0]
        s.close()
        return ip
    except Exception:
        return 'localhost'

LAN_IP = _get_lan_ip()

print(f"[HotUpdate Server]")
print(f"  Version:    {SERVE_VERSION}")
print(f"  Platforms:  {', '.join(platforms) if platforms else '(root files)'}")
print(f"  Serving:    {SERVE_DIR}")
print(f"  LAN IP:     {LAN_IP}")
print(f"  URL:        http://{LAN_IP}:{PORT}")
print(f"  Manifest:   http://{LAN_IP}:{PORT}/manifest.json")
print(f"  Press Ctrl+C to stop.")
print()

os.chdir(SERVE_DIR)

# Enable CORS for Unity WebGL testing
class CORSRequestHandler(http.server.SimpleHTTPRequestHandler):
    def end_headers(self):
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Access-Control-Allow-Methods", "GET, OPTIONS")
        self.send_header("Access-Control-Allow-Headers", "*")
        super().end_headers()

    def log_message(self, format, *args):
        print(f"[{self.client_address[0]}] {format % args}")

    def translate_path(self, path):
        # First try the root SERVE_DIR
        root_path = super().translate_path(path)
        if os.path.exists(root_path):
            return root_path
        # If not found, try platform subdirectories
        for platform in platforms:
            platform_dir = os.path.join(SERVE_DIR, platform)
            candidate = os.path.join(platform_dir, path.lstrip('/'))
            if os.path.exists(candidate):
                return candidate
        return root_path

with socketserver.TCPServer(("", PORT), CORSRequestHandler) as httpd:
    try:
        httpd.serve_forever()
    except KeyboardInterrupt:
        print("\n[HotUpdate Server] Shutting down...")
        httpd.shutdown()

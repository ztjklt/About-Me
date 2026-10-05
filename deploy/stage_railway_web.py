"""Version WebGL asset URLs and expose the exact deployed build provenance."""

import argparse
import hashlib
import json
import re
from datetime import datetime, timezone
from urllib.parse import urlparse
from pathlib import Path


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("web_root", type=Path)
    parser.add_argument("--release-sha", required=True)
    parser.add_argument("--client-build-sha", required=True)
    parser.add_argument("--release-id", required=True)
    parser.add_argument("--game-url", required=True)
    args = parser.parse_args()
    if urlparse(args.game_url).scheme != 'wss' or not urlparse(args.game_url).hostname:
        raise SystemExit('Public game endpoint must use wss')
    if not re.fullmatch(r'[a-z0-9-]+', args.release_id):
        raise SystemExit('Invalid release ID')
    if any(not re.fullmatch(r"[0-9a-f]{40}", value)
           for value in (args.release_sha, args.client_build_sha)):
        raise SystemExit("release and client build SHAs must be complete commits")
    root = args.web_root.resolve()
    index = root / "index.html"
    html = index.read_text()
    # Use the current public pair before Unity reads application.absoluteURL.
    setup = "const publicReleaseUrl=new URL(location.href);publicReleaseUrl.searchParams.set('api',location.origin+'/api');publicReleaseUrl.searchParams.set('game'," + json.dumps(args.game_url) + ");history.replaceState(history.state,'',publicReleaseUrl);"
    html = html.replace('<script>', '<script>\n' + setup, 1)
    badge = '<a id="release-version" href="/version.json" target="_blank" style="position:fixed;bottom:6px;right:8px;z-index:100;color:#374151;background:#fff9;padding:3px 8px;border-radius:6px;font:11px system-ui;text-decoration:none">最新版 R11 · ' + args.release_sha[:7] + '</a>'
    html = html.replace('</body>', badge + '\n</body>', 1)
    if "aboutme-web-fingerprint" in html or (root / "version.json").exists():
        raise SystemExit("WebGL index has already been versioned")
    references = sorted(set(re.findall(r"Build/([\w.-]+)", html)))
    if len(references) < 4 or not any(name.endswith(".wasm") for name in references):
        raise SystemExit("WebGL index is missing expected Build assets")
    assets = {}
    digest = hashlib.sha256()
    for name in references:
        path = root / "Build" / name
        if not path.is_file() or path.is_symlink():
            raise SystemExit("missing or linked WebGL asset: " + name)
        content_hash = sha256(path)
        assets[name] = content_hash
        digest.update(name.encode() + b"\0" + bytes.fromhex(content_hash))
    fingerprint = digest.hexdigest()[:16]
    for name in references:
        html = html.replace("Build/" + name, "Build/" + name + "?v=" + fingerprint)
    if "</head>" not in html:
        raise SystemExit("WebGL index has no closing head")
    html = html.replace("</head>",
                        '<meta name="aboutme-release" content="' + args.release_sha + '">\n'
                        '<meta name="aboutme-client-build" content="' + args.client_build_sha + '">\n'
                        '<meta name="aboutme-web-fingerprint" content="' + fingerprint + '">\n'
                        '</head>', 1)
    index.write_text(html)
    manifest = {"releaseId": args.release_id, "protocolVersion": 5, "publishedAtUTC": datetime.now(timezone.utc).isoformat(), "releaseSourceCommit": args.release_sha,
                "clientBuildSourceCommit": args.client_build_sha,
                "webFingerprint": fingerprint, "assets": assets}
    (root / "version.json").write_text(json.dumps(manifest, sort_keys=True, indent=2) + "\n")
    print(json.dumps({"webFingerprint": fingerprint, "assetCount": len(assets)}))


if __name__ == "__main__":
    main()

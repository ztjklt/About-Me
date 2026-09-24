"""Version WebGL asset URLs and expose the exact deployed build provenance."""

import argparse
import hashlib
import json
import re
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
    args = parser.parse_args()
    if any(not re.fullmatch(r"[0-9a-f]{40}", value)
           for value in (args.release_sha, args.client_build_sha)):
        raise SystemExit("release and client build SHAs must be complete commits")
    root = args.web_root.resolve()
    index = root / "index.html"
    html = index.read_text()
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
    manifest = {"releaseSourceCommit": args.release_sha,
                "clientBuildSourceCommit": args.client_build_sha,
                "webFingerprint": fingerprint, "assets": assets}
    (root / "version.json").write_text(json.dumps(manifest, sort_keys=True, indent=2) + "\n")
    print(json.dumps({"webFingerprint": fingerprint, "assetCount": len(assets)}))


if __name__ == "__main__":
    main()

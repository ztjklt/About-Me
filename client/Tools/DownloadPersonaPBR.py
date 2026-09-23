"""Download the selected CC0 Poly Haven FBX models and their 1K PBR maps."""
from __future__ import annotations

import json
import pathlib
import shutil
import urllib.request

PROJECT = pathlib.Path(__file__).resolve().parents[2]
MANIFEST = PROJECT / "shared" / "persona-prop-candidates.json"
DESTINATION = PROJECT / "client" / "Assets" / "AboutMe" / "Art" / "ThirdParty" / "PersonaPBR" / "PolyHaven"


def download(url: str, destination: pathlib.Path) -> None:
    destination.parent.mkdir(parents=True, exist_ok=True)
    if destination.exists() and destination.stat().st_size:
        return
    request = urllib.request.Request(url, headers={"User-Agent": "AboutMe-CC0-Asset-Importer/1.0"})
    with urllib.request.urlopen(request, timeout=90) as response, destination.open("wb") as output:
        shutil.copyfileobj(response, output)


manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
assets = {
    item["source"]["assetId"]
    for item in manifest["items"]
    if item.get("assetStatus") == "cc0_selected"
    and item.get("source", {}).get("provider") == "Poly Haven"
}

for asset_id in sorted(assets):
    request = urllib.request.Request(
        f"https://api.polyhaven.com/files/{asset_id}",
        headers={"User-Agent": "AboutMe-CC0-Asset-Importer/1.0"},
    )
    with urllib.request.urlopen(request, timeout=90) as response:
        files = json.load(response)["fbx"]["1k"]["fbx"]
    asset_dir = DESTINATION / asset_id
    download(files["url"], asset_dir / f"{asset_id}.fbx")
    for relative, entry in files.get("include", {}).items():
        download(entry["url"], asset_dir / relative)
    (asset_dir / "SOURCE.txt").write_text(
        f"{asset_id}\nhttps://polyhaven.com/a/{asset_id}\nLicense: CC0 1.0\n",
        encoding="utf-8",
    )
    print(f"DOWNLOADED {asset_id}")

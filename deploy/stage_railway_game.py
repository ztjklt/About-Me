"""Pack a Linux server build for Railway without editor debug symbols."""

import argparse
import gzip
import hashlib
import os
import tarfile
from pathlib import Path


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("source", type=Path, help="Linux server build directory")
    parser.add_argument("output", type=Path, help="Fresh linux.tar.gz destination")
    args = parser.parse_args()
    source = args.source.resolve()
    output = args.output.resolve()
    if not (source / "AboutMeServer").is_file() or output.exists():
        raise SystemExit("Build missing or output already exists")
    paths = sorted(path for path in source.rglob("*") if path.is_file())
    if any(path.is_symlink() for path in source.rglob("*")):
        raise SystemExit("Build contains a symlink")
    paths = [path for path in paths if path.suffix not in (".debug", ".pdb")]
    output.parent.mkdir(parents=True, exist_ok=True)
    fd = os.open(output, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    try:
        with os.fdopen(fd, "wb") as raw, gzip.GzipFile(fileobj=raw, mode="wb", mtime=0) as compressed:
            with tarfile.open(fileobj=compressed, mode="w") as archive:
                for path in paths:
                    info = tarfile.TarInfo(path.relative_to(source).as_posix())
                    info.size = path.stat().st_size
                    info.mode = path.stat().st_mode & 0o777
                    info.mtime = 0
                    with path.open("rb") as contents:
                        archive.addfile(info, contents)
    except Exception:
        output.unlink(missing_ok=True)
        raise
    digest = hashlib.sha256(output.read_bytes()).hexdigest()
    print(f"files={len(paths)} bytes={output.stat().st_size} sha256={digest}")


if __name__ == "__main__":
    main()

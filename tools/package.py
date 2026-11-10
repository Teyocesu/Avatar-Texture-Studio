#!/usr/bin/env python3
"""Build a local UPM tarball. Does not publish or modify an avatar project."""
import argparse
import gzip
import json
from pathlib import Path
import tarfile


def main():
    root = Path(__file__).resolve().parents[1]
    package = root / "Packages/com.teyocesu.avatar-texture-studio"
    identity = json.loads((package / "package.json").read_text())
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=root / "artifacts" / (identity["name"] + "-" + identity["version"] + ".tgz"))
    output = parser.parse_args().output
    output.parent.mkdir(parents=True, exist_ok=True)
    with output.open("wb") as stream:
        with gzip.GzipFile(filename="", fileobj=stream, mode="wb", mtime=0) as compressed:
            with tarfile.open(fileobj=compressed, mode="w") as archive:
                for path in sorted(package.rglob("*")):
                    if path.is_symlink():
                        raise ValueError("Package cannot contain symbolic links: " + str(path))
                    if not path.is_file():
                        continue
                    info = archive.gettarinfo(str(path), "package/" + str(path.relative_to(package)))
                    info.uid = info.gid = info.mtime = 0
                    info.uname = info.gname = ""
                    info.mode = 0o644
                    with path.open("rb") as content:
                        archive.addfile(info, content)
    print(output)


if __name__ == "__main__":
    main()

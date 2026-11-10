#!/usr/bin/env python3
"""File-level integrity checks. Does not compile Unity code or render shaders."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import tarfile

ROOT = Path(__file__).resolve().parents[1]
PACKAGE = ROOT / "Packages/com.teyocesu.avatar-texture-studio"
ASSETS = ROOT / "Tests/UnityProject/Assets"


def check(condition, message):
    if not condition:
        raise RuntimeError(message)
    print("STRUCTURE PASS " + message)


def sha(data):
    return hashlib.sha256(data).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--archive", type=Path)
    args = parser.parse_args()
    package = json.loads((PACKAGE / "package.json").read_text(encoding="utf-8"))
    check(package["name"] == PACKAGE.name and package["unity"] == "2022.3"
          and package["license"] == "MIT" and not package.get("dependencies"), "UPM identity, Unity target and dependency scope")
    for path in [*PACKAGE.rglob("*.asmdef"), *ASSETS.rglob("*.asmdef")]:
        data = json.loads(path.read_text(encoding="utf-8"))
        check(data["includePlatforms"] == ["Editor"], f"editor-only assembly {data['name']}")
    check(all("Editor" in p.relative_to(PACKAGE).parts for p in PACKAGE.rglob("*.cs")), "no runtime scripts in package")

    guids = {}
    meta_count = 0
    for base in (PACKAGE, ASSETS):
        for path in base.rglob("*"):
            if any(part.endswith("~") for part in path.relative_to(base).parts):
                continue
            if path.suffix == ".meta":
                if not path.with_suffix("").exists():
                    raise RuntimeError("Orphan meta: " + str(path.relative_to(ROOT)))
                match = re.search(r"^guid: ([0-9a-f]{32})$", path.read_text(encoding="utf-8"), re.M)
                if not match or match[1] in guids:
                    raise RuntimeError("Missing or duplicate GUID: " + str(path))
                guids[match[1]] = path
                meta_count += 1
            elif not Path(str(path) + ".meta").exists():
                raise RuntimeError("Missing meta: " + str(path))
    check(meta_count > 0, f"all asset metadata present with {meta_count} unique GUIDs")
    settings = (PACKAGE / "Editor/Core/BrushSettings.cs").read_text(encoding="utf-8")
    stamp_guids = re.findall(r'"([0-9a-f]{32})"', settings)
    check(len(stamp_guids) == 6 and all(g in guids and guids[g].parent.name == "Stamps" for g in stamp_guids), "six default brush-tip GUID references resolve")

    sources = list((PACKAGE / "Editor").rglob("*.cs"))
    code = "\n".join(p.read_text(encoding="utf-8") for p in sources)
    shader_names = set()
    for path in PACKAGE.rglob("*.shader"):
        shader_names.update(re.findall(r'Shader\s+"([^"]+)"', path.read_text(encoding="utf-8")))
    lookups = set(re.findall(r'"(Hidden/AvatarTextureStudio/[^"\s]+)"', code))
    check(lookups == shader_names and len(lookups) == 5, "all five branded shader lookups match assets")
    check("namespace MeshTexturePainter" not in code and "Hidden/MeshTexturePainter/" not in code,
          "painting namespace and shader identity isolated")
    check('[MenuItem("Tools/Avatar Texture Studio")]' in code, "requested Tools menu entry")

    inventory = json.loads((ROOT / "docs/upstream-files.json").read_text(encoding="utf-8"))
    check(inventory["commit"] == "29bbf3c728d55091190413066e549b0bbbb21bf6", "exact upstream pin recorded")
    adapted = {
        "Editor/Core/BrushEngine.cs", "Editor/Core/BrushSettings.cs", "Editor/Core/PaintProjectIO.cs",
        "Editor/Core/PaintResources.cs", "Editor/Core/PaintSession.cs", "Editor/Core/PaintTarget.cs",
        "Editor/UI/MeshTexturePainterWindow.cs", "Editor/AssemblyInfo.cs",
        "Editor/AvatarTextureStudio.Editor.asmdef", "package.json", "README.md", "CHANGELOG.md",
    }
    retained = 0
    for entry in inventory["files"]:
        path = ROOT / entry["destination"]
        if not path.is_file():
            raise RuntimeError("Upstream file removed: " + entry["destination"])
        data = path.read_bytes()
        if path.suffix == ".meta":
            expected = entry["sha256"]
        elif path.name == "MTPTests.cs":
            data = data.decode().replace("namespace AvatarTextureStudio.Painting", "namespace MeshTexturePainter")
            data = data.replace(", allowOverwrite: true", "").encode()
            expected = entry["sha256"]
        elif ASSETS in path.parents and path.suffix == ".asmdef":
            continue
        elif path.relative_to(PACKAGE).as_posix() in adapted:
            continue
        else:
            if path.suffix in (".cs", ".shader"):
                data = data.decode().replace("namespace AvatarTextureStudio.Painting", "namespace MeshTexturePainter")
                data = data.replace("Hidden/AvatarTextureStudio/Painting/", "Hidden/MeshTexturePainter/")
                data = data.replace('"AvatarTextureStudio.', '"MeshTexturePainter.').replace('"AvatarTextureStudio"', '"MeshTexturePainter"')
                data = data.replace("Avatar Texture Studio", "VRC Texture Painter").encode()
            expected = entry["sha256"]
        if sha(data) != expected:
            raise RuntimeError("Unexpected upstream content change: " + entry["destination"])
        retained += 1
    check(retained > 0, f"{retained} upstream files preserve bytes or declared identity-only transformations; no imported files missing")
    readme_entry = next(e for e in inventory["files"] if e["source"] == "Packages/com.vrctexturepainter.tool/README.md")
    check(sha((PACKAGE / "Documentation~/Upstream-README.md").read_bytes()) == readme_entry["sha256"], "original upstream manual retained verbatim")
    check("Copyright (c) 2026 ToffelsKater" in (PACKAGE / "LICENSE.md").read_text(encoding="utf-8")
          and (PACKAGE / "THIRD_PARTY_NOTICES.md").read_bytes() == (ROOT / "THIRD_PARTY_NOTICES.md").read_bytes(), "upstream license and distributed attribution retained")

    manifest_path = ROOT / "Tests/UnityProject/Packages/manifest.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    dependency = manifest["dependencies"][package["name"]]
    check((manifest_path.parent / dependency.removeprefix("file:")).resolve() == PACKAGE,
          "isolated Unity harness resolves the actual local package")
    check("2022.3.22f1" in (ROOT / "Tests/UnityProject/ProjectSettings/ProjectVersion.txt").read_text(encoding="utf-8")
          and "m_ActiveColorSpace: 1" in (ROOT / "Tests/UnityProject/ProjectSettings/ProjectSettings.asset").read_text(encoding="utf-8"), "harness declares exact editor and Linear color space")

    if args.archive:
        files = {"package/" + p.relative_to(PACKAGE).as_posix(): p for p in PACKAGE.rglob("*") if p.is_file()}
        with tarfile.open(args.archive, "r:gz") as archive:
            members = archive.getmembers()
            check(len(members) == len(files) and {m.name for m in members} == set(files), "tarball contains exactly the installable package files")
            for member in members:
                if not member.isfile() or sha(archive.extractfile(member).read()) != sha(files[member.name].read_bytes()):
                    raise RuntimeError("Unexpected archive member or bytes: " + member.name)
        check(True, "tarball bytes match source; no development projects or private assets packaged")


if __name__ == "__main__":
    main()

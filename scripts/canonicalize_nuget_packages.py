from __future__ import annotations

import argparse
import hashlib
import io
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path


RELATIONSHIP_NAMESPACE = "http://schemas.openxmlformats.org/package/2006/relationships"
CORE_PROPERTIES_RELATIONSHIP = (
    "http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties"
)
FIXED_TIMESTAMP = (1980, 1, 1, 0, 0, 0)


def package_id(files: dict[str, bytes]) -> str:
    nuspecs = [name for name in files if name.lower().endswith(".nuspec")]
    if len(nuspecs) != 1:
        raise ValueError(f"expected exactly one nuspec, found {len(nuspecs)}")
    root = ET.fromstring(files[nuspecs[0]])
    identifier = next(
        (
            (node.text or "").strip()
            for node in root.iter()
            if node.tag.rsplit("}", 1)[-1] == "id"
        ),
        "",
    )
    if not identifier:
        raise ValueError("nuspec package id is missing")
    return identifier


def deterministic_token(value: str, length: int = 32) -> str:
    return hashlib.sha256(value.encode("utf-8")).hexdigest()[:length]


def canonicalize_relationships(
    content: bytes,
    identifier: str,
    core_path: str,
) -> bytes:
    root = ET.fromstring(content)
    relationships = list(root)
    found_core = False
    for relationship in relationships:
        relationship_type = relationship.attrib.get("Type", "")
        if relationship_type == CORE_PROPERTIES_RELATIONSHIP:
            relationship.set("Target", f"/{core_path}")
            found_core = True
        target = relationship.attrib.get("Target", "")
        relationship.set(
            "Id",
            "R" + deterministic_token(f"{identifier}\0{relationship_type}\0{target}", 16).upper(),
        )

    if not found_core:
        raise ValueError("package relationship metadata does not reference core properties")

    relationships.sort(
        key=lambda node: (
            node.attrib.get("Type", ""),
            node.attrib.get("Target", ""),
        )
    )
    root[:] = relationships
    ET.register_namespace("", RELATIONSHIP_NAMESPACE)
    stream = io.BytesIO()
    ET.ElementTree(root).write(stream, encoding="utf-8", xml_declaration=True)
    return stream.getvalue()


def canonical_files(path: Path) -> dict[str, bytes]:
    with zipfile.ZipFile(path) as archive:
        if any(info.filename == ".signature.p7s" for info in archive.infolist()):
            raise ValueError(f"cannot canonicalize signed package: {path}")
        files = {
            info.filename.replace("\\", "/"): archive.read(info)
            for info in archive.infolist()
            if not info.is_dir()
        }

    identifier = package_id(files)
    core_paths = [
        name
        for name in files
        if name.startswith("package/services/metadata/core-properties/")
        and name.endswith(".psmdcp")
    ]
    if len(core_paths) != 1:
        raise ValueError(f"{path.name}: expected one core-properties part, found {core_paths}")
    if "_rels/.rels" not in files:
        raise ValueError(f"{path.name}: package relationships are missing")

    old_core_path = core_paths[0]
    new_core_path = (
        "package/services/metadata/core-properties/"
        f"{deterministic_token(identifier)}.psmdcp"
    )
    files[new_core_path] = files.pop(old_core_path)
    files["_rels/.rels"] = canonicalize_relationships(
        files["_rels/.rels"],
        identifier,
        new_core_path,
    )
    return files


def write_canonical_archive(path: Path, files: dict[str, bytes]) -> None:
    temporary = path.with_name(f".{path.name}.canonical.tmp")
    with zipfile.ZipFile(
        temporary,
        "w",
        compression=zipfile.ZIP_DEFLATED,
        compresslevel=9,
    ) as archive:
        for name in sorted(files, key=lambda value: (value.casefold(), value)):
            info = zipfile.ZipInfo(name, FIXED_TIMESTAMP)
            info.compress_type = zipfile.ZIP_DEFLATED
            info.create_system = 3
            info.external_attr = 0o100644 << 16
            archive.writestr(info, files[name], compress_type=zipfile.ZIP_DEFLATED, compresslevel=9)
    temporary.replace(path)


def verify_canonical_archive(path: Path) -> None:
    with zipfile.ZipFile(path) as archive:
        infos = [info for info in archive.infolist() if not info.is_dir()]
        names = [info.filename for info in infos]
        expected_order = sorted(names, key=lambda value: (value.casefold(), value))
        if names != expected_order:
            raise ValueError(f"{path.name}: archive entries are not in canonical order")
        if any(info.date_time != FIXED_TIMESTAMP for info in infos):
            raise ValueError(f"{path.name}: archive contains non-canonical timestamps")
        if any((info.external_attr >> 16) != 0o100644 for info in infos):
            raise ValueError(f"{path.name}: archive contains non-canonical permission bits")

        files = {info.filename: archive.read(info) for info in infos}
        identifier = package_id(files)
        expected_core_path = (
            "package/services/metadata/core-properties/"
            f"{deterministic_token(identifier)}.psmdcp"
        )
        if expected_core_path not in files:
            raise ValueError(f"{path.name}: deterministic core-properties part is missing")
        expected_relationships = canonicalize_relationships(
            files["_rels/.rels"],
            identifier,
            expected_core_path,
        )
        if files["_rels/.rels"] != expected_relationships:
            raise ValueError(f"{path.name}: package relationships are not canonical")


def canonicalize_archive(path: Path) -> None:
    write_canonical_archive(path, canonical_files(path))
    verify_canonical_archive(path)


def canonicalize_packages(package_root: Path) -> list[Path]:
    packages = sorted((*package_root.glob("*.nupkg"), *package_root.glob("*.snupkg")))
    if not packages:
        raise ValueError(f"no NuGet package archives found under {package_root}")
    for path in packages:
        canonicalize_archive(path)
    return packages


def main() -> None:
    parser = argparse.ArgumentParser(description="Canonicalize NuGet package ZIP metadata.")
    parser.add_argument("--packages", type=Path, required=True)
    args = parser.parse_args()
    try:
        packages = canonicalize_packages(args.packages)
    except (OSError, ValueError, zipfile.BadZipFile) as error:
        raise SystemExit(str(error)) from error
    print(f"canonicalized and verified {len(packages)} NuGet package archives")


if __name__ == "__main__":
    main()

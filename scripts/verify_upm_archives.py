from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
import tarfile
import zipfile
from pathlib import Path, PurePosixPath


BUILD_SCRIPT = Path(__file__).with_name("build_upm_package.py")
BUILD_SPEC = importlib.util.spec_from_file_location("nnrp_build_upm_package", BUILD_SCRIPT)
if BUILD_SPEC is None or BUILD_SPEC.loader is None:
    raise RuntimeError(f"Unable to load UPM package builder: {BUILD_SCRIPT}")
build_upm_package = importlib.util.module_from_spec(BUILD_SPEC)
BUILD_SPEC.loader.exec_module(build_upm_package)


def sha256(content: bytes) -> str:
    return hashlib.sha256(content).hexdigest()


def read_source_files(package_root: Path) -> dict[str, str]:
    return {
        path.relative_to(package_root).as_posix(): sha256(path.read_bytes())
        for path in sorted(package_root.rglob("*"))
        if path.is_file()
    }


def read_zip_files(path: Path) -> dict[str, str]:
    with zipfile.ZipFile(path) as archive:
        return {
            PurePosixPath(info.filename).as_posix(): sha256(archive.read(info))
            for info in archive.infolist()
            if not info.is_dir()
        }


def read_tgz_files(path: Path) -> dict[str, str]:
    files: dict[str, str] = {}
    with tarfile.open(path, "r:gz") as archive:
        for member in archive.getmembers():
            if not member.isfile():
                continue
            relative = PurePosixPath(member.name)
            if not relative.parts or relative.parts[0] != "package":
                raise ValueError(f"UPM tgz member is outside the package/ root: {member.name}")
            relative = PurePosixPath(*relative.parts[1:]).as_posix()
            extracted = archive.extractfile(member)
            if extracted is None:
                raise ValueError(f"Unable to read UPM tgz member: {member.name}")
            files[relative] = sha256(extracted.read())
    return files


def expected_native_paths() -> set[str]:
    paths: set[str] = set()
    for transport_name in build_upm_package.NATIVE_TRANSPORTS.values():
        for _, relative_output in build_upm_package.NATIVE_LAYOUT.values():
            scoped_output = build_upm_package.transport_scoped_plugin_path(
                transport_name,
                relative_output,
            )
            paths.add(
                (
                    Path("Runtime/Plugins/Transports")
                    / transport_name
                    / Path(*scoped_output.parts[2:])
                ).as_posix()
            )
    return paths


def verify_package_layout(files: set[str], version: str, package_root: Path) -> None:
    manifest = json.loads((package_root / "package.json").read_text(encoding="utf-8"))
    if manifest.get("name") != "com.nnrp.client":
        raise ValueError("UPM package name must be com.nnrp.client")
    if manifest.get("version") != version:
        raise ValueError(
            f"UPM package version mismatch: expected {version}, found {manifest.get('version')}"
        )

    expected_managed = {
        f"Runtime/Managed/{assembly}.dll"
        for assembly in build_upm_package.MANAGED_ASSEMBLIES
    }
    actual_managed = {
        path
        for path in files
        if path.startswith("Runtime/Managed/") and path.endswith(".dll")
    }
    if actual_managed != expected_managed:
        raise ValueError(
            "UPM managed assembly boundary mismatch: "
            f"expected {sorted(expected_managed)}, found {sorted(actual_managed)}"
        )

    prohibited = [
        path
        for path in files
        if "Nnrp.Server" in path or "/runtimes/" in f"/{path.lower()}/"
    ]
    if prohibited:
        raise ValueError(f"UPM archive contains prohibited server/runtime paths: {prohibited}")

    expected_native = expected_native_paths()
    native_suffixes = (".dll", ".so", ".dylib", ".a")
    actual_native = {
        path
        for path in files
        if path.startswith("Runtime/Plugins/Transports/") and path.endswith(native_suffixes)
    }
    if actual_native != expected_native:
        missing = sorted(expected_native - actual_native)
        unexpected = sorted(actual_native - expected_native)
        raise ValueError(
            f"UPM native transport matrix mismatch; missing={missing}, unexpected={unexpected}"
        )

    missing_meta = sorted(path for path in expected_native if f"{path}.meta" not in files)
    if missing_meta:
        raise ValueError(f"UPM native plugins are missing Unity metadata: {missing_meta}")


def verify_upm_archives(package_root: Path, zip_path: Path, tgz_path: Path, version: str) -> None:
    if not package_root.is_dir():
        raise ValueError(f"UPM package directory does not exist: {package_root}")
    if not zip_path.is_file():
        raise ValueError(f"UPM zip does not exist: {zip_path}")
    if not tgz_path.is_file():
        raise ValueError(f"UPM tgz does not exist: {tgz_path}")

    source_files = read_source_files(package_root)
    zip_files = read_zip_files(zip_path)
    tgz_files = read_tgz_files(tgz_path)

    if zip_files != source_files:
        raise ValueError("UPM zip file list or content differs from the verified package directory")
    if tgz_files != source_files:
        raise ValueError("UPM tgz file list or content differs from the verified package directory")

    verify_package_layout(set(source_files), version, package_root)


def main() -> None:
    parser = argparse.ArgumentParser(description="Verify final UPM zip and tgz release archives.")
    parser.add_argument("--package-root", type=Path, required=True)
    parser.add_argument("--zip", dest="zip_path", type=Path, required=True)
    parser.add_argument("--tgz", dest="tgz_path", type=Path, required=True)
    parser.add_argument("--version", required=True)
    args = parser.parse_args()

    try:
        verify_upm_archives(args.package_root, args.zip_path, args.tgz_path, args.version)
    except (OSError, ValueError, tarfile.TarError, zipfile.BadZipFile) as error:
        raise SystemExit(str(error)) from error

    print(f"verified UPM zip and tgz archives at {args.version}")


if __name__ == "__main__":
    main()

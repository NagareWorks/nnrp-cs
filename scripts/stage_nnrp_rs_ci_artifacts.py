from __future__ import annotations

import argparse
import json
import shutil
from pathlib import Path


TRANSPORTS = ("tcp", "quic", "ipc", "websocket")
RID_OS = {"windows": "win", "linux": "linux", "macos": "osx"}
RID_ARCH = {"x86": "x86", "x86_64": "x64", "armv7": "arm", "aarch64": "arm64"}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Stage nnrp-rs CI native transport artifacts in the C# runtime layout."
    )
    parser.add_argument("--input", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--require-abi-version", required=True)
    return parser.parse_args()


def stage_artifacts(input_root: Path, output_root: Path, required_abi_version: str) -> list[Path]:
    manifests = sorted(input_root.glob("*/manifest.json"))
    if not manifests:
        raise FileNotFoundError(f"no nnrp-rs transport manifests found under {input_root}")

    staged: list[Path] = []
    observed_transports: set[str] = set()
    observed_platforms: set[tuple[str, str]] = set()
    for manifest_path in manifests:
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        transport = manifest.get("transport_scope")
        if transport not in TRANSPORTS:
            raise ValueError(f"{manifest_path}: unsupported transport scope {transport!r}")
        if transport in observed_transports:
            raise ValueError(f"duplicate nnrp-rs CI artifact for transport {transport}")

        os_name = manifest.get("os")
        arch = manifest.get("arch")
        if os_name not in RID_OS or arch not in RID_ARCH:
            raise ValueError(f"{manifest_path}: unsupported native platform {os_name!r}/{arch!r}")
        observed_platforms.add((os_name, arch))

        abi_version = manifest.get("abi_version")
        if abi_version != required_abi_version:
            raise ValueError(
                f"{manifest_path}: expected ABI {required_abi_version}, found {abi_version!r}"
            )

        library_name = manifest.get("library")
        if not isinstance(library_name, str) or not library_name:
            raise ValueError(f"{manifest_path}: missing native library name")
        source_library = manifest_path.parent / library_name
        if not source_library.is_file():
            raise FileNotFoundError(f"{manifest_path}: native library is missing: {library_name}")

        rid = f"{RID_OS[os_name]}-{RID_ARCH[arch]}"
        destination = output_root / f"transport-{transport}" / rid
        destination.mkdir(parents=True, exist_ok=True)
        target_library = destination / library_name
        shutil.copy2(source_library, target_library)
        shutil.copy2(manifest_path, destination / "manifest.json")
        staged.append(target_library)
        observed_transports.add(transport)

    missing = set(TRANSPORTS) - observed_transports
    if missing:
        raise ValueError(f"nnrp-rs CI artifact is missing transports: {', '.join(sorted(missing))}")
    if len(observed_platforms) != 1:
        raise ValueError("nnrp-rs CI artifact mixes multiple native platforms")
    return staged


def prepare_output_root(output_root: Path) -> None:
    resolved = output_root.resolve()
    if resolved == Path(resolved.anchor):
        raise ValueError(f"refusing to delete filesystem root output path: {resolved}")
    if resolved.exists():
        shutil.rmtree(resolved)


def main() -> int:
    args = parse_args()
    input_root = Path(args.input).resolve()
    output_root = Path(args.output).resolve()
    prepare_output_root(output_root)
    for library in stage_artifacts(input_root, output_root, args.require_abi_version):
        print(library)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

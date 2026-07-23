from __future__ import annotations

import argparse
import shutil
import subprocess
import tempfile
import zipfile
from dataclasses import dataclass
from pathlib import Path


@dataclass(frozen=True)
class NativeArtifact:
    transport: str
    asset_tag: str
    rid: str
    library_name: str

    def asset_name(self, version: str) -> str:
        return f"nnrp-ffi-transport-{self.transport}-native-{self.asset_tag}-{version}.zip"


def native_artifacts() -> list[NativeArtifact]:
    artifacts: list[NativeArtifact] = []
    for transport in ("tcp", "quic", "ipc", "websocket"):
        artifacts.extend(
            [
                NativeArtifact(transport, "windows-x86", "win-x86", "nnrp_ffi.dll"),
                NativeArtifact(transport, "windows-x86_64", "win-x64", "nnrp_ffi.dll"),
                NativeArtifact(transport, "windows-aarch64", "win-arm64", "nnrp_ffi.dll"),
                NativeArtifact(transport, "macos-x86_64", "osx-x64", "libnnrp_ffi.dylib"),
                NativeArtifact(transport, "macos-aarch64", "osx-arm64", "libnnrp_ffi.dylib"),
                NativeArtifact(transport, "linux-x86", "linux-x86", "libnnrp_ffi.so"),
                NativeArtifact(transport, "linux-x86_64", "linux-x64", "libnnrp_ffi.so"),
                NativeArtifact(transport, "linux-armv7", "linux-arm", "libnnrp_ffi.so"),
                NativeArtifact(transport, "linux-aarch64", "linux-arm64", "libnnrp_ffi.so"),
                NativeArtifact(transport, "android-x86", "android-x86", "libnnrp_ffi.so"),
                NativeArtifact(transport, "android-x86_64", "android-x64", "libnnrp_ffi.so"),
                NativeArtifact(transport, "android-armv7", "android-arm", "libnnrp_ffi.so"),
                NativeArtifact(transport, "android-aarch64", "android-arm64", "libnnrp_ffi.so"),
                NativeArtifact(transport, "ios-aarch64", "ios-arm64", "libnnrp_ffi.a"),
                NativeArtifact(transport, "ios-aarch64-sim", "iossimulator-arm64", "libnnrp_ffi.a"),
                NativeArtifact(transport, "ios-x86_64-sim", "iossimulator-x64", "libnnrp_ffi.a"),
            ]
        )
    return artifacts


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Download nnrp-rs native FFI artifacts into C# package layout.")
    parser.add_argument("--version", required=True, help="nnrp-rs version without the leading v, for example 1.0.0-preview.4.15")
    parser.add_argument("--repo", default="NagareWorks/nnrp-rs")
    parser.add_argument("--output", required=True)
    parser.add_argument("--include-headers", action="store_true")
    return parser.parse_args()


def download_artifact(repo: str, version: str, artifact: NativeArtifact, download_dir: Path) -> Path:
    asset_name = artifact.asset_name(version)
    command = [
        "gh",
        "release",
        "download",
        f"v{version}",
        "--repo",
        repo,
        "--pattern",
        asset_name,
        "--dir",
        str(download_dir),
    ]
    subprocess.run(command, check=True)
    archive_path = download_dir / asset_name
    if not archive_path.exists():
        raise FileNotFoundError(f"nnrp-rs release asset was not downloaded: {asset_name}")
    return archive_path


def extract_library(archive_path: Path, artifact: NativeArtifact, output_root: Path, include_headers: bool) -> Path:
    rid_root = output_root / f"transport-{artifact.transport}" / artifact.rid
    rid_root.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(archive_path) as archive:
        names = set(archive.namelist())
        if artifact.library_name not in names:
            raise FileNotFoundError(f"{archive_path.name} does not contain {artifact.library_name}")

        target = rid_root / artifact.library_name
        with archive.open(artifact.library_name) as source, target.open("wb") as destination:
            shutil.copyfileobj(source, destination)

        manifest_name = "manifest.json"
        if manifest_name in names:
            with archive.open(manifest_name) as source, (rid_root / manifest_name).open("wb") as destination:
                shutil.copyfileobj(source, destination)

        if include_headers:
            for name in names:
                if name.startswith("include/") or name.endswith(".h"):
                    target_path = rid_root / name
                    target_path.parent.mkdir(parents=True, exist_ok=True)
                    with archive.open(name) as source, target_path.open("wb") as destination:
                        shutil.copyfileobj(source, destination)

    return target


def main() -> int:
    args = parse_args()
    output_root = Path(args.output).resolve()
    if output_root.exists():
        shutil.rmtree(output_root)
    output_root.mkdir(parents=True, exist_ok=True)

    with tempfile.TemporaryDirectory(prefix="nnrp-rs-artifacts-") as temp_dir:
        download_dir = Path(temp_dir)
        for artifact in native_artifacts():
            archive_path = download_artifact(args.repo, args.version, artifact, download_dir)
            target_path = extract_library(archive_path, artifact, output_root, args.include_headers)
            print(f"{artifact.transport}/{artifact.rid}: {target_path}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())

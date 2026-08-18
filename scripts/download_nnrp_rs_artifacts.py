from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import shutil
import subprocess
import tempfile
import time
import zipfile
from collections.abc import Iterator
from contextlib import contextmanager
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
    parser.add_argument("--version", required=True, help="nnrp-rs version without the leading v, for example 1.0.0-preview.4.19")
    parser.add_argument("--repo", default="NagareWorks/nnrp-rs")
    parser.add_argument("--output", required=True)
    parser.add_argument("--require-abi-version", required=True)
    parser.add_argument(
        "--transport",
        action="append",
        choices=("tcp", "quic", "ipc", "websocket"),
        help="Download only the selected transport. Repeat for multiple transports.",
    )
    parser.add_argument(
        "--rid",
        action="append",
        help="Download only the selected .NET runtime identifier. Repeat for multiple RIDs.",
    )
    parser.add_argument("--include-headers", action="store_true")
    parser.add_argument(
        "--workflow-run-id",
        help="Consume a completed nnrp-rs release workflow artifact instead of a published release.",
    )
    parser.add_argument(
        "--workflow-head-sha",
        help="Exact 40-character nnrp-rs commit expected for --workflow-run-id.",
    )
    return parser.parse_args()


def download_release_assets(
    repo: str,
    version: str,
    artifacts: list[NativeArtifact],
    download_dir: Path,
) -> None:
    expected_names = ["SHA256SUMS", *(artifact.asset_name(version) for artifact in artifacts)]
    last_return_code = 0
    for attempt in range(1, 6):
        for name in expected_names:
            path = download_dir / name
            if path.exists() and path.stat().st_size == 0:
                path.unlink()

        missing_names = [name for name in expected_names if not (download_dir / name).is_file()]
        if not missing_names:
            return

        command = [
            "gh",
            "release",
            "download",
            f"v{version}",
            "--repo",
            repo,
            "--dir",
            str(download_dir),
        ]
        for name in missing_names:
            command.extend(("--pattern", name))
        result = subprocess.run(command, check=False)
        last_return_code = result.returncode
        for name in expected_names:
            path = download_dir / name
            if path.exists() and path.stat().st_size == 0:
                path.unlink()

        checksum_path = download_dir / "SHA256SUMS"
        if checksum_path.is_file():
            try:
                checksums = read_checksums(checksum_path)
            except (OSError, UnicodeError, ValueError):
                checksum_path.unlink()
            else:
                for name in expected_names[1:]:
                    path = download_dir / name
                    if not path.is_file():
                        continue
                    try:
                        verify_checksum(path, checksums)
                    except (OSError, ValueError):
                        path.unlink()

        if all((download_dir / name).is_file() for name in expected_names):
            return
        if attempt < 5:
            time.sleep(min(2 ** (attempt - 1), 8))

    missing_names = [name for name in expected_names if not (download_dir / name).is_file()]
    raise RuntimeError(
        "failed to download nnrp-rs release assets after 5 attempts "
        f"(last exit code {last_return_code}): {', '.join(missing_names)}"
    )


def download_workflow_artifact(
    repo: str,
    version: str,
    run_id: str,
    expected_head_sha: str,
    download_dir: Path,
) -> None:
    if not run_id.isdigit():
        raise ValueError("workflow run id must contain only decimal digits")
    if not re.fullmatch(r"[0-9a-f]{40}", expected_head_sha):
        raise ValueError("workflow head SHA must be an exact lowercase 40-character commit hash")

    result = subprocess.run(
        ["gh", "run", "view", run_id, "--repo", repo, "--json", "headSha,status,conclusion"],
        check=True,
        capture_output=True,
        text=True,
    )
    metadata = json.loads(result.stdout)
    if metadata.get("headSha") != expected_head_sha:
        raise ValueError(
            f"nnrp-rs workflow run {run_id} belongs to {metadata.get('headSha')!r}, "
            f"not {expected_head_sha}"
        )
    if metadata.get("status") != "completed" or metadata.get("conclusion") != "success":
        raise ValueError(
            f"nnrp-rs workflow run {run_id} is not a completed success: "
            f"status={metadata.get('status')!r}, conclusion={metadata.get('conclusion')!r}"
        )

    subprocess.run(
        [
            "gh",
            "run",
            "download",
            run_id,
            "--repo",
            repo,
            "--name",
            f"nnrp-rs-release-{version}",
            "--dir",
            str(download_dir),
        ],
        check=True,
    )


def find_downloaded_file(download_dir: Path, name: str) -> Path:
    matches = list(download_dir.rglob(name))
    if len(matches) != 1:
        raise FileNotFoundError(f"expected exactly one downloaded {name}, found {len(matches)}")
    return matches[0]


def read_checksums(path: Path) -> dict[str, str]:
    checksums: dict[str, str] = {}
    for line in path.read_text(encoding="utf-8").splitlines():
        match = re.fullmatch(r"([0-9a-f]{64})  (.+)", line)
        if match is None:
            raise ValueError(f"malformed checksum entry: {line!r}")
        digest, name = match.groups()
        if name in checksums:
            raise ValueError(f"duplicate checksum entry: {name}")
        checksums[name] = digest
    return checksums


def verify_checksum(path: Path, checksums: dict[str, str]) -> None:
    expected = checksums.get(path.name)
    if expected is None:
        raise ValueError(f"SHA256SUMS does not contain {path.name}")
    actual = hashlib.sha256(path.read_bytes()).hexdigest()
    if actual != expected:
        raise ValueError(f"checksum mismatch for {path.name}: expected {expected}, found {actual}")


@contextmanager
def artifact_temporary_directory(output_root: Path) -> Iterator[Path]:
    temporary_root = output_root.parent / ".tmp"
    temporary_root.mkdir(parents=True, exist_ok=True)
    original_temp = os.environ.get("TEMP")
    original_tmp = os.environ.get("TMP")
    try:
        with tempfile.TemporaryDirectory(prefix="nnrp-rs-artifacts-", dir=temporary_root) as temp_dir:
            os.environ["TEMP"] = temp_dir
            os.environ["TMP"] = temp_dir
            yield Path(temp_dir)
    finally:
        if original_temp is None:
            os.environ.pop("TEMP", None)
        else:
            os.environ["TEMP"] = original_temp
        if original_tmp is None:
            os.environ.pop("TMP", None)
        else:
            os.environ["TMP"] = original_tmp
        try:
            temporary_root.rmdir()
        except OSError:
            pass


def extract_library(
    archive_path: Path,
    artifact: NativeArtifact,
    output_root: Path,
    include_headers: bool,
    required_abi_version: str,
) -> Path:
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
        if manifest_name not in names:
            raise FileNotFoundError(f"{archive_path.name} does not contain {manifest_name}")

        manifest = json.loads(archive.read(manifest_name))
        if manifest.get("transport_scope") != artifact.transport:
            raise ValueError(
                f"{archive_path.name}: expected transport scope {artifact.transport}, "
                f"found {manifest.get('transport_scope')!r}"
            )
        if manifest.get("abi_version") != required_abi_version:
            raise ValueError(
                f"{archive_path.name}: expected ABI {required_abi_version}, "
                f"found {manifest.get('abi_version')!r}"
            )
        with (rid_root / manifest_name).open("w", encoding="utf-8", newline="\n") as destination:
            json.dump(manifest, destination, indent=2, sort_keys=True)
            destination.write("\n")

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
    if bool(args.workflow_run_id) != bool(args.workflow_head_sha):
        raise ValueError("--workflow-run-id and --workflow-head-sha must be provided together")
    output_root = Path(args.output).resolve()
    if output_root.exists():
        shutil.rmtree(output_root)
    output_root.mkdir(parents=True, exist_ok=True)

    with artifact_temporary_directory(output_root) as download_dir:
        selected = [
            artifact
            for artifact in native_artifacts()
            if (not args.transport or artifact.transport in args.transport)
            and (not args.rid or artifact.rid in args.rid)
        ]
        if not selected:
            raise ValueError("transport/RID filters selected no nnrp-rs artifacts")

        if args.workflow_run_id:
            download_workflow_artifact(
                args.repo,
                args.version,
                args.workflow_run_id,
                args.workflow_head_sha,
                download_dir,
            )
        else:
            download_release_assets(args.repo, args.version, selected, download_dir)

        checksums = read_checksums(find_downloaded_file(download_dir, "SHA256SUMS"))

        for artifact in selected:
            archive_path = find_downloaded_file(download_dir, artifact.asset_name(args.version))
            verify_checksum(archive_path, checksums)
            target_path = extract_library(
                archive_path,
                artifact,
                output_root,
                args.include_headers,
                args.require_abi_version,
            )
            print(f"{artifact.transport}/{artifact.rid}: {target_path}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())

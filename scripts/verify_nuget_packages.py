from __future__ import annotations

import argparse
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path


ROLE_PACKAGE_IDS = {
    "Nnrp.Core",
    "Nnrp.Client",
    "Nnrp.Server",
    "Nnrp.NativeBridge",
}
TRANSPORT_PACKAGES = {
    "Nnrp.Transport.Tcp": "tcp",
    "Nnrp.Transport.Quic": "quic",
    "Nnrp.Transport.Ipc": "ipc",
    "Nnrp.Transport.WebSocket": "websocket",
}
EXPECTED_PACKAGE_IDS = ROLE_PACKAGE_IDS | set(TRANSPORT_PACKAGES)
RID_LIBRARIES = {
    "win-x86": "nnrp_ffi_{transport}.dll",
    "win-x64": "nnrp_ffi_{transport}.dll",
    "win-arm64": "nnrp_ffi_{transport}.dll",
    "osx-x64": "libnnrp_ffi_{transport}.dylib",
    "osx-arm64": "libnnrp_ffi_{transport}.dylib",
    "linux-x86": "libnnrp_ffi_{transport}.so",
    "linux-x64": "libnnrp_ffi_{transport}.so",
    "linux-arm": "libnnrp_ffi_{transport}.so",
    "linux-arm64": "libnnrp_ffi_{transport}.so",
    "android-x86": "libnnrp_ffi_{transport}.so",
    "android-x64": "libnnrp_ffi_{transport}.so",
    "android-arm": "libnnrp_ffi_{transport}.so",
    "android-arm64": "libnnrp_ffi_{transport}.so",
    "ios-arm64": "libnnrp_ffi_{transport}.a",
    "iossimulator-x64": "libnnrp_ffi_{transport}.a",
    "iossimulator-arm64": "libnnrp_ffi_{transport}.a",
}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Verify NNRP NuGet package identity and transport-native ownership."
    )
    parser.add_argument("--packages", type=Path, required=True)
    parser.add_argument("--version", required=True)
    return parser.parse_args()


def package_identity(archive: zipfile.ZipFile) -> tuple[str, str]:
    nuspecs = [name for name in archive.namelist() if name.lower().endswith(".nuspec")]
    if len(nuspecs) != 1:
        raise ValueError(f"expected exactly one nuspec, found {len(nuspecs)}")
    root = ET.fromstring(archive.read(nuspecs[0]))
    metadata = next((node for node in root.iter() if node.tag.rsplit("}", 1)[-1] == "metadata"), None)
    if metadata is None:
        raise ValueError("nuspec metadata is missing")
    values = {
        node.tag.rsplit("}", 1)[-1]: (node.text or "").strip()
        for node in metadata
    }
    return values.get("id", ""), values.get("version", "")


def native_paths(archive: zipfile.ZipFile) -> set[str]:
    suffixes = (".dll", ".so", ".dylib", ".a")
    return {
        name.replace("\\", "/")
        for name in archive.namelist()
        if name.lower().startswith("runtimes/") and name.lower().endswith(suffixes)
    }


def expected_transport_paths(transport: str) -> set[str]:
    return {
        f"runtimes/{rid}/native/nnrp/transport/{transport}/"
        f"{template.format(transport=transport)}"
        for rid, template in RID_LIBRARIES.items()
    }


def verify_packages(package_root: Path, version: str) -> None:
    package_paths = sorted(package_root.glob("*.nupkg"))
    if not package_paths:
        raise ValueError(f"no NuGet packages found under {package_root}")

    discovered: dict[str, Path] = {}
    for package_path in package_paths:
        with zipfile.ZipFile(package_path) as archive:
            package_id, package_version = package_identity(archive)
            if package_id not in EXPECTED_PACKAGE_IDS:
                raise ValueError(f"{package_path.name}: unexpected package id {package_id!r}")
            if package_id in discovered:
                raise ValueError(f"duplicate package id {package_id}: {package_path.name}")
            if package_version != version:
                raise ValueError(
                    f"{package_path.name}: expected version {version}, found {package_version}"
                )

            actual_native = native_paths(archive)
            if package_id in ROLE_PACKAGE_IDS:
                if actual_native:
                    raise ValueError(
                        f"{package_id}: role/common package contains native artifacts: "
                        f"{sorted(actual_native)}"
                    )
            else:
                transport = TRANSPORT_PACKAGES[package_id]
                expected_native = expected_transport_paths(transport)
                if actual_native != expected_native:
                    missing = sorted(expected_native - actual_native)
                    unexpected = sorted(actual_native - expected_native)
                    raise ValueError(
                        f"{package_id}: transport artifact boundary mismatch; "
                        f"missing={missing}, unexpected={unexpected}"
                    )
            discovered[package_id] = package_path

    missing_packages = sorted(EXPECTED_PACKAGE_IDS - set(discovered))
    if missing_packages:
        raise ValueError(f"missing required NuGet packages: {missing_packages}")


def main() -> int:
    args = parse_args()
    verify_packages(args.packages.resolve(), args.version)
    print(
        f"verified {len(EXPECTED_PACKAGE_IDS)} NuGet packages at {args.version}; "
        "role packages are native-free and every transport owns its complete RID matrix"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

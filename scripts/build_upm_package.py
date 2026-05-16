from __future__ import annotations

import argparse
import hashlib
import json
import shutil
import textwrap
import xml.etree.ElementTree as ET
from pathlib import Path


MANAGED_ASSEMBLIES = [
    "Nnrp.Core",
    "Nnrp.Client",
    "Nnrp.Transport.Tcp",
    "Nnrp.NativeBridge",
]

NATIVE_LAYOUT = {
    "win-x64": ("nnrp_quic_bridge.dll", Path("Runtime/Plugins/Windows/x86_64/nnrp_quic_bridge.dll")),
    "linux-x64": ("libnnrp_quic_bridge.so", Path("Runtime/Plugins/Linux/x86_64/libnnrp_quic_bridge.so")),
    "osx-x64": ("libnnrp_quic_bridge.dylib", Path("Runtime/Plugins/macOS/x86_64/libnnrp_quic_bridge.dylib")),
    "osx-arm64": ("libnnrp_quic_bridge.dylib", Path("Runtime/Plugins/macOS/arm64/libnnrp_quic_bridge.dylib")),
}

NATIVE_PLUGIN_SETTINGS = {
    "win-x64": ("Windows", "x86_64"),
    "linux-x64": ("Linux", "x86_64"),
    "osx-x64": ("OSX", "x86_64"),
    "osx-arm64": ("OSX", "ARM64"),
}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Build a deterministic Unity-style UPM package with .meta files.")
    parser.add_argument("--repo-root", required=True)
    parser.add_argument("--configuration", default="Release")
    parser.add_argument("--native-root", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--repository-url", default="")
    return parser.parse_args()


def read_version(repo_root: Path) -> str:
    props_path = repo_root / "Directory.Build.props"
    tree = ET.parse(props_path)
    version = tree.findtext(".//Version")
    if not version:
        raise ValueError(f"Version was not found in {props_path}")
    return version.strip()


def stable_guid(relative_path: str) -> str:
    normalized = relative_path.replace("\\", "/").strip("/").lower()
    return hashlib.sha256(f"nnrp-upm::{normalized}".encode("utf-8")).hexdigest()[:32]


def folder_meta(relative_path: str) -> str:
    return textwrap.dedent(
        f"""\
        fileFormatVersion: 2
        guid: {stable_guid(relative_path)}
        folderAsset: yes
        DefaultImporter:
          externalObjects: {{}}
          userData: 
          assetBundleName: 
          assetBundleVariant: 
        """
    )


def default_meta(relative_path: str) -> str:
    return textwrap.dedent(
        f"""\
        fileFormatVersion: 2
        guid: {stable_guid(relative_path)}
        DefaultImporter:
          externalObjects: {{}}
          userData: 
          assetBundleName: 
          assetBundleVariant: 
        """
    )


def managed_plugin_meta(relative_path: str) -> str:
    return textwrap.dedent(
        f"""\
        fileFormatVersion: 2
        guid: {stable_guid(relative_path)}
        PluginImporter:
          externalObjects: {{}}
          serializedVersion: 2
          iconMap: {{}}
          executionOrder: {{}}
          defineConstraints: []
          isPreloaded: 0
          isOverridable: 0
          isExplicitlyReferenced: 0
          validateReferences: 1
          platformData:
          - first:
              Any: Any
            second:
              enabled: 1
              settings: {{}}
          - first:
              Editor: Editor
            second:
              enabled: 0
              settings:
                DefaultValueInitialized: true
          userData: 
          assetBundleName: 
          assetBundleVariant: 
        """
    )


def native_plugin_meta(relative_path: str, rid: str) -> str:
    platform, cpu = NATIVE_PLUGIN_SETTINGS[rid]
    return textwrap.dedent(
        f"""\
        fileFormatVersion: 2
        guid: {stable_guid(relative_path)}
        PluginImporter:
          externalObjects: {{}}
          serializedVersion: 2
          iconMap: {{}}
          executionOrder: {{}}
          defineConstraints: []
          isPreloaded: 0
          isOverridable: 0
          isExplicitlyReferenced: 0
          validateReferences: 1
          platformData:
          - first:
              Any: Any
            second:
              enabled: 0
              settings: {{}}
          - first:
              {platform}: {platform}
            second:
              enabled: 1
              settings:
                CPU: {cpu}
          userData: 
          assetBundleName: 
          assetBundleVariant: 
        """
    )


def ensure_parent(path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)


def copy_managed_artifacts(repo_root: Path, configuration: str, output_root: Path) -> None:
    managed_root = output_root / "Runtime" / "Managed"
    managed_root.mkdir(parents=True, exist_ok=True)
    for assembly in MANAGED_ASSEMBLIES:
        source_dir = repo_root / "src" / assembly / "bin" / configuration / "netstandard2.1"
        for extension in (".dll", ".xml"):
            source_path = source_dir / f"{assembly}{extension}"
            if source_path.exists():
                shutil.copy2(source_path, managed_root / source_path.name)


def copy_native_artifacts(native_root: Path, output_root: Path) -> None:
    for rid, (filename, relative_output) in NATIVE_LAYOUT.items():
        source_path = native_root / rid / filename
        if not source_path.exists():
            continue
        target_path = output_root / relative_output
        ensure_parent(target_path)
        shutil.copy2(source_path, target_path)


def write_package_manifest(output_root: Path, version: str, repository_url: str) -> None:
    package_json = {
        "name": "com.nnrp.client",
        "displayName": "NNRP Client",
        "version": version,
        "unity": "2022.3",
        "description": "Unity-style package for the current NNRP/1-preview2 managed client and native bridge.",
        "author": {
            "name": "NNRP Contributors",
        },
        "keywords": ["nnrp", "unity", "neural-rendering", "networking"],
    }
    if repository_url:
        package_json["repository"] = {
            "type": "git",
            "url": repository_url,
        }
        package_json["documentationUrl"] = repository_url
    (output_root / "package.json").write_text(json.dumps(package_json, indent=2) + "\n", encoding="utf-8")


def write_package_readme(output_root: Path, version: str) -> None:
    readme = textwrap.dedent(
        f"""\
        # com.nnrp.client

        Version: {version}

        This Unity-style package contains the current preview2 managed client surface plus native bridge plugins generated by CI.

        Included managed assemblies:

        - Nnrp.Core
        - Nnrp.Client
        - Nnrp.Transport.Tcp
        - Nnrp.NativeBridge

        Included native plugins are placed under Runtime/Plugins for the supported desktop platforms built by CI.
        """
    )
    (output_root / "README.md").write_text(readme, encoding="utf-8")


def write_license(repo_root: Path, output_root: Path) -> None:
    license_path = repo_root / "LICENSE"
    if license_path.exists():
        shutil.copy2(license_path, output_root / "LICENSE")


def emit_meta_files(output_root: Path) -> None:
    directories = sorted(path for path in output_root.rglob("*") if path.is_dir())
    files = sorted(path for path in output_root.rglob("*") if path.is_file() and path.suffix != ".meta")

    for directory in directories:
        relative_path = directory.relative_to(output_root).as_posix()
        meta_path = directory.with_name(directory.name + ".meta")
        meta_path.write_text(folder_meta(relative_path), encoding="utf-8")

    for file_path in files:
        relative_path = file_path.relative_to(output_root).as_posix()
        meta_path = file_path.with_name(file_path.name + ".meta")
        if file_path.suffix == ".dll" and file_path.parts[-2] == "Managed":
            content = managed_plugin_meta(relative_path)
        elif file_path.suffix in {".dll", ".so", ".dylib"}:
            rid = relative_path.split("/")[2].lower() if relative_path.startswith("Runtime/Plugins/") else ""
            if rid == "windows":
                content = native_plugin_meta(relative_path, "win-x64")
            elif rid == "linux":
                content = native_plugin_meta(relative_path, "linux-x64")
            elif rid == "macos":
                arch = relative_path.split("/")[3].lower()
                content = native_plugin_meta(relative_path, "osx-arm64" if arch == "arm64" else "osx-x64")
            else:
                content = default_meta(relative_path)
        else:
            content = default_meta(relative_path)
        meta_path.write_text(content, encoding="utf-8")


def main() -> int:
    args = parse_args()
    repo_root = Path(args.repo_root).resolve()
    native_root = Path(args.native_root).resolve()
    output_root = Path(args.output).resolve()

    if output_root.exists():
        shutil.rmtree(output_root)
    output_root.mkdir(parents=True, exist_ok=True)

    version = read_version(repo_root)
    copy_managed_artifacts(repo_root, args.configuration, output_root)
    copy_native_artifacts(native_root, output_root)
    write_package_manifest(output_root, version, args.repository_url)
    write_package_readme(output_root, version)
    write_license(repo_root, output_root)
    emit_meta_files(output_root)

    print(output_root)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
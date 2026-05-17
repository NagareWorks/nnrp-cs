from __future__ import annotations

import argparse
import hashlib
import json
import shutil
import subprocess
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

PACKAGE_NAME = "com.nnrp.client"
PACKAGE_DISPLAY_NAME = "NNRP Client SDK"
PACKAGE_DESCRIPTION = "UPM distribution of the NNRP managed client SDK and packaged native bridge assets."
PACKAGE_DOCUMENTATION_URL = "https://nagareworks.github.io/nnrp-doc/"
PACKAGE_AUTHOR_NAME = "NNRP Contributors"
PACKAGE_KEYWORDS = ["nnrp", "runtime", "transport", "networking"]


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Build release UPM artifacts and sync tracked package metadata.")
    parser.add_argument("--repo-root", required=True)
    parser.add_argument("--configuration", default="Release")
    parser.add_argument("--native-root")
    parser.add_argument("--output")
    parser.add_argument("--tracked-output")
    parser.add_argument("--validate-tracked-metadata", action="store_true")
    parser.add_argument("--repository-url", default="")
    args = parser.parse_args()

    has_release_build_args = bool(args.native_root and args.output)
    has_partial_release_build_args = bool(args.native_root or args.output)

    if has_partial_release_build_args and not has_release_build_args:
        parser.error("--native-root and --output must be provided together.")

    if args.validate_tracked_metadata and not args.tracked_output:
        parser.error("--validate-tracked-metadata requires --tracked-output.")

    if not has_release_build_args and not args.tracked_output:
        parser.error("Provide --tracked-output, or provide both --native-root and --output.")

    return args


def read_msbuild_property(repo_root: Path, property_name: str, *, allow_empty: bool = False) -> str:
    project_path = repo_root / "src" / "Nnrp.Core" / "Nnrp.Core.csproj"
    result = subprocess.run(
        ["dotnet", "msbuild", str(project_path), "-nologo", f"-getProperty:{property_name}"],
        check=True,
        capture_output=True,
        text=True,
    )
    property_value = result.stdout.strip()
    if not property_value and not allow_empty:
        raise ValueError(f"Property {property_name} was not resolved from {project_path}")
    return property_value


def read_release_version(repo_root: Path) -> str:
    return read_msbuild_property(repo_root, "Version")


def read_tracked_metadata_version(repo_root: Path) -> str:
    version_prefix = read_msbuild_property(repo_root, "VersionPrefix")
    version_train = read_msbuild_property(repo_root, "VersionTrain", allow_empty=True)
    if not version_train:
        return version_prefix
    return f"{version_prefix}-{version_train}"


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


def build_package_manifest(version: str, repository_url: str) -> dict[str, object]:
    package_json = {
        "name": PACKAGE_NAME,
        "displayName": PACKAGE_DISPLAY_NAME,
        "version": version,
        "unity": "2022.3",
        "description": PACKAGE_DESCRIPTION,
        "documentationUrl": PACKAGE_DOCUMENTATION_URL,
        "author": {
            "name": PACKAGE_AUTHOR_NAME,
        },
        "keywords": PACKAGE_KEYWORDS,
    }
    if repository_url:
        package_json["repository"] = {
            "type": "git",
            "url": repository_url,
        }
    return package_json


def build_release_readme(version: str) -> str:
    return textwrap.dedent(
        f"""\
        # {PACKAGE_NAME}

        Version: {version}

        This package contains the current NNRP managed client surface plus packaged native bridge plugins generated by CI.

        Included managed assemblies:

        - Nnrp.Core
        - Nnrp.Client
        - Nnrp.Transport.Tcp
        - Nnrp.NativeBridge

        Included native plugins are placed under Runtime/Plugins for the supported desktop platforms built by CI.

        Full protocol and SDK documentation: {PACKAGE_DOCUMENTATION_URL}
        """
    )


def build_tracked_readme(version: str) -> str:
    return textwrap.dedent(
        f"""\
        # {PACKAGE_NAME}

        Version: {version}

        This tracked package definition exists so OpenUPM can discover the package metadata directly from the repository.

        Installable UPM tarballs are produced by CI and published as GitHub Release assets for each tagged version.

        Full protocol and SDK documentation: {PACKAGE_DOCUMENTATION_URL}
        """
    )


def write_text_file(path: Path, content: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content, encoding="utf-8")


def validate_text_file(path: Path, expected_content: str) -> None:
    if not path.exists():
        raise ValueError(f"Expected tracked metadata file was not found: {path}")

    actual_content = path.read_text(encoding="utf-8")
    if actual_content != expected_content:
        raise ValueError(
            f"Tracked metadata file is out of date: {path}. Regenerate it with scripts/build_upm_package.py --repo-root . --tracked-output {path.parent.as_posix()} --repository-url <repo-url>"
        )


def write_package_manifest(output_root: Path, version: str, repository_url: str) -> None:
    write_text_file(output_root / "package.json", json.dumps(build_package_manifest(version, repository_url), indent=2) + "\n")


def write_package_readme(output_root: Path, version: str) -> None:
    write_text_file(output_root / "README.md", build_release_readme(version))


def sync_tracked_metadata(output_root: Path, version: str, repository_url: str, validate_only: bool) -> None:
    manifest_content = json.dumps(build_package_manifest(version, repository_url), indent=2) + "\n"
    readme_content = build_tracked_readme(version)

    if validate_only:
        validate_text_file(output_root / "package.json", manifest_content)
        validate_text_file(output_root / "README.md", readme_content)
        return

    write_text_file(output_root / "package.json", manifest_content)
    write_text_file(output_root / "README.md", readme_content)


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

    release_version = read_release_version(repo_root)

    if args.tracked_output:
        tracked_output = Path(args.tracked_output).resolve()
        tracked_version = read_tracked_metadata_version(repo_root)
        sync_tracked_metadata(tracked_output, tracked_version, args.repository_url, args.validate_tracked_metadata)
        print(tracked_output)

    if args.output and args.native_root:
        native_root = Path(args.native_root).resolve()
        output_root = Path(args.output).resolve()

        if output_root.exists():
            shutil.rmtree(output_root)
        output_root.mkdir(parents=True, exist_ok=True)

        copy_managed_artifacts(repo_root, args.configuration, output_root)
        copy_native_artifacts(native_root, output_root)
        write_package_manifest(output_root, release_version, args.repository_url)
        write_package_readme(output_root, release_version)
        write_license(repo_root, output_root)
        emit_meta_files(output_root)

        print(output_root)

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
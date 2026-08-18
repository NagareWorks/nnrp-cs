from __future__ import annotations

import argparse
import subprocess
import tempfile
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
EXPECTED_DEPENDENCIES = {
    "Nnrp.Core": set(),
    "Nnrp.NativeBridge": {"Nnrp.Core"},
    "Nnrp.Client": {"Nnrp.Core", "Nnrp.NativeBridge"},
    "Nnrp.Server": {"Nnrp.Core", "Nnrp.NativeBridge"},
    **{
        package_id: {"Nnrp.Core", "Nnrp.NativeBridge"}
        for package_id in TRANSPORT_PACKAGES
    },
}
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
    parser.add_argument(
        "--smoke-install",
        action="store_true",
        help="Restore and build clean client/server projects from the verified package directory.",
    )
    return parser.parse_args()


def package_metadata(archive: zipfile.ZipFile) -> tuple[str, str, ET.Element]:
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
    return values.get("id", ""), values.get("version", ""), metadata


def child(metadata: ET.Element, name: str) -> ET.Element | None:
    return next(
        (node for node in metadata if node.tag.rsplit("}", 1)[-1] == name),
        None,
    )


def dependency_ids(metadata: ET.Element) -> set[str]:
    dependencies = child(metadata, "dependencies")
    if dependencies is None:
        return set()
    return {
        node.attrib.get("id", "")
        for node in dependencies.iter()
        if node.tag.rsplit("}", 1)[-1] == "dependency"
    }


def verify_package_metadata(
    archive: zipfile.ZipFile,
    package_id: str,
    metadata: ET.Element,
) -> None:
    names = [name.replace("\\", "/") for name in archive.namelist()]
    if len(names) != len(set(names)):
        raise ValueError(f"{package_id}: package contains duplicate archive entries")

    required_files = {
        "README.md",
        "LICENSE",
        f"lib/netstandard2.1/{package_id}.dll",
    }
    missing_files = sorted(required_files - set(names))
    if missing_files:
        raise ValueError(f"{package_id}: package files are missing: {missing_files}")

    required_text = {
        "authors": "NNRP Contributors",
        "description": None,
        "tags": "nnrp",
        "readme": "README.md",
    }
    for field, expected in required_text.items():
        node = child(metadata, field)
        value = (node.text or "").strip() if node is not None else ""
        if not value or (expected is not None and expected not in value):
            raise ValueError(f"{package_id}: nuspec {field} metadata is missing or invalid")

    license_node = child(metadata, "license")
    if (
        license_node is None
        or license_node.attrib.get("type") != "file"
        or (license_node.text or "").strip() != "LICENSE"
    ):
        raise ValueError(f"{package_id}: nuspec license must reference the packaged LICENSE file")

    repository = child(metadata, "repository")
    if (
        repository is None
        or repository.attrib.get("type") != "git"
        or repository.attrib.get("url") != "https://github.com/NagareWorks/nnrp-cs"
    ):
        raise ValueError(f"{package_id}: nuspec repository metadata is missing or invalid")

    actual_dependencies = dependency_ids(metadata)
    expected_dependencies = EXPECTED_DEPENDENCIES[package_id]
    if actual_dependencies != expected_dependencies:
        raise ValueError(
            f"{package_id}: dependency boundary mismatch; "
            f"expected={sorted(expected_dependencies)}, actual={sorted(actual_dependencies)}"
        )


def verify_symbol_package(package_path: Path, package_id: str, version: str) -> None:
    symbol_path = package_path.with_name(f"{package_id}.{version}.snupkg")
    if not symbol_path.is_file():
        raise ValueError(f"{package_id}: missing symbol package {symbol_path.name}")
    with zipfile.ZipFile(symbol_path) as archive:
        names = {name.replace("\\", "/") for name in archive.namelist()}
        expected_pdb = f"lib/netstandard2.1/{package_id}.pdb"
        if expected_pdb not in names:
            raise ValueError(f"{package_id}: symbol package is missing {expected_pdb}")


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
            package_id, package_version, metadata = package_metadata(archive)
            if package_id not in EXPECTED_PACKAGE_IDS:
                raise ValueError(f"{package_path.name}: unexpected package id {package_id!r}")
            if package_id in discovered:
                raise ValueError(f"duplicate package id {package_id}: {package_path.name}")
            if package_version != version:
                raise ValueError(
                    f"{package_path.name}: expected version {version}, found {package_version}"
                )

            verify_package_metadata(archive, package_id, metadata)

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
            verify_symbol_package(package_path, package_id, version)

    missing_packages = sorted(EXPECTED_PACKAGE_IDS - set(discovered))
    if missing_packages:
        raise ValueError(f"missing required NuGet packages: {missing_packages}")


def write_smoke_nuget_config(path: Path, package_root: Path) -> None:
    configuration = ET.Element("configuration")
    package_sources = ET.SubElement(configuration, "packageSources")
    ET.SubElement(package_sources, "clear")
    ET.SubElement(
        package_sources,
        "add",
        key="NNRP Local",
        value=str(package_root),
    )
    ET.SubElement(
        package_sources,
        "add",
        key="NuGet.org",
        value="https://api.nuget.org/v3/index.json",
    )
    source_mapping = ET.SubElement(configuration, "packageSourceMapping")
    local_source = ET.SubElement(source_mapping, "packageSource", key="NNRP Local")
    ET.SubElement(local_source, "package", pattern="Nnrp.*")
    nuget_source = ET.SubElement(source_mapping, "packageSource", key="NuGet.org")
    ET.SubElement(nuget_source, "package", pattern="Microsoft.*")
    ET.ElementTree(configuration).write(
        path,
        encoding="utf-8",
        xml_declaration=True,
    )


def smoke_install(package_root: Path, version: str) -> None:
    temporary_root = package_root.parent / ".tmp"
    temporary_root.mkdir(parents=True, exist_ok=True)
    try:
        with tempfile.TemporaryDirectory(
            prefix="nnrp-cs-package-smoke-",
            dir=temporary_root,
        ) as temp_dir:
            root = Path(temp_dir)
            nuget_config = root / "NuGet.Config"
            write_smoke_nuget_config(nuget_config, package_root)
            projects = {
                "ClientSmoke": [
                    "Nnrp.Client",
                    "Nnrp.Transport.Tcp",
                    "Nnrp.Transport.Quic",
                    "Nnrp.Transport.Ipc",
                    "Nnrp.Transport.WebSocket",
                ],
                "ServerSmoke": [
                    "Nnrp.Server",
                    "Nnrp.Transport.Tcp",
                    "Nnrp.Transport.Quic",
                    "Nnrp.Transport.Ipc",
                    "Nnrp.Transport.WebSocket",
                ],
            }
            for project_name, package_ids in projects.items():
                project_root = root / project_name
                project_root.mkdir()
                references = "\n".join(
                    f'    <PackageReference Include="{package_id}" Version="{version}" />'
                    for package_id in package_ids
                )
                (project_root / f"{project_name}.csproj").write_text(
                    "<Project Sdk=\"Microsoft.NET.Sdk\">\n"
                    "  <PropertyGroup>\n"
                    "    <OutputType>Exe</OutputType>\n"
                    "    <TargetFramework>net8.0</TargetFramework>\n"
                    "  </PropertyGroup>\n"
                    "  <ItemGroup>\n"
                    f"{references}\n"
                    "  </ItemGroup>\n"
                    "</Project>\n",
                    encoding="utf-8",
                )
                role_type = (
                    "Nnrp.Client.NnrpClientOptions"
                    if project_name == "ClientSmoke"
                    else "Nnrp.Server.NnrpServerOptions"
                )
                (project_root / "Program.cs").write_text(
                    "using System;\n"
                    f'Console.WriteLine(typeof({role_type}).Assembly.GetName().Name);\n'
                    "Console.WriteLine(typeof(Nnrp.Transport.Tcp.NnrpNativeTcpTransportProvider).Assembly.GetName().Name);\n"
                    "Console.WriteLine(typeof(Nnrp.Transport.Quic.NnrpNativeQuicTransportProvider).Assembly.GetName().Name);\n"
                    "Console.WriteLine(typeof(Nnrp.Transport.Ipc.NnrpNativeIpcTransportProvider).Assembly.GetName().Name);\n"
                    "Console.WriteLine(typeof(Nnrp.Transport.WebSocket.NnrpNativeWebSocketTransportProvider).Assembly.GetName().Name);\n",
                    encoding="utf-8",
                )
                subprocess.run(
                    [
                        "dotnet",
                        "restore",
                        str(project_root),
                        "--configfile",
                        str(nuget_config),
                        "--packages",
                        str(root / ".nuget" / "packages"),
                        "--no-http-cache",
                    ],
                    check=True,
                )
                subprocess.run(
                    [
                        "dotnet",
                        "build",
                        str(project_root),
                        "--no-restore",
                        "--configuration",
                        "Release",
                    ],
                    check=True,
                )
    finally:
        try:
            temporary_root.rmdir()
        except OSError:
            pass


def main() -> int:
    args = parse_args()
    verify_packages(args.packages.resolve(), args.version)
    if args.smoke_install:
        smoke_install(args.packages.resolve(), args.version)
    print(
        f"verified {len(EXPECTED_PACKAGE_IDS)} NuGet packages at {args.version}; "
        "role packages are native-free and every transport owns its complete RID matrix"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

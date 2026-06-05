from __future__ import annotations

import importlib.util
import shutil
import tempfile
import unittest
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[2]
SCRIPT_PATH = REPO_ROOT / "scripts" / "build_upm_package.py"


def load_packaging_module():
    spec = importlib.util.spec_from_file_location("build_upm_package", SCRIPT_PATH)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Unable to load packaging script: {SCRIPT_PATH}")

    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


packaging = load_packaging_module()


class UpmPackageMetadataTests(unittest.TestCase):
    def test_stable_guid_normalizes_paths(self) -> None:
        first = packaging.stable_guid("Runtime/Plugins/Windows/x86_64/nnrp_ffi.dll")
        second = packaging.stable_guid(r"runtime\plugins\windows\x86_64\nnrp_ffi.dll")

        self.assertEqual(first, second)
        self.assertEqual(32, len(first))
        self.assertNotEqual(first, packaging.stable_guid("Runtime/Managed/Nnrp.Core.dll"))

    def test_supported_native_layout_has_unity_importer_settings(self) -> None:
        expected_rids = {
            "win-x86",
            "win-x64",
            "win-arm64",
            "linux-x86",
            "linux-x64",
            "linux-arm",
            "linux-arm64",
            "osx-x64",
            "osx-arm64",
            "android-x86",
            "android-x64",
            "android-arm",
            "android-arm64",
            "ios-arm64",
            "iossimulator-arm64",
            "iossimulator-x64",
        }

        self.assertEqual(expected_rids, set(packaging.NATIVE_LAYOUT))
        self.assertEqual(expected_rids, set(packaging.NATIVE_PLUGIN_SETTINGS))

        for rid, (_, relative_output) in packaging.NATIVE_LAYOUT.items():
            relative_path = relative_output.as_posix()
            self.assertEqual(rid, packaging.native_plugin_rid_for_relative_path(relative_path))

            metadata = packaging.native_plugin_meta(relative_path, rid)
            platform, cpu = packaging.NATIVE_PLUGIN_SETTINGS[rid]

            self.assertIn("PluginImporter:", metadata)
            self.assertIn("Any: Any", metadata)
            self.assertIn("enabled: 0", metadata)
            self.assertIn(f"{platform}: {platform}", metadata)
            self.assertIn(f"CPU: {cpu}", metadata)

        self.assertIsNone(packaging.native_plugin_rid_for_relative_path("Runtime/Plugins/Android/arm64/libnnrp_ffi.so"))

    def test_emit_meta_files_covers_folders_managed_and_native_plugins(self) -> None:
        temp_root = Path(tempfile.mkdtemp(prefix="nnrp-upm-meta-test-"))
        try:
            self.write_file(temp_root / "README.md")
            self.write_file(temp_root / "Runtime" / "Managed" / "Nnrp.Core.dll")
            self.write_file(temp_root / "Runtime" / "Managed" / "Nnrp.Core.xml", "<doc />")

            for _, relative_output in packaging.NATIVE_LAYOUT.values():
                self.write_file(temp_root / relative_output)

            packaging.emit_meta_files(temp_root)

            metadata_snapshot = {
                path.relative_to(temp_root).as_posix(): path.read_text(encoding="utf-8")
                for path in sorted(temp_root.rglob("*.meta"))
            }

            expected_meta_paths = {
                "README.md.meta",
                "Runtime.meta",
                "Runtime/Managed.meta",
                "Runtime/Managed/Nnrp.Core.dll.meta",
                "Runtime/Managed/Nnrp.Core.xml.meta",
                "Runtime/Plugins.meta",
                "Runtime/Plugins/Windows.meta",
                "Runtime/Plugins/Windows/x86_64.meta",
                "Runtime/Plugins/Windows/x86_64/nnrp_ffi.dll.meta",
                "Runtime/Plugins/Linux.meta",
                "Runtime/Plugins/Linux/x86_64.meta",
                "Runtime/Plugins/Linux/x86_64/libnnrp_ffi.so.meta",
                "Runtime/Plugins/macOS.meta",
                "Runtime/Plugins/macOS/x86_64.meta",
                "Runtime/Plugins/macOS/x86_64/libnnrp_ffi.dylib.meta",
                "Runtime/Plugins/macOS/arm64.meta",
                "Runtime/Plugins/macOS/arm64/libnnrp_ffi.dylib.meta",
                "Runtime/Plugins/Android.meta",
                "Runtime/Plugins/Android/arm64-v8a.meta",
                "Runtime/Plugins/Android/arm64-v8a/libnnrp_ffi.so.meta",
                "Runtime/Plugins/iOS.meta",
                "Runtime/Plugins/iOS/arm64.meta",
                "Runtime/Plugins/iOS/arm64/libnnrp_ffi.a.meta",
                "Runtime/Plugins/iOSSimulator.meta",
                "Runtime/Plugins/iOSSimulator/x86_64.meta",
                "Runtime/Plugins/iOSSimulator/x86_64/libnnrp_ffi.a.meta",
            }
            self.assertTrue(expected_meta_paths.issubset(metadata_snapshot))

            managed_meta = metadata_snapshot["Runtime/Managed/Nnrp.Core.dll.meta"]
            self.assertIn("PluginImporter:", managed_meta)
            self.assertIn("Any: Any", managed_meta)
            self.assertIn("enabled: 1", managed_meta)

            text_meta = metadata_snapshot["Runtime/Managed/Nnrp.Core.xml.meta"]
            self.assertIn("DefaultImporter:", text_meta)
            self.assertNotIn("PluginImporter:", text_meta)

            windows_meta = metadata_snapshot["Runtime/Plugins/Windows/x86_64/nnrp_ffi.dll.meta"]
            linux_meta = metadata_snapshot["Runtime/Plugins/Linux/x86_64/libnnrp_ffi.so.meta"]
            mac_arm_meta = metadata_snapshot["Runtime/Plugins/macOS/arm64/libnnrp_ffi.dylib.meta"]
            android_arm_meta = metadata_snapshot["Runtime/Plugins/Android/arm64-v8a/libnnrp_ffi.so.meta"]
            ios_arm_meta = metadata_snapshot["Runtime/Plugins/iOS/arm64/libnnrp_ffi.a.meta"]

            self.assertIn("Windows: Windows", windows_meta)
            self.assertIn("CPU: x86_64", windows_meta)
            self.assertIn("Linux: Linux", linux_meta)
            self.assertIn("CPU: x86_64", linux_meta)
            self.assertIn("OSX: OSX", mac_arm_meta)
            self.assertIn("CPU: ARM64", mac_arm_meta)
            self.assertIn("Android: Android", android_arm_meta)
            self.assertIn("CPU: ARM64", android_arm_meta)
            self.assertIn("iOS: iOS", ios_arm_meta)
            self.assertIn("CPU: ARM64", ios_arm_meta)

            guids = []
            for content in metadata_snapshot.values():
                guid_line = next(line for line in content.splitlines() if line.startswith("guid: "))
                guids.append(guid_line.removeprefix("guid: "))
            self.assertEqual(len(guids), len(set(guids)))

            packaging.emit_meta_files(temp_root)
            rerun_snapshot = {
                path.relative_to(temp_root).as_posix(): path.read_text(encoding="utf-8")
                for path in sorted(temp_root.rglob("*.meta"))
            }
            self.assertEqual(metadata_snapshot, rerun_snapshot)
        finally:
            shutil.rmtree(temp_root)

    @staticmethod
    def write_file(path: Path, content: str = "") -> None:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(content, encoding="utf-8")


if __name__ == "__main__":
    unittest.main()

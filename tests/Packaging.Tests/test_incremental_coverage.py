import subprocess
import tempfile
import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
SCRIPT_PATH = REPOSITORY_ROOT / "scripts" / "check_incremental_coverage.ps1"


class IncrementalCoverageTests(unittest.TestCase):
    def test_single_line_hunk_is_counted(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            repository = Path(temporary_directory)
            source_path = repository / "src" / "Sample.cs"
            coverage_root = repository / "coverage"
            source_path.parent.mkdir(parents=True)
            coverage_root.mkdir()

            self.run_git(repository, "init")
            self.run_git(repository, "config", "user.name", "NNRP Tests")
            self.run_git(repository, "config", "user.email", "tests@nnrp.invalid")

            source_path.write_text(self.source_text(1), encoding="utf-8")
            self.run_git(repository, "add", "src/Sample.cs")
            self.run_git(repository, "commit", "-m", "baseline")

            source_path.write_text(self.source_text(2), encoding="utf-8")
            self.run_git(repository, "add", "src/Sample.cs")
            self.run_git(repository, "commit", "-m", "change one line")
            (coverage_root / "coverage.cobertura.xml").write_text(
                self.coverage_xml(repository, line_number=5, hits=1),
                encoding="utf-8",
            )

            result = subprocess.run(
                [
                    "pwsh",
                    "-NoProfile",
                    "-File",
                    str(SCRIPT_PATH),
                    "-BaseSha",
                    "HEAD^",
                    "-HeadSha",
                    "HEAD",
                    "-Threshold",
                    "90",
                    "-RepoRoot",
                    str(repository),
                    "-CoverageRoot",
                    str(coverage_root),
                ],
                check=False,
                capture_output=True,
                text=True,
            )

            self.assertEqual(0, result.returncode, result.stdout + result.stderr)
            self.assertIn("Incremental line coverage: 100% (1/1", result.stdout)
            self.assertNotIn("Skipping incremental coverage gate", result.stdout)

    @staticmethod
    def run_git(repository: Path, *arguments: str) -> None:
        subprocess.run(
            ["git", *arguments],
            cwd=repository,
            check=True,
            capture_output=True,
            text=True,
        )

    @staticmethod
    def source_text(value: int) -> str:
        return (
            "namespace Sample;\n"
            "public static class Value\n"
            "{\n"
            "    // Keep the changed statement on a stable line.\n"
            f"    public static int Get() => {value};\n"
            "}\n"
        )

    @staticmethod
    def coverage_xml(repository: Path, line_number: int, hits: int) -> str:
        source_root = str(repository).replace("&", "&amp;")
        return f"""<?xml version="1.0" encoding="utf-8"?>
<coverage>
  <sources><source>{source_root}</source></sources>
  <packages>
    <package name="Sample">
      <classes>
        <class name="Sample.Value" filename="src/Sample.cs">
          <lines><line number="{line_number}" hits="{hits}" /></lines>
        </class>
      </classes>
    </package>
  </packages>
</coverage>
"""


if __name__ == "__main__":
    unittest.main()

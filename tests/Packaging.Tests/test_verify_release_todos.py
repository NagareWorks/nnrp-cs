from __future__ import annotations

import importlib.util
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "scripts" / "verify_release_todos.py"
SPEC = importlib.util.spec_from_file_location("verify_release_todos", SCRIPT)
assert SPEC is not None and SPEC.loader is not None
VERIFIER = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(VERIFIER)


class VerifyReleaseTodosTests(unittest.TestCase):
    def test_accepts_a_closed_todo_tree(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "01-contract.md").write_text("# Contract\n\n- [x] Complete.\n", encoding="utf-8")

            VERIFIER.verify_release_todos(root)

    def test_reports_every_open_checkbox_with_location(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            nested = root / "nested"
            nested.mkdir()
            (root / "01-contract.md").write_text("# Contract\n\n- [ ] First item.\n", encoding="utf-8")
            (nested / "02-runtime.md").write_text("- [ ] Second item.\n", encoding="utf-8")

            with self.assertRaisesRegex(ValueError, "2 open TODO checkbox") as raised:
                VERIFIER.verify_release_todos(root)

            message = str(raised.exception)
            self.assertIn("01-contract.md:3: First item.", message)
            self.assertIn("02-runtime.md:1: Second item.", message)

    def test_rejects_a_missing_todo_tree(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            missing = Path(directory) / "missing"
            with self.assertRaisesRegex(ValueError, "does not exist"):
                VERIFIER.verify_release_todos(missing)


if __name__ == "__main__":
    unittest.main()

from __future__ import annotations

import argparse
import re
from pathlib import Path


OPEN_CHECKBOX = re.compile(r"^\s*- \[ \] (?P<label>.+?)\s*$")


def find_open_todos(todo_root: Path) -> list[tuple[Path, int, str]]:
    open_todos: list[tuple[Path, int, str]] = []
    for path in sorted(todo_root.rglob("*.md")):
        for line_number, line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
            match = OPEN_CHECKBOX.match(line)
            if match is not None:
                open_todos.append((path, line_number, match.group("label")))
    return open_todos


def verify_release_todos(todo_root: Path) -> None:
    if not todo_root.is_dir():
        raise ValueError(f"Preview TODO directory does not exist: {todo_root}")

    open_todos = find_open_todos(todo_root)
    if open_todos:
        details = "\n".join(
            f"  {path}:{line_number}: {label}"
            for path, line_number, label in open_todos
        )
        raise ValueError(f"Preview release has {len(open_todos)} open TODO checkbox(es):\n{details}")


def main() -> None:
    parser = argparse.ArgumentParser(description="Reject releases with open preview TODO checkboxes.")
    parser.add_argument("--todo-root", type=Path, required=True)
    args = parser.parse_args()

    try:
        verify_release_todos(args.todo_root)
    except ValueError as error:
        raise SystemExit(str(error)) from error


if __name__ == "__main__":
    main()

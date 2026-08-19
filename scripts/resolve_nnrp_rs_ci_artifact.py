from __future__ import annotations

import argparse
from pathlib import Path


OS_NAMES = {
    "Linux": "Linux",
    "macOS": "macOS",
    "Windows": "Windows",
}

ARCH_NAMES = {
    "X64": "X64",
    "X86": "X86",
    "ARM64": "ARM64",
}


def artifact_name(runner_os: str, runner_arch: str) -> str:
    try:
        os_name = OS_NAMES[runner_os]
    except KeyError as exc:
        raise ValueError(f"unsupported runner OS: {runner_os}") from exc

    try:
        arch_name = ARCH_NAMES[runner_arch]
    except KeyError as exc:
        raise ValueError(f"unsupported runner architecture: {runner_arch}") from exc

    return f"nnrp-ffi-native-{os_name}-{arch_name}"


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Resolve the coordinated nnrp-rs CI artifact name."
    )
    parser.add_argument("--runner-os", required=True)
    parser.add_argument("--runner-arch", required=True)
    parser.add_argument("--github-output", type=Path)
    args = parser.parse_args()

    name = artifact_name(args.runner_os, args.runner_arch)
    if args.github_output is None:
        print(name)
    else:
        with args.github_output.open("a", encoding="utf-8", newline="\n") as output:
            output.write(f"name={name}\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

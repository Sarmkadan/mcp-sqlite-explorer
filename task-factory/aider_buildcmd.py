#!/usr/bin/env python3
"""
A minimal helper script used by the task‑factory environment to build the
C# solution.

Running this script will invoke `dotnet build` on the solution located in the
current working directory (or its parent directories).  It forwards the exit
code from `dotnet` so that callers can detect build failures.

The script is deliberately lightweight – it does not add any new NuGet
packages or modify the project files.  It simply provides a convenient entry
point for the `aider_buildcmd.py` command that was missing.
"""

import subprocess
import sys
from pathlib import Path

def find_solution(start: Path) -> Path | None:
    """
    Walk up the directory tree from ``start`` looking for a *.sln* file.
    Returns the path to the first solution file found, or ``None`` if no
    solution is discovered.
    """
    for directory in [start, *start.parents]:
        sln_files = list(directory.glob("*.sln"))
        if sln_files:
            # Prefer the first one found; there should normally be only one.
            return sln_files[0]
    return None

def main() -> int:
    cwd = Path.cwd()
    solution = find_solution(cwd)

    if solution is None:
        print("Error: No .sln file found in the current directory or any parent.", file=sys.stderr)
        return 1

    # Run `dotnet build` on the discovered solution.
    cmd = ["dotnet", "build", str(solution), "--configuration", "Release"]
    try:
        result = subprocess.run(cmd, check=False)
        return result.returncode
    except FileNotFoundError:
        print("Error: `dotnet` executable not found. Ensure the .NET SDK is installed and on PATH.", file=sys.stderr)
        return 1
    except Exception as exc:
        print(f"Unexpected error while running dotnet build: {exc}", file=sys.stderr)
        return 1

if __name__ == "__main__":
    sys.exit(main())

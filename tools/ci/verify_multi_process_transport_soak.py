#!/usr/bin/env python3
"""Parallel *transport* soak for real OGL two-process signed NexBus regression.

This exercises 3 simultaneous pairs of separate simulator-style OS processes.
It does NOT test real avatar crossings, native physics or Viewer frame time;
those require a staging grid and the manual acceptance matrix.
"""
from concurrent.futures import ThreadPoolExecutor, as_completed
from pathlib import Path
import subprocess
import time

root = Path(__file__).resolve().parents[2]
project = root / "tools/ci/CrossRegionNexBusProcessRegression/CrossRegionNexBusProcessRegression.csproj"
command = ["dotnet", "run", "--configuration", "Release", "--no-build", "--no-restore",
           "--project", str(project)]

def run_pair(index):
    start = time.monotonic()
    completed = subprocess.run(command, cwd=root, capture_output=True,
                               text=True, timeout=90, check=False)
    elapsed = time.monotonic() - start
    if completed.returncode != 0:
        raise RuntimeError(f"Transport pair {index} failed (exit {completed.returncode}): "
                           + (completed.stdout + "\n" + completed.stderr)[-2500:])
    if ("Cross-region NexBus signed HTTP + real WorldComm listeners: PASS"
            not in completed.stdout or
            "Two independent OS PIDs:" not in completed.stdout):
        raise RuntimeError(f"Transport pair {index} did not prove two-process delivery")
    return index, elapsed

if __name__ == "__main__":
    with ThreadPoolExecutor(max_workers=3) as pool:
        futures = [pool.submit(run_pair, index) for index in range(1, 4)]
        results = sorted(future.result() for future in as_completed(futures))
    for index, elapsed in results:
        print(f"Two-process transport pair {index}: PASS ({elapsed:.2f}s)")
    print("Three simultaneous independent two-process NexBus transports: PASS")
    print("NOT AN AVATAR/PHYSICS/VIEWER CROSSING CERTIFICATION")

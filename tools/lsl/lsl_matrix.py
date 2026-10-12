#!/usr/bin/env python3
"""Machine-readable, fail-closed *source* inventory of OGL LSL contracts.

This is not an automated claim of Second Life behavioural compatibility.
Source presence and runtime conformance are separate evidence dimensions.
"""
import argparse
import json
import re
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
BASE = Path("OpenSim/Region/ScriptEngine/Shared/Api")
IFACE = BASE / "Interface/ILSL_Api.cs"
STUB = BASE / "Runtime/LSL_Stub.cs"
IMPL = BASE / "Implementation/LSL_Api.cs"
CONSTS = BASE / "Runtime/LSL_Constants.cs"
EVENTS = Path("OpenSim/Region/ScriptEngine/YEngine/MMRScriptEventCode.cs")
CATEGORIES = ("FULL", "PARTIAL", "STUB", "MISSING", "INTENTIONALLY_UNSUPPORTED")
DEFERRED = re.compile(r"(Experience|KeyValue|Pathfind|Character)", re.IGNORECASE)
DECL = re.compile(r"^\s*([A-Za-z_][\w.<>]*)\s+(ll[A-Za-z0-9_]+)\s*\(([^)]*)\)\s*;", re.M)
METHOD = re.compile(r"^\s*public\s+(?:(?:virtual|static|override|new|unsafe)\s+)*([A-Za-z_][\w.<>]*)\s+(ll[A-Za-z0-9_]+)\s*\(([^)]*)\)", re.M)
CONST = re.compile(r"^\s*public\s+(?:(?:static\s+readonly|const)\s+)([\w.<>]+)\s+(\w+)\s*=\s*([^;]+);", re.M)
EVT = re.compile(r"^\s*([a-z][\w]*)\s*=\s*(\d+),", re.M)


def signature(result_type, args):
    # Strip parameter *names* to compare API type contracts across
    # ILSL_Api / ScriptBaseClass / LSL_Api without changing aliases.
    params = []
    for raw in args.split(","):
        raw = raw.strip()
        if not raw:
            continue
        fields = raw.split()
        if len(fields) < 2:
            raise ValueError("Ambiguous LSL parameter: " + raw)
        params.append(" ".join(fields[:-1]))
    return {"return_type": result_type, "parameter_types": params}


def methods(path, pattern):
    source = (ROOT / path).read_text(encoding="utf-8-sig")
    items = {}
    for match in pattern.finditer(source):
        result_type, name, args = match.groups()
        if name in items:
            # Overloads may exist: source parity requires a separate
            # signature for each overload, never silently overwrite one.
            items[name].append(signature(result_type, args))
        else:
            items[name] = [signature(result_type, args)]
    return items


def build():
    interface = methods(IFACE, DECL)
    wrappers = methods(STUB, METHOD)
    implementations = methods(IMPL, METHOD)
    if len(interface) < 200:
        raise RuntimeError("Insufficient interface declarations; LSL parser likely broken")
    functions = []
    for name, signatures in sorted(interface.items()):
        wrapper = wrappers.get(name, [])
        implementation = implementations.get(name, [])
        available = bool(wrapper and implementation)
        scope = "POST_0_9_4_0" if DEFERRED.search(name) else "CORE"
        # PARTIAL merely means that code is present, NOT that behaviour
        # or timings are verified. Defer XP even if old methods exist.
        level = "PARTIAL" if available else "MISSING"
        mismatches = {
            "interface_vs_wrapper": [s for s in signatures if s not in wrapper],
            "interface_vs_implementation": [s for s in signatures if s not in implementation],
        }
        functions.append({
            "name": name, "signatures": signatures,
            "support_level": level, "scope": scope,
            "api_wrapper_present": bool(wrapper),
            "implementation_present": bool(implementation),
            "signature_differences": mismatches,
            "behaviour_verified": False, "timing_verified": False,
            "permission_verified": False, "live_evidence": None,
        })
    constants = []
    for type_name, name, value in CONST.findall((ROOT / CONSTS).read_text(encoding="utf-8-sig")):
        constants.append({"name": name, "type": type_name, "expression": value.strip(), "runtime_verified": False})
    events = [{"name": name, "id": int(num), "live_verified": False, "scope": "POST_0_9_4_0" if DEFERRED.search(name) or name == "path_update" else "CORE"}
              for name, num in EVT.findall((ROOT / EVENTS).read_text(encoding="utf-8-sig"))]
    if len(constants) < 100 or len(events) < 25:
        raise RuntimeError("Insufficient constants/events: parser or source drift")
    counts = Counter(f["support_level"] for f in functions)
    return {
        "format": "ogl-lsl-conformance-matrix-v1",
        "source_of_truth": {"interface": str(IFACE), "wrapper": str(STUB), "implementation": str(IMPL), "constants": str(CONSTS), "events": str(EVENTS)},
        "policy": "PARTIAL means source present; no FULL without live/semantic evidence. Deferred functions are not 0.9.3.11/12 release gates.",
        "summary": {"functions": len(functions), "constants": len(constants), "events": len(events),
                    "support": {status: counts[status] for status in CATEGORIES},
                    "deferred_functions": sum(f["scope"] != "CORE" for f in functions)},
        "functions": functions, "constants": sorted(constants, key=lambda x: x["name"]), "events": sorted(events, key=lambda x: x["id"]),
    }


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", default="-", help="JSON destination, default stdout")
    parser.add_argument("--verify", action="store_true", help="Reject missing API/wrapper and suspicious signature drift")
    args = parser.parse_args()
    matrix = build()
    missing = [f["name"] for f in matrix["functions"] if f["support_level"] == "MISSING"]
    if args.verify and missing:
        raise SystemExit("Missing LSL bindings: " + ", ".join(missing))
    payload = json.dumps(matrix, ensure_ascii=False, indent=2, sort_keys=True) + "\n"
    if args.output == "-":
        print(payload, end="")
    else:
        Path(args.output).write_text(payload, encoding="utf-8")
    print(f"LSL interface inventory: {matrix['summary']}", file=__import__("sys").stderr)


if __name__ == "__main__":
    main()

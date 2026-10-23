#!/usr/bin/env python3
"""Compare runtime OpenAPI paths with the three generated ChatGPT documents."""
import argparse
import json
import re
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument("--openapi", type=Path, required=True)
parser.add_argument("--all", type=Path, required=True)
parser.add_argument("--citizen", type=Path, required=True)
parser.add_argument("--admin", type=Path, required=True)
args = parser.parse_args()

spec = json.loads(args.openapi.read_text(encoding="utf-8"))
paths = set(spec.get("paths", {}))
assert len(paths) >= 151, f"OpenAPI unexpectedly small: {len(paths)}"
title = "## Vollstaendiges Pfadverzeichnis der deklarierten OpenAPI-World-API"

def parse(path):
    text = path.read_text(encoding="utf-8")
    assert title in text, f"Missing complete route index in {path.name}"
    appendix = text.split(title, 1)[1]
    return text, re.findall(r"^- `(/[^`]+)`$", appendix, flags=re.MULTILINE)

all_doc, all_paths = parse(args.all)
citizen_doc, citizen_paths = parse(args.citizen)
admin_doc, admin_paths = parse(args.admin)

assert len(all_paths) == len(set(all_paths)), "Duplicate full-manual paths"
assert set(all_paths) == paths, (
    f"Runtime ChatGPT/OpenAPI disagreement: missing={sorted(paths-set(all_paths))} "
    f"unknown={sorted(set(all_paths)-paths)}"
)
assert set(citizen_paths) <= paths and set(admin_paths) <= paths
assert len(citizen_paths) == len(set(citizen_paths))
assert len(admin_paths) == len(set(admin_paths))
assert len(citizen_paths) < len(all_paths)
assert len(admin_paths) < len(all_paths)
assert {"/api/v1/users/me", "/api/v1/inventory/tree",
        "/api/v1/banking/transactions", "/api/v1/groups/mine"} <= set(citizen_paths)
assert {"/api/v1/audit", "/api/v1/auth/clients",
        "/api/v1/users/{principalId}/authorization",
        "/api/v1/regions/{regionId}/lifecycle"} <= set(admin_paths)
assert {"/api/v1/users/{principalId}/authorization",
        "/api/v1/audit", "/api/v1/auth/api-keys"}.isdisjoint(citizen_paths), (
            "Admin-only capabilities exposed as citizen route index"
        )
for path in (
    "/api/v1/experiences/viewer/permissions",
    "/api/v1/experiences/viewer/manage",
    "/api/v1/experiences/script/kv",
    "/api/v1/portal/avatars/register",
):
    assert path not in citizen_paths, f"Private service bridge exposed in citizen index: {path}"
    assert path in admin_paths, f"Admin guide lacks service boundary catalog: {path}"
for doc in (all_doc, citizen_doc, admin_doc):
    for phrase in (
        "100.000", "statistics:read", "economy:read", "inventory:read",
        "default_scopes", "OpenAPI", "X-NexVerse-Api-Key",
        "## Praxisablauf fuer jede ChatGPT-Aufgabe",
        "## Authentifizierung, Rollen und aktuelle Scope-Automatik",
    ):
        assert phrase in doc, f"Missing critical AI/security guidance: {phrase}"
assert "## Vollstaendige Administrations-Funktionskarte" in all_doc
assert "## Vollstaendige Administrations-Funktionskarte" in admin_doc
assert "## Vollstaendige Administrations-Funktionskarte" not in citizen_doc
assert "## Vollstaendiger Buergerportal-Funktionsumfang" in all_doc
assert "## Vollstaendiger Buergerportal-Funktionsumfang" in citizen_doc
assert "## Vollstaendiger Buergerportal-Funktionsumfang" not in admin_doc
print(
    f"Runtime ChatGPT documents: {len(paths)} canonical OpenAPI paths; "
    f"complete={len(all_paths)}, citizen={len(citizen_paths)}, "
    f"admin={len(admin_paths)}; audience and security boundaries OK"
)

#!/usr/bin/env python3
"""Static + SQLite regression for the OGL roaming grant schema.
Run: python3 tools/ci/verify_ogl_hg_roaming_grants.py
"""
from pathlib import Path
import re
import sqlite3
import time

ROOT = Path(__file__).resolve().parents[2]
schema = (ROOT / "OpenSim/Services/InventoryService/OglHgRoamingGrantSchema.cs").read_text()
repository = (ROOT / "OpenSim/Services/InventoryService/OglHgRoamingGrantRepository.cs").read_text()
match = re.search(r'case OglHgGrantDatabase.Sqlite:\s*return @"(.*?)";', schema, re.S)
assert match, "SQLite DDL missing"
ddl = match.group(1)
db = sqlite3.connect(":memory:")
db.executescript(ddl)
db.executescript(ddl)
now = int(time.time())
resident, session, destination = "resident", "session", "https://example.org"
db.execute(
    "INSERT INTO ogl_hg_roaming_grants VALUES (?,?,?,?,?,?,?)",
    ("grant", resident, session, destination, now + 120, None, now)
)
predicate = (
    "resident_id = ? AND session_id = ? AND destination_uri = ? "
    "AND revoked_utc IS NULL AND created_utc <= ? AND expires_utc > ?"
)
def active(at, grid=destination):
    return db.execute("SELECT COUNT(*) FROM ogl_hg_roaming_grants WHERE " + predicate,
                      (resident, session, grid, at, at)).fetchone()[0] > 0
assert active(now)
assert not active(now, "https://other.example.org")
assert not active(now + 121)
db.execute("UPDATE ogl_hg_roaming_grants SET revoked_utc = ? "
           "WHERE resident_id = ? AND session_id = ? AND revoked_utc IS NULL",
           (now, resident, session))
assert not active(now)
assert "TryRevokeSession" in repository
assert "TryPurgeInactiveGrants" in repository
assert "ValidDestination(destination)" in repository
print("PASS: SQLite schema idempotence, active, destination, expiry, revoke; repository hooks")

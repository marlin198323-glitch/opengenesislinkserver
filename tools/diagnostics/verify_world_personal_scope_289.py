#!/usr/bin/env python3
"""Read-only Issue #289 live acceptance: personal bearer, NOT a machine API key.

Export WORLD_PERSONAL_BEARER privately in your shell (never in GitHub), then run:
 python3 tools/diagnostics/verify_world_personal_scope_289.py --expect statistics:read regions:read
Only HTTPS, only safe GETs; no tokens, principals or personal data printed.
"""
import argparse
import base64
import json
import os
import sys
import urllib.error
import urllib.parse
import urllib.request


def get(base, path, token):
    request = urllib.request.Request(
        base + path,
        headers={"Authorization": "Bearer " + token, "Accept": "application/json"},
        method="GET",
    )
    try:
        with urllib.request.urlopen(request, timeout=18) as response:
            status = response.status
            raw = response.read(2_000_000)
            return status, json.loads(raw.decode("utf-8"))
    except urllib.error.HTTPError as failure:
        raw = failure.read(8192)
        try:
            parsed = json.loads(raw.decode("utf-8"))
        except (ValueError, UnicodeError):
            parsed = {}
        return failure.code, parsed


def inspect_claims_without_trusting_them(token):
    """JWT payload fields are diagnostic only; the World server validates the signature."""
    try:
        payload = token.split(".")[1]
        payload += "=" * (-len(payload) % 4)
        obj = json.loads(base64.urlsafe_b64decode(payload))
        value = obj.get("scope", "")
        return set(value.split()) if isinstance(value, str) else set()
    except (ValueError, IndexError, UnicodeError):
        return set()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base", default="https://world.stadt-nexverse.de")
    parser.add_argument("--expect", nargs="+", required=True,
                        help="Exact individual scope names to verify")
    args = parser.parse_args()
    parsed = urllib.parse.urlsplit(args.base)
    if parsed.scheme != "https" or not parsed.hostname or parsed.username or parsed.password or parsed.query or parsed.fragment:
        parser.error("Only an HTTPS base URL without credentials/query/fragment is permitted")
    base = args.base.rstrip("/")
    token = os.environ.get("WORLD_PERSONAL_BEARER", "")
    if not token or len(token) < 24:
        parser.error("WORLD_PERSONAL_BEARER must contain a freshly issued personal World Bearer (not a machine API key)")
    expected = set(args.expect)
    claims = inspect_claims_without_trusting_them(token)

    me_status, me = get(base, "/api/v1/users/me", token)
    if me_status != 200 or not isinstance(me, dict) or not me.get("principal_id"):
        print("PERSONAL_WORLD_BEARER_VALIDATION=FAIL", "http=" + str(me_status),
              "error=" + str(me.get("error", "unknown") if isinstance(me, dict) else "bad_json"))
        return 1
    print("PERSONAL_WORLD_BEARER_VALIDATION=PASS")
    print("TOKEN_SCOPE_NAMES_UNVERIFIED=" + " ".join(sorted(claims)))
    print("EXPECTED_EXPLICIT_IN_TOKEN=" + str(all(x in claims for x in expected)).lower())
    print("ASSIGNED_EXPLICIT_NAMES=" + " ".join(sorted(me.get("explicit_scopes", []))))
    if not expected.issubset(claims):
        print("RESULT=FAIL; personal token lacks explicit scopes, grant via World user authorization and login again")
        return 1
    if "statistics:read" in expected:
        status, summary = get(base, "/api/v1/statistics/summary", token)
        protected = isinstance(summary, dict) and summary.get("protected_details") is True
        print("WORLD_PROTECTED_STATISTICS_HTTP=" + str(status))
        print("WORLD_PROTECTED_STATISTICS_AUTHORIZED=" + str(protected).lower())
        if status != 200 or not protected:
            print("RESULT=FAIL; scope string alone does not prove authorization / protected data")
            return 1
    print("RESULT=PASS; freshly presented personal World bearer has requested explicit scopes")
    return 0


if __name__ == "__main__":
    sys.exit(main())

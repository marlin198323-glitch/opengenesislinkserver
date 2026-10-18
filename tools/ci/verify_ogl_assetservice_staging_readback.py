#!/usr/bin/env python3
"""Read-only FSAssets -> OGL compatibility comparison against staging ROBUST.

Run CAPTURE with the legacy FSAssets connector, stop that staging ROBUST,
then run VERIFY with the OGL connector against the SAME isolated SQL/filesystem
clone. Only HTTP GET is used; no asset content is copied into the manifest.
Production data and public endpoints must never be used for this rehearsal.
"""
import argparse
import base64
import hashlib
import json
import os
import re
import sys
import tempfile
import threading
import urllib.error
import urllib.parse
import urllib.request
import uuid
import xml.etree.ElementTree as ET
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

MAX_PAYLOAD = 64 * 1024 * 1024
MAX_ASSETS = 100
FIELDS = ("ID", "Name", "Description", "Type", "CreatorID", "Local",
          "Temporary", "Flags", "CreationDate")
UUID_PATTERN = re.compile(r"^[0-9a-fA-F]{8}-(?:[0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}$")


def staging_url(value):
    parsed = urllib.parse.urlsplit(value)
    if (parsed.scheme != "http" or parsed.hostname not in ("127.0.0.1", "localhost")
            or parsed.username is not None or parsed.password is not None
            or parsed.query or parsed.fragment or parsed.path not in ("", "/")
            or parsed.port is None):
        raise ValueError("Only an explicit http://127.0.0.1:PORT staging URL is accepted")
    return f"http://127.0.0.1:{parsed.port}"


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


OPENER = urllib.request.build_opener(NoRedirect)


def http_get(endpoint, path):
    url = endpoint + path
    req = urllib.request.Request(url, method="GET")
    try:
        with OPENER.open(req, timeout=15) as response:
            if response.status != 200:
                raise ValueError(f"HTTP {response.status} for {path}")
            data = response.read(MAX_PAYLOAD + 1)
            if len(data) > MAX_PAYLOAD:
                raise ValueError("Asset response exceeds 64 MiB maximum")
            return data
    except urllib.error.HTTPError as exc:
        raise ValueError(f"HTTP {exc.code} for {path}") from exc


def fields(data, root_name):
    root = ET.fromstring(data)
    if root.tag.rsplit("}", 1)[-1] != root_name:
        raise ValueError(f"Unexpected XML root instead of {root_name}")
    found = {}
    for item in root:
        key = item.tag.rsplit("}", 1)[-1]
        if key in FIELDS:
            found[key] = item.text or ""
    return found, root


def snapshot(endpoint, asset_id):
    path = "/assets/" + asset_id
    full_fields, full_root = fields(http_get(endpoint, path), "AssetBase")
    meta_fields, _ = fields(http_get(endpoint, path + "/metadata"), "AssetMetadata")
    full_bytes = None
    for element in full_root:
        if element.tag.rsplit("}", 1)[-1] == "Data":
            full_bytes = base64.b64decode(element.text or "", validate=True)
            break
    if full_bytes is None:
        raise ValueError(f"Asset XML lacks Data for {asset_id}")
    binary = http_get(endpoint, path + "/data")
    if binary != full_bytes:
        raise ValueError(f"Get/GetData differ for {asset_id}")
    if full_fields.get("ID", "").lower() != asset_id.lower():
        raise ValueError(f"Unexpected asset ID in full GET: {asset_id}")
    if meta_fields.get("ID", "").lower() != asset_id.lower():
        raise ValueError(f"Unexpected asset ID in metadata: {asset_id}")
    for name in ("Name", "Type"):
        if name in full_fields and name in meta_fields and full_fields[name] != meta_fields[name]:
            raise ValueError(f"Inconsistent {name} for {asset_id}")
    return {"id": asset_id.lower(),
            "bytes": len(binary),
            "sha256": hashlib.sha256(binary).hexdigest(),
            "full": full_fields, "metadata": meta_fields}


def asset_ids(path):
    ids = []
    seen = set()
    for line in path.read_text(encoding="utf-8").splitlines():
        value = line.strip()
        if not value or value.startswith("#"):
            continue
        if not UUID_PATTERN.fullmatch(value):
            raise ValueError("IDs file must contain UUIDs only, one per line")
        if value.lower() in seen:
            raise ValueError("Duplicate UUID in IDs file")
        ids.append(value.lower())
        seen.add(value.lower())
        if len(ids) > MAX_ASSETS:
            raise ValueError(f"Maximum {MAX_ASSETS} selected assets per check")
    if not ids:
        raise ValueError("No asset UUIDs selected for staging comparison")
    return ids


def capture(endpoint, ids, manifest_path):
    if manifest_path.exists():
        raise ValueError("Refusing to overwrite existing baseline manifest")
    entries = [snapshot(endpoint, asset_id) for asset_id in ids]
    manifest = {"format": "ogl-staging-readback-v1", "entries": entries}
    fd = os.open(str(manifest_path), os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    try:
        with os.fdopen(fd, "w", encoding="utf-8") as output:
            json.dump(manifest, output, ensure_ascii=False, indent=2, sort_keys=True)
            output.write("\n")
    except BaseException:
        manifest_path.unlink(missing_ok=True)
        raise
    print(f"Legacy staging baseline captured: {len(entries)} assets (GET only)")


def verify(endpoint, manifest_path):
    baseline = json.loads(manifest_path.read_text(encoding="utf-8"))
    if baseline.get("format") != "ogl-staging-readback-v1":
        raise ValueError("Unsupported manifest format")
    entries = baseline.get("entries")
    if not isinstance(entries, list) or not 1 <= len(entries) <= MAX_ASSETS:
        raise ValueError("Invalid manifest entries")
    mismatches = []
    for expected in entries:
        asset_id = expected["id"]
        if not UUID_PATTERN.fullmatch(asset_id):
            raise ValueError("Invalid UUID in baseline")
        try:
            got = snapshot(endpoint, asset_id)
        except Exception as exc:
            mismatches.append((asset_id, str(exc)))
            continue
        if got != expected:
            changed = [name for name in ("bytes", "sha256", "full", "metadata")
                       if got.get(name) != expected.get(name)]
            mismatches.append((asset_id, ",".join(changed)))
    if mismatches:
        for asset_id, reason in mismatches:
            print(f"FAIL {asset_id}: {reason}", file=sys.stderr)
        raise ValueError(f"{len(mismatches)} of {len(entries)} assets differ from FSAssets baseline")
    print(f"OGL staging readback PASS: {len(entries)} unchanged assets (GET only)")


def self_test():
    asset_id = str(uuid.uuid4())
    payload = {"bytes": b"ogl-staging-readback-test" * 64}

    class Fixture(BaseHTTPRequestHandler):
        def do_GET(self):
            prefix = "/assets/" + asset_id
            if self.path not in (prefix, prefix + "/data", prefix + "/metadata"):
                self.send_error(404)
                return
            if self.path.endswith("/data"):
                result = payload["bytes"]
            elif self.path.endswith("/metadata"):
                result = (f"<AssetMetadata><ID>{asset_id}</ID><Name>Test</Name>"
                          f"<Type>0</Type></AssetMetadata>").encode()
            else:
                b64 = base64.b64encode(payload["bytes"]).decode()
                result = (f"<AssetBase><ID>{asset_id}</ID><Name>Test</Name>"
                          f"<Type>0</Type><Data>{b64}</Data></AssetBase>").encode()
            self.send_response(200)
            self.send_header("Content-Length", str(len(result)))
            self.end_headers()
            self.wfile.write(result)

        def log_message(self, *args):
            pass

    server = ThreadingHTTPServer(("127.0.0.1", 0), Fixture)
    thread = threading.Thread(target=server.serve_forever, daemon=True)
    thread.start()
    try:
        endpoint = staging_url(f"http://127.0.0.1:{server.server_port}")
        assert snapshot(endpoint, asset_id)["bytes"] == len(payload["bytes"])
        try:
            staging_url("http://example.com:8003")
            raise AssertionError("Public host was not rejected")
        except ValueError:
            pass
        with tempfile.TemporaryDirectory() as folder:
            file = Path(folder) / "manifest.json"
            capture(endpoint, [asset_id], file)
            verify(endpoint, file)
            payload["bytes"] = b"changed test payload"
            try:
                verify(endpoint, file)
                raise AssertionError("Changed data was accepted")
            except ValueError as exc:
                if "assets differ" not in str(exc):
                    raise
    finally:
        server.shutdown()
        server.server_close()
    print("OGL staging readback self-test PASS")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=("capture", "verify", "self-test"))
    parser.add_argument("--endpoint", help="Isolated staging ROBUST URL, loopback only")
    parser.add_argument("--ids", type=Path, help="Selected asset UUIDs, one per line")
    parser.add_argument("--manifest", type=Path, help="Private JSON baseline path")
    args = parser.parse_args()
    if args.command == "self-test":
        self_test()
        return
    if not args.endpoint or not args.manifest:
        parser.error("--endpoint and --manifest are required")
    try:
        endpoint = staging_url(args.endpoint)
        if args.command == "capture":
            if not args.ids:
                parser.error("capture requires --ids")
            capture(endpoint, asset_ids(args.ids), args.manifest)
        else:
            verify(endpoint, args.manifest)
    except (ValueError, OSError, ET.ParseError) as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        sys.exit(1)


if __name__ == "__main__":
    main()

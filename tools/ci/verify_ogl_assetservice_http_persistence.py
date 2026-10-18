#!/usr/bin/env python3
"""CI-only HTTP/FSAssets persistence test for the opt-in OGL ROBUST connector.

Only targets the hard-coded loopback port of the disposable ROBUST test instance.
It never deletes assets or connects to a production service.
"""
import argparse
import base64
import gzip
import hashlib
import json
import time
import urllib.error
import urllib.request
import uuid
import xml.etree.ElementTree as ET
from pathlib import Path

BASE_URL = "http://127.0.0.1:19495"


def request(method, path, body=None):
    req = urllib.request.Request(BASE_URL + path, data=body, method=method)
    if body is not None:
        req.add_header("Content-Type", "text/xml; charset=utf-8")
    with urllib.request.urlopen(req, timeout=10) as response:
        assert response.status == 200, f"{method} {path}: {response.status}"
        return response.read()


def field(xml_body, key):
    root = ET.fromstring(xml_body)
    for element in root.iter():
        if element.tag.split("}")[-1] == key:
            return element.text or ""
    raise AssertionError(f"XML response missing {key}")


def assert_missing():
    for path in ["/assets/" + str(uuid.uuid4()), "/assets/" + str(uuid.uuid4()) + "/data"]:
        try:
            request("GET", path)
        except urllib.error.HTTPError as exc:
            assert exc.code == 404, f"Missing asset returned HTTP {exc.code}"
        else:
            raise AssertionError("Missing asset must return 404")


def assert_asset(entry):
    asset_id, data = entry["id"], base64.b64decode(entry["payload"])
    assert request("GET", f"/assets/{asset_id}/data") == data, "GetData bytes mismatch"
    meta = request("GET", f"/assets/{asset_id}/metadata")
    assert field(meta, "ID") == asset_id, "GetMetadata ID mismatch"
    assert field(meta, "Name") == entry["name"], "GetMetadata name mismatch"
    full = request("GET", f"/assets/{asset_id}")
    assert field(full, "ID") == asset_id, "Get ID mismatch"
    assert field(full, "Name") == entry["name"], "Get name mismatch"
    assert base64.b64decode(field(full, "Data")) == data, "Get payload mismatch"


def assert_durable_fsassets(entries, fs_dir):
    """Require the FSAssets spool writer to commit gzip data under the test root."""
    deadline = time.monotonic() + 25
    unresolved = {entry["id"]: entry for entry in entries}
    while unresolved and time.monotonic() < deadline:
        for asset_id, entry in list(unresolved.items()):
            digest = hashlib.sha256(base64.b64decode(entry["payload"])).hexdigest().upper()
            matches = list(fs_dir.rglob(digest + ".gz"))
            for filename in matches:
                try:
                    with gzip.open(filename, "rb") as stream:
                        restored = stream.read()
                    assert restored == base64.b64decode(entry["payload"]), f"Disk payload mismatch: {asset_id}"
                    del unresolved[asset_id]
                    break
                except EOFError:
                    # Writer may still be finalizing the gzip file.
                    pass
        if unresolved:
            time.sleep(0.25)
    assert not unresolved, f"FSAssets writer did not persist: {list(unresolved)}"


def make_asset(asset_id, name, asset_type, payload):
    # XmlSerializer<AssetBase> accepts its public property names. FullID is
    # resolved by AssetMetadata.ID's setter when a valid UUID is provided.
    root = ET.Element("AssetBase")
    for key, value in [
        ("Data", base64.b64encode(payload).decode("ascii")),
        ("ID", asset_id),
        ("Name", name),
        ("Description", "OGL isolated persistence smoke"),
        ("Type", str(asset_type)),
        ("Temporary", "false"),
        ("CreatorID", "00000000-0000-0000-0000-000000000000"),
    ]:
        ET.SubElement(root, key).text = value
    return ET.tostring(root, encoding="utf-8", xml_declaration=True)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("phase", choices=("write", "read"))
    parser.add_argument("state", type=Path)
    parser.add_argument("fs_dir", type=Path)
    args = parser.parse_args()
    if args.phase == "write":
        fixtures = [
            ("Texture", 0, b"\x00\x01\xfe\xff" * 8192),
            ("Mesh", 49, bytes(range(256)) * 192),
            ("Notecard", 7, b"OGL compatibility persisted asset" * 1024),
        ]
        entries = []
        for index, (name, asset_type, payload) in enumerate(fixtures):
            asset_id = str(uuid.uuid4())
            name = f"OGL CI {name} {index}"
            result = request("POST", "/assets", make_asset(asset_id, name, asset_type, payload))
            assert (ET.fromstring(result).text or "").strip() == asset_id, (
                f"Store returned wrong UUID: {result[:250]!r}"
            )
            entry = {
                "id": asset_id,
                "name": name,
                "type": asset_type,
                "payload": base64.b64encode(payload).decode("ascii"),
            }
            assert_asset(entry)
            entries.append(entry)
        # Persist only test-generated UUIDs and payloads in the disposable workdir.
        args.state.write_text(json.dumps(entries), encoding="utf-8")
        assert_missing()
        assert_durable_fsassets(entries, args.fs_dir)
        print("OGL asset HTTP Store/Get/GetMetadata/GetData and FSAssets writer PASS")
    else:
        entries = json.loads(args.state.read_text(encoding="utf-8"))
        assert len(entries) == 3, "Expected three fixtures after ROBUST restart"
        for entry in entries:
            assert_asset(entry)
        assert_durable_fsassets(entries, args.fs_dir)
        assert_missing()
        print("OGL asset persisted readback after ROBUST restart PASS")


if __name__ == "__main__":
    main()

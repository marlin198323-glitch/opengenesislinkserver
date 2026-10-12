#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/../../bin"

LOG="../nexverse-login-smoke.log"
RESPONSE="/tmp/nexverse-login-response.xml"

dotnet Robust.dll -inifile Robust.NexVerseLogin.Tests.ini > "$LOG" 2>&1 &
ROBUST_PID=$!

cleanup() {
  kill "$ROBUST_PID" 2>/dev/null || true
  wait "$ROBUST_PID" 2>/dev/null || true
}
on_exit() {
  status=$?
  if [ "$status" -ne 0 ]; then
    echo "::group::NexVerse LLLogin Robust log"
    cat "$LOG" 2>/dev/null || true
    echo "::endgroup::"
    echo "::group::NexVerse LLLogin XML-RPC response"
    cat "$RESPONSE" 2>/dev/null || true
    echo "::endgroup::"
  fi
  cleanup
  exit "$status"
}
trap on_exit EXIT

READY=0
for i in $(seq 1 40); do
  if ! kill -0 "$ROBUST_PID" 2>/dev/null; then
    echo "::error::Robust exited before LLLogin became ready."
    cat "$LOG"
    exit 1
  fi

  if (echo > /dev/tcp/127.0.0.1/19092) >/dev/null 2>&1; then
    READY=1
    break
  fi

  sleep 0.25
done

if [ "$READY" -ne 1 ]; then
  echo "::error::LLLogin connector did not become ready."
  cat "$LOG"
  exit 1
fi

cat > /tmp/nexverse-login-request.xml <<'XML'
<?xml version="1.0"?>
<methodCall>
  <methodName>login_to_simulator</methodName>
  <params>
    <param>
      <value>
        <struct>
          <member><name>first</name><value><string>NexVerseCiMissing</string></value></member>
          <member><name>last</name><value><string>Resident</string></value></member>
          <member><name>passwd</name><value><string>$1$00000000000000000000000000000000</string></value></member>
          <member><name>start</name><value><string>last</string></value></member>
          <member><name>scope_id</name><value><string>00000000-0000-0000-0000-000000000000</string></value></member>
          <member><name>version</name><value><string>Firestorm-Release CI</string></value></member>
          <member><name>channel</name><value><string>Firestorm-Releasex64</string></value></member>
          <member><name>mac</name><value><string>00:00:00:00:00:00</string></value></member>
          <member><name>id0</name><value><string>nexverse-ci</string></value></member>
        </struct>
      </value>
    </param>
  </params>
</methodCall>
XML

HANDLER_READY=0
for i in $(seq 1 40); do
  if ! kill -0 "$ROBUST_PID" 2>/dev/null; then
    echo "::error::Robust exited while waiting for login_to_simulator registration."
    exit 1
  fi

  curl --silent --show-error --max-time 5 \
    -H 'Content-Type: text/xml' \
    --data-binary @/tmp/nexverse-login-request.xml \
    http://127.0.0.1:19092/ > "$RESPONSE" || true

  if grep -F 'Requested method [login_to_simulator] not found' "$RESPONSE" >/dev/null 2>&1; then
    sleep 0.25
    continue
  fi

  if grep -F '<methodResponse>' "$RESPONSE" >/dev/null 2>&1; then
    HANDLER_READY=1
    break
  fi

  sleep 0.25
done

if [ "$HANDLER_READY" -ne 1 ]; then
  echo "::error::login_to_simulator XML-RPC handler did not become ready."
  exit 1
fi

grep -F '<methodResponse>' "$RESPONSE"
grep -F '<name>login</name>' "$RESPONSE"
grep -F 'false' "$RESPONSE"
grep -F 'NexVerseCiMissing Resident' "$LOG"
grep -F 'Firestorm-Release CI' "$LOG"
grep -F 'reason: user not found' "$LOG"

# The OGL viewer has its own versioned JSON discovery on the SAME
# public listener as the legacy XML-RPC/LLSD login. Neither endpoint
# may override the other, and the JSON discovery must never echo
# arbitrary config keys or advertise a nonexistent native login.
OGL_JSON="/tmp/nexverse-ogl-grid-info.json"
OGL_HEADERS="/tmp/nexverse-ogl-grid-info.headers"
curl --fail --silent --show-error --max-time 5 \
  --dump-header "$OGL_HEADERS" \
  "http://127.0.0.1:19092/ogl/v1/grid-info" > "$OGL_JSON"
grep -i 'content-type: application/json' "$OGL_HEADERS"
python3 - <<'PY'
import json
from pathlib import Path
doc = json.loads(Path("/tmp/nexverse-ogl-grid-info.json").read_text())
assert doc["schema"] == "org.opengenesislink.viewer.grid-info.v1", doc
assert doc["protocol_version"] == 1
assert doc["server"]["product"] == "OpenGenesisLINK"
assert doc["grid"]["name"] == "OGL CI Grid"
assert doc["grid"]["nick"] == "OGLCI"
assert doc["endpoints"]["legacy_login"] == "http://127.0.0.1:19092/"
assert doc["endpoints"]["ogl_grid_info"] == "/ogl/v1/grid-info"
assert doc["endpoints"]["ogl_login"] is None
assert doc["features"]["legacy_login"] is True
assert doc["features"]["ogl_native_login"] is False
assert doc["features"]["ogl_experiences_2"] is False
assert doc["features"]["ogl_pathfinding_2"] is False
assert doc["security"]["viewer_identity_verified_by_discovery"] is False
assert "internal_test_secret" not in str(doc)
assert "must-never-appear-in-ogl-discovery" not in str(doc)
print("OpenGenesisLINK OGL GridInfo JSON discovery: PASS")
PY

LEGACY_XML="/tmp/nexverse-legacy-grid-info.xml"
LEGACY_JSON="/tmp/nexverse-legacy-grid-info.json"
curl --fail --silent --show-error --max-time 5 \
  "http://127.0.0.1:19092/get_grid_info" > "$LEGACY_XML"
grep -F '<gridinfo>' "$LEGACY_XML"
curl --fail --silent --show-error --max-time 5 \
  "http://127.0.0.1:19092/json_grid_info" > "$LEGACY_JSON"
python3 - <<'PY'
import json
from pathlib import Path
legacy = json.loads(Path("/tmp/nexverse-legacy-grid-info.json").read_text())
assert legacy["gridname"] == "OGL CI Grid"
assert legacy["login"] == "http://127.0.0.1:19092/"
print("OpenSim legacy GridInfo paths preserved: PASS")
PY

POST_CODE=$(curl --silent --show-error --output /tmp/nexverse-ogl-grid-info-post.json \
  --write-out '%{http_code}' --max-time 5 \
  -X POST "http://127.0.0.1:19092/ogl/v1/grid-info")
test "$POST_CODE" = "405"
python3 - <<'PY'
import json
from pathlib import Path
err=json.loads(Path("/tmp/nexverse-ogl-grid-info-post.json").read_text())
assert err["error"] == "method_not_allowed"
PY

# Native OGL login is a separate JSON handler on the SAME Robust
# listener. Production defaults disabled, CI explicitly opts in with
# localhost as a simulated trusted TLS proxy. No real credentials used.
OGL_LOGIN="http://127.0.0.1:19092/ogl/v1/login"
POST_BODY='{"protocol_version":1,"first":"NexVerseCiMissing","last":"Resident","passwd":"not-a-real-password","start":"last"}'

# GET is never login, and requests without verified transport are denied
# BEFORE their credentials are parsed or given to the login service.
CODE=$(curl --silent --show-error --max-time 5 --output /tmp/ogl-login-get.json \
  --write-out '%{http_code}' "$OGL_LOGIN")
test "$CODE" = "405"
CODE=$(curl --silent --show-error --max-time 5 --output /tmp/ogl-login-plain.json \
  --write-out '%{http_code}' -H 'Content-Type: application/json' \
  --data "$POST_BODY" "$OGL_LOGIN")
test "$CODE" = "426"
python3 - <<'PY'
import json
from pathlib import Path
assert json.loads(Path("/tmp/ogl-login-plain.json").read_text())["error"] == "tls_required"
PY

# An unrelated forwarded header never makes a request secure.
CODE=$(curl --silent --show-error --max-time 5 --output /tmp/ogl-login-spoof.json \
  --write-out '%{http_code}' -H 'Content-Type: application/json' \
  -H 'X-Forwarded-Proto: http' --data "$POST_BODY" "$OGL_LOGIN")
test "$CODE" = "426"

# This is allowed ONLY because the CI config explicitly trusts localhost as
# its simulated TLS terminator. Invalid account must fail without leaking
# an account-enumeration reason, tokens or source passwords.
CODE=$(curl --silent --show-error --max-time 5 --output /tmp/ogl-login-unknown.json \
  --write-out '%{http_code}' -H 'Content-Type: application/json' \
  -H 'X-Forwarded-Proto: https' --data "$POST_BODY" "$OGL_LOGIN")
test "$CODE" = "401"
python3 - <<'PY'
import json
from pathlib import Path
doc = json.loads(Path("/tmp/ogl-login-unknown.json").read_text())
assert doc["protocol_version"] == 1
assert doc["error"] == "login_failed"
assert "NexVerseCiMissing" not in str(doc)
assert "not-a-real-password" not in str(doc)
PY

# Strict protocol validation and request body size limit.
CODE=$(curl --silent --show-error --max-time 5 --output /tmp/ogl-login-protocol.json \
  --write-out '%{http_code}' -H 'Content-Type: application/json' \
  -H 'X-Forwarded-Proto: https' \
  --data '{"protocol_version":999,"first":"x","last":"y","passwd":"z"}' "$OGL_LOGIN")
test "$CODE" = "400"
python3 - <<'PY'
import json
from pathlib import Path
assert json.loads(Path("/tmp/ogl-login-protocol.json").read_text())["error"] == "unsupported_protocol"
PY

CODE=$(curl --silent --show-error --max-time 5 --output /tmp/ogl-login-method.json \
  --write-out '%{http_code}' -H 'Content-Type: text/plain' \
  -H 'X-Forwarded-Proto: https' --data 'hello' "$OGL_LOGIN")
test "$CODE" = "415"

echo "NexVerse Firestorm XML-RPC + OGL GridInfo JSON + optional OGL JSON login negative smoke: OK"

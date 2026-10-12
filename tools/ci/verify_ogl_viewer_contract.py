#!/usr/bin/env python3
"""Fail-closed OGL Viewer protocol documentation/implementation consistency.

Every OGL viewer-facing protocol change must keep the living viewer
handoff, public JSON discovery, login adapter, sample configs, and smoke
coverage in sync. This verifies declared contracts, not live E2E acceptance.
"""
from pathlib import Path

def read(path):
    return Path(path).read_text(encoding="utf-8-sig")

contract = read("doc/NexVerse/OGL_VIEWER_DEVELOPMENT.md")
discovery_doc = read("doc/NexVerse/OGL_VIEWER_DISCOVERY.md")
readme = read("README.md")
login = read("OpenSim/Server/Handlers/Login/OglViewerLoginHandler.cs")
connector = read("OpenSim/Server/Handlers/Login/LLLoginServiceInConnector.cs")
grid = read("OpenSim/Server/Handlers/Grid/GridInfoHandlers.cs")
grid_connector = read("OpenSim/Server/Handlers/Grid/GridInfoServerInConnector.cs")
project = read("OpenSim/Server/Handlers/OpenSim.Server.Handlers.csproj")
smoke = read("tools/ci/smoke_lllogin_protocol.sh")

for filename, body in (
    ("README.md", readme),
    ("OGL_VIEWER_DISCOVERY.md", discovery_doc),
):
    assert "OGL_VIEWER_DEVELOPMENT.md" in body, filename + ": missing handoff link"

for required in (
    "org.opengenesislink.viewer.grid-info.v1",
    "GET /ogl/v1/grid-info",
    "POST /ogl/v1/login",
    "protocol_version",
    "login_response",
    "tls_required",
    "login_failed",
    "MaxAttemptsPerMinute",
    "TrustedTlsProxyAddress",
    "PublicLoginUrl",
    "Experiences 2.0",
    "Pathfinding 2.0",
    "My Outfits 2.0",
    "Firestorm",
    "Fallback",
    "HTTPS",
    "Nach",
):
    assert required in contract, "Living viewer contract missing: " + required

for source, required in (
    (grid_connector, '"/ogl/v1/grid-info"'),
    (grid, '"org.opengenesislink.viewer.grid-info.v1"'),
    (grid, 'ogl_login = nativeLoginUrl'),
    (grid, 'ogl_native_login = nativeLoginUrl != null'),
    (grid, 'candidate.Scheme == Uri.UriSchemeHttps'),
    (grid, 'ogl_experiences_2 = false'),
    (grid, 'ogl_pathfinding_2 = false'),
    (connector, 'OglViewerLoginHandler.Route'),
    (connector, 'server.AddXmlRPCHandler("login_to_simulator"'),
    (connector, 'server.SetDefaultLLSDHandler('),
    (project, 'Login\\OglViewerLoginHandler.cs'),
    (login, 'public const string Route = "/ogl/v1/login"'),
    (login, 'm_Login.Login('),
    (login, 'request.IsSecured'),
    (login, 'request.RemoteIPEndPoint.Address.Equals(m_TrustedTlsProxy)'),
    (login, '"X-Forwarded-Proto"'),
    (login, 'MaxBodyBytes = 8192'),
    (login, 'Error(response, 426, "tls_required")'),
    (login, 'Error(response, 401, "login_failed")'),
):
    assert required in source, "OGL viewer contract implementation missing: " + required

for name in ("bin/Robust.HG.ini", "bin/Robust.HG.ini.example"):
    cfg = read(name)
    assert "[OglViewerLogin]" in cfg, name
    section = cfg.split("\n[OglViewerLogin]\n", 1)[1].split("\n[", 1)[0]
    assert "Enabled = false" in section, name + ": do not enable untested native login in product templates"
    assert 'TrustedTlsProxyAddress = ""' in section, name
    assert 'PublicLoginUrl = ""' in section, name

for required in (
    'login_to_simulator',
    '/ogl/v1/grid-info',
    '/ogl/v1/login',
    'tls_required',
    'login_failed',
    'unsupported_protocol',
    'content-type: application/json',
):
    assert required in smoke, "OGL viewer smoke missing: " + required

assert "public const string ProductName" not in login
print("OGL Viewer living contract, dual login routes, TLS and disabled-by-default configuration: OK")

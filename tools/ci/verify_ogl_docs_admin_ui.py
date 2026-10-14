#!/usr/bin/env python3
"""Fail closed checks for OGL World API protected admin docs UI."""
from pathlib import Path

root = Path(__file__).resolve().parents[2]
api = (root / "NexVerse/Server/Api/NexVerseWorldApiConnector.cs").read_text()
access = (root / "NexVerse/Server/Api/OglApiDocsAccess.cs").read_text()
dashboard = (root / "NexVerse/Server/Api/NexApiDocsPage.cs").read_text()
user = (root / "NexVerse/Server/Api/NexVerseUserApi.cs").read_text()

def must(text, *parts):
    for part in parts:
        assert part in text, "Missing protected docs assertion: " + part

must(api,
    'new OglApiDocsAccess(', 'apiGate.Wrap(oglDocs.HandleLogin)',
    'apiGate.Wrap(oglDocs.HandlePanel)', '"/api/v1/docs/panel"',
    '"OpenGenesisLINK Control Center Login"')
assert 'apiGate.Wrap(docsPage.Handle)' not in api
must(access,
    'public void HandleLogin(', 'public void HandlePanel(',
    'm_Authenticator.TryAuthenticate(', 'NexScopes.AdminAll',
    'account.UserLevel < m_MinimumLevel', 'Math.Max(200, minimumLevel)',
    '!account.LoginAllowed', 'principal.Subject, account.PrincipalID.ToString()',
    'authorization.StartsWith("Bearer "', 'IsSecureTransport(request)',
    'X-NexVerse-Api-Key', 'WWW-Authenticate', 'HttpStatusCode.Unauthorized',
    'm_Docs.Handle(request, response)', 'Cache-Control', 'no-store, private',
    'frame-ancestors \'none\'', 'OpenGenesisLINK',
    '/api/v1/auth/admin/session', '/api/v1/docs/panel',
    "form.elements.namedItem('password').value=''",
    'credentials', 'window.isSecureContext',
    "frame.srcdoc=html", "event.source!==frame.contentWindow",
    'frame.contentWindow.postMessage(', 'token=\'\';expires=0',
)
must(dashboard,
    '<title>OpenGenesisLINK', '<div class="ogl-logo"',
    'OpenGenesisLINK / WORLD API',
    'window.addEventListener(\'message\',event=>',
    'adminSessionToken=access;', "event.data.type!=='ogl-auth'",
    "window.parent.postMessage({type:'ogl-logout'}",
    '@media(prefers-reduced-motion:reduce)', 'ogl-aurora',
    '/api/v1/openapi.json', 'id="page-explorer"')
# No regression in the original credential backend: scope+level are checked
# BEFORE issuing administrator access tokens.
must(user,
    '"/api/v1/auth/admin/session"',
    'HandleNativeSessionLogin(request, response, true)',
    'user.UserLevel < Math.Max(200, m_AdminMinimumLevel)',
    'NexScopes.AdminAll', 'm_Tokens.Issue(')
assert user.index('user.UserLevel < Math.Max(200, m_AdminMinimumLevel)') < user.index('m_Tokens.Issue(')
# Hard boundary: both routes must use the shared request gate.
assert api.count('apiGate.Wrap(oglDocs.Handle') == 2
print("OGL protected Control Center UI / >=200 server gate / OGL branding: OK")

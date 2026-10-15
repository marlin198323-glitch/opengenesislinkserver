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
    'm_Authenticator.TryAuthenticate(', 'request, string.Empty,',
    'account.UserLevel < MinimumLevel', 'private const int MinimumLevel = 200',
    '!account.LoginAllowed', 'principal.Subject, account.PrincipalID.ToString()',
    'authorization.StartsWith("Bearer "', 'HttpStatusCode.Unauthorized',
    'm_Docs.Handle(request, response)', 'Cache-Control', 'no-store, private',
    'admin_token_required', 'admin_session_invalid',
    'admin_permission_required', 'admin_api_disabled',
    'X-OGL-Docs-Error', "frame-ancestors 'none'", 'OpenGenesisLINK',
    "/api/v1/auth/session", '/api/v1/docs/panel',
    "form.elements.namedItem('password').value=''",
    'Number(data.user_level)<200', "typeof data.access_token!=='string'",
    "frame.srcdoc=html", "event.source!==frame.contentWindow",
    'frame.contentWindow.postMessage(', "token='';expires=0",
)
# The UI must distinguish an unrelated HTTP 200 JSON response from a
# successful native login: Number(undefined) is NaN, not <200.
for login_ui in (access, dashboard):
    must(login_ui, "!Number.isInteger(data.user_level)",
         "typeof data.access_token!=='string'",
         "/api/v1/docs", "Der Server konnte keine Sitzung erstellen.")
# One stable HTTPS path for the entire browser login handshake, independent
# of KeyHelp's rules for an additional /api/v1/auth/session URL.
must(access,
     'public void ConfigureDocsSessionLogin(',
     'login(request, response);',
     'HandlePanel(request, response);',
     "fetch('/api/v1/docs',{method:'POST'",
     "fetch('/api/v1/docs',{headers:",
     'if (login == null)', 'admin_login_unavailable')
must(user,
     'public void HandleControlCenterLogin(',
     'HandleNativeSessionLogin(request, response, true);')
must(api, 'oglDocs.ConfigureDocsSessionLogin(userRouter.HandleControlCenterLogin);')
must(user,
     '"session_token_unavailable"',
     "if (string.IsNullOrWhiteSpace(accessToken))")
assert api.count('"/api/v1/auth/session"') == 1, "Native session exact registration missing"
assert api.count('"/api/v1/auth/admin/session"') == 1, "Admin session exact registration missing"
assert api.index('"/api/v1/auth/session"') < api.index('"/api", apiGate.Wrap(userRouter.Handle)')
assert "if(!data.administrator" not in access
assert "window.isSecureContext" not in access
assert "IsSecureTransport(request)" not in access
assert "principal.HasScope(NexScopes.AdminAll)" not in access
assert 'request, NexScopes.AdminAll,' not in access
must(dashboard,
    '<title>OpenGenesisLINK', '<div class="ogl-logo"',
    'OpenGenesisLINK / WORLD API',
    'window.addEventListener(\'message\',event=>',
    'adminSessionToken=access;', "event.data.type!=='ogl-auth'",
    "window.parent.postMessage({type:'ogl-logout'}",
    '@media(prefers-reduced-motion:reduce)', 'ogl-aurora',
    '/api/v1/openapi.json', 'id="page-explorer"')
# The ordinary GridUser login verifies the avatar password and returns
# a signed session. The panel independently validates it plus current UserLevel.
must(user,
    '"/api/v1/auth/session"', 'HandleNativeSessionLogin(request, response);',
    'm_Users.VerifyPassword(', 'm_Tokens.Issue(', 'user_level = user.UserLevel',
    'access_token =', 'GetUserAccount(', 'nativeClaims.SecurityStamp')
assert user.index('m_Users.VerifyPassword(') < user.index('m_Tokens.Issue(')
assert "const res=await fetch('/api/v1/docs'" in dashboard
assert "const res=await fetch('/api/v1/auth/session'" not in dashboard
assert "Number(data.user_level)<200" in dashboard
assert "if(!window.isSecureContext)" not in dashboard[dashboard.index("async function adminLogin()"):dashboard.index("const $=id=>")]
assert "data.administrator" not in dashboard
assert "data.scope?.split" not in dashboard
assert api.count('apiGate.Wrap(oglDocs.Handle') == 2
print("OGL GridUser password + UserLevel 200 login, signed session, anonymous panel deny: OK")

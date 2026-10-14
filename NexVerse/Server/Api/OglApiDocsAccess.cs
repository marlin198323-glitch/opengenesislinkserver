// SPDX-License-Identifier: MPL-2.0
using System;
using System.Net;
using System.Text;
using NexVerse.Core.Security;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Services.Interfaces;

namespace NexVerse.Server.Api
{
    /// <summary>
    /// OpenGenesisLINK Control Center access boundary.
    /// /api/v1/docs is a login-only shell; full UI markup lives at
    /// /api/v1/docs/panel and is NEVER sent before a server-side
    /// bearer-token, local GridUser, scope and UserLevel check.
    /// Existing machine-key API clients and public OpenAPI routes are unchanged.
    /// </summary>
    internal sealed class OglApiDocsAccess
    {
        private static readonly log4net.ILog m_Log =
            log4net.LogManager.GetLogger(typeof(OglApiDocsAccess));

        private readonly NexApiDocsPage m_Docs = new();
        private readonly System.Collections.Generic.HashSet<string> m_TrustedProxies =
            new(StringComparer.OrdinalIgnoreCase);
        private volatile NexApiAuthenticator m_Authenticator;
        private int m_MinimumLevel = 200;

        // Loopback TLS-terminating proxies are trusted by default. Non-loopback
        // proxy IPs must be explicitly allowlisted by the administrator; never
        // trust client-controlled X-Forwarded-For to establish this peer identity.
        public OglApiDocsAccess(string trustedProxyAddresses = "")
        {
            foreach (string raw in (trustedProxyAddresses ?? string.Empty).Split(
                ',', StringSplitOptions.RemoveEmptyEntries))
            {
                string address = raw.Trim();
                if (!IPAddress.TryParse(address, out IPAddress parsed))
                    throw new ArgumentException(
                        "Ungültige IP-Adresse in AdminDocsTrustedProxyAddresses.",
                        nameof(trustedProxyAddresses));
                m_TrustedProxies.Add(NormalizeAddress(parsed));
            }
        }

        public void ConfigureAdministratorAccess(
            NexApiAuthenticator authenticator, int minimumLevel)
        {
            m_MinimumLevel = Math.Max(200, minimumLevel);
            m_Authenticator = authenticator ??
                throw new ArgumentNullException(nameof(authenticator));
        }

        public void HandleLogin(IOSHttpRequest request, IOSHttpResponse response)
        {
            CommonHeaders(response);
            if (request == null ||
                !string.Equals(request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
            {
                Reject(response, HttpStatusCode.MethodNotAllowed, "method_not_allowed");
                return;
            }

            response.StatusCode = (int)HttpStatusCode.OK;
            response.ContentType = "text/html; charset=utf-8";
            response.AddHeader("Content-Security-Policy",
                "default-src 'none'; style-src 'unsafe-inline'; script-src 'unsafe-inline'; " +
                "connect-src 'self'; frame-src 'self' about:; img-src 'self' data:; " +
                "font-src 'self'; base-uri 'none'; form-action 'none'; frame-ancestors 'none'");
            response.AddHeader("X-Frame-Options", "DENY");
            response.RawBuffer = Encoding.UTF8.GetBytes(s_LoginPage);
        }

        public void HandlePanel(IOSHttpRequest request, IOSHttpResponse response)
        {
            CommonHeaders(response);
            if (request == null ||
                !string.Equals(request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
            {
                Reject(response, HttpStatusCode.MethodNotAllowed, "method_not_allowed");
                return;
            }

            // Anonymous requests remain 401 even when the backend uses plain
            // HTTP. Transport diagnostics are revealed only to a bearer holder.
            string authorization = request.Headers?["Authorization"] ?? string.Empty;
            if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ||
                !string.IsNullOrWhiteSpace(request.Headers?["X-NexVerse-Api-Key"]))
            {
                Reject(response, HttpStatusCode.Unauthorized, "admin_token_required");
                return;
            }

            if (m_Authenticator == null)
            {
                Reject(response, HttpStatusCode.ServiceUnavailable, "admin_api_disabled");
                return;
            }

            // The client has an HTTPS connection to the front proxy but the
            // backend may use HTTP. A trusted proxy must attest the outer TLS
            // scheme. A missing header previously yielded a misleading 401
            // even for a successfully authenticated level-200 GridUser.
            if (!IsSecureTransport(request))
            {
                m_Log.Warn("[OGL-DOCS]: Adminpanel verweigert: Backend konnte HTTPS nicht " +
                    "bestätigen. Reverse-Proxy X-Forwarded-Proto und " +
                    "AdminDocsTrustedProxyAddresses prüfen.");
                Reject(response, HttpStatusCode.UpgradeRequired, "https_not_verified");
                return;
            }

            if (!m_Authenticator.TryAuthenticate(
                    request, NexScopes.AdminAll,
                    out NexPrincipal principal, out UserAccount account,
                    out int authStatus, out string _))
            {
                // Do not leak the reason for failed token validation. Deny
                // everything except a genuinely authenticated local GridUser.
                Reject(response,
                    authStatus == (int)HttpStatusCode.Forbidden
                        ? HttpStatusCode.Forbidden
                        : HttpStatusCode.Unauthorized,
                    "admin_session_invalid");
                return;
            }

            if (!principal.IsAuthenticated ||
                account == null ||
                !account.LoginAllowed ||
                account.UserLevel < m_MinimumLevel ||
                !principal.HasScope(NexScopes.AdminAll) ||
                !string.Equals(principal.Subject, account.PrincipalID.ToString(),
                    StringComparison.OrdinalIgnoreCase))
            {
                Reject(response, HttpStatusCode.Forbidden, "admin_permission_required");
                return;
            }

            m_Docs.Handle(request, response);
            response.AddHeader("X-Frame-Options", "SAMEORIGIN");
        }

        private static string NormalizeAddress(IPAddress address) =>
            address.IsIPv4MappedToIPv6
                ? address.MapToIPv4().ToString()
                : address.ToString();

        private bool IsSecureTransport(IOSHttpRequest request)
        {
            if (request.IsSecured)
                return true;

            IPAddress peer = request.RemoteIPEndPoint?.Address;
            if (peer == null)
                return false;

            bool trusted = IPAddress.IsLoopback(peer) ||
                (peer.IsIPv4MappedToIPv6 && IPAddress.IsLoopback(peer.MapToIPv4())) ||
                m_TrustedProxies.Contains(NormalizeAddress(peer));
            // Exact scheme only: no comma-separated values or forwarded chain
            // which an untrusted client could prepend.
            return trusted &&
                string.Equals(request.Headers?["X-Forwarded-Proto"]?.Trim(),
                    "https", StringComparison.OrdinalIgnoreCase);
        }

        private static void CommonHeaders(IOSHttpResponse response)
        {
            response.KeepAlive = false;
            response.AddHeader("Cache-Control", "no-store, private");
            response.AddHeader("Pragma", "no-cache");
            response.AddHeader("X-Content-Type-Options", "nosniff");
            response.AddHeader("Referrer-Policy", "no-referrer");
        }

        private static void Reject(
            IOSHttpResponse response, HttpStatusCode status, string code)
        {
            response.StatusCode = (int)status;
            response.ContentType = "text/plain; charset=utf-8";
            response.AddHeader("X-OGL-Docs-Error", code);
            if (status == HttpStatusCode.Unauthorized)
                response.AddHeader("WWW-Authenticate", "Bearer");
            response.RawBuffer = Encoding.UTF8.GetBytes(
                status == HttpStatusCode.MethodNotAllowed
                    ? "GET erforderlich."
                    : "OpenGenesisLINK: Adminpanel nicht verfügbar.");
        }

        private const string s_LoginPage = """
<!doctype html>
<html lang="de">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<meta name="color-scheme" content="dark">
<title>OpenGenesisLINK – Control Center</title>
<style>
:root{color-scheme:dark;--a:#62e4fb;--v:#ad8aff;--paper:#f3f8ff;--muted:#95a6bf;--line:rgba(132,178,228,.23)}
*{box-sizing:border-box}html,body{margin:0;min-height:100%;font-family:Inter,ui-sans-serif,system-ui,-apple-system,"Segoe UI",sans-serif}
body{color:var(--paper);background:#060b16;overflow-x:hidden}
body:before{content:"";position:fixed;inset:-35%;z-index:-2;pointer-events:none;background:radial-gradient(ellipse 30% 31% at 18% 35%,rgba(29,163,207,.22),transparent 90%),radial-gradient(ellipse 34% 31% at 83% 54%,rgba(105,66,221,.2),transparent 95%),radial-gradient(ellipse 20% 22% at 47% 80%,rgba(13,139,184,.16),transparent 95%);animation:glow 22s ease-in-out infinite alternate}
body:after{content:"";position:fixed;inset:0;z-index:-1;pointer-events:none;opacity:.19;background-image:linear-gradient(rgba(144,179,237,.16) 1px,transparent 1px),linear-gradient(90deg,rgba(144,179,237,.16) 1px,transparent 1px);background-size:66px 66px;mask-image:linear-gradient(110deg,transparent 6%,black 48%,transparent)}
@keyframes glow{to{transform:translate3d(4%,-3%,0) rotate(6deg)}}
a{color:var(--a)}.top{display:flex;align-items:center;justify-content:space-between;gap:20px;padding:24px clamp(24px,5vw,76px);max-width:1640px;margin:auto}
.brand{display:flex;align-items:center;gap:13px}.sigil{width:48px;height:48px;display:grid;place-items:center;background:linear-gradient(145deg,#102d42,#34205c);border:1px solid rgba(130,227,255,.59);border-radius:15px;box-shadow:0 0 34px rgba(60,206,240,.21),inset 0 0 18px rgba(79,208,241,.13);font-size:18px;font-weight:900;letter-spacing:-1px;color:#b6f5ff}
.brand strong{display:block;font-size:18px;letter-spacing:.015em}.brand span{display:block;font-size:11px;letter-spacing:.19em;color:#8aadd1;text-transform:uppercase;margin-top:1px}
.topright{font-size:11px;letter-spacing:.18em;text-transform:uppercase;color:#9bb1cd;display:flex;align-items:center;gap:12px}.light{width:7px;height:7px;background:#46edc8;border-radius:50%;box-shadow:0 0 13px #46edc8}
.stage{max-width:1450px;margin:0 auto;min-height:calc(100vh - 163px);padding:35px clamp(24px,5vw,76px) 65px;display:grid;grid-template-columns:minmax(0,1.18fr) minmax(365px,.82fr);align-items:center;gap:clamp(35px,6vw,100px)}
.eyebrow{font-size:12px;font-weight:700;letter-spacing:.21em;color:var(--a);text-transform:uppercase;display:flex;align-items:center;gap:10px}.eyebrow:before{content:"";height:1px;width:31px;background:var(--a)}
h1{font-size:clamp(48px,6vw,86px);font-weight:750;letter-spacing:-.065em;line-height:1.04;margin:26px 0 25px;max-width:760px}h1 em{font-style:normal;background:linear-gradient(110deg,#7aebff,#b3b3ff 62%,#e4aeff);background-clip:text;color:transparent}
.intro{font-size:16px;line-height:1.8;color:#a4b6ce;max-width:530px}.highlights{display:flex;gap:12px;flex-wrap:wrap;margin-top:34px}.highlight{display:flex;gap:9px;align-items:center;padding:10px 13px;border:1px solid rgba(124,169,220,.17);background:rgba(15,28,53,.48);border-radius:12px;color:#b5c8df;font-size:12px}
.orbit{position:relative;width:min(390px,90%);height:190px;margin:32px 0 0;overflow:hidden}.orbit:before{content:"";position:absolute;width:315px;height:315px;left:10px;top:17px;border:1px solid rgba(75,218,253,.26);border-radius:50%;box-shadow:0 0 0 34px rgba(56,112,223,.034),0 0 0 70px rgba(56,112,223,.025),inset 0 0 45px rgba(54,194,241,.06)}.orbit:after{content:"";position:absolute;left:118px;top:68px;width:78px;height:78px;border-radius:23px;transform:rotate(-28deg);background:linear-gradient(140deg,rgba(87,237,255,.29),rgba(107,78,221,.23));border:1px solid rgba(117,213,255,.62);box-shadow:0 0 48px rgba(59,163,253,.28)}
.window{position:relative;isolation:isolate;padding:1px;border-radius:23px;background:linear-gradient(120deg,rgba(102,217,255,.82),rgba(125,129,249,.33) 36%,rgba(159,88,255,.54) 76%,rgba(102,217,255,.62));box-shadow:0 36px 120px rgba(0,0,0,.5),0 0 58px rgba(82,143,250,.13)}
.window:before{content:"";position:absolute;inset:-1px;z-index:-1;border-radius:24px;background:linear-gradient(135deg,rgba(63,229,247,.12),transparent,rgba(160,99,250,.13));filter:blur(17px)}
.login{border-radius:22px;padding:clamp(27px,4vw,47px);background:linear-gradient(155deg,rgba(19,33,56,.96),rgba(11,18,34,.99));border:1px solid rgba(255,255,255,.06);backdrop-filter:blur(25px)}
.loginkicker{font-size:11px;letter-spacing:.18em;text-transform:uppercase;color:#8cadd1;font-weight:700}.login h2{font-size:30px;letter-spacing:-.04em;margin:12px 0 8px}.login p{color:#9dafc7;line-height:1.6;font-size:13px;margin:0 0 29px}
label{display:block;color:#d3deed;font-size:12px;margin:18px 0 8px;font-weight:650}input{width:100%;padding:15px 16px;border-radius:11px;background:#0b1527;border:1px solid rgba(129,171,214,.28);outline:0;color:#fff;transition:.2s;font-size:14px}input::placeholder{color:#647b99}input:focus{border-color:#6bdcfd;box-shadow:0 0 0 3px rgba(79,204,250,.12)}.fieldhelp{font-size:11px;color:#8299b3;margin-top:7px}.submit{margin-top:24px;width:100%;min-height:49px;border:0;border-radius:11px;color:#061322;background:linear-gradient(100deg,#65e6f5,#8bbbff 56%,#c5a0ff);font-weight:800;font-size:14px;cursor:pointer;transition:transform .2s,filter .2s;box-shadow:0 9px 35px rgba(89,183,255,.18)}.submit:hover{filter:brightness(1.12);transform:translateY(-1px)}.submit:disabled{cursor:wait;opacity:.56;transform:none}.status{min-height:38px;font-size:12px;line-height:1.5;color:#a3b8d0;margin-top:16px}.status.error{color:#ff9ba8}.status.success{color:#80efd0}.footnote{border-top:1px solid rgba(120,159,203,.19);padding-top:18px;font-size:11px;color:#8296b2}.formlabel{display:flex;justify-content:space-between;align-items:center}.formlabel span{color:#8ba4c4;font-size:11px}.bottom{padding:10px clamp(24px,5vw,76px) 25px;max-width:1640px;margin:auto;color:#7991af;font-size:11px;display:flex;justify-content:space-between;gap:12px;border-top:1px solid rgba(113,155,205,.12)}
#app{border:0;display:block;width:100%;height:100vh;background:#060b16}#app[hidden],#loginView[hidden]{display:none!important}
@media(max-width:1000px){.stage{grid-template-columns:1fr;max-width:670px;padding-top:34px}.orbit{display:none}h1{font-size:clamp(43px,9vw,65px)}.intro{max-width:580px}.top{padding:22px 24px}.topright{font-size:9px}}@media(max-width:570px){.stage{gap:30px;padding:25px 18px 45px}.highlights{gap:7px}.highlight{font-size:11px;padding:8px 9px}.login{padding:25px 21px}.brand strong{font-size:15px}.sigil{width:41px;height:41px}.topright{display:none}.bottom{padding:15px 22px}}
@media(prefers-reduced-motion:reduce){body:before{animation:none}.submit{transition:none}}
</style>
</head>
<body>
<div id="loginView">
<header class="top"><div class="brand"><div class="sigil">OGL</div><div><strong>OpenGenesisLINK</strong><span>World API · Control Center</span></div></div><div class="topright"><i class="light" aria-hidden="true"></i> Secure administration <span> / </span> OGL 0.9.3.12</div></header>
<main class="stage">
<section class="showcase"><div class="eyebrow">The next generation of OpenSimulator</div><h1>Deine Welt.<br><em>Deine Kontrolle.</em></h1><div class="intro">Willkommen im OpenGenesisLINK Control Center. Verwalte Simulator-Nodes, Grid-Regionen, Dienste, Sicherheit, API-Zugänge und die Infrastruktur deiner virtuellen Welt an einem zentralen Ort.</div><div class="highlights"><div class="highlight">◈ Grid &amp; Regions</div><div class="highlight">⬡ Simulator Control</div><div class="highlight">⌁ World API Explorer</div></div><div class="orbit" aria-hidden="true"></div></section>
<div class="window"><section class="login"><div class="loginkicker">Authorized Personnel Only</div><h2>Administrator Login</h2><p>Bitte melde dich mit deinem lokalen GridUser-Konto an. Nur Benutzer ab <strong>UserLevel 200</strong> und Administratorberechtigung erhalten Zugang.</p><form id="adminForm" autocomplete="on"><label for="username">Grid-Benutzername</label><input id="username" name="username" autocomplete="username" maxlength="100" required placeholder="Vorname.Nachname"><div class="fieldhelp">Beispiel: Sleimer.Akina oder Vorname Resident</div><label for="password">Passwort</label><input id="password" name="password" autocomplete="current-password" type="password" maxlength="256" required placeholder="Dein Grid-Passwort"><div class="formlabel"><label for="totp">Zwei-Faktor-Code (falls aktiviert)</label><span>Optional</span></div><input id="totp" name="totp" autocomplete="one-time-code" inputmode="numeric" maxlength="12" placeholder="6-stelliger TOTP-Code"><button class="submit" id="submit" type="submit">Sicher anmelden →</button><div class="status" id="status" role="status" aria-live="polite">Verschlüsselte Administrator-Anmeldung über die World API.</div></form><div class="footnote">Anmeldung ausschließlich über HTTPS. Rechte werden auf dem Server geprüft. Zugangstokens werden weder in Cookies noch im Browserspeicher gespeichert.</div></section></div>
</main>
<footer class="bottom"><div>© OpenGenesisLINK · OGL Server Technology</div><div>Protected administration environment</div></footer>
</div>
<iframe id="app" hidden title="OpenGenesisLINK World API Control Center" referrerpolicy="no-referrer"></iframe>
<script>
'use strict';
const form=document.getElementById('adminForm'),status=document.getElementById('status'),submit=document.getElementById('submit');
const frame=document.getElementById('app');
let token='',expires=0;
function message(text,isError){status.textContent=text;status.className='status'+(isError?' error':'');}
function logout(){
  token='';expires=0;
  frame.srcdoc='';frame.hidden=true;
  document.getElementById('loginView').hidden=false;
  form.elements.namedItem('password').value='';form.elements.namedItem('totp').value='';submit.disabled=false;
  message('Abgemeldet. Bitte erneut anmelden.',false);
}
window.addEventListener('message',event=>{
  if(event.origin!==location.origin || event.source!==frame.contentWindow)return;
  if(event.data && event.data.type==='ogl-logout')logout();
});
form.addEventListener('submit',async event=>{
  event.preventDefault();
  if(!window.isSecureContext || location.protocol!=='https:'){message('HTTPS ist für die Administrator-Anmeldung zwingend erforderlich.',true);return;}
  if(!form.reportValidity())return;
  submit.disabled=true;message('Anmeldung wird überprüft …',false);
  try{
    const credentials={username:form.elements.namedItem('username').value.trim(),password:form.elements.namedItem('password').value};
    if(form.elements.namedItem('totp').value.trim())credentials.totp=form.elements.namedItem('totp').value.trim();
    const login=await fetch('/api/v1/auth/admin/session',{method:'POST',headers:{'Content-Type':'application/json','Accept':'application/json'},body:JSON.stringify(credentials),cache:'no-store',credentials:'same-origin'});
    form.elements.namedItem('password').value='';form.elements.namedItem('totp').value='';
    const data=await login.json().catch(()=>({}));
    if(!login.ok)throw Error(login.status===403?'Keine Berechtigung: UserLevel 200 und admin:* erforderlich.':login.status===401?'Anmeldung fehlgeschlagen. Zugangsdaten oder TOTP überprüfen.':'Administrator-Dienst nicht verfügbar (HTTP '+login.status+').');
    if(!data.administrator||Number(data.user_level)<200||!String(data.scope||'').split(/\s+/).includes('admin:*')||!data.access_token)throw Error('Dieses Konto hat keine gültige OGL-Administratorberechtigung.');
    token=data.access_token;
    expires=Date.now()+Math.max(0,Number(data.expires_in||0)-30)*1000;
    if(!expires||expires<=Date.now())throw Error('Die Administratorsitzung ist bereits abgelaufen.');
    const panel=await fetch('/api/v1/docs/panel',{headers:{'Authorization':'Bearer '+token,'Accept':'text/html'},cache:'no-store',credentials:'same-origin'});
    if(!panel.ok){
      const errorCode=panel.headers.get('X-OGL-Docs-Error')||'';
      const messages={
        https_not_verified:'HTTPS-Verbindung am OGL-Backend nicht bestätigt. Reverse-Proxy-Konfiguration prüfen.',
        admin_api_disabled:'Der Administrator-Dienst ist nicht aktiviert.',
        admin_token_required:'Das Sitzungstoken wurde nicht übermittelt.',
        admin_session_invalid:'Die Administrator-Sitzung ist ungültig oder abgelaufen.',
        admin_permission_required:'GridUser-Berechtigung erforderlich: UserLevel mindestens 200 und admin:*.'};
      throw Error(messages[errorCode]||('Zugang zum Control Center verweigert (HTTP '+panel.status+').'));
    }
    const html=await panel.text();
    frame.onload=()=>{
      if(token && Date.now()<expires)
        frame.contentWindow.postMessage({type:'ogl-auth',access_token:token,expires_at:expires},location.origin);
    };
    document.getElementById('loginView').hidden=true;
    frame.hidden=false;
    frame.srcdoc=html;
  }catch(err){
    token='';expires=0;message(String(err.message||'Anmeldung fehlgeschlagen.'),true);
  }finally{submit.disabled=false;}
});
</script>
</body>
</html>
""";
    }
}

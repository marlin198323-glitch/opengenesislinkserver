# OGL Native Login v1 – Implementierungsstatus

Stand: 2026-10-12. Entwicklungsbranch, **nicht Stable**.

## Implementiert

- Separater POST-Pfad `/ogl/v1/login` am bestehenden LLLoginServiceInConnector und damit am vorhandenen Robust-PublicPort.
- Fail-closed: POST liefert 503 `native_login_not_enabled`; andere Methoden liefern 405.
- `Cache-Control: no-store`, `X-Content-Type-Options: nosniff`.
- Keine Zugangsdaten werden im deaktivierten Zustand eingelesen oder protokolliert.
- Bestehende XML-RPC- und LLSD-Routen bleiben unverändert.

## Noch offen – keine Freigabe für Produktivbetrieb

1. TLS-Vertrauensgrenze für HTTPS-Proxy sauber definieren (keine unvalidierten Forwarded-Header).
2. JSON-Anfrageschema, Größenbegrenzung, Validierung und einheitliche Fehlerantworten.
3. Rate-Limit und Schutz vor Replay/Brute-Force.
4. Aufruf derselben `ILoginService.Login(...)`-Instanz, die der Legacy-Handler verwendet.
5. Sichere JSON-Serialisierung von `LoginResponse.ToOSDMap()` und Kompatibilität mit CAPS/Circuit/Region-Placement.
6. End-to-End-Tests für gültige/ungültige Anmeldungen, Proxy/TLS, Hypergrid und Legacy-Firestorm.
7. Erst danach Discovery-Felder `ogl_login` und `ogl_native_login` aktivieren.

**Hinweis:** Die Route ist ein sicherer Integrationsanker, noch kein funktionsfähiger nativer Login. Keine Stable-Freigabe ohne Build und Tests.

## Experimenteller JSON-Handler (Branch feature/ogl-native-login-v1)

Der Handler ist mit derselben `ILoginService`-Instanz wie XML-RPC und LLSD verbunden.
Er ist **standardmäßig deaktiviert**. Nur für kontrollierte Tests kann in der
Robust-Konfiguration unter `[LoginService]` gesetzt werden:

```ini
OglNativeLoginEnabled = false
```

Die Freigabe über `true` ist ausschließlich für einen direkten HTTPS-Testlistener
vorgesehen. `IOSHttpRequest.IsSecured` muss wahr sein; ein HTTP-Listener hinter
einem TLS-terminierenden Reverse-Proxy wird derzeit bewusst abgewiesen.
`X-Forwarded-Proto` wird aus Sicherheitsgründen nicht vertraut.

Beispiel für den Testrequest (nur über HTTPS):

```json
{
  "first": "Test",
  "last": "Resident",
  "passwd": "PASSWORD",
  "start": "last",
  "version": "OGL Viewer Dev",
  "channel": "OpenGenesisLINK Viewer"
}
```

Die Antwort ist die JSON-Serialisierung des regulären OpenSim-`LoginResponse.ToOSDMap()`.
Das ist noch **kein finalisiertes, unabhängig versioniertes OGL-Antwortschema**.

### Bekannte Einschränkungen

- Der aktuelle IP-Limiter (mindestens 2 Sekunden Abstand) ist nur ein Prototyp:
  keine verteilte Koordination, keine garantierte Speicherbereinigung, keine
  per-Account-Sperre. Vor Produktivbetrieb ersetzen.
- Ein Anti-Replay-Mechanismus für das native Protokoll fehlt noch.
- Keine Ende-zu-Ende-Tests oder erfolgreiche Kompilierung bestätigt.
- Die Discovery bleibt absichtlich auf `ogl_native_login: false` und
  `ogl_login: null`.
- Nicht auf öffentlichen Produktionssystemen aktivieren.

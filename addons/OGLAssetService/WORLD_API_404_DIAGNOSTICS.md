# World API 404 diagnosis — 2026-10-16

The “Ooops! ... obsconded with by knomes” HTML is OpenSim's built-in HTTP 404 page (`OpenSim/Framework/Servers/HttpServer/BaseHttpServer.cs` and `bin/http_404.html.example`). It is not a World API application error.

The current `main` source registers the World API in `NexVerse/Server/Api/NexVerseWorldApiConnector.cs` using `[ServiceList] NexVerseWorldApiConnector = "NexVerseWorldApi@${Const|PublicPort}/NexVerse.Server.Api.dll:NexVerseWorldApiConnector"`. Registration is conditional on `[NexVerseWorldApi] Enabled = true`. Core routes are registered at `/api/v1`, `/api/v1/health`, `/api/v1/docs`, and `/api/v1/openapi.json`.

## Safe read-only checks on ROBUST

1. Check actual active configuration, not `.ini.example`: `[ServiceList]` connector entry, `[NexVerseWorldApi] Enabled`, `[Const] PublicPort`, and presence of `bin/NexVerse.Server.Api.dll`.
2. Inspect startup logs for `[NEX-WORLD-API]`, `NexVerseWorldApiConnector`, `ReflectionTypeLoadException`, `FileNotFoundException` or `Failed to load`.
3. Probe the actual ROBUST listener directly, e.g. `curl -si --max-time 5 http://127.0.0.1:8002/api/v1/health` (substitute configured port).
4. Compare `curl -si --max-time 5 https://world.stadt-nexverse.de/api/v1/health`. A successful local response but external 404 indicates reverse-proxy routing or upstream selection.
5. If both return 404, inspect connector load errors and route registration before editing handler logic.
6. If health works but privileged routes do not, check `EnablePrivilegedEndpoints` and authentication rather than assuming all routes are broken.

Do not expose API keys in diagnostic logs or command output. No production configuration is changed by this document.

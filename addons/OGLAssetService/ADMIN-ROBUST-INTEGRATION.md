# OGL AssetService administrative ROBUST integration (development)

This is an opt-in, read-only administrative surface. It does not replace
the standard AssetService HTTP connector, and must not share its public listener.

## Host startup contract

1. Construct and own a dedicated TLS-enabled IHttpServer with restricted
   network reachability. Reserve the two OGL administrative GET paths.
2. Provide an OGLAssetConnector on an isolated staging ROBUST instance.
3. Supply the [OGLAssetServiceAdmin] configuration. Enabled defaults to false.
   Set DedicatedListenerConfirmed=true and ExpectedTlsPort to the actual
   dedicated listener SSLPort. A mismatch aborts registration.
4. Configure separate credentials verified by OGLAssetServiceDedicatedAuth and explicitly
   select Viewer or Administrator. Do not reuse the normal asset-service secret.
5. Call OGLAssetServiceAdminListener.Start(config, adminServer, connector,
   journalPath, maxBytes, retainedFiles) from the host startup lifecycle.
   A null result means disabled. Keep the returned object for host shutdown.
6. Dispose the returned listener during ROBUST shutdown to remove handlers
   and close the audit journal.

## Boundaries and release blockers

The addon does not create an HTTP server or wire itself into the ROBUST
bootstrap sequence. The host must establish actual network isolation and
exclusive ownership of the administrative paths. TLS port matching alone
does not prove isolation. The dedicated inbound-only IServiceAuth identifies a service credential,
not a human operator. Existing audit logs contain outcome and endpoint, not
an attributable operator identity. Multi-process journal access is unsupported.
Do not enable this on production or merge to main before the final acceptance.

## Credential isolation

Administrative credentials are read exclusively from the dedicated configuration section and verified by OGLAssetServiceDedicatedAuth. General [Network] authentication credentials are not inherited. Basic authentication must only be used over TLS. Protect configuration files, rotate credentials during maintenance, and restrict listener access by network policy. No per-operator identity is established by this adapter.

## Startup preflight

The host-owned OGLAssetServiceAdminListener now invokes OGLAssetServiceAdminPreflight.Check before creating the persistent audit journal. Invalid TLS listener settings, credentials, or role selections fail without creating a journal or registering routes. This is not a network ACL verification, and the host must still provide exclusive route ownership and dedicated network isolation. Final acceptance is pending.

## Registration failure handling

The registration adapter now removes the status handler if registration of the configuration handler throws. The host lifecycle also attempts route cleanup if startup fails. This is not proof of exclusive route ownership: IHttpServer.AddStreamHandler returns void and may silently ignore duplicates. The dedicated host must reserve both paths before starting the addon. This behavior and concurrent start/stop need final acceptance testing.

## Existing service instance bridge

The standard ROBUST AssetServiceConnector now exposes LoadedAssetService (IAssetService). After the host constructs that connector, it may pass its LoadedAssetService to OGLAssetServiceRobustBootstrap.StartAdmin(config, dedicatedAdminServer, loadedAssetService, journalPath, maxJournalBytes, retainedFiles). This opt-in bridge requires the loaded instance to be OGLAssetConnector, avoiding a second asset store connector. The host owns the returned listener and must dispose it at shutdown. No automatic ROBUST bootstrap invocation or dedicated network listener creation has been added. The bridge must never receive the public asset listener.

## Exclusive route ownership

BaseHttpServer now offers TryAddExclusiveStreamHandler and TryRemoveOwnedStreamHandler. The administrative registration requires BaseHttpServer and atomically rejects occupied routes; it retains exact handler identities for cleanup. Shutdown and partial-start rollback remove only OGL-owned handlers. This prevents deletion of unrelated HTTP routes. Host network isolation and actual startup wiring remain separate requirements. Final compilation and acceptance testing are deferred.

## ROBUST startup and shutdown lifecycle boundary

OGLAssetServiceRobustLifecycle owns the optional admin registration after the normal ROBUST asset connector has loaded. The host supplies the public asset IHttpServer, a distinct dedicated TLS IHttpServer, and the exact IAssetService instance returned by AssetServiceConnector.LoadedAssetService. Start rejects a shared listener object or shared port and delegates TLS, credential and audit checks to the bootstrap. Retain the lifecycle object and call Dispose during shutdown before disposing the HTTP servers. This adapter is not yet invoked automatically by OpenSim/Server/ServerMain.cs; it does not create listeners, modify existing public routes or deploy configuration. No production enablement is implied.

## ROBUST executable integration (opt-in)

OpenSim/Server/Robust.csproj references the addon project. ServerMain.cs retains the loaded AssetServiceConnector only when its LoadedAssetService is OGLAssetConnector. After service connector initialization, [OGLAssetServiceAdmin] Enabled=true requests an existing HTTP server at ExpectedTlsPort, then OGLAssetServiceRobustLifecycle.Start performs listener separation and TLS preflight. The host reads AuditJournalPath (default logs/ogl-asset-admin-audit.jsonl), AuditMaxJournalBytes (default 10485760), and AuditRetainedFiles (default 5). The lifecycle is disposed after m_Server.Run() returns, including exceptions from Run. This wiring does not provision a certificate or create a dedicated TLS server; the operator must configure that server separately. If Enabled=false, no admin startup occurs. The implementation is unbuilt and awaits final acceptance.

## Port separation and journal settings

ROBUST rejects an enabled admin configuration if ExpectedTlsPort matches either the loaded public asset listener port or the primary ROBUST listener port. The example configuration now documents AuditJournalPath, AuditMaxJournalBytes, and AuditRetainedFiles. Operators must provide a genuinely dedicated TLS listener with restricted network access and a certificate; a distinct port alone does not establish network isolation. Compilation and acceptance tests remain outstanding.

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
4. Configure a separate BasicHttpAuthentication credential and explicitly
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
does not prove isolation. IServiceAuth identifies a dedicated service credential,
not a human operator. Existing audit logs contain outcome and endpoint, not
an attributable operator identity. Multi-process journal access is unsupported.
Do not enable this on production or merge to main before the final acceptance.

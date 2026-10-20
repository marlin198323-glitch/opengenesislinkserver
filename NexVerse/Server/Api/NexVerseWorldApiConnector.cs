// SPDX-License-Identifier: MPL-2.0

using System;
using System.Collections.Generic;
using log4net;
using Nini.Config;
using NexVerse.Core.Audit;
using NexVerse.Core.ControlPlane;
using NexVerse.Core.Economy;
using NexVerse.Core.Experiences;
using NexVerse.Core.Discovery;
using NexVerse.Core.Messaging;
using NexVerse.Core.Jobs;
using NexVerse.Core.Observability;
using NexVerse.Core.Security;
using NexVerse.Core.Voice;
using OpenSim.Data;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Server.Base;
using OpenSim.Server.Handlers.Base;
using OpenSim.Server.Handlers.Asset;
using OpenSim.Services.Interfaces;
using OpenSim.Groups;
using OpenSim.Services.Friends;
using OpenSim.Services.EstateService;

namespace NexVerse.Server.Api
{
    public sealed class NexVerseWorldApiConnector : ServiceConnector
    {
        private static readonly ILog m_Log = LogManager.GetLogger(typeof(NexVerseWorldApiConnector));
        private static readonly object s_TelemetrySync = new object();
        private static NexOtlpHttpExporter s_OtlpExporter;
        private static readonly object s_OfflineImMailRelaySync = new object();
        private static NexOfflineImMailRelay s_OfflineImMailRelay;

        // The HTTP route must exist even if later privileged connector startup
        // fails (e.g. an unavailable optional service). The public-only
        // fallback is loaded on demand; no authorization is bypassed.
        private sealed class EarlyStatisticsRoute
        {
            private readonly IConfigSource m_Config;
            private readonly object m_Sync = new object();
            private NexStatisticsApi m_PublicApi;
            private NexStatisticsApi m_PrivilegedApi;

            public EarlyStatisticsRoute(IConfigSource config)
            {
                m_Config = config;
            }

            public void AttachPrivileged(NexStatisticsApi api)
            {
                if (api == null)
                    throw new ArgumentNullException(nameof(api));

                System.Threading.Volatile.Write(ref m_PrivilegedApi, api);
            }

            public void Summary(IOSHttpRequest request, IOSHttpResponse response)
            {
                NexStatisticsApi api = System.Threading.Volatile.Read(ref m_PrivilegedApi);
                if (api != null)
                {
                    api.Summary(request, response);
                    return;
                }

                // During bootstrap or when privileged services cannot start,
                // never return OpenSim's misleading HTML 404 for this route.
                // Anonymous requests get only public aggregates; requests
                // carrying credentials fail closed without an authenticator.
                lock (m_Sync)
                {
                    api = System.Threading.Volatile.Read(ref m_PrivilegedApi);
                    if (api == null)
                    {
                        if (m_PublicApi == null)
                        {
                            try
                            {
                                m_PublicApi = new NexStatisticsApi(
                                    LoadOptionalService<IUserAccountService>(m_Config, "UserAccountService"),
                                    LoadGridUserData(m_Config),
                                    LoadOptionalService<IPresenceService>(m_Config, "PresenceService"),
                                    LoadOptionalService<IGridService>(m_Config, "GridService"),
                                    null,
                                    true,
                                    LoadRegionData(m_Config),
                                    LoadOptionalService<IAssetService>(m_Config, "AssetService"));
                            }
                            catch (Exception e)
                            {
                                m_Log.Error("[NEX-STATS]: Public statistics data source failed to initialize.", e);
                                // Retry provider initialization on the next request.
                                api = new NexStatisticsApi(null, null, null, null, null, true);
                            }
                        }

                        if (api == null)
                            api = m_PublicApi;
                    }
                }

                api.Summary(request, response);
            }
        }

        public NexVerseWorldApiConnector(IConfigSource config, IHttpServer server, string configName)
            : base(config, server, configName)
        {
            string sectionName = string.IsNullOrWhiteSpace(configName) ? "NexVerseWorldApi" : configName;
            IConfig apiConfig = config.Configs[sectionName];

            if (apiConfig == null)
                throw new InvalidOperationException("Missing [" + sectionName + "] configuration section.");

            if (!apiConfig.GetBoolean("Enabled", true))
            {
                m_Log.Info("[NEX-WORLD-API]: World API connector is disabled.");
                return;
            }

            string publicBaseUrl = apiConfig
                .GetString("PublicBaseUrl", "http://world.stadt-nexverse.de")
                .TrimEnd('/');

            NexMetricsRegistry metrics = NexMetricsRegistry.Default;
            metrics.SetGauge(
                "nexverse_build_info",
                "NexVerse build metadata.",
                1,
                new System.Collections.Generic.Dictionary<string, string>
                {
                    ["api_version"] = Core.NexVersePlatform.ApiVersion,
                    ["milestone"] = Core.NexVersePlatform.MilestoneCodename
                });

            IConfig metricsConfig = config.Configs["NexMetrics"];
            if (metricsConfig != null && metricsConfig.GetBoolean("Enabled", false))
            {
                string metricsPath = metricsConfig.GetString("Path", "/internal/metrics");
                NexMetricsEndpoint metricsEndpoint = new NexMetricsEndpoint(metrics);
                server.AddSimpleStreamHandler(
                    new SimpleStreamHandler(metricsPath, metricsEndpoint.Handle, "NexVerse Prometheus Metrics"));
                m_Log.WarnFormat("[NEX-METRICS]: Metrics endpoint enabled at {0}. Restrict access with firewall/TLS policy.", metricsPath);
            }

            IConfig telemetryConfig = config.Configs["NexTelemetry"];
            if (telemetryConfig != null &&
                telemetryConfig.GetBoolean("Enabled", false))
            {
                string protocol =
                    telemetryConfig.GetString(
                        "Protocol",
                        "http/json").Trim();

                if (!string.Equals(
                        protocol,
                        "http/json",
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "NexVerse currently supports OTLP protocol http/json.");
                }

                lock (s_TelemetrySync)
                {
                    if (s_OtlpExporter == null)
                    {
                        string instanceId =
                            telemetryConfig.GetString(
                                "ServiceInstanceId",
                                string.Empty);

                        if (string.IsNullOrWhiteSpace(instanceId))
                            instanceId = Environment.MachineName;

                        NexOtlpHttpOptions options =
                            new NexOtlpHttpOptions
                            {
                                Endpoint =
                                    ParseOptionalAbsoluteUri(
                                        telemetryConfig.GetString(
                                            "Endpoint",
                                            "http://127.0.0.1:4318")),
                                TracesEndpoint =
                                    ParseOptionalAbsoluteUri(
                                        telemetryConfig.GetString(
                                            "TracesEndpoint",
                                            string.Empty)),
                                MetricsEndpoint =
                                    ParseOptionalAbsoluteUri(
                                        telemetryConfig.GetString(
                                            "MetricsEndpoint",
                                            string.Empty)),
                                ServiceName =
                                    telemetryConfig.GetString(
                                        "ServiceName",
                                        "NexVerse.Robust").Trim(),
                                ServiceInstanceId =
                                    instanceId.Trim(),
                                Headers =
                                    ParseOtlpHeaders(
                                        telemetryConfig.GetString(
                                            "Headers",
                                            string.Empty)),
                                ExportIntervalSeconds =
                                    telemetryConfig.GetInt(
                                        "ExportIntervalSeconds",
                                        10),
                                BatchSize =
                                    telemetryConfig.GetInt(
                                        "BatchSize",
                                        256),
                                QueueCapacity =
                                    telemetryConfig.GetInt(
                                        "QueueCapacity",
                                        4096),
                                TimeoutMilliseconds =
                                    telemetryConfig.GetInt(
                                        "TimeoutMilliseconds",
                                        10000),
                                MaxRetries =
                                    telemetryConfig.GetInt(
                                        "MaxRetries",
                                        3),
                                AllowInsecure =
                                    telemetryConfig.GetBoolean(
                                        "AllowInsecure",
                                        false)
                            };

                        s_OtlpExporter =
                            new NexOtlpHttpExporter(
                                metrics,
                                options);

                        AppDomain.CurrentDomain.ProcessExit +=
                            (_, __) =>
                            {
                                lock (s_TelemetrySync)
                                {
                                    s_OtlpExporter?.Dispose();
                                    s_OtlpExporter = null;
                                }
                            };

                        m_Log.InfoFormat(
                            "[NEX-OTLP]: OTLP/HTTP JSON export enabled for service {0}.",
                            options.ServiceName);
                    }
                }
            }

            INexEventBus eventBus = CreateEventBus(config, server);
            IConfig nexBusConfig = config.Configs["NexBus"];
            // The UI must remain available for secure bootstrap even when
            // Enabled=true but a NexBus shared secret has not been provisioned yet.
            // Report the effective transport, not just the requested setting.
            bool distributedNexBusEnabled =
                eventBus is DistributedNexEventBus;
            int nodeStaleAfterSeconds =
                nexBusConfig == null
                    ? 90
                    : nexBusConfig.GetInt("NodeStaleAfterSeconds", 90);
            NexNodeRegistry nodeRegistry =
                new NexNodeRegistry(
                    eventBus,
                    nodeStaleAfterSeconds);

            INexAuditSink publicAuditSink = new LogNexAuditSink();
            INexAuthorizationService authorization = new NexAuthorizationService();

            NexApiRequestGate apiGate = new NexApiRequestGate(
                apiConfig.GetBoolean("RateLimitEnabled", true),
                apiConfig.GetInt("RateLimitRequests", 240),
                apiConfig.GetInt("RateLimitWindowSeconds", 60),
                apiConfig.GetBoolean("TrustForwardedFor", false));

            NexVerseWorldApiHandlers handlers = new NexVerseWorldApiHandlers(publicBaseUrl, eventBus, publicAuditSink);
            NexChatGptInstructionDownloads chatGptInstructions =
                new NexChatGptInstructionDownloads(publicBaseUrl);
            // Keep the login shell reachable even without privileged endpoints.
            // The panel fails closed until a real GridUser authenticator is configured.
            OglApiDocsAccess oglDocs = new OglApiDocsAccess();
            server.AddSimpleStreamHandler(new SimpleStreamHandler(
                "/api/v1/docs", apiGate.Wrap(oglDocs.HandleLogin),
                "OpenGenesisLINK Control Center Login"));
            server.AddSimpleStreamHandler(new SimpleStreamHandler(
                "/api/v1/docs/panel", apiGate.Wrap(oglDocs.HandlePanel),
                "OpenGenesisLINK protected administration panel"));

            server.AddSimpleStreamHandler(new SimpleStreamHandler("/api/v1", apiGate.Wrap(handlers.Root), "NexVerse World API"));
            server.AddSimpleStreamHandler(new SimpleStreamHandler("/api/v1/chatgpt-instructions.md", apiGate.Wrap(chatGptInstructions.HandleAll), "NexVerse ChatGPT API Instructions"));
            server.AddSimpleStreamHandler(new SimpleStreamHandler("/api/v1/chatgpt-buergerportal.md", apiGate.Wrap(chatGptInstructions.HandleCitizen), "NexVerse ChatGPT Citizen Portal Instructions"));
            server.AddSimpleStreamHandler(new SimpleStreamHandler("/api/v1/chatgpt-admin.md", apiGate.Wrap(chatGptInstructions.HandleAdmin), "NexVerse ChatGPT Admin Instructions"));
            server.AddSimpleStreamHandler(new SimpleStreamHandler("/api/v1/health", apiGate.Wrap(handlers.Health), "NexVerse World API Health"));
            // Register statistics before the much larger privileged service graph.
            // A failure later in connector startup must never silently remove
            // this public endpoint or fall through to OpenSim's default 404.
            EarlyStatisticsRoute statisticsRoute = new EarlyStatisticsRoute(config);
            server.AddSimpleStreamHandler(new SimpleStreamHandler(
                "/api/v1/statistics/summary",
                apiGate.Wrap(statisticsRoute.Summary),
                "NexVerse statistics summary"));
            m_Log.Info("[NEX-WORLD-API]: Statistics summary route registered.");

            server.AddSimpleStreamHandler(new SimpleStreamHandler("/api/v1/version", apiGate.Wrap(handlers.Version), "NexVerse World API Version"));
            server.AddSimpleStreamHandler(new SimpleStreamHandler("/api/v1/capabilities", apiGate.Wrap(handlers.Capabilities), "NexVerse World API Capabilities"));
            server.AddSimpleStreamHandler(new SimpleStreamHandler("/api/v1/openapi.json", apiGate.Wrap(handlers.OpenApi), "NexVerse World API OpenAPI"));

            // Central OGLVoice provider authority for trusted simulator nodes.
            // This publishes signed public metadata only; room tokens and
            // Firestorm/LiveKit session provisioning are not implemented here.
            IConfig oglVoiceConfig = config.Configs["OGLVoice"];
            if (oglVoiceConfig?.GetBoolean("Enabled", false) == true)
            {
                string mode = oglVoiceConfig.GetString("Mode", "GridAuthority").Trim();
                if (!string.Equals(mode, "GridAuthority", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Robust [OGLVoice] requires Mode=GridAuthority");

                OglVoiceProviderDescriptor provider = new()
                {
                    provider_url = oglVoiceConfig.GetString("ServiceUrl", string.Empty).Trim(),
                    tenant_id = oglVoiceConfig.GetString("TenantId", string.Empty).Trim(),
                    hypergrid_guests = oglVoiceConfig.GetBoolean("IncludeHypergridGuests", true),
                    media_gateway_url = oglVoiceConfig.GetBoolean("EnableFirestormGateway", false)
                        ? oglVoiceConfig.GetString("MediaGatewayUrl", "").Trim()
                        : null,
                    viewer_capability = oglVoiceConfig.GetBoolean("EnableFirestormGateway", false)
                        ? "firestorm-webrtc-v1"
                        : "not-yet-implemented"
                };
                provider.Validate();

                string discoveryKey = nexBusConfig?.GetString("SharedKey", string.Empty);
                OglVoiceDiscoveryProof.ValidateKey(discoveryKey);
                OglVoiceGridDiscoveryEndpoint discovery =
                    new OglVoiceGridDiscoveryEndpoint(provider, discoveryKey);
                server.AddSimpleStreamHandler(new SimpleStreamHandler(
                    OglVoiceGridDiscoveryEndpoint.Route, discovery.Handle,
                    "OGLVoice signed grid provider discovery"));
                m_Log.Info("[OGL-VOICE]: Sichere Grid-Provider-Discovery aktiv; Viewer-CAPS noch nicht implementiert.");

                if (oglVoiceConfig.GetBoolean("EnableSessionAuthority", false))
                {
                    // Server-only secrets, NEVER serialized into provider
                    // descriptors or stored in public simulator INI examples.
                    string apiKey = Environment.GetEnvironmentVariable("OGLVOICE_LIVEKIT_API_KEY");
                    string apiSecret = Environment.GetEnvironmentVariable("OGLVOICE_LIVEKIT_API_SECRET");
                    OglVoiceLiveKitTokenIssuer issuer =
                        new OglVoiceLiveKitTokenIssuer(provider.tenant_id, apiKey, apiSecret);

                    string allowedNodeCsv = oglVoiceConfig.GetString("AllowedNodes", "");
                    string[] allowedNodes = allowedNodeCsv.Split(',',
                        StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                    OglVoiceSessionAuthorityEndpoint sessions =
                        new OglVoiceSessionAuthorityEndpoint(
                            issuer, provider, discoveryKey, allowedNodes);
                    server.AddSimpleStreamHandler(new SimpleStreamHandler(
                        OglVoiceSessionAuthorityEndpoint.Route, sessions.Handle,
                        "OGLVoice internal LiveKit session authority"));
                    m_Log.Info("[OGL-VOICE]: LiveKit-Session-Authority fuer explizit zugelassene Simulatoren bereit.");
                }
            }

            IConfig offlineImMailRelayConfig = config.Configs["OfflineIMMailRelay"];
            if (offlineImMailRelayConfig != null &&
                offlineImMailRelayConfig.GetBoolean("Enabled", false))
            {
                lock (s_OfflineImMailRelaySync)
                {
                    if (s_OfflineImMailRelay == null)
                    {
                        try
                        {
                            IUserAccountService relayUserAccounts =
                                LoadOptionalService<IUserAccountService>(
                                    config,
                                    "UserAccountService");
                            IInstantMessage relayInstantMessages =
                                LoadOptionalService<IInstantMessage>(
                                    config,
                                    "HGInstantMessageService");

                            if (relayUserAccounts == null)
                                throw new InvalidOperationException(
                                    "Offline IM mail relay could not load UserAccountService.");

                            if (relayInstantMessages == null)
                                throw new InvalidOperationException(
                                    "Offline IM mail relay could not load HGInstantMessageService.");

                            s_OfflineImMailRelay =
                                new NexOfflineImMailRelay(
                                    offlineImMailRelayConfig,
                                    relayUserAccounts,
                                    relayInstantMessages);
                            s_OfflineImMailRelay.Start();

                            AppDomain.CurrentDomain.ProcessExit +=
                                (_, __) =>
                                {
                                    lock (s_OfflineImMailRelaySync)
                                    {
                                        s_OfflineImMailRelay?.Dispose();
                                        s_OfflineImMailRelay = null;
                                    }
                                };
                        }
                        catch (Exception e)
                        {
                            m_Log.ErrorFormat(
                                "[NEX-IM-MAIL-RELAY]: Failed to start: {0}",
                                e.Message);
                        }
                    }
                }
            }

            bool privilegedEndpoints = apiConfig.GetBoolean("EnablePrivilegedEndpoints", false);
            if (privilegedEndpoints)
            {
                string auditStorePath = apiConfig.GetString(
                    "AuditStorePath",
                    "data/nexverse-audit.jsonl");

                INexAuditStore auditStore =
                    new PersistentNexAuditStore(auditStorePath);

                string apiKeyStorePath =
                    apiConfig.GetString(
                        "ApiKeyStorePath",
                        "data/nexverse-api-keys.json");
                INexApiKeyStore apiKeyStore =
                    new PersistentNexApiKeyStore(
                        apiKeyStorePath);

                string idempotencyStorePath =
                    apiConfig.GetString(
                        "IdempotencyStorePath",
                        "data/nexverse-idempotency.json");
                int idempotencyTtlSeconds =
                    apiConfig.GetInt(
                        "IdempotencyTtlSeconds",
                        86400);

                INexIdempotencyStore idempotencyStore =
                    new PersistentNexIdempotencyStore(
                        idempotencyStorePath);

                INexAuditSink auditSink =
                    new CompositeNexAuditSink(
                        publicAuditSink,
                        auditStore);

                NexRegionOperationRegistry regionOperationRegistry =
                    new NexRegionOperationRegistry(
                        eventBus,
                        auditSink);

                IOglJobStore jobStore = new PersistentOglJobStore(
                    apiConfig.GetString("JobStorePath", "data/opengenesislink-jobs.json"));
                OglJobEventBridge jobBridge = new OglJobEventBridge(jobStore, eventBus);
                OglJobRunner jobRunner = new OglJobRunner(jobStore);
                jobRunner.Register(new OglDirectoryBackupWorker());
                jobRunner.Register(new OglDirectoryRestoreWorker());
                IConfig assetServiceConfig = config.Configs["AssetService"];
                string assetRoot = assetServiceConfig == null
                    ? string.Empty
                    : assetServiceConfig.GetString("BaseDirectory", string.Empty);
                string assetIndexPath = apiConfig.GetString(
                    "AssetIndexPath",
                    "data/opengenesislink-assets-index.json");
                jobRunner.Register(new OglAssetReindexWorker(assetRoot, assetIndexPath));

                IConfig databaseServiceConfig = config.Configs["DatabaseService"];
                string databaseStorageProvider = databaseServiceConfig == null
                    ? string.Empty
                    : databaseServiceConfig.GetString("StorageProvider", string.Empty);
                string databaseConnectionString = databaseServiceConfig == null
                    ? string.Empty
                    : databaseServiceConfig.GetString("ConnectionString", string.Empty);
                jobRunner.Register(
                    new OglDatabaseMaintenanceWorker(
                        databaseStorageProvider,
                        databaseConnectionString,
                        apiConfig.GetInt("DatabaseMaintenanceCommandTimeoutSeconds", 300)));

                IConfig userConfig = config.Configs["UserAccountService"];
                IConfig authConfig = config.Configs["AuthenticationService"];

                string userModule = apiConfig.GetString(
                    "UserAccountServiceModule",
                    userConfig == null ? string.Empty : userConfig.GetString("LocalServiceModule", string.Empty));

                string authModule = apiConfig.GetString(
                    "AuthenticationServiceModule",
                    authConfig == null ? string.Empty : authConfig.GetString("LocalServiceModule", string.Empty));

                if (string.IsNullOrWhiteSpace(userModule) || string.IsNullOrWhiteSpace(authModule))
                    throw new InvalidOperationException("NexVerse privileged API requires UserAccountServiceModule and AuthenticationServiceModule.");

                IUserAccountService userAccounts =
                    ServerUtils.LoadPlugin<IUserAccountService>(userModule, new object[] { config });

                if (userAccounts == null)
                    throw new InvalidOperationException("Unable to load NexVerse World API user account service.");

                IAuthenticationService authentication =
                    ServerUtils.LoadPlugin<IAuthenticationService>(authModule, new object[] { config, userAccounts });

                if (authentication == null)
                    authentication = ServerUtils.LoadPlugin<IAuthenticationService>(authModule, new object[] { config });

                if (authentication == null)
                    throw new InvalidOperationException("Unable to load NexVerse World API authentication service.");

                IInventoryService inventory = LoadOptionalService<IInventoryService>(config, "InventoryService");
                // Reuse ROBUST's initialized AssetService connector, including the
                // OGL/FSAsset implementation, instead of constructing a second
                // provider with an incompatible one-argument constructor.
                IAssetService assets = AssetServiceConnector.GetLocalService(config, "AssetService");
                if (assets == null)
                    m_Log.Warn("[NEX-WORLD-API]: [AssetService] is not yet registered; profile images will resolve it at request time.");
                jobRunner.Register(new OglInventoryRepairWorker(inventory));

                IGridUserService gridUsers = LoadOptionalService<IGridUserService>(config, "GridUserService");
                IGridService grid = LoadOptionalService<IGridService>(config, "GridService");
                IEstateDataService estateData = LoadOptionalService<IEstateDataService>(config, "EstateDataStore");

                jobRunner.Register(
                    new OglRegionMigrationWorker(
                        nodeRegistry,
                        eventBus,
                        grid,
                        estateData,
                        apiConfig.GetInt("RegionMigrationOperationTimeoutSeconds", 1800)));

                IPresenceService presence = LoadOptionalService<IPresenceService>(config, "PresenceService");
                IGridUserData gridUserData = LoadGridUserData(config);

                int adminMinimumLevel = apiConfig.GetInt("AdminMinimumUserLevel", 200);
                int tokenLifetimeSeconds = apiConfig.GetInt("TokenLifetimeSeconds", 1800);

                INexAccessTokenService nativeTokens = null;
                INexOAuthStore oauthStore = null;
                INexOidcSigningService oidcSigner = null;
                if (apiConfig.GetBoolean("EnableNativeTokens", false))
                {
                    string signingKey = apiConfig.GetString("NativeTokenSigningKey", string.Empty);
                    string issuer = apiConfig.GetString("NativeTokenIssuer", publicBaseUrl);
                    string audience = apiConfig.GetString("NativeTokenAudience", "nexverse-world-api");

                    nativeTokens = new HmacNexAccessTokenService(
                        issuer,
                        audience,
                        signingKey,
                        tokenLifetimeSeconds);

                    string authStorePath = apiConfig.GetString(
                        "AuthStorePath",
                        "data/nexverse-auth.json");
                    string oidcIssuer = apiConfig.GetString("OidcIssuer", publicBaseUrl);
                    string oidcKeyPath = apiConfig.GetString(
                        "OidcSigningKeyPath",
                        "data/nexverse-oidc-es256.pem");
                    int oidcTokenLifetimeSeconds = apiConfig.GetInt(
                        "OidcTokenLifetimeSeconds",
                        tokenLifetimeSeconds);

                    oauthStore = new PersistentNexOAuthStore(authStorePath);
                    oidcSigner = new PersistentEs256OidcSigningService(
                        oidcIssuer,
                        oidcKeyPath,
                        oidcTokenLifetimeSeconds);

                    m_Log.Info("[NEX-WORLD-API]: NexVerse native scoped access tokens and persistent OAuth/OIDC are enabled.");
                }

                NexApiAuthenticator authenticator = new NexApiAuthenticator(
                    userAccounts,
                    authorization,
                    nativeTokens,
                    oauthStore,
                    apiKeyStore,
                    adminMinimumLevel);

                // Attach the existing, server-side native admin authenticator
                // once privileged services are available. No duplicate routing.
                oglDocs.ConfigureAdministratorAccess(
                    authenticator, adminMinimumLevel);

                OglFeatureControlApi featureControl = null;
                IConfig featureConfig = config.Configs["OGLFeatureControl"];
                if (featureConfig?.GetBoolean("Enabled", false) == true)
                {
                    string privateKey = Environment.GetEnvironmentVariable("OGL_FEATURE_PRIVATE_KEY_PEM");
                    if (string.IsNullOrWhiteSpace(privateKey))
                    {
                        string keyPath = featureConfig.GetString("PrivateKeyPath", "");
                        if (string.IsNullOrWhiteSpace(keyPath) || !System.IO.File.Exists(keyPath))
                            throw new InvalidOperationException(
                                "[OGL FEATURE CONTROL] Privater ES256-Schlüssel für Robust fehlt.");
                        privateKey = System.IO.File.ReadAllText(keyPath);
                    }
                    featureControl = new OglFeatureControlApi(
                        featureConfig.GetString("StorePath", "data/ogl-feature-control.json"),
                        privateKey, authenticator, nodeRegistry, eventBus);
                    OglFeatureControlApi registered = featureControl;
                    AppDomain.CurrentDomain.ProcessExit += (_, __) => registered.Dispose();
                    server.AddSimpleStreamHandler(new SimpleStreamHandler(
                        "/api/v1/feature-control", apiGate.Wrap(featureControl.Handle),
                        "OpenGenesisLINK Feature Control"), true);
                }

                OpenSimNexUserService userService = new OpenSimNexUserService(
                    userAccounts,
                    authentication,
                    inventory,
                    gridUsers,
                    grid);

                // Dedicated Stadtportal backend endpoint; never expose an API
                // key to browser JavaScript. Disabled until reviewed/published
                // Benjamin/Luna templates and TLS reverse proxy are deployed.
                OglPortalRegistrationApi portalRegistration = new OglPortalRegistrationApi(
                    authenticator,
                    userAccounts,
                    apiConfig.GetBoolean("PortalAvatarRegistrationEnabled", false));
                server.AddSimpleStreamHandler(new SimpleStreamHandler(
                    "/api/v1/portal/avatars/register",
                    apiGate.Wrap(portalRegistration.Handle),
                    "OpenGenesisLINK Stadtportal avatar registration"));

                NexUserApiRouter userRouter = new NexUserApiRouter(
                    userService,
                    authenticator,
                    eventBus,
                    auditSink,
                    nativeTokens,
                    oauthStore,
                    auditStore,
                    idempotencyStore,
                    idempotencyTtlSeconds,
                    apiKeyStore,
                    grid,
                    estateData,
                    userAccounts,
                    nodeRegistry,
                    regionOperationRegistry,
                    distributedNexBusEnabled,
                    adminMinimumLevel,
                    new PersistentNexSecurityStore(apiConfig.GetString("SecurityStorePath", "data/nexverse-security.json")));

                // Admin login now POSTs to the exact same URL serving the
                // login page, /api/v1/docs. This works even when a KeyHelp
                // reverse proxy routes only the public docs path correctly.
                // The endpoint still checks credentials and UserLevel >= 200,
                // and the dashboard is still guarded by a signed bearer.
                oglDocs.ConfigureDocsSessionLogin(userRouter.HandleControlCenterLogin);

                NexEconomyApi economyApi =
                    new NexEconomyApi(
                        authenticator,
                        userAccounts,
                        auditSink,
                        () =>
                            NexEconomyConnector.Current?.Enabled == true
                                ? NexEconomyConnector.Current.Economy
                                : null);
                server.AddSimpleStreamHandler(
                    new SimpleStreamHandler(
                        "/api/v1/economy",
                        apiGate.Wrap(economyApi.Handle),
                        "OpenGenesisLINK NV$ Economy API"),
                    true);

                NexBankingCommerceApi bankingCommerceApi =
                    new NexBankingCommerceApi(
                        authenticator,
                        userAccounts,
                        auditSink,
                        () =>
                            NexEconomyConnector.Current?.Enabled == true
                                ? NexEconomyConnector.Current.Economy
                                : null);

                server.AddSimpleStreamHandler(
                    new SimpleStreamHandler(
                        "/api/v1/banking",
                        apiGate.Wrap(bankingCommerceApi.HandleBanking),
                        "OpenGenesisLINK NV$ Banking API"),
                    true);
                server.AddSimpleStreamHandler(
                    new SimpleStreamHandler(
                        "/api/v1/commerce",
                        apiGate.Wrap(bankingCommerceApi.HandleCommerce),
                        "OpenGenesisLINK NexCommerce API"),
                    true);
                server.AddSimpleStreamHandler(
                    new SimpleStreamHandler(
                        "/api/v1/land-commerce",
                        apiGate.Wrap(bankingCommerceApi.HandleLandCommerce),
                        "OpenGenesisLINK Land Commerce API"),
                    true);

                IConfig nexGroupsConfig =
                    config.Configs["NexGroups"];

                GroupsService nexGroupsService = null;

                if (nexGroupsConfig?.GetBoolean("Enabled", false) == true)
                {
                    nexGroupsService =
                        new GroupsService(
                            config);

                    NexGroupsApi nexGroupsApi =
                        new NexGroupsApi(
                            authenticator,
                            auditSink,
                            nexGroupsService,
                            () =>
                                NexEconomyConnector.Current?.Enabled == true
                                    ? NexEconomyConnector.Current.Economy
                                    : null);

                    server.AddSimpleStreamHandler(
                        new SimpleStreamHandler(
                            "/api/v1/groups",
                            apiGate.Wrap(nexGroupsApi.Handle),
                            "OpenGenesisLINK NexGroups API"),
                        true);
                }

                IConfig experiencesConfig =
                    config.Configs["NexExperiences"];

                // JSON remains a single-authority backend. Do not silently
                // accept a requested SQL/cluster backend that does not exist.
                string experienceBackend = experiencesConfig?.GetString(
                    "StoreBackend", "SingleWriterJson")?.Trim();
                if (!string.Equals(experienceBackend, "SingleWriterJson",
                        StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        "Unsupported [NexExperiences] StoreBackend: only " +
                        "SingleWriterJson is implemented. Active/active Robust " +
                        "requires a future transactional distributed backend.");

                string experienceStorePath =
                    experiencesConfig?.GetString(
                        "StorePath",
                        "data/nexverse-experiences.json") ??
                    "data/nexverse-experiences.json";

                // The constructor acquires the local writer lease BEFORE
                // loading resident grants and script/KV state. A second
                // authority sharing this file refuses to start.
                NexExperienceStore experienceStore =
                    new NexExperienceStore(
                        experienceStorePath);
                AppDomain.CurrentDomain.ProcessExit +=
                    (_, __) => experienceStore.Dispose();
                m_Log.Info("[NEX-EXPERIENCES]: SingleWriterJson authority acquired. " +
                    "One Robust writer only; no cross-host shared-file failover.");

                NexExperiencesApi experiencesApi =
                    new NexExperiencesApi(
                        authenticator,
                        userAccounts,
                        auditSink,
                        experienceStore);

                server.AddSimpleStreamHandler(
                    new SimpleStreamHandler(
                        "/api/v1/experiences",
                        apiGate.Wrap(experiencesApi.Handle),
                        "OpenGenesisLINK NexExperiences API"),
                    true);

                IConfig discoveryConfig =
                    config.Configs["NexDiscovery"];

                string discoveryStorePath =
                    discoveryConfig?.GetString(
                        "StorePath",
                        "data/opengenesislink-discovery.json") ??
                    "data/opengenesislink-discovery.json";

                string teleportBaseUri =
                    discoveryConfig?.GetString(
                        "TeleportBaseUri",
                        string.Empty) ??
                    string.Empty;

                NexDiscoveryStore discoveryStore =
                    new NexDiscoveryStore(
                        discoveryStorePath);

                NexDiscoveryApi discoveryApi =
                    new NexDiscoveryApi(
                        authenticator,
                        userAccounts,
                        grid,
                        nexGroupsService,
                        experienceStore,
                        discoveryStore,
                        () =>
                            NexEconomyConnector.Current?.Enabled == true
                                ? NexEconomyConnector.Current.Economy
                                : null,
                        auditSink,
                        teleportBaseUri);

                NexCitizenWorldMapApi citizenWorldMap = new NexCitizenWorldMapApi(grid, teleportBaseUri);
                server.AddSimpleStreamHandler(
                    new SimpleStreamHandler(
                        "/api/v1/world-map",
                        apiGate.Wrap(citizenWorldMap.Handle),
                        "OpenGenesisLINK Citizen World Map"),
                    true);

                server.AddSimpleStreamHandler(
                    new SimpleStreamHandler(
                        "/api/v1/search",
                        apiGate.Wrap(discoveryApi.HandleSearch),
                        "OpenGenesisLINK NexSearch API"),
                    true);

                server.AddSimpleStreamHandler(
                    new SimpleStreamHandler(
                        "/api/v1/places",
                        apiGate.Wrap(discoveryApi.HandlePlaces),
                        "OpenGenesisLINK Places API"),
                    true);

                server.AddSimpleStreamHandler(
                    new SimpleStreamHandler(
                        "/api/v1/events",
                        apiGate.Wrap(discoveryApi.HandleContent),
                        "OpenGenesisLINK Discovery Events API"),
                    true);

                server.AddSimpleStreamHandler(
                    new SimpleStreamHandler(
                        "/api/v1/classifieds",
                        apiGate.Wrap(discoveryApi.HandleContent),
                        "OpenGenesisLINK Classifieds API"),
                    true);

                server.AddSimpleStreamHandler(
                    new SimpleStreamHandler(
                        "/api/v1/land-portal",
                        apiGate.Wrap(discoveryApi.HandleLandPortal),
                        "OpenGenesisLINK Land Portal API"),
                    true);

                server.AddSimpleStreamHandler(
                    new SimpleStreamHandler(
                        "/api/v1/destinations",
                        apiGate.Wrap(discoveryApi.HandleDestinations),
                        "OpenGenesisLINK Destination Guide API"),
                    true);

                NexDiscoveryWebPage discoveryWeb =
                    new NexDiscoveryWebPage(
                        publicBaseUrl);

                server.AddSimpleStreamHandler(
                    new SimpleStreamHandler(
                        "/search",
                        apiGate.Wrap(discoveryWeb.Search),
                        "OpenGenesisLINK Firestorm Web Search"));

                server.AddSimpleStreamHandler(
                    new SimpleStreamHandler(
                        "/destinations",
                        apiGate.Wrap(discoveryWeb.Destinations),
                        "OpenGenesisLINK Firestorm Destination Guide"));

                NexInventoryApi inventoryApi =
                    new NexInventoryApi(
                        inventory,
                        assets,
                        authenticator);
                server.AddSimpleStreamHandler(
                    new SimpleStreamHandler(
                        "/api/v1/inventory",
                        apiGate.Wrap(inventoryApi.Handle),
                        "NexVerse Inventory API"),
                    true);

                OglOarApi oarApi = new OglOarApi(authenticator, nodeRegistry, eventBus, jobBridge, featureControl);
                server.AddSimpleStreamHandler(
                    new SimpleStreamHandler(
                        "/api/v1/oar",
                        apiGate.Wrap(oarApi.Handle),
                        "OpenGenesisLINK OAR API"),
                    true);

                OglIarApi iarApi = new OglIarApi(authenticator, nodeRegistry, userAccounts, eventBus, jobBridge, featureControl);
                server.AddSimpleStreamHandler(
                    new SimpleStreamHandler(
                        "/api/v1/iar",
                        apiGate.Wrap(iarApi.Handle),
                        "OpenGenesisLINK IAR API"),
                    true);

                OglJobsApi jobsApi = new OglJobsApi(authenticator, jobStore, jobRunner);
                server.AddSimpleStreamHandler(
                    new SimpleStreamHandler(
                        "/api/v1/jobs",
                        apiGate.Wrap(jobsApi.Handle),
                        "OpenGenesisLINK Jobs API"),
                    true);

                NexStatisticsApi statisticsApi = new NexStatisticsApi(
                    userAccounts,
                    gridUserData,
                    presence,
                    grid,
                    authenticator,
                    true,
                    LoadRegionData(config),
                    assets);

                INexSecurityStore securityStore =
                    new PersistentNexSecurityStore(
                        apiConfig.GetString("SecurityStorePath", "data/nexverse-security.json"));
                string webAuthnRpId = apiConfig.GetString("WebAuthnRpId", string.Empty);
                string webAuthnOrigin = apiConfig.GetString("WebAuthnOrigin", string.Empty);
                NexWebAuthnVerifier webAuthnVerifier =
                    string.IsNullOrWhiteSpace(webAuthnRpId) || string.IsNullOrWhiteSpace(webAuthnOrigin)
                        ? null
                        : new NexWebAuthnVerifier(webAuthnRpId, webAuthnOrigin);
                NexSecurityApi securityApi = new NexSecurityApi(authenticator, securityStore, webAuthnVerifier);
                server.AddSimpleStreamHandler(
                    new SimpleStreamHandler(
                        "/api/v1/security",
                        apiGate.Wrap(securityApi.Handle),
                        "NexVerse Security"),
                    true);

                FriendsService friendsService = new FriendsService(config);
                NexSocialGraphApi socialGraph =
                    new NexSocialGraphApi(userAccounts, friendsService, authenticator, new MuteListService(config), auditSink, presence);
                server.AddSimpleStreamHandler(
                    new SimpleStreamHandler(
                        "/api/v1/relationships",
                        apiGate.Wrap(socialGraph.Handle),
                        "NexVerse Social Graph"),
                    true);

                IInstantMessage citizenInstantMessages =
                    LoadOptionalService<IInstantMessage>(config, "HGInstantMessageService");
                NexCitizenImStore citizenImStore = null;
                IConfig portalImConfig = config.Configs["NexPortalIM"];
                if (portalImConfig != null && portalImConfig.GetBoolean("Enabled", false))
                {
                    string sharedImSecret =
                        Environment.GetEnvironmentVariable("NEX_PORTAL_IM_SECRET");
                    if (string.IsNullOrWhiteSpace(sharedImSecret))
                        sharedImSecret = portalImConfig.GetString("SharedSecret", "");
                    if (System.Text.Encoding.UTF8.GetByteCount(sharedImSecret) < 32)
                        throw new InvalidOperationException(
                            "[NEX-PORTAL-IM]: Enabled archive requires a 32-byte shared secret.");
                    citizenImStore = new NexCitizenImStore(
                        portalImConfig.GetString("StorePath", "data/nexverse-portal-im.sqlite"),
                        portalImConfig.GetInt("RetentionDays", 30));
                    NexCitizenImIngress citizenImIngress = new NexCitizenImIngress(
                        sharedImSecret, citizenImStore, userAccounts, friendsService);
                    server.AddSimpleStreamHandler(
                        new SimpleStreamHandler("/internal/nexportal/im/v1",
                            citizenImIngress.Handle, "NexVerse Simulator Portal IM Ingest"),
                        true);
                    m_Log.Info("[NEX-PORTAL-IM]: Opt-in portal conversation archive enabled.");
                }
                NexCitizenImApi citizenImApi =
                    new NexCitizenImApi(userAccounts, friendsService, authenticator,
                        citizenInstantMessages, auditSink, citizenImStore,
                        () => LoadOptionalService<IInstantMessage>(config, "HGInstantMessageService"));
                server.AddSimpleStreamHandler(
                    new SimpleStreamHandler(
                        "/api/v1/messages",
                        apiGate.Wrap(citizenImApi.Handle),
                        "NexVerse Citizen Friend IM"),
                    true);

                IConfig profilesConfig = config.Configs["UserProfilesService"];
                if (profilesConfig != null && profilesConfig.GetBoolean("Enabled", false))
                {
                    IConfig databaseConfig = config.Configs["DatabaseService"];
                    string profileStorageProvider =
                        profilesConfig.GetString(
                            "StorageProvider",
                            databaseConfig == null
                                ? string.Empty
                                : databaseConfig.GetString("StorageProvider", string.Empty));
                    string profileConnectionString =
                        profilesConfig.GetString(
                            "ConnectionString",
                            databaseConfig == null
                                ? string.Empty
                                : databaseConfig.GetString("ConnectionString", string.Empty));

                    IProfilesData profilesData =
                        ServerUtils.LoadPlugin<IProfilesData>(
                            profileStorageProvider,
                            new object[] { profileConnectionString });

                    if (profilesData == null)
                        throw new InvalidOperationException("Unable to load NexVerse WebProfileV3 profile datastore.");

                    // Publication belongs to userprofile.profileAllowPublish.
                    // No InventoryService database dependency is needed for
                    // the public (or owner/admin private) profile image route.
                    NexWebProfileV3Api webProfileV3 =
                        new NexWebProfileV3Api(
                            userAccounts,
                            profilesData,
                            assets,
                            authenticator,
                            auditSink,
                            () => AssetServiceConnector.GetLocalService(config, "AssetService"));

                    server.AddSimpleStreamHandler(
                        new SimpleStreamHandler(
                            "/api/v1/profiles",
                            apiGate.Wrap(webProfileV3.Handle),
                            "NexVerse WebProfileV3"),
                        true);

                    m_Log.Info("[NEX-WEBPROFILE-V3]: WebProfileV3 enabled using the authoritative UserProfilesService store.");
                }

                // Preserve authenticated statistics: the early route now delegates
                // to the fully initialized, scope-enforcing administrator service.
                statisticsRoute.AttachPrivileged(statisticsApi);

                if (nativeTokens != null && oauthStore != null && oidcSigner != null)
                {
                    int authCodeLifetimeSeconds = apiConfig.GetInt("AuthorizationCodeLifetimeSeconds", 120);
                    int refreshLifetimeSeconds = apiConfig.GetInt("RefreshTokenLifetimeSeconds", 2592000);

                    NexOAuthApiRouter oauthRouter = new NexOAuthApiRouter(
                        publicBaseUrl,
                        nativeTokens,
                        oidcSigner,
                        oauthStore,
                        authenticator,
                        userAccounts,
                        userService,
                        eventBus,
                        auditSink,
                        adminMinimumLevel,
                        authCodeLifetimeSeconds,
                        refreshLifetimeSeconds,
                        securityStore);

                    server.AddSimpleStreamHandler(new SimpleStreamHandler(
                        "/.well-known/openid-configuration",
                        apiGate.Wrap(oauthRouter.Discovery),
                        "NexVerse OIDC Discovery"));
                    server.AddSimpleStreamHandler(new SimpleStreamHandler(
                        "/oauth/jwks",
                        apiGate.Wrap(oauthRouter.Jwks),
                        "NexVerse OIDC JWKS"));
                    server.AddSimpleStreamHandler(new SimpleStreamHandler(
                        "/oauth/authorize",
                        apiGate.Wrap(oauthRouter.Authorize),
                        "NexVerse OAuth Authorization"));
                    server.AddSimpleStreamHandler(new SimpleStreamHandler(
                        "/oauth/token",
                        apiGate.Wrap(oauthRouter.Token),
                        "NexVerse OAuth Token"));
                    server.AddSimpleStreamHandler(new SimpleStreamHandler(
                        "/oauth/revoke",
                        apiGate.Wrap(oauthRouter.Revoke),
                        "NexVerse OAuth Revocation"));
                    server.AddSimpleStreamHandler(new SimpleStreamHandler(
                        "/api/v1/auth/clients",
                        apiGate.Wrap(oauthRouter.Clients),
                        "NexVerse OAuth Client Administration"));
                    server.AddSimpleStreamHandler(new SimpleStreamHandler(
                        "/api/v1/auth/sessions/revoke",
                        apiGate.Wrap(oauthRouter.RevokeSessions),
                        "NexVerse Session Revocation"));
                }

                // Register authentication as exact routes rather than relying
                // only on /api variable-path fallback. A wrong handler or
                // reverse-proxy fallback can otherwise respond HTTP 200 without
                // the access_token contract expected by the Control Center.
                // The existing userRouter still validates POST, credentials,
                // TOTP, permissions and issues the signed bearer token.
                server.AddSimpleStreamHandler(new SimpleStreamHandler(
                    "/api/v1/auth/session",
                    apiGate.Wrap(userRouter.Handle),
                    "OpenGenesisLINK native resident session"));
                server.AddSimpleStreamHandler(new SimpleStreamHandler(
                    "/api/v1/auth/admin/session",
                    apiGate.Wrap(userRouter.Handle),
                    "OpenGenesisLINK native administrator session"));

                server.AddSimpleStreamHandler(
                    new SimpleStreamHandler("/api", apiGate.Wrap(userRouter.Handle), "NexVerse World API privileged router"),
                    true);

                m_Log.Warn("[NEX-WORLD-API]: Privileged endpoints are enabled. Use only over a transport that protects bearer tokens.");
            }
            else
            {
                // The already-registered route lazily loads the same public
                // aggregate services; no duplicate handler or database plugin.
                m_Log.Info(
                    "[NEX-WORLD-API]: Privileged endpoints are disabled; privacy-safe aggregate statistics remain available.");
            }

            m_Log.InfoFormat("[NEX-WORLD-API]: World API {0} enabled at {1}/api/v1", Core.NexVersePlatform.ApiVersion, publicBaseUrl);
        }

        private static INexEventBus CreateEventBus(IConfigSource config, IHttpServer server)
        {
            IConfig busConfig = config.Configs["NexBus"];
            if (busConfig == null || !busConfig.GetBoolean("Enabled", false))
            {
                m_Log.Info("[NEXBUS]: Distributed transport is disabled; using in-memory event bus.");
                return new InMemoryNexEventBus();
            }

            string nodeId = busConfig.GetString("NodeId", Environment.MachineName);
            string sharedKey = busConfig.GetString("SharedKey", string.Empty);
            if (string.IsNullOrEmpty(sharedKey) ||
                System.Text.Encoding.UTF8.GetByteCount(sharedKey) < 32)
            {
                m_Log.Warn(
                    "[NEXBUS]: Verteilter Transport angefordert, aber NEXVERSE_NEXBUS_SHARED_KEY fehlt oder ist zu kurz. " +
                    "Robust startet mit lokalem EventBus, damit der Administrator die Secrets im World-API-Control-Center erstellen kann. " +
                    "Nach sicherer Hinterlegung des SharedKey Robust neu starten.");
                return new InMemoryNexEventBus();
            }

            string peersRaw = busConfig.GetString("Peers", string.Empty);
            string inboundPath = busConfig.GetString("InboundPath", "/internal/nexbus/v1/events");
            int queueCapacity = busConfig.GetInt("QueueCapacity", 4096);
            int timeoutMs = busConfig.GetInt("RequestTimeoutMilliseconds", 2000);
            int deduplicationWindow = busConfig.GetInt("DeduplicationWindow", 10000);

            string[] peers = peersRaw.Split(
                new[] { ';', ',' },
                StringSplitOptions.RemoveEmptyEntries);

            HttpNexEventTransport transport = new HttpNexEventTransport(
                nodeId,
                peers,
                sharedKey,
                queueCapacity,
                timeoutMs);

            DistributedNexEventBus distributed = new DistributedNexEventBus(
                transport,
                deduplicationWindow);

            NexBusHttpEndpoint endpoint = new NexBusHttpEndpoint(distributed, sharedKey);
            server.AddSimpleStreamHandler(
                new SimpleStreamHandler(inboundPath, endpoint.Handle, "NexBus peer transport"));

            m_Log.WarnFormat(
                "[NEXBUS]: Distributed HTTP transport enabled for node {0} with {1} configured peer(s). Protect this transport with TLS/firewall policy.",
                nodeId,
                peers.Length);

            return distributed;
        }

        private static Uri ParseOptionalAbsoluteUri(
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            if (!Uri.TryCreate(
                    value.Trim(),
                    UriKind.Absolute,
                    out Uri parsed))
            {
                throw new InvalidOperationException(
                    "Invalid NexTelemetry endpoint URL: " +
                    value);
            }

            return parsed;
        }

        private static IReadOnlyDictionary<string, string> ParseOtlpHeaders(
            string value)
        {
            Dictionary<string, string> headers =
                new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(value))
                return headers;

            foreach (string rawPair in value.Split(
                         ',',
                         StringSplitOptions.RemoveEmptyEntries))
            {
                int separator = rawPair.IndexOf('=');
                if (separator <= 0)
                {
                    throw new InvalidOperationException(
                        "NexTelemetry Headers must use comma-separated key=value pairs.");
                }

                string key =
                    Uri.UnescapeDataString(
                        rawPair.Substring(
                            0,
                            separator).Trim());

                string headerValue =
                    Uri.UnescapeDataString(
                        rawPair.Substring(
                            separator + 1).Trim());

                if (string.IsNullOrWhiteSpace(key))
                {
                    throw new InvalidOperationException(
                        "NexTelemetry header names must not be empty.");
                }

                headers[key] = headerValue;
            }

            return headers;
        }

        // GridStore contains *all* locally registered regions, including
        // offline records. GridService.GetOnlineRegions alone is insufficient.
        private static IRegionData LoadRegionData(IConfigSource config)
        {
            IConfig database = config.Configs["DatabaseService"];
            IConfig grid = config.Configs["GridService"];
            string provider = grid?.GetString(
                "StorageProvider",
                database?.GetString("StorageProvider", string.Empty)) ??
                database?.GetString("StorageProvider", string.Empty) ?? string.Empty;
            string connection = grid?.GetString(
                "ConnectionString",
                database?.GetString("ConnectionString", string.Empty)) ??
                database?.GetString("ConnectionString", string.Empty) ?? string.Empty;
            string realm = grid?.GetString("Realm", "regions") ?? "regions";

            if (string.IsNullOrWhiteSpace(provider))
            {
                m_Log.Warn("[NEX-WORLD-API]: GridStore statistics source has no StorageProvider.");
                return null;
            }

            try
            {
                return ServerUtils.LoadPlugin<IRegionData>(
                    provider,
                    new object[] { connection, realm });
            }
            catch (Exception e)
            {
                m_Log.WarnFormat(
                    "[NEX-WORLD-API]: GridStore statistics source unavailable: {0}",
                    e.Message);
                return null;
            }
        }

        private static IGridUserData LoadGridUserData(IConfigSource config)
        {
            string dllName = string.Empty;
            string connectionString = string.Empty;
            string realm = "GridUser";

            IConfig database = config.Configs["DatabaseService"];
            if (database != null)
            {
                dllName = database.GetString("StorageProvider", dllName);
                connectionString = database.GetString("ConnectionString", connectionString);
            }

            IConfig gridUsers = config.Configs["GridUserService"];
            if (gridUsers != null)
            {
                dllName = gridUsers.GetString("StorageProvider", dllName);
                connectionString = gridUsers.GetString("ConnectionString", connectionString);
                realm = gridUsers.GetString("Realm", realm);
            }

            if (string.IsNullOrWhiteSpace(dllName))
            {
                m_Log.Warn("[NEX-WORLD-API]: Statistics GridUser data source is unavailable because no StorageProvider is configured.");
                return null;
            }

            try
            {
                return ServerUtils.LoadPlugin<IGridUserData>(
                    dllName,
                    new object[] { connectionString, realm });
            }
            catch (Exception e)
            {
                m_Log.WarnFormat(
                    "[NEX-WORLD-API]: Statistics GridUser data source could not be loaded: {0}",
                    e.Message);
                return null;
            }
        }

        private static T LoadOptionalService<T>(IConfigSource config, string sectionName)
            where T : class
        {
            IConfig section = config.Configs[sectionName];
            if (section == null)
                return null;

            string module = section.GetString("LocalServiceModule", string.Empty);
            if (string.IsNullOrWhiteSpace(module))
                return null;

            try
            {
                return ServerUtils.LoadPlugin<T>(module, new object[] { config });
            }
            catch (Exception e)
            {
                m_Log.WarnFormat(
                    "[NEX-WORLD-API]: Optional service {0} could not be loaded: {1}",
                    sectionName,
                    e.Message);
                return null;
            }
        }
    }

    internal sealed class LogNexAuditSink : INexAuditSink
    {
        private static readonly ILog m_Log = LogManager.GetLogger(typeof(LogNexAuditSink));

        public void Record(NexAuditEvent auditEvent)
        {
            if (auditEvent == null)
                return;

            m_Log.InfoFormat(
                "[NEX-AUDIT]: id={0} actor={1} action={2} resource={3} correlation={4}",
                auditEvent.EventId,
                auditEvent.Actor,
                auditEvent.Action,
                auditEvent.Resource,
                auditEvent.CorrelationId);
        }
    }
}

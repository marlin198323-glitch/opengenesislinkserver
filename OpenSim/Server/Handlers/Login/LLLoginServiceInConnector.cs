/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using System.Reflection;
using System.Net;
using System.Text.Json;
using log4net;
using Nini.Config;
using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Server.Handlers.Base;

namespace OpenSim.Server.Handlers.Login
{
    public class LLLoginServiceInConnector : ServiceConnector
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private ILoginService m_LoginService;
        private bool m_Proxy;
        private BasicDosProtectorOptions m_DosProtectionOptions;
        private bool m_OglNativeLoginEnabled;
        private readonly ConcurrentDictionary<string, DateTime> m_OglLastAttempt = new ConcurrentDictionary<string, DateTime>();

        public LLLoginServiceInConnector(IConfigSource config, IHttpServer server, IScene scene) :
                base(config, server, String.Empty)
        {
            m_log.Debug("[LLLOGIN IN CONNECTOR]: Starting...");
            string loginService = ReadLocalServiceFromConfig(config);

            ISimulationService simService = scene.RequestModuleInterface<ISimulationService>();
            ILibraryService libService = scene.RequestModuleInterface<ILibraryService>();

            Object[] args = new Object[] { config, simService, libService };
            m_LoginService = ServerUtils.LoadPlugin<ILoginService>(loginService, args);

            InitializeHandlers(server);
        }

        public LLLoginServiceInConnector(IConfigSource config, IHttpServer server, string configName) :
            base(config, server, configName)
        {
            string loginService = ReadLocalServiceFromConfig(config);

            Object[] args = new Object[] { config };

            m_LoginService = ServerUtils.LoadPlugin<ILoginService>(loginService, args);

            InitializeHandlers(server);
        }

        public LLLoginServiceInConnector(IConfigSource config, IHttpServer server) :
            this(config, server, String.Empty)
        {
        }

        private string ReadLocalServiceFromConfig(IConfigSource config)
        {
            IConfig serverConfig = config.Configs["LoginService"];
            if (serverConfig == null)
                throw new Exception(String.Format("No section LoginService in config file"));

            string loginService = serverConfig.GetString("LocalServiceModule", String.Empty);
            if (loginService.Length == 0)
                throw new Exception(String.Format("No LocalServiceModule for LoginService in config file"));

            m_Proxy = serverConfig.GetBoolean("HasProxy", false);
            m_OglNativeLoginEnabled = serverConfig.GetBoolean("OglNativeLoginEnabled", false);
            m_DosProtectionOptions = new BasicDosProtectorOptions();
            // Dos Protection Options
            m_DosProtectionOptions.AllowXForwardedFor = serverConfig.GetBoolean("DOSAllowXForwardedForHeader", false);
            m_DosProtectionOptions.RequestTimeSpan =
                TimeSpan.FromMilliseconds(serverConfig.GetInt("DOSRequestTimeFrameMS", 10000));
            m_DosProtectionOptions.MaxRequestsInTimeframe = serverConfig.GetInt("DOSMaxRequestsInTimeFrame", 5);
            m_DosProtectionOptions.ForgetTimeSpan =
                TimeSpan.FromMilliseconds(serverConfig.GetInt("DOSForgiveClientAfterMS", 120000));
            m_DosProtectionOptions.ReportingName = "LOGINDOSPROTECTION";


            return loginService;
        }

        private void HandleOglLogin(IOSHttpRequest request, IOSHttpResponse response)
        {
            response.KeepAlive = false;
            response.ContentType = "application/json; charset=utf-8";
            response.AddHeader("Cache-Control", "no-store");
            response.AddHeader("X-Content-Type-Options", "nosniff");
            if (!string.Equals(request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
            {
                response.StatusCode = 405;
                response.AddHeader("Allow", "POST");
                WriteOglError(response, "method_not_allowed");
                return;
            }
            if (!m_OglNativeLoginEnabled)
            {
                response.StatusCode = 503;
                WriteOglError(response, "native_login_not_enabled");
                return;
            }
            // Never trust X-Forwarded-Proto from arbitrary clients. Terminate TLS
            // at this listener or use a separately secured trusted proxy design.
            if (!request.IsSecured)
            {
                response.StatusCode = 403;
                WriteOglError(response, "tls_required");
                return;
            }
            if (request.ContentLength64 <= 0 || request.ContentLength64 > 8192 ||
                request.ContentType == null ||
                !request.ContentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
            {
                response.StatusCode = 400;
                WriteOglError(response, "invalid_request");
                return;
            }
            string ip = request.RemoteIPEndPoint?.Address.ToString() ?? "unknown";
            DateTime now = DateTime.UtcNow;
            if (m_OglLastAttempt.TryGetValue(ip, out DateTime previous) &&
                (now - previous).TotalSeconds < 2)
            {
                response.StatusCode = 429;
                response.AddHeader("Retry-After", "2");
                WriteOglError(response, "rate_limited");
                return;
            }
            m_OglLastAttempt[ip] = now;
            try
            {
                byte[] buffer = new byte[(int)request.ContentLength64];
                int count = 0;
                while (count < buffer.Length)
                {
                    int n = request.InputStream.Read(buffer, count, buffer.Length - count);
                    if (n <= 0) break;
                    count += n;
                }
                if (count != buffer.Length) throw new JsonException("Incomplete JSON body");
                using (JsonDocument doc = JsonDocument.Parse(buffer))
                {
                    JsonElement root = doc.RootElement;
                    if (root.ValueKind != JsonValueKind.Object ||
                        !RequiredString(root, "first", out string first) ||
                        !RequiredString(root, "last", out string last) ||
                        !RequiredString(root, "passwd", out string passwd))
                    {
                        response.StatusCode = 400;
                        WriteOglError(response, "invalid_request");
                        return;
                    }
                    string start = OptionalString(root, "start", "last");
                    string version = OptionalString(root, "version", "OGL Viewer");
                    string channel = OptionalString(root, "channel", "OpenGenesisLINK Viewer");
                    string mac = OptionalString(root, "mac", "Unknown");
                    string id0 = OptionalString(root, "id0", "Unknown");
                    UUID scope = UUID.Zero;
                    string scopeText = OptionalString(root, "scope_id", "");
                    if (scopeText.Length > 0 && !UUID.TryParse(scopeText, out scope))
                    {
                        response.StatusCode = 400;
                        WriteOglError(response, "invalid_scope");
                        return;
                    }
                    LoginResponse login = m_LoginService.Login(first, last, passwd, start,
                        scope, version, channel, mac, id0, request.RemoteIPEndPoint);
                    response.StatusCode = 200;
                    response.RawBuffer = Encoding.UTF8.GetBytes(OSDParser.SerializeJsonString(login.ToOSDMap()));
                }
            }
            catch (JsonException)
            {
                response.StatusCode = 400;
                WriteOglError(response, "invalid_json");
            }
            catch (Exception e)
            {
                m_log.Error("[OGL LOGIN]: Native login failed", e);
                response.StatusCode = 500;
                WriteOglError(response, "internal_error");
            }
        }

        private static bool RequiredString(JsonElement root, string name, out string value)
        {
            value = null;
            if (!root.TryGetProperty(name, out JsonElement item) ||
                item.ValueKind != JsonValueKind.String) return false;
            value = item.GetString();
            return !string.IsNullOrWhiteSpace(value) && value.Length <= 256;
        }

        private static string OptionalString(JsonElement root, string name, string fallback)
        {
            if (!root.TryGetProperty(name, out JsonElement item) ||
                item.ValueKind != JsonValueKind.String) return fallback;
            string value = item.GetString();
            return value.Length <= 256 ? value : fallback;
        }

        private static void WriteOglError(IOSHttpResponse response, string error)
        {
            response.RawBuffer = JsonSerializer.SerializeToUtf8Bytes(new
            {
                schema = "org.opengenesislink.viewer.login.v1",
                error
            });
        }

        private void InitializeHandlers(IHttpServer server)
        {
            LLLoginHandlers loginHandlers = new LLLoginHandlers(m_LoginService, m_Proxy);
//            server.AddXmlRPCHandler("login_to_simulator",
//                new XmlRpcBasicDOSProtector(loginHandlers.HandleXMLRPCLogin, loginHandlers.HandleXMLRPCLoginBlocked,
//                    m_DosProtectionOptions).Process, false);
            server.AddXmlRPCHandler("login_to_simulator",loginHandlers.HandleXMLRPCLogin, false);
            server.AddXmlRPCHandler("set_login_level", loginHandlers.HandleXMLRPCSetLoginLevel, false);
            server.SetDefaultLLSDHandler(loginHandlers.HandleLLSDLogin);
            // Separate, fail-closed native OGL protocol route. It is not advertised
            // as available until TLS, rate limiting and session tests are complete.
            server.AddSimpleStreamHandler(new SimpleStreamHandler("/ogl/v1/login", HandleOglLogin));
            //server.AddWebSocketHandler("/WebSocket/GridLogin", loginHandlers.HandleWebSocketLoginEvents);
        }
    }
}

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
using System.Threading;
using System.Reflection;
using System.Net;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using log4net;
using Nini.Config;

namespace OpenSim.Server.Base
{
    public class HttpServerBase : ServicesServerBase
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private uint m_consolePort;

        // Handle all the automagical stuff
        //
        public HttpServerBase(string prompt, string[] args) : base(prompt, args)
        {
        }

        protected override void ReadConfig()
        {
            IConfig networkConfig = Config.Configs["Network"];

            if (networkConfig == null)
            {
                System.Console.WriteLine("ERROR: Section [Network] not found, server can't start");
                Environment.Exit(1);
            }

            uint port = (uint)networkConfig.GetInt("port", 0);

            if (port == 0)
            {
                System.Console.WriteLine("ERROR: No 'port' entry found in [Network].  Server can't start");
                Environment.Exit(1);
            }

            bool ssl_main = networkConfig.GetBoolean("https_main",false);
            bool ssl_listener = networkConfig.GetBoolean("https_listener",false);
            bool ssl_external = networkConfig.GetBoolean("https_external",false);

            m_consolePort = (uint)networkConfig.GetInt("ConsolePort", 0);

            BaseHttpServer httpServer = null;

            //
            // This is where to make the servers:
            //
            //
            // Make the base server according to the port, etc.
            // ADD: Possibility to make main server ssl
            // Then, check for https settings and ADD a server to
            // m_Servers
            //
            if (!ssl_main)
            {
                httpServer = new BaseHttpServer(port);
            }
            else
            {
                string cert_path = networkConfig.GetString("cert_path", string.Empty);
                if (cert_path.Length == 0)
                {
                    System.Console.WriteLine("ERROR: Path to X509 certificate is missing, server can't start.");
                    Environment.Exit(1);
                }

                string cert_pass = networkConfig.GetString("cert_pass", string.Empty);
                if (cert_pass.Length == 0)
                {
                    System.Console.WriteLine("ERROR: Password for X509 certificate is missing, server can't start.");
                    Environment.Exit(1);
                }

                httpServer = new BaseHttpServer(port, ssl_main, cert_path, cert_pass);
            }

            MainServer.AddHttpServer(httpServer);
            MainServer.Instance = httpServer;

            // If https_listener = true, then add an ssl listener on the https_port...
            if (ssl_listener == true)
            {
                uint https_port = (uint)networkConfig.GetInt("https_port", 0);

                m_log.WarnFormat("[SSL]: External flag is {0}", ssl_external);
                if (!ssl_external)
                {
                    string cert_path = networkConfig.GetString("cert_path", string.Empty);
                    if ( cert_path.Length == 0 )
                    {
                        System.Console.WriteLine("Path to X509 certificate is missing, server can't start.");
                        //Thread.CurrentThread.Abort();
                    }
                    string cert_pass = networkConfig.GetString("cert_pass", string.Empty);
                    if ( cert_pass.Length == 0 )
                    {
                        System.Console.WriteLine("Password for X509 certificate is missing, server can't start.");
                        //Thread.CurrentThread.Abort();
                    }

                    MainServer.AddHttpServer(new BaseHttpServer(https_port, ssl_listener, cert_path, cert_pass));
                }
                else
                {
                    m_log.WarnFormat("[SSL]: SSL port is active but no SSL is used because external SSL was requested.");
                    MainServer.AddHttpServer(new BaseHttpServer(https_port));
                }
            }
        }

        private void ConfigureOglAdminTls()
        {
            IConfig admin = Config.Configs["OGLAssetServiceAdmin"];
            if (admin == null || !admin.GetBoolean("Enabled", false))
                return;
            int requested = admin.GetInt("ExpectedTlsPort", 0);
            if (requested <= 0 || requested > 65535)
                throw new InvalidOperationException("Invalid OGL admin TLS port.");
            if (MainServer.Servers.ContainsKey((uint)requested))
                throw new InvalidOperationException("OGL admin port already allocated.");
            string cert = admin.GetString("TlsCertificatePath", "");
            string secret = admin.GetString("TlsCertificatePassword", "");
            if (String.IsNullOrWhiteSpace(cert) || String.IsNullOrEmpty(secret))
                throw new InvalidOperationException("OGL admin TLS certificate not configured.");
            string bind = admin.GetString("ListenAddress", "127.0.0.1");
            if (!IPAddress.TryParse(bind, out IPAddress ip) ||
                ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any))
                throw new InvalidOperationException("OGL admin cannot bind to a wildcard address.");
            BaseHttpServer listener = new BaseHttpServer((uint)requested, true, cert, secret);
            listener.ListenIPAddress = ip;
            MainServer.AddHttpServer(listener);
            ConfigureOglAdminTls();
        }

        protected override void Initialise()
        {
            foreach (BaseHttpServer s in MainServer.Servers.Values)
                s.Start();

            MainServer.RegisterHttpConsoleCommands(MainConsole.Instance);

            MethodInfo mi = m_console.GetType().GetMethod("SetServer", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(BaseHttpServer) }, null);

            if (mi != null)
            {
                if (m_consolePort == 0)
                    mi.Invoke(MainConsole.Instance, new object[] { MainServer.Instance });
                else
                    mi.Invoke(MainConsole.Instance, new object[] { MainServer.GetHttpServer(m_consolePort) });
            }
        }
    }
}

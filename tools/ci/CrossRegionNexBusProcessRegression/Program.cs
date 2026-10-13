// SPDX-License-Identifier: MPL-2.0
// Two separate OS processes: real NodeAgent HTTP/HMAC transport and llListen.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Nini.Config;
using NexVerse.Core.Messaging;
using NexVerse.RegionModules.NodeAgent;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.CoreModules.Avatar.Chat;
using OpenSim.Region.CoreModules.Scripting.WorldComm;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Tests.Common;

internal static class Program
{
    private static readonly string Secret = new string('S', 64);
    private const string EventName = "chat.neighbor.requested";
    private static int m_Assertions;

    private static void Require(bool valid, string reason)
    {
        ++m_Assertions;
        if (!valid) throw new InvalidOperationException(reason);
    }

    private static (TestScene scene, SceneHelpers scenes, ChatModule chat,
        WorldCommModule world, NexVerseNodeAgentModule agent) CreateNode(
            bool sender, string peerUrl)
    {
        SceneHelpers scenes = new SceneHelpers();
        TestScene scene = scenes.SetupScene(sender ? "NexBus East" : "NexBus West",
            UUID.Random(), sender ? 1001u : 1000u, 1010u);
        IniConfigSource settings = new IniConfigSource();
        settings.AddConfig("Chat");
        settings.AddConfig("LL-Functions");
        var cfg = settings.AddConfig("NexVerseNodeAgent");
        cfg.Set("Enabled", "true");
        // Deliberately identical NodeIds: two simulators on one host may
        // use the same machine-name default. Source REGION IDs prevent loops.
        cfg.Set("NodeId", "test-shared-host");
        cfg.Set("PeerUrl", peerUrl);
        cfg.Set("SharedKey", Secret);
        cfg.Set("CrossRegionChatMessaging", "true");
        cfg.Set("EstateScriptMessaging", "false");
        cfg.Set("ObjectMessagesPerSecond", "100");
        cfg.Set("RequestTimeoutMilliseconds", "5000");

        ChatModule chat = new ChatModule();
        WorldCommModule world = new WorldCommModule();
        SceneHelpers.SetupSceneModules(scene, settings, chat);
        SceneHelpers.SetupSceneModules(scene, settings, world);

        NexVerseNodeAgentModule agent = new NexVerseNodeAgentModule();
        agent.Initialise(settings);
        agent.AddRegion(scene);
        // Test-only subscription without starting MainServer/Robust control APIs.
        // Use real production callback and production DistributedNexEventBus.
        MethodInfo callback = typeof(NexVerseNodeAgentModule).GetMethod(
            "HandleNeighbourAvatarChat", BindingFlags.NonPublic | BindingFlags.Instance);
        Require(callback != null, "production node receiver callback missing");
        var bus = (DistributedNexEventBus)typeof(NexVerseNodeAgentModule)
            .GetField("m_Bus", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(agent);
        Require(bus != null, "node's signed event transport unavailable");
        bus.Subscribe(EventName,
            (Action<NexEvent>)Delegate.CreateDelegate(typeof(Action<NexEvent>), agent, callback));
        return (scene, scenes, chat, world, agent);
    }

    private static int AvailablePort()
    {
        using TcpListener port = new TcpListener(IPAddress.Loopback, 0);
        port.Start();
        return ((IPEndPoint)port.LocalEndpoint).Port;
    }

    private static Process StartChild(string role, string[] arguments, string output)
    {
        string assembly = Assembly.GetExecutingAssembly().Location;
        var info = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        info.ArgumentList.Add(assembly);
        info.ArgumentList.Add(role);
        foreach (string arg in arguments)
            info.ArgumentList.Add(arg);
        Process p = Process.Start(info) ?? throw new InvalidOperationException("Child did not start");
        _ = Task.Run(async () => File.AppendAllText(output, await p.StandardOutput.ReadToEndAsync()));
        _ = Task.Run(async () => File.AppendAllText(output, await p.StandardError.ReadToEndAsync()));
        return p;
    }

    private static async Task WaitForFile(string path, Process proc, int ms)
    {
        var clock = Stopwatch.StartNew();
        while (!File.Exists(path) && clock.ElapsedMilliseconds < ms)
        {
            if (proc.HasExited) throw new Exception("Child exited early: " + proc.ExitCode);
            await Task.Delay(50);
        }
        Require(File.Exists(path), "Receiver did not start listening");
    }

    private static async Task Parent()
    {
        int port = AvailablePort();
        string directory = Path.Combine(Path.GetTempPath(),
            "ogl-nexbus-two-process-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string ready = Path.Combine(directory, "ready");
        string result = Path.Combine(directory, "result.txt");
        string receiverLog = Path.Combine(directory, "receiver.log");
        string senderLog = Path.Combine(directory, "sender.log");
        Process receiver = null;
        Process sender = null;
        try
        {
            receiver = StartChild("receiver",
                new[] { port.ToString(), ready, result }, receiverLog);
            await WaitForFile(ready, receiver, 20000);

            sender = StartChild("sender",
                new[] { "http://127.0.0.1:" + port + "/events" }, senderLog);
            Require(sender.WaitForExit(20000), "sender process timed out");
            Require(sender.ExitCode == 0, "sender process failed");

            var clock = Stopwatch.StartNew();
            while (!File.Exists(result) && clock.ElapsedMilliseconds < 15000)
                await Task.Delay(50);
            Require(File.Exists(result), "receiver did not report success");
            Require(receiver.WaitForExit(5000), "receiver did not terminate");
            Require(receiver.ExitCode == 0, "receiver process failed");
            string actual = File.ReadAllText(result).Trim();
            Require(actual == "PASS", "two-process listener verification failed: " + actual);

            Console.WriteLine("Cross-region NexBus signed HTTP + real WorldComm listeners: PASS");
            Console.WriteLine("Two independent OS PIDs: " + sender.Id + " -> " + receiver.Id);
        }
        catch
        {
            foreach (string log in new[] { receiverLog, senderLog })
                if (File.Exists(log))
                    Console.Error.WriteLine(Path.GetFileName(log) + ":\n" +
                        File.ReadAllText(log).Split('\n').TakeLast(30).Aggregate("", (a,b) => a + b + "\n"));
            throw;
        }
        finally
        {
            if (sender != null && !sender.HasExited) sender.Kill(true);
            if (receiver != null && !receiver.HasExited) receiver.Kill(true);
            sender?.Dispose();
            receiver?.Dispose();
            try { Directory.Delete(directory, true); } catch { }
        }
    }

    private static async Task<int> Receive(int port, string ready, string result)
    {
        var source = CreateNode(false, "http://127.0.0.1:1/no-loop");
        var scene = source.scene;
        SceneObjectGroup a = SceneHelpers.AddSceneObject(scene, "Cube A", UUID.Random());
        SceneObjectGroup b = SceneHelpers.AddSceneObject(scene, "Cube B", UUID.Random());
        a.AbsolutePosition = new Vector3(253, 128, 25);
        b.AbsolutePosition = new Vector3(244, 128, 25);
        UUID scriptA = UUID.Random(), scriptB = UUID.Random();
        Require(source.world.Listen(scriptA, a.RootPart.UUID, 0, "", UUID.Zero, "Beep") > 0,
            "public receiver A listen");
        Require(source.world.Listen(scriptB, b.RootPart.UUID, 0, "", UUID.Zero, "Beep") > 0,
            "public receiver B listen");
        Require(source.world.Listen(scriptA, a.RootPart.UUID, 7, "", UUID.Zero, "Beep") > 0,
            "private receiver A listen");
        Require(source.world.Listen(scriptB, b.RootPart.UUID, 7, "", UUID.Zero, "Beep") > 0,
            "private receiver B listen");

        ConcurrentDictionary<string, int> hits = new();
        scene.EventManager.OnScriptListenEvent += (script, channel, name, id, message) =>
        {
            string key = script + ":" + channel;
            hits.AddOrUpdate(key, 1, (_, count) => count + 1);
        };
        int Hit(UUID script, int channel) =>
            hits.TryGetValue(script + ":" + channel, out int count) ? count : 0;

        var bus = (DistributedNexEventBus)typeof(NexVerseNodeAgentModule)
            .GetField("m_Bus", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(source.agent);

        using HttpListener listener = new HttpListener();
        listener.Prefixes.Add("http://127.0.0.1:" + port + "/");
        listener.Start();
        File.WriteAllText(ready, "ready");
        var firstPayload = Array.Empty<byte>();
        int accepted = 0, rejected = 0, duplicates = 0;
        var clock = Stopwatch.StartNew();
        Task<HttpListenerContext> pending = listener.GetContextAsync();
        while (clock.Elapsed < TimeSpan.FromSeconds(15))
        {
            Task finished = await Task.WhenAny(pending, Task.Delay(1000));
            if (finished != pending)
                continue;

            HttpListenerContext context = await pending;
            byte[] payload;
            using (var buffer = new MemoryStream())
            {
                await context.Request.InputStream.CopyToAsync(buffer);
                payload = buffer.ToArray();
            }
            bool signed = NexBusProtocol.Verify(payload, Secret,
                context.Request.Headers["X-NexBus-Signature"]);
            if (!signed)
            {
                rejected++;
                context.Response.StatusCode = 401;
            }
            else
            {
                NexEvent ev = NexBusProtocol.Deserialize(payload);
                bool first = bus.Receive(ev);
                if (first)
                {
                    accepted++;
                    if (firstPayload.Length == 0 && ev.Name == EventName)
                        firstPayload = payload;
                }
                else duplicates++;
                context.Response.StatusCode = 202;
            }
            context.Response.Close();

            if (accepted >= 3)
                break;
            pending = listener.GetContextAsync();
        }

        Require(accepted >= 3, "expected 3 chat events: say, private and whisper; got " + accepted);
        Require(Hit(scriptA, 0) == 2, "A should hear say and whisper");
        Require(Hit(scriptB, 0) == 1, "B should hear say but not whisper");
        Require(Hit(scriptA, 7) == 1 && Hit(scriptB, 7) == 1, "both hear private exactly once");

        // Replay and invalid-signature requests use the same real HTTP endpoint.
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        string address = "http://127.0.0.1:" + port + "/events";
        async Task<(int status, bool delivered)> Inject(byte[] data, string key)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, address);
            request.Headers.Add("X-NexBus-Signature",
                "sha256=" + NexBusProtocol.Sign(data, key));
            request.Content = new ByteArrayContent(data);
            Task<HttpResponseMessage> sendTask = client.SendAsync(request);
            Task<HttpListenerContext> pending = listener.GetContextAsync();
            HttpListenerContext context = await pending;
            byte[] bytes;
            using (var buffer = new MemoryStream())
            {
                await context.Request.InputStream.CopyToAsync(buffer);
                bytes = buffer.ToArray();
            }
            bool valid = NexBusProtocol.Verify(bytes, Secret,
                context.Request.Headers["X-NexBus-Signature"]);
            bool delivered = valid && bus.Receive(NexBusProtocol.Deserialize(bytes));
            if (valid && !delivered) duplicates++;
            if (!valid) rejected++;
            context.Response.StatusCode = valid ? 202 : 401;
            context.Response.Close();
            using HttpResponseMessage response = await sendTask;
            return ((int)response.StatusCode, delivered);
        }

        var repeated = await Inject(firstPayload, Secret);
        Require(repeated.status == 202 && !repeated.delivered, "same EventId must deduplicate");
        var invalid = await Inject(firstPayload, new string('X', 64));
        Require(invalid.status == 401 && !invalid.delivered, "wrong HMAC must reject");
        Require(duplicates >= 1 && rejected >= 1, "negative transport assertions");
        Require(Hit(scriptA, 0) == 2 && Hit(scriptB, 0) == 1 &&
                Hit(scriptA, 7) == 1 && Hit(scriptB, 7) == 1,
            "negative requests never create duplicate listens");

        File.WriteAllText(result, "PASS");
        source.agent.Close();
        return 0;
    }

    private static async Task<int> Send(string peerUrl)
    {
        var source = CreateNode(true, peerUrl);
        ScenePresence speaker = SceneHelpers.AddScenePresence(source.scene, UUID.Random());
        speaker.Flying = true;
        speaker.AbsolutePosition = new Vector3(3, 128, 25);
        var viewer = (TestClient)speaker.ControllingClient;
        var bus = (DistributedNexEventBus)typeof(NexVerseNodeAgentModule)
            .GetField("m_Bus", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(source.agent);
        int publishedChat = 0;
        using IDisposable emitted = bus.Subscribe(
            EventName, _ => Interlocked.Increment(ref publishedChat));

        Require(viewer.Chat(0, ChatTypeEnum.Say, "Beep"), "remote public say");
        await Task.Delay(100);
        Require(viewer.Chat(7, ChatTypeEnum.Say, "Beep"), "remote private say");
        await Task.Delay(100);
        Require(viewer.Chat(0, ChatTypeEnum.Whisper, "Beep"), "remote whisper");
        await Task.Delay(800);

        // Boundary filter: this is >20m from the target region.
        speaker.AbsolutePosition = new Vector3(30, 128, 25);
        viewer.Chat(0, ChatTypeEnum.Say, "Beep");
        await Task.Delay(350);
        Require(Volatile.Read(ref publishedChat) == 3,
            "out-of-range say must not generate a fourth NexBus event");
        source.agent.Close();
        return 0;
    }

    private static async Task<int> Main(string[] args)
    {
        Environment.CurrentDirectory = AppContext.BaseDirectory;
        try
        {
            if (args.Length == 0) await Parent();
            else if (args[0] == "sender") return await Send(args[1]);
            else if (args[0] == "receiver") return await Receive(
                int.Parse(args[1]), args[2], args[3]);
            else throw new ArgumentException("Unknown role");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error.ToString());
            return 1;
        }
    }
}

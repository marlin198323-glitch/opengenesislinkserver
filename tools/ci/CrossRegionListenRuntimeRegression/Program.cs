using System;
using System.Collections.Generic;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.CoreModules.Avatar.Chat;
using OpenSim.Region.CoreModules.Scripting.WorldComm;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Tests.Common;

internal static class Program
{
    private static int m_checks;
    private static void Check(bool ok, string name)
    {
        ++m_checks;
        if (!ok) throw new InvalidOperationException("Cross-region llListen: " + name);
    }

    public static int Main()
    {
        // SceneHelpers loads local OpenSim test data plugins by DLL name.
        Environment.CurrentDirectory = AppContext.BaseDirectory;
        try
        {
            Run();
            Console.WriteLine("Cross-region llListen (real ChatModule + WorldCommModule): PASS (" +
                m_checks + " checks)");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("Cross-region llListen runtime: FAIL\n" + error);
            return 1;
        }
    }

    private static void Run()
    {
        SceneHelpers setup = new SceneHelpers();
        TestScene west = setup.SetupScene("Chat West", UUID.Random(), 1000, 1010);
        TestScene east = setup.SetupScene("Chat East", UUID.Random(), 1001, 1010);
        IniConfigSource config = new IniConfigSource();
        config.AddConfig("Chat");
        config.AddConfig("LL-Functions");

        ChatModule chat = new ChatModule();
        WorldCommModule receiver = new WorldCommModule();
        WorldCommModule local = new WorldCommModule();
        SceneHelpers.SetupSceneModules(new Scene[] { west, east }, config, chat);
        SceneHelpers.SetupSceneModules(west, config, receiver);
        SceneHelpers.SetupSceneModules(east, config, local);

        UUID avatarId = UUID.Random();
        ScenePresence speaker = SceneHelpers.AddScenePresence(east, avatarId);
        speaker.Flying = true;
        speaker.AbsolutePosition = new Vector3(3, 128, 25);
        TestClient client = (TestClient)speaker.ControllingClient;

        SceneObjectGroup cubeA = SceneHelpers.AddSceneObject(west, "cube A", UUID.Random());
        SceneObjectGroup cubeB = SceneHelpers.AddSceneObject(west, "cube B", UUID.Random());
        cubeA.AbsolutePosition = new Vector3(253, 128, 25);
        cubeB.AbsolutePosition = new Vector3(244, 128, 25);

        UUID scriptA = UUID.Random();
        UUID scriptB = UUID.Random();
        Check(receiver.Listen(scriptA, cubeA.RootPart.UUID, 0, "", UUID.Zero, "Beep") > 0,
            "cube A public listen registration");
        Check(receiver.Listen(scriptB, cubeB.RootPart.UUID, 0, "", UUID.Zero, "Beep") > 0,
            "cube B public listen registration");
        Check(receiver.Listen(scriptA, cubeA.RootPart.UUID, 7, "", UUID.Zero, "Beep") > 0,
            "cube A private listen registration");
        Check(receiver.Listen(scriptB, cubeB.RootPart.UUID, 7, "", UUID.Zero, "Beep") > 0,
            "cube B private listen registration");

        Dictionary<string, int> hits = new Dictionary<string, int>();
        west.EventManager.OnScriptListenEvent +=
            (id, channel, name, source, message) =>
            {
                string key = id + ":" + channel;
                hits[key] = hits.TryGetValue(key, out int previous) ? previous + 1 : 1;
                Check(source == avatarId, "sender identity preserved");
                Check(message == "Beep", "message payload preserved");
            };

        int GetHits(UUID script, int channel) =>
            hits.TryGetValue(script + ":" + channel, out int count) ? count : 0;

        Check(client.Chat(0, ChatTypeEnum.Say, "Beep"), "border chat dispatch");
        Check(GetHits(scriptA, 0) == 1 && GetHits(scriptB, 0) == 1,
            "both cubes heard border say exactly once");

        Check(client.Chat(7, ChatTypeEnum.Say, "Beep"), "private border chat dispatch");
        Check(GetHits(scriptA, 7) == 1 && GetHits(scriptB, 7) == 1,
            "both cubes heard private channel exactly once");

        client.Chat(0, ChatTypeEnum.Say, "NotBeep");
        Check(GetHits(scriptA, 0) == 1 && GetHits(scriptB, 0) == 1,
            "message filter still applied");

        speaker.AbsolutePosition = new Vector3(30, 128, 25);
        client.Chat(0, ChatTypeEnum.Say, "Beep");
        client.Chat(7, ChatTypeEnum.Say, "Beep");
        Check(GetHits(scriptA, 0) == 1 && GetHits(scriptB, 0) == 1 &&
              GetHits(scriptA, 7) == 1 && GetHits(scriptB, 7) == 1,
            "beyond 20m: no listen delivery");

        speaker.AbsolutePosition = new Vector3(3, 128, 25);
        client.Chat(0, ChatTypeEnum.Whisper, "Beep");
        Check(GetHits(scriptA, 0) == 2 && GetHits(scriptB, 0) == 1,
            "10m whisper correctly reaches near cube only");

        client.Chat(0, ChatTypeEnum.Region, "Beep");
        Check(GetHits(scriptA, 0) == 2 && GetHits(scriptB, 0) == 1,
            "regionwide chat not relayed to neighbour listeners");

        receiver.RemoveRegion(west);
        client.Chat(0, ChatTypeEnum.Say, "Beep");
        Check(GetHits(scriptA, 0) == 2 && GetHits(scriptB, 0) == 1,
            "region removal detaches script listener delivery");
        local.RemoveRegion(east);
        chat.RemoveRegion(west);
        chat.RemoveRegion(east);
    }
}

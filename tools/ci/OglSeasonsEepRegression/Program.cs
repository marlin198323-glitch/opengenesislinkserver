// SPDX-License-Identifier: MPL-2.0
using System;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenGenesisLINK.Addons.OGLSeasons;

internal static class Program
{
    private static void Require(bool condition, string what)
    {
        if (!condition) throw new Exception("EEP regression: " + what);
    }

    private static OSDMap GetFrames(ViewerEnvironment v)
    {
        var root = (OSDMap)v.ToOSD();
        return (OSDMap)((OSDMap)root["day_cycle"])["frames"];
    }

    private static void VerifyAddinRegistrationMetadata()
    {
        // Verify compiled attributes, not only the source code text: Mono.Addins
        // resolves extension points using the declaring root's add-in identity.
        bool foundSeasonsAddin = false;
        bool foundFrameworkDependency = false;
        foreach (var attribute in typeof(OglSeasonsRegionModule).Assembly.GetCustomAttributesData())
        {
            if (attribute.AttributeType.FullName == "Mono.Addins.AddinAttribute" &&
                attribute.ConstructorArguments.Count > 0 &&
                (string)attribute.ConstructorArguments[0].Value == "OGLSeasons")
            {
                foreach (var named in attribute.NamedArguments)
                    if (named.MemberName == "Namespace" &&
                        (string)named.TypedValue.Value == "OpenGenesisLINK")
                        foundSeasonsAddin = true;
            }
            if (attribute.AttributeType.FullName == "Mono.Addins.AddinDependencyAttribute" &&
                attribute.ConstructorArguments.Count > 0 &&
                (string)attribute.ConstructorArguments[0].Value == "OpenSim.Region.Framework")
                foundFrameworkDependency = true;
        }
        Require(foundSeasonsAddin, "canonical OpenGenesisLINK.OGLSeasons add-in identity");
        Require(foundFrameworkDependency, "dependency on /OpenSim/RegionModules provider");

        bool foundRoot = false;
        foreach (var attribute in typeof(OpenSim.Region.Framework.Interfaces.IRegionModuleBase)
                     .Assembly.GetCustomAttributesData())
            if (attribute.AttributeType.FullName == "Mono.Addins.AddinRootAttribute" &&
                attribute.ConstructorArguments.Count > 0 &&
                (string)attribute.ConstructorArguments[0].Value == "OpenSim.Region.Framework")
                foundRoot = true;
        Require(foundRoot, "region framework root add-in exists");
    }

    public static void Main()
    {
        VerifyAddinRegistrationMetadata();
        // This fixture exercises the genuine OpenSim EEP serializer, not
        // a mock or a file-based substitute. FromLightShare provides a
        // valid sky+water daycycle with deterministic test data.
        var env = new ViewerEnvironment();
        env.FromLightShare(new RegionLightShareData());
        string original = ViewerEnvironment.ToOSDString(env);
        var first = SeasonWeatherModel.Calculate(
            new DateTimeOffset(2026, 12, 15, 12, 0, 0, TimeSpan.Zero),
            Guid.Parse("46d88f05-fb22-4a13-bf8d-1f6842182739"));

        ViewerEnvironment overlay = env.Clone();
        Require(OglSeasonalEepOverlay.TransformSkyFrames(overlay, first),
            "valid sky frames transformed");
        Require(ViewerEnvironment.ToOSDString(env) == original,
            "source must remain unchanged");
        Require(env.DayLength == overlay.DayLength && env.DayOffset == overlay.DayOffset,
            "day cycle timing preserved");
        var sourceFrames = GetFrames(env);
        var resultFrames = GetFrames(overlay);
        Require(sourceFrames.Count == resultFrames.Count, "frame count preserved");
        int skyCount = 0;
        foreach (System.Collections.Generic.KeyValuePair<string, OSD> frame in sourceFrames)
        {
            var before = (OSDMap)frame.Value;
            var after = (OSDMap)resultFrames[frame.Key];
            if (before["type"].AsString() == "water")
            {
                Require(OSDParser.SerializeLLSDXmlString(before) ==
                    OSDParser.SerializeLLSDXmlString(after),
                    "water settings must not change");
                continue;
            }
            if (before["type"].AsString() != "sky") continue;
            skyCount++;
            Require(before["sun_rotation"].AsString() == after["sun_rotation"].AsString(),
                "sun position not overwritten");
            Require(before["cloud_shadow"].AsReal() != after["cloud_shadow"].AsReal(),
                "weather cloud cover must change");
        }
        Require(skyCount > 0, "sky asset frames exercised");
        Require(!OglSeasonalEepOverlay.TransformSkyFrames(null, first),
            "null environment rejected");
        Require(!OglSeasonalEepOverlay.TransformSkyFrames(overlay, null),
            "null weather rejected");
        Console.WriteLine("OGL Seasons EEP: cloud/fog overlay, preserved daylight/water and input isolation: OK");
    }
}
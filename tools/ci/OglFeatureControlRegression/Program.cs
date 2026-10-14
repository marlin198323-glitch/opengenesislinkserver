// SPDX-License-Identifier: MPL-2.0
using System;
using System.Linq;
using System.Security.Cryptography;
using OpenSim.Framework;
using OpenSim.Framework.Console;

internal static class Program
{
    private static int checks;
    private static void Assert(bool condition, string reason)
    {
        ++checks;
        if (!condition) throw new Exception(reason);
    }

    public static int Main()
    {
        try
        {
            const string node = "mainland-01";
            using ECDsa issuer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using ECDsa adversary = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            string valid = issuer.ExportPkcs8PrivateKeyPem();
            string publicKey = issuer.ExportSubjectPublicKeyInfoPem();
            string wrong = adversary.ExportPkcs8PrivateKeyPem();
            long expiry = DateTimeOffset.UtcNow.AddSeconds(100).ToUnixTimeSeconds();

            OglArchiveFeatureGate.Configure(false, node, "");
            Assert(OglArchiveFeatureGate.Allowed(OglArchiveFeatureGate.All),
                "Legacy mode must preserve original archive commands");

            Assert(OglArchiveFeatureGate.ResolveNodeId(null, "host-01") == "host-01",
                "missing NodeId must use machine fallback");
            Assert(OglArchiveFeatureGate.ResolveNodeId("  ", "host-01") == "host-01",
                "empty NodeId must use machine fallback");
            Assert(OglArchiveFeatureGate.ResolveNodeId(" mainland-02 ", "host-01") == "mainland-02",
                "explicit NodeId must be trimmed and retained");

            OglArchiveFeatureGate.Configure(true, node, publicKey);
            Assert(OglArchiveFeatureGate.IsForConfiguredNode(node),
                "own-node feature policies must be selected");
            Assert(!OglArchiveFeatureGate.IsForConfiguredNode("foreign-node"),
                "foreign-node broadcasts must be ignored before Apply");
            Assert(!OglArchiveFeatureGate.Allowed(1), "enabled mode requires an explicit policy");
            string signed = OglArchiveFeatureGate.Sign(valid, node, 10, expiry, 1 | 8);
            Assert(!OglArchiveFeatureGate.Apply(node, 10, expiry, 1 | 8,
                OglArchiveFeatureGate.Sign(wrong, node, 10, expiry, 1 | 8)),
                "invalid signing secret should never grant access");
            Assert(!OglArchiveFeatureGate.Apply("foreign-node", 10, expiry, 1 | 8, signed),
                "a policy for another simulator must not work");
            Assert(!OglArchiveFeatureGate.IsForConfiguredNode("foreign-node") &&
                   !OglArchiveFeatureGate.Allowed(1),
                "foreign policy does not grant any rights");
            Assert(!OglArchiveFeatureGate.Apply(node, 10, expiry, 17,
                OglArchiveFeatureGate.Sign(valid, node, 10, expiry, 17)),
                "unsupported permission flag rejected");
            Assert(OglArchiveFeatureGate.Apply(node, 10, expiry, 1 | 8, signed),
                "signed policy accepted");
            Assert(OglArchiveFeatureGate.Allowed(OglArchiveFeatureGate.OarImport),
                "OAR import enabled");
            Assert(!OglArchiveFeatureGate.Allowed(OglArchiveFeatureGate.OarExport),
                "OAR export disabled");
            Assert(!OglArchiveFeatureGate.Allowed(OglArchiveFeatureGate.IarImport),
                "IAR import disabled");
            Assert(OglArchiveFeatureGate.Allowed(OglArchiveFeatureGate.IarExport),
                "IAR export enabled");
            Assert(!OglArchiveFeatureGate.Apply(node, 9, expiry, 15,
                OglArchiveFeatureGate.Sign(valid, node, 9, expiry, 15)),
                "older revision replay rejected");
            Assert(OglArchiveFeatureGate.Allowed(1) &&
                !OglArchiveFeatureGate.Allowed(2), "older revision cannot override permissions");
            long expired = DateTimeOffset.UtcNow.AddSeconds(-1).ToUnixTimeSeconds();
            Assert(!OglArchiveFeatureGate.Apply(node, 11, expired, 15,
                OglArchiveFeatureGate.Sign(valid, node, 11, expired, 15)),
                "expired policy must be refused");
            long tooFar = DateTimeOffset.UtcNow.AddHours(2).ToUnixTimeSeconds();
            Assert(!OglArchiveFeatureGate.Apply(node, 11, tooFar, 15,
                OglArchiveFeatureGate.Sign(valid, node, 11, tooFar, 15)),
                "over-long policy duration must be refused");
            string revoke = OglArchiveFeatureGate.Sign(valid, node, 11, expiry, 0);
            Assert(OglArchiveFeatureGate.Apply(node, 11, expiry, 0, revoke),
                "Revocation policy should apply");
            Assert(!OglArchiveFeatureGate.Allowed(1) &&
                !OglArchiveFeatureGate.Allowed(8),
                "revocation must deny previous capabilities");
            Assert(OglArchiveFeatureGate.DeniedMessage(1).Contains("info@stadt-nexverse.de"),
                "German support address missing");
            bool threw = false;
            try { OglArchiveFeatureGate.Require(1); }
            catch (InvalidOperationException) { threw = true; }
            Assert(threw, "direct executable guard must throw");

            Commands help = new Commands { OglGermanHelp = true, OglExpertHelp = false };
            help.AddCommand("Archiving", false, "load oar", "load oar <file>",
                "Load archive", (module, arguments) => { });
            help.AddCommand("Debug", false, "debug foo", "debug foo",
                "Debug command", (module, arguments) => { });
            string summary = string.Join("\n", help.GetHelp(new[] { "help" }));
            Assert(summary.Contains("SIMULATOR-KONSOLE") && summary.Contains("Archiv"),
                "German OGL console summary");
            Assert(!summary.Contains("Diagnose und Fehleranalyse"),
                "expert-only section should be hidden");
            string all = string.Join("\n", help.GetHelp(new[] { "help", "all" }));
            Assert(all.Contains("Regionsarchiv importieren") && !all.Contains("debug foo"),
                "all help only shows localized standard commands");
            help.OglExpertHelp = true;
            Assert(string.Join("\n", help.GetHelp(new[] { "help", "all" }))
                .Contains("debug foo"), "expert mode should reveal hidden topic");
            OglArchiveFeatureGate.Configure(false, "", "");
            Console.WriteLine("OGL Feature Control + german console: PASS (" + checks + " checks)");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("OGL Feature Control FAIL: " + error);
            return 1;
        }
    }
}

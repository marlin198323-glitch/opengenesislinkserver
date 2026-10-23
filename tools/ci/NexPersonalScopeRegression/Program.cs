using System;
using System.Collections.Generic;
using System.Linq;
using NexVerse.Core.Security;

// Issue #289 regression: native personal World token scopes vs admin wildcard,
// independently scoped service credentials, targeted grants and re-login.
internal static class Program
{
    private static void Check(bool success, string message)
    {
        if (!success) throw new InvalidOperationException(message);
    }

    private static int Main()
    {
        const int adminLevel = 200;
        const string adminId = "11111111-2222-3333-4444-555555555555";
        const string residentId = "11111111-2222-3333-4444-666666666666";
        var signer = new HmacNexAccessTokenService(
            "https://world.stadt-nexverse.de", "nexverse-world-api",
            new string('N', 64), 600);

        string[] before = NexAuthorizationPolicy.GetEffectiveScopes(
            adminLevel, adminLevel, new[] { NexRoles.Administrator }, Array.Empty<string>());
        Check(before.Contains(NexScopes.AdminAll), "Admin must retain admin:*");
        Check(!before.Contains(NexScopes.StatisticsRead),
            "Admin wildcard must not fabricate a separately issued statistics:read");
        string oldToken = signer.Issue(adminId, before, 100);
        Check(signer.TryValidate(oldToken, out NexAccessTokenClaims oldClaims) &&
            !oldClaims.Scopes.Contains(NexScopes.StatisticsRead),
            "Old bearer unexpectedly contains a newly assigned read scope");

        var requested = new[] {
            NexScopes.StatisticsRead, NexScopes.RegionsRead,
            NexScopes.SimulatorsRead, NexScopes.EstatesRead
        };
        Check(NexAuthorizationPolicy.TryNormalizeAssignment(
            new[] { NexRoles.Administrator }, requested, true,
            out string[] roles, out string[] explicitScopes, out string error),
            "Administrator-approved read grants must be valid: " + error);
        string[] after = NexAuthorizationPolicy.GetEffectiveScopes(
            adminLevel, adminLevel, roles, explicitScopes);
        Check(requested.All(after.Contains), "World effective token lacks an explicitly assigned read grant");
        string newToken = signer.Issue(adminId, after, 101);
        Check(signer.TryValidate(newToken, out NexAccessTokenClaims refreshed) &&
            refreshed.Scopes.Contains(NexScopes.StatisticsRead) &&
            refreshed.Scopes.Contains(NexScopes.AdminAll),
            "Re-login must produce a bearer containing both explicit and admin scopes");
        Check(oldClaims.SecurityStamp != refreshed.SecurityStamp,
            "Old bearer must remain stamped with its old authorization version");
        Check(!oldClaims.Scopes.Contains(NexScopes.StatisticsRead),
            "Existing access token gained rights without re-issuance");

        Check(NexAuthorizationPolicy.TryNormalizeAssignment(
            new[] { NexRoles.Resident }, new[] { NexScopes.EconomyRead }, false,
            out string[] residentRoles, out string[] residentExplicit, out error),
            "Resident own economy read should be selectively assignable: " + error);
        string[] residentScopes = NexAuthorizationPolicy.GetEffectiveScopes(
            0, adminLevel, residentRoles, residentExplicit);
        Check(residentScopes.Contains(NexScopes.EconomyRead) &&
            !residentScopes.Contains(NexScopes.AdminAll) &&
            !residentScopes.Contains(NexScopes.EconomyTransfer),
            "Personal economy read must not grant administrator or transfer powers");
        string residentToken = signer.Issue(residentId, residentScopes, 50);
        Check(signer.TryValidate(residentToken, out NexAccessTokenClaims citizen) &&
            citizen.Scopes.Contains(NexScopes.EconomyRead) &&
            !citizen.Scopes.Contains(NexScopes.StatisticsRead),
            "Citizen bearer has unassigned administrator/statistics privileges");

        Check(!NexAuthorizationPolicy.TryNormalizeAssignment(
            new[] { NexRoles.Resident }, new[] { NexScopes.AdminAll }, false,
            out _, out _, out error),
            "Resident should not be able to self-grant admin:*");
        Check(!NexAuthorizationPolicy.TryNormalizeAssignment(
            new[] { NexRoles.Resident }, new[] { NexScopes.SecurityManage }, false,
            out _, out _, out error),
            "Resident should not self-grant security:manage");

        NexPrincipal admin = new NexPrincipal(adminId, refreshed.Scopes);
        NexPrincipal citizenPrincipal = new NexPrincipal(residentId, citizen.Scopes);
        NexPrincipal machine = new NexPrincipal("api-key:service-key",
            new[] { NexScopes.StatisticsRead });
        Check(admin.HasScope(NexScopes.StatisticsRead), "Admin read scope denied");
        Check(citizenPrincipal.HasScope(NexScopes.EconomyRead) &&
            !citizenPrincipal.HasScope(NexScopes.SecurityManage),
            "Citizen ownership/scope guard regression");
        Check(machine.HasScope(NexScopes.StatisticsRead) &&
            !machine.HasScope(NexScopes.AdminAll) &&
            !machine.HasScope(NexScopes.EconomyRead),
            "Service key grants must not become personal bearer grants");

        Console.WriteLine("Personal World-API scopes: role, targeted grant, native token re-login, revocation boundary and service isolation PASS");
        return 0;
    }
}

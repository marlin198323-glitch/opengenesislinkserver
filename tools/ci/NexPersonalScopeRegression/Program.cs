using System;
using System.Linq;
using NexVerse.Core.Security;

// Native World bearer tests: automatic per-role baseline, legacy UserLevel,
// explicit overrides, least privilege and the fact that existing JWTs do not
// magically gain newly configured role-derived grants until their next issue.
internal static class Program
{
    private static void Check(bool yes, string message)
    {
        if (!yes) throw new InvalidOperationException(message);
    }

    private static void HasAll(string[] actual, params string[] expected)
    {
        Check(expected.All(actual.Contains),
            "Missing automatically issued grants: " +
            string.Join(",", expected.Where(s => !actual.Contains(s))));
    }

    private static void HasNone(string[] actual, params string[] forbidden)
    {
        Check(!forbidden.Any(actual.Contains),
            "Excessive automatic grants: " +
            string.Join(",", forbidden.Where(actual.Contains)));
    }

    private static int Main()
    {
        const int adminMinimum = 200;
        const string adminId = "11111111-2222-3333-4444-555555555555";
        const string citizenId = "11111111-2222-3333-4444-666666666666";
        var issuer = new HmacNexAccessTokenService(
            "https://world.stadt-nexverse.de", "nexverse-world-api",
            new string('N', 64), 600);

        string[] baseResident = NexAuthorizationPolicy.GetEffectiveScopes(
            0, adminMinimum, new[] { NexRoles.Resident }, Array.Empty<string>());
        HasAll(baseResident, NexScopes.UsersRead, NexScopes.ProfileRead,
            NexScopes.InventoryRead, NexScopes.EconomyRead,
            NexScopes.RegionsRead, NexScopes.GroupsRead,
            NexScopes.ExperiencesRead, NexScopes.DiscoveryRead);
        HasNone(baseResident, NexScopes.AdminAll, NexScopes.SecurityManage,
            NexScopes.StatisticsRead, NexScopes.SimulatorsRead,
            NexScopes.EstatesRead, NexScopes.EconomyTransfer,
            NexScopes.RegionsManage, NexScopes.SimulatorsManage,
            NexScopes.InventoryWrite, NexScopes.DiscoveryManage,
            NexScopes.ExperiencesManage, NexScopes.ExperiencesViewerManage);

        // SQL accounts with the old default "resident" role must work exactly
        // like newly registered accounts. No explicit database migration or
        // 100,000 separate PATCH calls should be necessary.
        string[] emptyStoredRoles = NexAuthorizationPolicy.GetEffectiveScopes(
            0, adminMinimum, Array.Empty<string>(), Array.Empty<string>());
        HasAll(emptyStoredRoles, NexScopes.InventoryRead, NexScopes.EconomyRead);
        string[] legacyAdmin = NexAuthorizationPolicy.GetEffectiveScopes(
            200, adminMinimum, new[] { NexRoles.Resident }, Array.Empty<string>());
        HasAll(legacyAdmin, NexScopes.AdminAll, NexScopes.StatisticsRead,
            NexScopes.SimulatorsRead, NexScopes.EstatesRead,
            NexScopes.EconomyRead, NexScopes.InventoryRead,
            NexScopes.RegionsRead, NexScopes.DiscoveryRead);

        string[] roleAdmin = NexAuthorizationPolicy.GetEffectiveScopes(
            0, adminMinimum, new[] { NexRoles.Administrator }, Array.Empty<string>());
        HasAll(roleAdmin, NexScopes.AdminAll, NexScopes.StatisticsRead,
            NexScopes.SimulatorsRead, NexScopes.EstatesRead,
            NexScopes.EconomyRead, NexScopes.InventoryRead);
        HasNone(roleAdmin, NexScopes.EconomyTransfer, NexScopes.InventoryWrite,
            NexScopes.SimulatorsManage, NexScopes.ExperiencesViewerPermissions);

        string oldAdminToken = issuer.Issue(adminId,
            new[] { NexScopes.UsersRead, NexScopes.AdminAll }, 100);
        Check(issuer.TryValidate(oldAdminToken, out NexAccessTokenClaims oldAdmin)
            && !oldAdmin.Scopes.Contains(NexScopes.StatisticsRead),
            "Existing signed tokens should not magically acquire newly configured read scopes");
        string refreshedAdminToken = issuer.Issue(adminId, legacyAdmin, 100);
        Check(issuer.TryValidate(refreshedAdminToken,
            out NexAccessTokenClaims newAdmin), "New admin JWT invalid");
        HasAll(newAdmin.Scopes.ToArray(), NexScopes.AdminAll,
            NexScopes.StatisticsRead, NexScopes.RegionsRead,
            NexScopes.SimulatorsRead, NexScopes.EstatesRead);

        string residentToken = issuer.Issue(citizenId, baseResident, 50);
        Check(issuer.TryValidate(residentToken, out NexAccessTokenClaims citizen),
            "New resident JWT invalid");
        HasAll(citizen.Scopes.ToArray(), NexScopes.EconomyRead,
            NexScopes.InventoryRead, NexScopes.RegionsRead);
        HasNone(citizen.Scopes.ToArray(), NexScopes.AdminAll,
            NexScopes.StatisticsRead, NexScopes.EconomyTransfer,
            NexScopes.SecurityManage);

        Check(NexAuthorizationPolicy.TryNormalizeAssignment(
            new[] { NexRoles.Resident },
            new[] { NexScopes.EconomyRead, NexScopes.DiscoverySubmit }, false,
            out string[] roles, out string[] custom, out string error),
            "Resident explicit scope assignment should be valid: " + error);
        var withCustom = NexAuthorizationPolicy.GetEffectiveScopes(
            0, adminMinimum, roles, custom);
        HasAll(withCustom, NexScopes.EconomyRead, NexScopes.DiscoverySubmit);
        Check(!NexAuthorizationPolicy.TryNormalizeAssignment(
            new[] { NexRoles.Resident }, new[] { NexScopes.AdminAll },
            false, out _, out _, out error), "Residents must not self-grant admin:*");
        Check(!NexAuthorizationPolicy.TryNormalizeAssignment(
            new[] { NexRoles.Resident }, new[] { NexScopes.SecurityManage },
            false, out _, out _, out error), "Residents must not self-grant security:manage");

        string[] support = NexAuthorizationPolicy.GetEffectiveScopes(
            0, adminMinimum, new[] { NexRoles.Support }, Array.Empty<string>());
        HasAll(support, NexScopes.StatisticsRead, NexScopes.RegionsRead,
            NexScopes.SimulatorsRead, NexScopes.EstatesRead);
        HasNone(support, NexScopes.AdminAll, NexScopes.EconomyTransfer);
        string[] manager = NexAuthorizationPolicy.GetEffectiveScopes(
            0, adminMinimum, new[] { NexRoles.RegionManager }, Array.Empty<string>());
        HasAll(manager, NexScopes.RegionsManage, NexScopes.SimulatorsManage);
        HasNone(manager, NexScopes.AdminAll, NexScopes.EconomyTransfer);

        // Scale test: no role-to-user SQL materialization. Existing 100K users
        // get exactly the policy appropriate for their role when logging in.
        for (int i = 0; i < 100_000; i++)
        {
            string[] computed = NexAuthorizationPolicy.GetEffectiveScopes(
                0, adminMinimum, new[] { NexRoles.Resident }, Array.Empty<string>());
            if (i == 0 || i == 99_999)
            {
                HasAll(computed, NexScopes.EconomyRead, NexScopes.InventoryRead);
                HasNone(computed, NexScopes.AdminAll, NexScopes.StatisticsRead);
            }
        }

        var adminPrincipal = new NexPrincipal(adminId, newAdmin.Scopes);
        var citizenPrincipal = new NexPrincipal(citizenId, citizen.Scopes);
        var servicePrincipal = new NexPrincipal("api-key:test", new[] { NexScopes.StatisticsRead });
        Check(adminPrincipal.HasScope(NexScopes.StatisticsRead) &&
            citizenPrincipal.HasScope(NexScopes.EconomyRead) &&
            !citizenPrincipal.HasScope(NexScopes.AdminAll) &&
            servicePrincipal.HasScope(NexScopes.StatisticsRead) &&
            !servicePrincipal.HasScope(NexScopes.AdminAll) &&
            !servicePrincipal.HasScope(NexScopes.EconomyRead),
            "Principal machine-key isolation or least privilege regression");

        Console.WriteLine("World personal scopes: 100K role-derived users, admin read grants, OAuth/native JWT, preserved explicit scopes and least privilege OK");
        return 0;
    }
}

// SPDX-License-Identifier: MPL-2.0
// Live Groups V2 authorization and persistence regression against CI MariaDB.
// Uses OpenSim's real MySQL Groups storage/migrations, not an in-memory fake.
using System;
using System.IO;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework.Console;
using OpenSim.Groups;

internal static class Program
{
    private static void Check(bool condition, string reason)
    {
        if (!condition)
            throw new InvalidOperationException("NexGroups MariaDB regression: " + reason);
    }

    private static void Main()
    {
        string connection = Environment.GetEnvironmentVariable("NEX_GROUPS_MARIADB");
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException(
                "Set NEX_GROUPS_MARIADB to an isolated CI MariaDB connection string.");

        MainConsole.Instance = new MockConsole();
        string provider = Path.GetFullPath(Path.Combine("bin", "OpenSim.Data.MySQL.dll"));
        Check(File.Exists(provider), "compiled MySQL provider unavailable");

        string scratch = Path.Combine(Path.GetTempPath(),
            "nexgroups-sql-regression-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        try
        {
            var config = new IniConfigSource();
            IConfig database = config.AddConfig("DatabaseService");
            database.Set("StorageProvider", provider);
            database.Set("ConnectionString", connection);

            IConfig groupConfig = config.AddConfig("Groups");
            groupConfig.Set("Realm", "os_groups");
            groupConfig.Set("NexModerationStorePath",
                Path.Combine(scratch, "moderation.json"));

            IConfig gridUsers = config.AddConfig("GridUserService");
            gridUsers.Set("StorageProvider", provider);
            gridUsers.Set("ConnectionString", connection);
            gridUsers.Set("Realm", "GridUser");

            var groups = new GroupsService(config, "Groups");
            UUID founder = UUID.Random();
            UUID accountant = UUID.Random();
            UUID stranger = UUID.Random();
            UUID invitee = UUID.Random();
            UUID group = groups.CreateGroup(founder.ToString(),
                "OGL-CI-Group-" + Guid.NewGuid().ToString("N"),
                "Real MariaDB authorization regression", false, UUID.Zero,
                0, false, false, false, founder, out string reason);
            Check(!group.IsZero(), "failed creating persisted group: " + reason);
            Check(groups.GetGroupRecord(founder.ToString(), group) != null,
                "created group cannot be read");
            Check(groups.CanReadGroupAccounting(founder.ToString(), group),
                "founder with Accountable powers cannot read group accounting");
            Check(!groups.CanReadGroupAccounting(stranger.ToString(), group),
                "nonmember obtained accounting access");
            Check(!groups.CanReadGroupAccounting(string.Empty, group),
                "empty requester obtained accounting access");
            Check(!groups.CanReadGroupAccounting(founder.ToString(), UUID.Random()),
                "unknown group passed accounting access");
            Check(!groups.CanReadGroupAccounting(founder.ToString(), UUID.Zero),
                "zero group passed accounting access");

            // Add an ordinary member and remove Accountable from Everyone.
            Check(groups.AddAgentToGroup(founder.ToString(),
                accountant.ToString(), group, UUID.Zero, string.Empty,
                out reason), "failed adding ordinary member: " + reason);
            Check(groups.GetAgentGroupMembership(
                accountant.ToString(), accountant.ToString(), group) != null,
                "ordinary member not persisted");
            Check(groups.UpdateGroupRole(founder.ToString(), group, UUID.Zero,
                "Everyone", "Safe non-accounting group membership",
                "Member", (ulong)GroupPowers.JoinChat),
                "could not update Everyone's persisted group rights");
            Check(!groups.CanReadGroupAccounting(accountant.ToString(), group),
                "ordinary member without Accountable gained financial access");
            Check(groups.CanReadGroupAccounting(founder.ToString(), group),
                "founder lost accounting access when Everyone role changed");

            UUID accountantRole = UUID.Random();
            Check(groups.AddGroupRole(founder.ToString(), group, accountantRole,
                "Accountants", "Authorized group financial viewers", "Accountant",
                (ulong)GroupPowers.Accountable, out reason),
                "could not create accountant role: " + reason);
            Check(groups.AddAgentToGroupRole(founder.ToString(),
                accountant.ToString(), group, accountantRole),
                "could not grant accountant role");
            Check(groups.CanReadGroupAccounting(accountant.ToString(), group),
                "assigned accountant role did not enable access");
            Check(groups.RemoveAgentFromGroupRole(founder.ToString(),
                accountant.ToString(), group, accountantRole),
                "could not revoke accountant role");
            Check(!groups.CanReadGroupAccounting(accountant.ToString(), group),
                "revoked accountant role still grants access");

            UUID invite = UUID.Random();
            Check(groups.AddAgentToGroupInvite(founder.ToString(),
                invite, group, UUID.Zero, invitee.ToString()),
                "owner could not create invitation");
            Check(groups.GetAgentToGroupInvite(invitee.ToString(), invite) != null,
                "invitee cannot read own invitation");
            Check(groups.GetAgentToGroupInvite(founder.ToString(), invite) != null,
                "inviting owner cannot inspect the invitation");
            Check(groups.GetAgentToGroupInvite(stranger.ToString(), invite) == null,
                "stranger can read private invitation");
            Check(groups.GetAgentToGroupInvite(accountant.ToString(), invite) == null,
                "ordinary member without Invite can read private invitation");
            Check(!groups.CanReadGroupAccounting(invitee.ToString(), group),
                "invitation alone grants accounting access");

            // Repeat across a new GroupsService instance to verify DB state is
            // actually persisted rather than an in-process permission cache.
            var reopened = new GroupsService(config, "Groups");
            Check(reopened.GetGroupRecord(founder.ToString(), group) != null,
                "group was not persisted across service restart");
            Check(reopened.GetAgentToGroupInvite(stranger.ToString(), invite) == null,
                "stranger reads invitation after service restart");
            Check(reopened.GetAgentToGroupInvite(invitee.ToString(), invite) != null,
                "invitee loses persisted invitation after restart");
            Check(!reopened.CanReadGroupAccounting(accountant.ToString(), group),
                "revoked accountant access reappeared after restart");
            reopened.RemoveAgentToGroupInvite(stranger.ToString(), invite);
            Check(reopened.GetAgentToGroupInvite(invitee.ToString(), invite) != null,
                "stranger deleted invitation from SQL");
            reopened.RemoveAgentToGroupInvite(accountant.ToString(), invite);
            Check(reopened.GetAgentToGroupInvite(invitee.ToString(), invite) != null,
                "member without Invite deleted invitation from SQL");
            reopened.RemoveAgentToGroupInvite(invitee.ToString(), invite);
            Check(reopened.GetAgentToGroupInvite(founder.ToString(), invite) == null,
                "invitee could not delete own invitation");

            Check(reopened.DeleteGroup(founder.ToString(), group, out reason),
                "cleanup failed deleting fixture group: " + reason);
            Check(reopened.GetGroupRecord(founder.ToString(), group) == null,
                "fixture group survives deletion");

            Console.WriteLine("NexGroups real MariaDB authorization / persistence: OK");
        }
        finally
        {
            try { Directory.Delete(scratch, true); } catch (IOException) {}
        }
    }
}

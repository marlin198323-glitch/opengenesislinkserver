// SPDX-License-Identifier: MPL-2.0
// Real TCP/HTTP, signed resident JWTs, MariaDB Groups V2 and NV$ ledger.
// Run ONLY against the isolated GitHub Actions test database.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Nini.Config;
using NexVerse.Core.Economy;
using NexVerse.Core.Security;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Groups;
using OpenSim.Services.Interfaces;
using OpenSim.Services.UserAccountService;

internal static class Program
{
    private static IGridService m_TestGrid;
    private static string m_TestConnection;

    private static void Check(bool ok, string what)
    {
        if (!ok) throw new InvalidOperationException("Groups HTTP integration: " + what);
    }

    private static int FindPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static UserAccount Seed(UserAccountService users, string name)
    {
        var account = new UserAccount(UUID.Zero, UUID.Random(), name, "Resident",
            name.ToLowerInvariant() + "@nexgroups-ci.invalid")
        {
            Active = true,
            LocalToGrid = true,
            UserLevel = 0,
            NexVerseState = "active",
            NexVerseRoles = "resident",
            NexVerseStateChanged = 7
        };
        Check(users.StoreUserAccount(account), "unable to persist resident " + name);
        var stored = users.GetUserAccount(UUID.Zero, account.PrincipalID);
        Check(stored != null && stored.NexVerseStateChanged == account.NexVerseStateChanged,
            "resident identity/security stamp did not persist");
        return stored;
    }

    private static object Construct(Type type, params object[] args)
    {
        object result = Activator.CreateInstance(type,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null, args, null);
        return result ?? throw new InvalidOperationException("Could not construct " + type.Name);
    }

    private static SimpleStreamMethod Route(GroupsService groups,
        UserAccountService users, HmacNexAccessTokenService tokens,
        Func<NexEconomyService> economy)
    {
        Assembly assembly = Assembly.Load("NexVerse.Server.Api");
        Type authType = assembly.GetType("NexVerse.Server.Api.NexApiAuthenticator", true);
        Type groupsType = assembly.GetType("NexVerse.Server.Api.NexGroupsApi", true);
        var auth = Construct(authType, users, new NexAuthorizationService(),
            tokens, null, null, 200);
        var api = Construct(groupsType, auth, null, groups, economy);
        // A second simulator's group parcels are projected by the central
        // directory. Configure it through the same optional production hook.
        // Tests use an isolated SQL table and a fake grid registry.
        if (m_TestGrid != null)
        {
            Type directoryType = assembly.GetType(
                "NexVerse.Server.Api.NexGroupLandDirectory", true);
            object directory = Construct(directoryType,
                "OpenSim.Data.MySQL.dll", m_TestConnection, m_TestGrid);
            groupsType.GetMethod("ConfigureLandDirectory",
                BindingFlags.Public | BindingFlags.Instance)!
                .Invoke(api, new[] { directory });
        }

        MethodInfo handle = groupsType.GetMethod("Handle",
            BindingFlags.Instance | BindingFlags.Public);
        Check(handle != null, "NexGroupsApi.Handle missing");
        return (SimpleStreamMethod)Delegate.CreateDelegate(
            typeof(SimpleStreamMethod), api, handle);
    }

    private static async System.Threading.Tasks.Task Verify(
        HttpClient client, string path, string token,
        HttpStatusCode expected, string jsonProperty = null,
        HttpMethod method = null, string body = null)
    {
        using var req = new HttpRequestMessage(method ?? HttpMethod.Get, path);
        if (token != null)
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body != null)
            req.Content = new StringContent(body, Encoding.UTF8, "application/json");
        using var res = await client.SendAsync(req);
        string response = await res.Content.ReadAsStringAsync();
        Check(res.StatusCode == expected,
            (method ?? HttpMethod.Get) + " " + path +
            " expected " + (int)expected + ", got " + (int)res.StatusCode +
            "; body=" + response);
        if (jsonProperty != null)
        {
            using var json = JsonDocument.Parse(response);
            Check(json.RootElement.TryGetProperty(jsonProperty, out _),
                "JSON property '" + jsonProperty + "' missing from " + path);
        }
    }

    private static async System.Threading.Tasks.Task Main()
    {
        string connection = Environment.GetEnvironmentVariable("NEX_GROUPS_MARIADB");
        Check(!string.IsNullOrWhiteSpace(connection),
            "isolated NEX_GROUPS_MARIADB connection is required");

        MainConsole.Instance = new MockConsole();
        var config = new IniConfigSource();
        var db = config.AddConfig("DatabaseService");
        string mysql = Path.GetFullPath(Path.Combine("bin", "OpenSim.Data.MySQL.dll"));
        Check(File.Exists(mysql), "MySQL provider missing");
        db.Set("StorageProvider", mysql);
        db.Set("ConnectionString", connection);
        var userCfg = config.AddConfig("UserAccountService");
        userCfg.Set("CreateDefaultAvatarEntries", false);
        var groupsCfg = config.AddConfig("Groups");
        string scratch = Path.Combine(Path.GetTempPath(), "nexgroups-http-" +
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        groupsCfg.Set("Realm", "os_groups");
        groupsCfg.Set("NexModerationStorePath", Path.Combine(scratch, "moderation.json"));
        var grid = config.AddConfig("GridUserService");
        grid.Set("StorageProvider", mysql);
        grid.Set("ConnectionString", connection);
        grid.Set("Realm", "GridUser");
        BaseHttpServer server = null;
        try
        {
            var users = new UserAccountService(config);
            var groups = new GroupsService(config, "Groups");
            UserAccount owner = Seed(users, "Owner" + Guid.NewGuid().ToString("N").Substring(0, 9));
            UserAccount member = Seed(users, "Member" + Guid.NewGuid().ToString("N").Substring(0, 9));
            UserAccount stranger = Seed(users, "Stranger" + Guid.NewGuid().ToString("N").Substring(0, 9));
            UserAccount invitee = Seed(users, "Invitee" + Guid.NewGuid().ToString("N").Substring(0, 9));

            string key = new string('H', 64);
            var tokens = new HmacNexAccessTokenService(
                "http://127.0.0.1:19981", "nexgroups-http-ci", key, 600);
            string Jwt(UserAccount account, params string[] scopes) =>
                tokens.Issue(account.PrincipalID.ToString(), scopes,
                    account.NexVerseStateChanged);
            string ownerToken = Jwt(owner, NexScopes.GroupsRead, NexScopes.GroupsManage);
            string memberToken = Jwt(member, NexScopes.GroupsRead, NexScopes.GroupsManage);
            string strangerToken = Jwt(stranger, NexScopes.GroupsRead, NexScopes.GroupsManage);
            string inviteeReadToken = Jwt(invitee, NexScopes.GroupsRead);
            string inviteeToken = Jwt(invitee, NexScopes.GroupsRead, NexScopes.GroupsManage);
            string narrowToken = Jwt(stranger, NexScopes.UsersRead);

            var runtime = NexLedgerSqlRuntime.Resolve("OpenSim.Data.MySQL.dll", connection);
            NexLedgerSqlStore store = runtime.CreateStore();
            var economy = new NexEconomyService(new NexDoubleEntryLedger(store), store);
            NexEconomyService activeEconomy = economy;

            // One remote group parcel in the same authoritative RegionStore.
            // Its region is not hosted by this HTTP process.
            UUID remoteRegion = UUID.Random();
            UUID parcel = UUID.Random();
            m_TestGrid = DispatchProxy.Create<IGridService, GroupGridProxy>();
            ((GroupGridProxy)m_TestGrid).Region = new GridRegion
            {
                RegionID = remoteRegion, RegionName = "Remote CI Region",
                RegionLocX = 256000, RegionLocY = 258560,
                RegionSizeX = 256, RegionSizeY = 256
            };
            m_TestConnection = connection;
            var handler = Route(groups, users, tokens, () => activeEconomy);
            int port = FindPort();
            server = new BaseHttpServer((uint)port) { ListenIPAddress = IPAddress.Loopback };
            server.AddSimpleStreamHandler(
                new SimpleStreamHandler("/api/v1/groups", handler, "Groups HTTP CI"), true);
            server.Start();
            using var http = new HttpClient { BaseAddress =
                new Uri("http://127.0.0.1:" + port + "/"),
                Timeout = TimeSpan.FromSeconds(12) };

            UUID group = groups.CreateGroup(owner.PrincipalID.ToString(),
                "HTTP-CI-" + Guid.NewGuid().ToString("N"), "HTTP fixture",
                false, UUID.Zero, 0, false, false, false,
                owner.PrincipalID, out string reason);
            Check(!group.IsZero(), "fixture group creation failed: " + reason);
            UUID otherGroup = groups.CreateGroup(owner.PrincipalID.ToString(),
                "HTTP-CI-OTHER-" + Guid.NewGuid().ToString("N"), "URL mismatch",
                false, UUID.Zero, 0, false, false, false,
                owner.PrincipalID, out reason);
            Check(!otherGroup.IsZero(), "second group creation failed: " + reason);
            using (var landDb = NexLedgerSqlRuntime.Resolve(
                    "OpenSim.Data.MySQL.dll", connection).CreateConnection())
            {
                landDb.Open();
                using var schema = landDb.CreateCommand();
                schema.CommandText =
                    "CREATE TABLE IF NOT EXISTS land (" +
                    "UUID varchar(255) PRIMARY KEY, RegionUUID varchar(255), " +
                    "OwnerUUID varchar(255), GroupUUID varchar(255), " +
                    "Name varchar(255), Description varchar(255), " +
                    "IsGroupOwned int, Area int, SalePrice int, " +
                    "SnapshotUUID varchar(255), UserLocationX float, " +
                    "UserLocationY float, UserLocationZ float)";
                schema.ExecuteNonQuery();
                using var insert = landDb.CreateCommand();
                insert.CommandText =
                    "INSERT INTO land (UUID, RegionUUID, OwnerUUID, GroupUUID, " +
                    "Name, Description, IsGroupOwned, Area, SalePrice, " +
                    "SnapshotUUID, UserLocationX, UserLocationY, UserLocationZ) " +
                    "VALUES (@parcel, @region, @owner, @group, " +
                    "'Remote parcel', 'Other simulator', 1, 512, 0, " +
                    "@snapshot, 12, 24, 30)";
                foreach ((string name, string value) in new[] {
                    ("@parcel", parcel.ToString()),
                    ("@region", remoteRegion.ToString()),
                    ("@owner", owner.PrincipalID.ToString()),
                    ("@group", group.ToString()),
                    ("@snapshot", UUID.Random().ToString()) })
                {
                    var param = insert.CreateParameter();
                    param.ParameterName = name;
                    param.Value = value;
                    insert.Parameters.Add(param);
                }
                Check(insert.ExecuteNonQuery() == 1,
                    "failed seeding remote land fixture");
            }
            string accounting = "/api/v1/groups/" + group + "/accounting";

            Check(groups.AddAgentToGroup(owner.PrincipalID.ToString(),
                member.PrincipalID.ToString(), group, UUID.Zero, "", out reason),
                "could not join member: " + reason);
            Check(groups.UpdateGroupRole(owner.PrincipalID.ToString(), group,
                UUID.Zero, "Everyone", "Without financial access", "Member",
                (ulong)GroupPowers.JoinChat),
                "could not drop Accountable from Everyone");

            await Verify(http, accounting, null, HttpStatusCode.Unauthorized, "error");
            await Verify(http, accounting, "invalid.jwt.token",
                HttpStatusCode.Unauthorized, "error");
            await Verify(http, accounting, narrowToken,
                HttpStatusCode.Forbidden, "error");
            await Verify(http, accounting, strangerToken,
                HttpStatusCode.Forbidden, "error");
            await Verify(http, accounting, memberToken,
                HttpStatusCode.Forbidden, "error");
            await Verify(http, accounting + "?requester_id=" + owner.PrincipalID,
                strangerToken, HttpStatusCode.Forbidden, "error");
            await Verify(http, accounting, ownerToken, HttpStatusCode.OK, "nvban");
            string land = "/api/v1/groups/" + group + "/land";
            await Verify(http, land, ownerToken, HttpStatusCode.OK, "parcels");
            await Verify(http, land, strangerToken, HttpStatusCode.Forbidden, "error");
            await Verify(http, land + "?requester_id=" + owner.PrincipalID,
                strangerToken, HttpStatusCode.Forbidden, "error");
            await Verify(http, "/api/v1/groups/" + group + "/transactions",
                ownerToken, HttpStatusCode.OK, "transactions");
            await Verify(http, "/api/v1/groups/" + group + "/transactions",
                strangerToken, HttpStatusCode.Forbidden, "error");
            await Verify(http, "/api/v1/groups/" + group + "/transactions",
                memberToken, HttpStatusCode.Forbidden, "error");
            using (var request = new HttpRequestMessage(HttpMethod.Get, land))
            {
                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", ownerToken);
                using var response = await http.SendAsync(request);
                using var result = JsonDocument.Parse(
                    await response.Content.ReadAsStringAsync());
                var items = result.RootElement.GetProperty("parcels");
                Check(items.GetArrayLength() == 1 &&
                    items[0].GetProperty("parcel_id").GetString() == parcel.ToString() &&
                    items[0].GetProperty("region_name").GetString() == "Remote CI Region" &&
                    items[0].GetProperty("global_x").GetUInt32() == 256012u,
                    "grid-wide land omitted remote region or incorrect coordinates");
            }


            UUID accountingRole = UUID.Random();
            Check(groups.AddGroupRole(owner.PrincipalID.ToString(), group,
                accountingRole, "Accountants", "Finance", "Accountant",
                (ulong)GroupPowers.Accountable, out reason),
                "accountant role creation failed: " + reason);
            Check(groups.AddAgentToGroupRole(owner.PrincipalID.ToString(),
                member.PrincipalID.ToString(), group, accountingRole),
                "accountant assignment failed");
            await Verify(http, accounting, memberToken, HttpStatusCode.OK, "balance");
            Check(groups.RemoveAgentFromGroupRole(owner.PrincipalID.ToString(),
                member.PrincipalID.ToString(), group, accountingRole),
                "accountant role revocation failed");
            await Verify(http, accounting, memberToken, HttpStatusCode.Forbidden, "error");

            UUID invite = UUID.Random();
            Check(groups.AddAgentToGroupInvite(owner.PrincipalID.ToString(),
                invite, group, UUID.Zero, invitee.PrincipalID.ToString()),
                "cannot seed invitation");
            string invitation = "/api/v1/groups/" + group + "/invites/" + invite;
            await Verify(http, invitation, inviteeToken, HttpStatusCode.OK, "invite");
            await Verify(http, invitation, ownerToken, HttpStatusCode.OK, "invite");
            await Verify(http, invitation, strangerToken, HttpStatusCode.NotFound, "error");
            await Verify(http, invitation + "?requester_id=" + invitee.PrincipalID,
                strangerToken, HttpStatusCode.NotFound, "error");
            await Verify(http, "/api/v1/groups/" + otherGroup + "/invites/" + invite,
                inviteeToken, HttpStatusCode.NotFound, "error");
            await Verify(http, invitation, strangerToken, HttpStatusCode.NotFound,
                "error", HttpMethod.Delete);
            await Verify(http, invitation, inviteeToken, HttpStatusCode.OK,
                "invite");
            await Verify(http, invitation, inviteeReadToken, HttpStatusCode.Forbidden,
                "error", HttpMethod.Delete);
            await Verify(http, invitation, inviteeToken, HttpStatusCode.OK,
                "deleted", HttpMethod.Delete);
            await Verify(http, invitation, inviteeToken, HttpStatusCode.NotFound, "error");

            // Persisted group creation succeeds even during an economy outage:
            // the API must return 201/pending rather than misleading 500.
            activeEconomy = null;
            string name = "HTTP-CREATE-" + Guid.NewGuid().ToString("N");
            using (var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/groups"))
            {
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
                req.Content = new StringContent(
                    JsonSerializer.Serialize(new { name, charter = "temporary economy outage" }),
                    Encoding.UTF8, "application/json");
                using var response = await http.SendAsync(req);
                string body = await response.Content.ReadAsStringAsync();
                Check(response.StatusCode == HttpStatusCode.Created,
                    "group creation during outage returned " + body);
                using var json = JsonDocument.Parse(body);
                Check(json.RootElement.GetProperty("wallet_status").GetString() == "pending",
                    "outage did not report wallet_status=pending");
                UUID created = new UUID(json.RootElement.GetProperty("group_id").GetString());
                Check(groups.GetGroupRecord(owner.PrincipalID.ToString(), created) != null,
                    "group with pending wallet was not persisted");
                activeEconomy = economy;
                await Verify(http, "/api/v1/groups/" + created + "/accounting",
                    ownerToken, HttpStatusCode.OK, "nvban");
                Check(groups.DeleteGroup(owner.PrincipalID.ToString(), created, out reason),
                    "pending wallet fixture group cleanup failed: " + reason);
            }

            using (var landDb = NexLedgerSqlRuntime.Resolve(
                    "OpenSim.Data.MySQL.dll", connection).CreateConnection())
            {
                landDb.Open();
                using var clean = landDb.CreateCommand();
                clean.CommandText = "DELETE FROM land WHERE UUID = @id";
                var param = clean.CreateParameter();
                param.ParameterName = "@id";
                param.Value = parcel.ToString();
                clean.Parameters.Add(param);
                clean.ExecuteNonQuery();
            }
            Check(groups.DeleteGroup(owner.PrincipalID.ToString(), group, out reason),
                "first fixture cleanup failed: " + reason);
            Check(groups.DeleteGroup(owner.PrincipalID.ToString(), otherGroup, out reason),
                "second fixture cleanup failed: " + reason);
            Console.WriteLine("NexGroups signed bearer HTTP / real MariaDB / NV$ ledger: OK");
        }
        finally
        {
            server?.Stop(true);
            try { Directory.Delete(scratch, true); } catch (IOException) {}
        }
    }
}

internal class GroupGridProxy : DispatchProxy
{
    public GridRegion Region { get; set; }

    protected override object Invoke(MethodInfo method, object[] args)
    {
        if (method.Name == "GetRegionByUUID")
            return (UUID)args[1] == Region?.RegionID ? Region : null;
        throw new NotSupportedException(method.Name);
    }
}

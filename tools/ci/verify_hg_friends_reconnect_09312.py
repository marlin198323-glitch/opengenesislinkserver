#!/usr/bin/env python3
"""Regression guard: Hypergrid friend status must survive logout/relogin.

This is a structural guard over the production implementations. The .NET
build and real HG integration tests run separately in the CI workflow.
"""
from pathlib import Path
import re

root = Path(__file__).resolve().parents[2]
def read(path):
    return (root / path).read_text(encoding="utf-8")

for name in ("HGFriendsService.cs", "UserAgentService.cs"):
    content = read("OpenSim/Services/HypergridService/" + name)
    start = content.index("public List<UUID> StatusNotification(")
    end = content.find("return new List<UUID>();", content.index("if (online)", start))
    if end < 0:
        raise AssertionError(f"{name}: status reply branch unavailable")
    block = content[start:end]
    assert "foreach (PresenceInfo friendSession in friendSessions)" in block, name
    assert "HashSet<string> authorizedRecipients" in block, name
    assert "authorizedRecipients.Contains(localFriendID.ToString())" in block, name
    assert "HashSet<UUID> deliveredRecipients" in block, name
    assert "deliveredRecipients.Add(localFriendID)" in block, name
    assert "ForwardStatusNotificationToSim(" in block, name
    assert "localFriendsOnline.Add(localFriendID)" in block, name
    assert "friendSession.RegionID.IsZero()" in block, name
    assert "finfo.Friend.StartsWith(" in block and "finfo.Friend.EndsWith(secret)" in block, name
    # A first-presence-only loop was the root cause for multiple local friends.
    presences = block[block.index("PresenceInfo[] friendSessions"):]
    assert not re.search(r"\bbreak\s*;", presences), f"{name}: stops after one recipient"
    assert "foreach (PresenceInfo friendSession in friendSessions)" in presences, name

management = read("OpenSim/Region/CoreModules/Framework/UserManagement/UserManagementModule.cs")
start = management.index("public virtual string GetUserServerURL(")
end = management.index("public virtual string GetUserUUI(", start)
body = management[start:end]
assert body.count("public virtual string GetUserServerURL(") == 2
assert "userdata.ServerURLs = new Dictionary<string, object>();" not in body
assert body.count("userdata.ServerURLs = null;") >= 6
assert body.count('serverType.Equals("FriendsServerURI", StringComparison.OrdinalIgnoreCase)') >= 4
assert body.count("userdata.LastWebFail = Util.GetTimeStamp();") >= 6
assert "BADURLEXPIRE * 1000" in body
# A foreign home-grid may not be reachable on the *first* status
# lookup at viewer login. Confirm that delayed reconciliation is bounded,
# does not invent online status, does not refresh local users, and never
# publishes a foreign UUID not present in the requested friend list.
module = read("OpenSim/Region/CoreModules/Avatar/Friends/HGFriendsModule.cs")
for marker in (
    "using System.Threading.Tasks;",
    "PresenceRetrySeconds",
    "PresenceRetryAttempts",
    "PendingForeignPresence(client.AgentId).Count > 0",
    "_ = RetryForeignPresenceAsync(client);",
    "private List<string> PendingForeignPresence(UUID owner)",
    "(friend.TheirFlags & (int)FriendRights.CanSeeOnline) == 0",
    "IsFriendOnline(owner, friendId)",
    "private async Task RetryForeignPresenceAsync(IClientAPI client)",
    "await Task.Delay(TimeSpan.FromSeconds(retrySeconds))",
    "LocateClientObject(owner) != client",
    "GetOnlineFriends(owner, pending, online)",
    "client.SendAgentOnline(online.ToArray())",
    "HashSet<UUID> requested = new();",
    "if (!requested.Contains(friendID))",
):
    assert marker in module, "HG foreign reconnect reconciliation missing: " + marker
assert "m_HGPresenceRetryAttempts = Math.Clamp(" in module
assert "0, 4);" in module, "foreign status requests need a hard retry bound"
retry = module.split("private async Task RetryForeignPresenceAsync", 1)[1].split(
    "protected override void GetOnlineFriends", 1)[0]
assert retry.index("LocateClientObject(owner) != client") < retry.index("GetOnlineFriends(owner, pending, online)")
assert retry.index("GetOnlineFriends(owner, pending, online)") < retry.index("client.SendAgentOnline(online.ToArray())")
assert "SendAgentOffline" not in retry, "an unreachable HG grid must not be treated as proven offline"
getonline = module.split("protected override void GetOnlineFriends", 1)[1].split(
    "protected override void StatusNotify", 1)[0]
assert getonline.index("HashSet<UUID> requested") < getonline.index("connector.StatusNotification(")
assert getonline.index("if (!requested.Contains(friendID))") < getonline.index("friends.Add(friendID);")

for path in ("bin/config-include/GridCommon.ini",
             "bin/config-include/GridCommon.ini.example",
             "bin/config-include/StandaloneCommon.ini.example"):
    cfg = read(path).split("[HGFriendsModule]", 1)[1].split("\n[", 1)[0]
    assert "PresenceRetrySeconds = 135" in cfg, path
    assert "PresenceRetryAttempts = 3" in cfg, path

print("OGL HG friends: all-presence delivery, duplicate guard, recoverable URL cache, bounded relog presence retries: OK")

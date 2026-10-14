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
print("OGL HG friends: all-presence delivery, duplicate guard, recoverable URL cache: OK")

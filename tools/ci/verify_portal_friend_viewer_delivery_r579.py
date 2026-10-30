#!/usr/bin/env python3
"""Static guard for R579 portal-originated friendship offers."""
from pathlib import Path
social = Path("NexVerse/Server/Api/NexSocialGraphApi.cs").read_text(encoding="utf-8")
connector = Path("NexVerse/Server/Api/NexVerseWorldApiConnector.cs").read_text(encoding="utf-8")
friend_module = Path("OpenSim/Region/CoreModules/Avatar/Friends/FriendsModule.cs").read_text(encoding="utf-8")
for fragment in (
    "m_Friends.StoreFriend(owner.ToString(), target.ToString(), -1)",
    "m_Friends.StoreFriend(target.ToString(), owner.ToString(), 1)",
    "NotifyViewerAboutFriendRequest(owner, target)",
    "if (m_Presence == null)",
    "m_Presence.GetAgents(new[] { target.ToString() })",
    "if (recipients == null || !recipients.Any(",
    'return "offline_pending";',
    'return "delivery_unavailable";',
    "InstantMessageDialog.FriendshipOffered",
    "offer.imSessionID = offer.fromAgentID;",
    "delivery.IncomingInstantMessage(offer)",
    "viewer_notification = viewerNotification",
):
    assert fragment in social, "missing authenticated World viewer delivery contract: " + fragment
store = social.index("bool a = m_Friends.StoreFriend(owner.ToString(), target.ToString(), -1)")
notify = social.index("string viewerNotification = a && b ? NotifyViewerAboutFriendRequest(owner, target)")
assert store < notify, "must not notify viewer before durable pending relationship"
assert social.index("if (m_Presence == null)") < social.index("delivery.IncomingInstantMessage(offer)")
assert '() => LoadOptionalService<IInstantMessage>(config, "HGInstantMessageService")' in connector
assert 'new NexSocialGraphApi(userAccounts, friendsService' in connector
assert "Send outstanding friendship offers" in friend_module
assert "LocalFriendshipOffered(client.AgentId, im)" in friend_module
print("R579 World API friendship requests: stored pending, online viewer IM, offline login replay: OK")

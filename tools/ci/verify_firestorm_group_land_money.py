#!/usr/bin/env python3
"""Firestorm 7.2.4 group land and NV$ packet wiring regression."""
from pathlib import Path
def load(path):
    return Path(path).read_text(encoding="utf-8")

client = load("OpenSim/Region/ClientStack/Linden/UDP/LLClientView.cs")
interface = load("OpenSim/Framework/IClientAPI.cs")
money = load("NexVerse/RegionModules/Economy/NexVerseMoneyModule.cs")
discovery = load("NexVerse/RegionModules/Discovery/NexDiscoveryViewerModule.cs")
for needle in (
    "GroupAccountSummaryRequest.MoneyData.RequestID",
    "GASRP.MoneyData.RequestID = requestID;",
    "GASRP.MoneyData.CurrentInterval = 0;",
    "GASRP.MoneyData.IntervalDays = 7;",
    "GADRP.HistoryData = Array.Empty<GroupAccountDetailsReplyPacket.HistoryDataBlock>();",
    "GATRP.HistoryData = Array.Empty<GroupAccountTransactionsReplyPacket.HistoryDataBlock>();",
):
    assert needle in client, needle
assert "GroupAccountSummaryRequest(IClientAPI client, UUID agentID, UUID groupID, UUID requestID)" in interface
for needle in (
    "OnGroupAccountSummaryRequest +=",
    "OnGroupAccountDetailsRequest +=",
    "OnGroupAccountTransactionsRequest +=",
    "GroupPowers.Accountable",
    "GetFullGroupPowers(agentId, groupId)",
    "TryGetGroupBalance(",
    "/api/v1/economy/balance?account_id=",
    "/api/v1/economy/accounts/ensure",
    "SendGroupAccountingSummary(client, groupId, requestID,",
):
    assert needle in money, needle
for needle in (
    "OnPlacesQuery += OnPlacesQuery",
    "OnPlacesQuery -= OnPlacesQuery",
    "const uint GroupOwnedSearch = 1u << 8",
    "GetMembershipData(queryID, client.AgentId)",
    "scene.LandChannel.AllParcels()",
    "land.IsGroupOwned",
    "land.GroupID != queryID",
    "client.SendPlacesReply(queryID, transactionID, rows.ToArray())",
):
    assert needle in discovery, needle
assert "OnDirPlacesQuery += OnDirPlacesQuery" in discovery
groups = load("OpenSim/Addons/Groups/GroupsModule.cs")
assert "if (sp.ControllingClient != null)" in groups
assert "SendAgentGroupDataUpdate(sp.ControllingClient, false);" in groups
assert "if(!sp.m_haveGroupInformation)" not in groups
print("Firestorm group account packet correlation and group land PlacesQuery: OK")

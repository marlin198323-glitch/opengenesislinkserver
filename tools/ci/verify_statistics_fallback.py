#!/usr/bin/env python3
"""Regression guard: statistics must keep safe public aggregates during GridUser failure."""
from pathlib import Path

root = Path(__file__).resolve().parents[2]
stats = (root / "NexVerse/Server/Api/NexStatisticsApi.cs").read_text(encoding="utf-8")
ui = (root / "NexVerse/Server/Api/NexApiDocsPage.cs").read_text(encoding="utf-8")
routes = (root / "NexVerse/Server/Api/NexVerseWorldApiConnector.cs").read_text(encoding="utf-8")

for marker in (
    '"statistics_data_unavailable"',
    'if (m_UserAccounts == null)',
    'bool gridUserAvailable = m_GridUsers != null;',
    'm_GridUsers.GetAll(string.Empty)',
    'catch (Exception e)',
    'gridUserAvailable = false;',
    'if (!gridUserAvailable && !completeAccountEnumeration)',
    '"Both GridUser history and complete UserAccount enumeration are unavailable."',
    'active_last_7_days = gridUserAvailable',
    'online_now = gridUserAvailable ? hypergridOnline.Count : (int?)null',
    'total = gridUserAvailable ? allOnline.Count : (int?)null',
    'missing_metrics = gridUserAvailable',
    'griduser_source = gridUserAvailable ? "available" : "unavailable"',
    'complete = gridUserAvailable && completeAccountEnumeration',
):
    assert marker in stats, "Statistics fallback contract missing: " + marker

assert "m_UserAccounts == null || m_GridUsers == null" not in stats
assert stats.index('if (m_UserAccounts == null)') < stats.index('BuildSummary(response, includeProtectedDetails);')
assert '"/api/v1/statistics/summary"' in routes
assert routes.count('"/api/v1/statistics/summary"') == 2, "Statistics route must work with and without privileged API"

start = ui.split("async function loadStatistics(){", 1)[1].split("\nfunction ", 1)[0]
assert "const bearer=$('statsBearer').value.trim()||adminAuthToken();" in start
assert "const key=$('statsApiKey').value.trim();" in start
assert "$('bearer').value.trim()" not in start, "Unrelated explorer bearer must never break public stats"
assert "$('apiKey').value.trim()" not in start, "Unrelated explorer API key must never break public stats"
assert "Teilweise (Datenquelle fehlt)" in start
assert "feh" in ui

print("OGL statistics partial-data and public aggregate routing: PASS")

#!/usr/bin/env python3
from pathlib import Path

api = Path("NexVerse/Server/Api/NexEconomyApi.cs").read_text(encoding="utf-8")
connector = Path("NexVerse/Server/Api/NexVerseWorldApiConnector.cs").read_text(encoding="utf-8")
viewer = Path("NexVerse/RegionModules/Economy/NexVerseMoneyModule.cs").read_text(encoding="utf-8")
region_addin = Path("NexVerse/RegionModules/Properties/AssemblyInfo.cs").read_text(encoding="utf-8")
openapi = Path("NexVerse/Server/Api/NexVerseWorldApiHandlers.cs").read_text(encoding="utf-8")
opensim = Path("bin/OpenSim.ini.example").read_text(encoding="utf-8")

for marker in (
    "internal sealed class NexEconomyApi",
    '"/api/v1/economy/balance"',
    '"/api/v1/economy/virtual-account"',
    '"/api/v1/economy/transfers"',
    '"/api/v1/economy/transactions/"',
    '"reverse"',
    '"/api/v1/economy/accounts/"',
    "NexScopes.EconomyRead",
    "NexScopes.EconomyTransfer",
    "NexScopes.AdminAll",
    '"Idempotency-Key"',
    "DeterministicGuid(",
    "economy.Transfer(",
    "economy.Reverse(",
    "economy.SetAccountStatus(",
    "economy.EnsureResidentAccount(",
    "economy.EnsureVirtualBankAccount(",
    "NexVirtualBankAccount.Scheme",
):
    assert marker in api, f"missing economy API marker: {marker}"

for forbidden in (
    "INexLedgerStore",
    ".Append(",
    "NexLedgerSqlStore",
    "DbConnection",
    "AdministrativeAdjustment(",
):
    assert forbidden not in api, f"economy API bypasses policy boundary: {forbidden}"

for marker in (
    "new NexEconomyApi(",
    '"/api/v1/economy"',
    "NexEconomyConnector.Current?.Enabled == true",
    "NexEconomyConnector.Current.Economy",
):
    assert marker in connector, f"missing World API economy registration: {marker}"

for marker in (
    "public sealed class NexVerseMoneyModule",
    "IMoneyModule",
    "ISharedRegionModule",
    'Id = "NexVerseMoneyModule"',
    "scene.RegisterModuleInterface<IMoneyModule>",
    "OnEconomyDataRequest",
    "OnMoneyBalanceRequest",
    "OnMoneyTransfer",
    "currency-base-uri",
    "getCurrencyQuote",
    "buyCurrency",
    "preflightBuyLandPrep",
    "buyLandPrep",
    '"/currency.php"',
    '"/landtool.php"',
    '"/api/v1/economy/balance?account_id="',
    '"/api/v1/economy/transfers"',
    '"X-NexVerse-Api-Key"',
    '"Idempotency-Key"',
    "SendMoneyBalance(",
    "ThreadPool.QueueUserWorkItem(",
    "QueueInitialBalanceRefresh",
    "InitialBalanceMaxAttempts",
    "InitialBalanceRetryDelayMilliseconds",
    "InitialBalanceConfirmDelayMilliseconds",
    "client.IsActive",
    "SendEconomyData(",
    "public Type ReplaceableInterface =>",
    "null;",
    '"economymodule"',
    '"EconomyModule"',
    "StringComparison.OrdinalIgnoreCase",
    "Als IMoneyModule fuer Region",
    "MoneyBalanceReply mit",
    "BalanceRefreshSeconds",
    "RefreshConnectedBalances(",
    "m_BalanceRefreshTimer",
    "m_BalanceRefreshRunning",
    "Interlocked.Exchange(",
    "scene.GetScenePresences()",
    "presence.IsChildAgent",
    "presence.IsNPC",
):
    assert marker in viewer, f"missing Viewer economy adapter marker: {marker}"

for marker in (
    '[assembly: Addin("NexVerse.RegionModules", OpenSim.VersionInfo.VersionNumber)]',
    '[assembly: AddinDependency("OpenSim", OpenSim.VersionInfo.VersionNumber)]',
    '[assembly: AddinDependency("OpenSim.Region.Framework", OpenSim.VersionInfo.VersionNumber)]',
    '[assembly: AssemblyVersion(OpenSim.VersionInfo.AssemblyVersionNumber)]',
):
    assert marker in region_addin, f"missing NexVerse.RegionModules addin registration: {marker}"

for forbidden in (
    "DbConnection",
    "NexLedgerSqlStore",
    "INexLedgerStore",
    "ConnectionString",
    "MySql",
    "Npgsql",
    "Mono.Data.Sqlite",
):
    assert forbidden not in viewer, f"Viewer adapter must not access ledger DB directly: {forbidden}"

for marker in (
    '["/api/v1/economy/balance"]',
    '["/api/v1/economy/virtual-account"]',
    '["/api/v1/economy/transfers"]',
    '["/api/v1/economy/transactions/{transactionId}"]',
    '["/api/v1/economy/transactions/{transactionId}/reverse"]',
    '["/api/v1/economy/accounts/{accountId}/status"]',
    '["EconomyTransferRequest"]',
    '["EconomyBalanceResponse"]',
    '["EconomyVirtualAccountResponse"]',
    '["EconomyTransferResponse"]',
    '["EconomyTransactionResponse"]',
    '["EconomyAccountStatusRequest"]',
):
    assert marker in openapi, f"missing OpenAPI economy contract marker: {marker}"

assert "[Economy]" in opensim
economy_config = opensim.split("[Economy]", 1)[1].split("\n[", 1)[0]
assert "economymodule = NexVerseMoneyModule" in economy_config

assert "[NexEconomyViewer]" in opensim
viewer_config = opensim.split("[NexEconomyViewer]", 1)[1].split("\n[", 1)[0]
assert "Enabled = true" in viewer_config
assert 'ApiKey = "${Environment|NEXVERSE_ECONOMY_API_KEY}"' in viewer_config
assert 'WorldApiBaseUrl = "https://world.stadt-nexverse.de"' in viewer_config
assert "ConnectionString" not in viewer_config
assert "CurrencyPurchasePortalUrl" in viewer_config
assert "BalanceRefreshSeconds = 5" in viewer_config
assert "AllowHypergridGuests = false" in viewer_config

print("NV$ World API and Viewer adapter contract: OK")

# NexVerseMoneyModule must be loaded deterministically before the legacy
# replaceable SampleMoneyModule. Otherwise both compete for IMoneyModule in
# RegionModulesController's deferred dictionary and the viewer receives no
# MoneyBalanceReply despite Initialise() succeeding.
replaceable_block = viewer.split("public Type ReplaceableInterface =>", 1)[1].split(";", 1)[0]
assert "null" in replaceable_block
assert "typeof(IMoneyModule)" not in replaceable_block

selection_pos = viewer.find('"economymodule"')
enabled_pos = viewer.find('config?.Configs["NexEconomyViewer"]')
assert selection_pos >= 0 and enabled_pos >= 0 and selection_pos < enabled_pos

register_pos = viewer.find("scene.RegisterModuleInterface<IMoneyModule>")
balance_hook_pos = viewer.find("client.OnMoneyBalanceRequest +=")
assert register_pos >= 0 and balance_hook_pos >= 0

# HG wallet onboarding is opt-in and derived only from the trusted
# scene agent circuit. It must never rely on a viewer-supplied home URL.
hg_identity = Path("NexVerse/Core/Economy/NexHypergridWalletIdentity.cs").read_text()
hg_service = Path("NexVerse/Core/Economy/NexEconomyService.cs").read_text()
hg_tests = Path("tools/ci/NexEconomyPolicyRegression/Program.cs").read_text()
for marker in ("CanonicalHome(", "Reference(", "AccountId(",
               "opengenesislink:nvd:hypergrid:wallet:v1",
               "SHA256.HashData"):
    assert marker in hg_identity, "Missing namespaced HG identity: " + marker
for marker in ("EnsureHypergridResidentAccount(",
               'existing.Reference, reference', "NexLedgerSide.Credit"):
    assert marker in hg_service, "Missing HG wallet policy: " + marker
for marker in ("/api/v1/economy/hypergrid/wallets/ensure",
               "hypergrid_service_required",
               "HandleEnsureHypergridWallet(", "account != null"):
    assert marker in api, "Unprotected HG wallet API: " + marker
for marker in ("GetAgentCircuitData(avatarId)",
               "circuit.SessionID != sp.ControllingClient.SessionId",
               'TryGetValue("HomeURI", out object rawHome)',
               "NexHypergridWalletIdentity.AccountId(",
               "m_AllowHypergridGuests", "TryResolveWallet(fromUser, true",
               "TryResolveWallet(toUser, false",
               "TryResolveWallet(buyer, true", "TryResolveWallet(seller, false",
               "HG NV$ debits require a live verified session"):
    assert marker in viewer, "HG user wallet not safely mapped: " + marker
assert "AllowHypergridGuests = false" in Path("bin/OpenSim.ini").read_text()
for marker in ("HG origin isolation/canonicalization failed",
               "HG transfer ledger balance mismatch",
               "HG account allowed overdraft"):
    assert marker in hg_tests, "Missing HG wallet regression: " + marker

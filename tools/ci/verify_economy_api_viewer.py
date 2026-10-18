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
assert "AllowHypergridGuests = true" in viewer_config

# Regression: ViaHGLogin also marks local residents returning from a
# Hypergrid visit. A returnee must use the local avatar UUID, never create
# an HG wallet keyed by a self-home URI; a foreign collision must still fail.
wallet_resolver = viewer.split("private bool TryResolveWallet(", 1)[1].split(
    "private bool EnsureHypergridServiceWallet(", 1
)[0]
local_branch = wallet_resolver.split("if (hasLocalAccount)", 1)[1].split(
    "bool guest =", 1
)[0]
foreign_branch = wallet_resolver.split("bool guest =", 1)[1]
for marker in (
    "scene.UserAccountService?",
    ".GetUserAccount(scene.RegionInfo.ScopeID, avatarId)",
    "account != null && account.LocalToGrid",
    "scene.SceneGridInfo.IsLocalHome(home) == 1",
    "bool viaHypergrid =",
    "if (hasHome && !localHome)",
    "if (viaHypergrid && !localHome)",
    "m_HypergridWallets.TryGetValue(avatarId,",
    "NexHypergridWalletIdentity.AccountId(",
    "if (!localHome || cached != localHomeWallet)",
    "m_HypergridWallets.Remove(avatarId)",
):
    assert marker in wallet_resolver, f"Missing HG-home/local-ID wallet guard: {marker}"
assert "return true;" in local_branch
assert 'reason = "Ambiguous avatar UUID across grid identities.";' in local_branch
assert 'reason = "Local avatar UUID has a non-local HG HomeURI.";' in local_branch
assert 'reason = "Local HG return lacks verified local HomeURI.";' in local_branch
assert "EnsureHypergridServiceWallet(" not in local_branch
assert "m_HypergridWallets[avatarId] = expected;" in foreign_branch
assert "EnsureHypergridServiceWallet(avatarId, home," in foreign_branch
assert 'reason = "Local HG HomeURI without a local user account.";' in foreign_branch
assert "if (!m_AllowHypergridGuests)" in foreign_branch
assert "if (!hasHome)" in foreign_branch
assert 'reason = "Conflicting verified Hypergrid homes for avatar UUID.";' in foreign_branch
assert "No authoritative local account for active avatar." in foreign_branch

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

# HG wallet onboarding is explicitly enabled in shipped simulator templates,
# but each wallet still requires an authenticated, verified HG circuit.
# It must never rely on a viewer-supplied home URL.
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
assert "AllowHypergridGuests = true" in Path("bin/OpenSim.ini").read_text()
assert "AllowHypergridGuests = true" in Path("bin/OpenSim.ini.example").read_text()
# Missing option still defaults to denied in the code; explicit INI opts in.
assert 'economy.GetBoolean("AllowHypergridGuests", false)' in viewer
for marker in ("HG origin isolation/canonicalization failed",
               "HG transfer ledger balance mismatch",
               "HG account allowed overdraft"):
    assert marker in hg_tests, "Missing HG wallet regression: " + marker

# Master Bank: object/script authority, prefunded separate wallet,
# explicit operator debit grant, no LSL-side API key or minting.
master_policy = Path("NexVerse/Core/Economy/NexMasterBankPayoutPolicy.cs").read_text()
master_intf = Path("OpenSim/Region/Framework/Interfaces/INexMasterBankPayoutModule.cs").read_text()
lsl = Path("OpenSim/Region/ScriptEngine/Shared/Api/Implementation/LSL_Api.cs").read_text()
regression = Path("tools/ci/NexEconomyPolicyRegression/Program.cs").read_text()
for marker in ("IsAuthorized(", "CanPayout(", "objectUUID/scriptItemUUID",
               "ownerId == OperatorId", "amount <= MaxAmount"):
    assert marker in master_policy, "Master Bank policy missing: " + marker
for marker in ("INexMasterBankPayoutModule", "IsAuthorizedBankScript(",
               "TryBankPayout("):
    assert marker in master_intf and marker in viewer, "Missing native bank script boundary: " + marker
for marker in ("MasterBankEnabled", "MasterBankAccountId", "MasterBankOperatorId",
               "MasterBankAuthorizedScripts", "MasterBankMaxPayout",
               '"/api/v1/banking/transfers"', "EnsureMasterBankAccount(",
               "GetAgentCircuitData(avatarId)", "IsAuthorizedBankScript(objectId"):
    assert marker in viewer, "Incomplete bank payout implementation: " + marker
for marker in ("INexMasterBankPayoutModule", "llNexBankPayout(",
               "bank.TryBankPayout(", "m_item.PermsGranter != ownerId",
               "MASTER_BANK_SCRIPT_NOT_AUTHORIZED"):
    assert marker in lsl, "Master bank lacks dedicated LSL payout: " + marker
assert "llNexBankPayout(" in Path(
    "OpenSim/Region/ScriptEngine/Shared/Api/Interface/ILSL_Api.cs").read_text()
assert "llNexBankPayout(" in Path(
    "OpenSim/Region/ScriptEngine/Shared/Api/Runtime/LSL_Stub.cs").read_text()
assert "llNexBankPayout(TARGET_AVATAR, AMOUNT_NV)" in Path(
    "tools/diagnostics/ogl_master_bank_payout_test.lsl").read_text()
assert "TryBankPayout(" not in lsl.split(
    "public LSL_Integer llGiveMoney(", 1)[1].split(
    "public LSL_Key llNexBankPayout(", 1)[0]
assert "TryBankPayout(" not in lsl.split(
    "public LSL_Key llTransferLindenDollars(", 1)[1].split("#endregion", 1)[0]
for marker in ("unauthorized Master Bank payout delegation accepted",
               "master bank could not payout to HG resident wallet",
               "master bank created unbacked money"):
    assert marker in regression, "Missing bank payout regression: " + marker
for p in ("bin/OpenSim.ini", "bin/OpenSim.ini.example"):
    config = Path(p).read_text()
    assert "MasterBankEnabled = false" in config
    expected_bank_id = ('6571e388-6218-4574-87db-f9379718315e'
                        if p == "bin/OpenSim.ini" else '')
    assert f'MasterBankAccountId = "{expected_bank_id}"' in config
    assert 'MasterBankAuthorizedScripts = ""' in config
assert "NEXVERSE_ECONOMY_API_KEY" not in Path("tools/diagnostics/ogl_master_bank_payout_test.lsl").read_text()

# NexVerse GRID SERVICES is the explicit default bank identity, not an
# implicit operator or unrestricted script payout authorization.
master_bank_id = "6571e388-6218-4574-87db-f9379718315e"
constants = Path("OpenSim/Framework/Constants.cs").read_text()
assert f'servicesAgentIDString = "{master_bank_id}"' in constants
profile = Path("bin/OpenSim.ini").read_text()
assert f'MasterBankAccountId = "{master_bank_id}"' in profile
assert "MasterBankEnabled = false" in profile
assert 'MasterBankOperatorId = ""' in profile
assert 'MasterBankAuthorizedScripts = ""' in profile

# The registration helper reports IDs but may never pay or authorize.
for filename in ("OpenSim/Region/ScriptEngine/Shared/Api/Interface/ILSL_Api.cs",
                 "OpenSim/Region/ScriptEngine/Shared/Api/Runtime/LSL_Stub.cs",
                 "OpenSim/Region/ScriptEngine/Shared/Api/Implementation/LSL_Api.cs"):
    assert "llNexBankRegistrationInfo(" in Path(filename).read_text()
registration = lsl.split("public LSL_String llNexBankRegistrationInfo()", 1)[1].split(
    "public LSL_Key llNexBankPayout(", 1)[0]
for guard in ("m_item.PermsGranter != root.OwnerID",
              "ScriptBaseClass.PERMISSION_DEBIT",
              "m_host.UUID != root.UUID",
              "!root.TaskInventory.ContainsKey(m_item.ItemID)",
              'root.UUID.ToString() + "/" + m_item.ItemID.ToString()'):
    assert guard in registration, "unsafe bank registration helper: " + guard
assert "TryBankPayout" not in registration
assert "llNexBankRegistrationInfo()" in Path(
    "tools/diagnostics/ogl_master_bank_payout_test.lsl").read_text()

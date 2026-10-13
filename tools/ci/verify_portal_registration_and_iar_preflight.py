#!/usr/bin/env python3
"""Reject missing account/asset safety checks in the portal/IAR additions."""
from pathlib import Path

def text(name):
    return Path(name).read_text(encoding="utf-8")

portal = text("NexVerse/Server/Api/NexPortalRegistrationApi.cs")
connector = text("NexVerse/Server/Api/NexVerseWorldApiConnector.cs")
registration = text("OpenSim/Services/UserAccountService/UserAccountService.cs")
interface = text("OpenSim/Services/Interfaces/IUserAccountService.cs")
scope = text("NexVerse/Core/Security/NexSecurity.cs")
guard = text("OpenSim/Region/CoreModules/Avatar/Inventory/Archiver/OglIarImportGuard.cs")
archiver = text("OpenSim/Region/CoreModules/Avatar/Inventory/Archiver/InventoryArchiverModule.cs")
proj = text("OpenSim/Region/CoreModules/OpenSim.Region.CoreModules.csproj")

for cfg_path in ("bin/Robust.HG.ini", "bin/Robust.HG.ini.example"):
    cfg = text(cfg_path)
    assert cfg.count("[NexPortalRegistration]") == 1, cfg_path
    assert cfg.count("[OglAvatarTemplates]") == 1, cfg_path
    portal_cfg = cfg.split("[NexPortalRegistration]", 1)[1].split("\n[", 1)[0]
    templates = cfg.split("[OglAvatarTemplates]", 1)[1].split("\n[", 1)[0]
    assert "Enabled = false" in portal_cfg
    assert "TrustedTlsProxy = false" in portal_cfg
    assert "RequirePublishedModels = true" in templates
    assert "Enabled = false" in templates
    assert 'BenjaminPublishedModel = ""' in templates
    assert 'LunaPublishedModel = ""' in templates

for marker in (
    '"/api/v1/portal/avatars/register"',
    'apiGate.Wrap(portalRegistrationApi.Handle)',
    'portalRegistration?.GetBoolean("Enabled", false)',
):
    assert marker in connector, "Portal endpoint missing " + marker
for marker in (
    'NexScopes.PortalRegister',
    'request.Headers?["X-NexVerse-Api-Key"]',
    'request.IsSecured',
    '"X-Forwarded-Proto"',
    'm_ProxyIp.Equals(request.RemoteIPEndPoint?.Address)',
    'm_RateLimiter.TryConsume(',
    'first_name', 'last_name', 'email', 'password', 'gender',
    'last = "Resident"',
    'model = "ogl:benjamin"',
    'model = "ogl:luna"',
    'displayModel = "Benjamin Resident"',
    'displayModel = "Luna Resident"',
    'm_Starter.CreateOglStarterAccount(',
    'if (m_Accounts.GetUserAccount(UUID.Zero, first, last) != null)',
    'HttpStatusCode.Conflict',
    'HttpStatusCode.Created',
    'HttpStatusCode.ServiceUnavailable',
):
    assert marker in portal, "Portal safety/contract marker missing: " + marker
assert "portal:avatars:register" in scope
assert "IOglStarterAccountRegistration" in interface
assert "CreateOglStarterAccount(" in registration
assert 'if (!string.Equals(modelId, "ogl:benjamin"' in registration
assert "CompensateFailedOglStarterRegistration(" in registration
assert "CreateDefaultAppearanceEntries(" in registration

for marker in (
    'MaxCompressedBytes',
    'MaxExpandedBytes',
    'MaxSingleAssetBytes',
    'MaxInventoryItemBytes',
    'MaxEntries',
    'MaxAssets',
    'DtdProcessing = DtdProcessing.Prohibit',
    'XmlResolver = null',
    'GZipStream',
    'declaredChecksum != actualChecksum',
    'names.Add(name.TrimEnd',
    'IsAllowedPath(name, directory)',
    'Uri.UnescapeDataString(raw)',
    'MaxCharactersInDocument',
    'expanded > archiveLength * 500L',
):
    assert marker in guard, "IAR protective check missing: " + marker
assert proj.count("OglIarImportGuard.cs") == 1
assert archiver.count("OglIarImportGuard.TryValidate(") == 3, (
    "Guard must cover all three independent IAR import entrypoints."
)
for call in ("new InventoryArchiveReadRequest(", "request.Execute()"):
    assert call in archiver
assert "OglIarPreflightRegression" not in guard
print("Stadtportal avatar registration and IAR preflight structural contracts: PASS")

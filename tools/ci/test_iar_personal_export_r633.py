#!/usr/bin/env python3
"""R633 defensive source-contract checks; compiled/E2E tests run separately."""
from pathlib import Path
import unittest

ROOT=Path(__file__).resolve().parents[2]
API=(ROOT/'NexVerse/Server/Api/OglIarApi.cs').read_text()
CONNECTOR=(ROOT/'NexVerse/Server/Api/NexVerseWorldApiConnector.cs').read_text()
NODE=(ROOT/'NexVerse/RegionModules/NodeAgent/NexVerseNodeAgentModule.cs').read_text()
ARCHIVER=(ROOT/'NexVerse/RegionModules/Archives/OglIarOperationManager.cs').read_text()


class OwnerExportR633(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.export=API.split('private void StartPersonalExport(',1)[1].split('private bool Authenticate(',1)[0]

    def test_routed_only_after_personal_auth(self):
        handler=API.split('private void HandlePersonal(',1)[1].split('private static bool OnlyConfirmation(',1)[0]
        self.assertLess(handler.index('m_Authenticator.TryAuthenticate'),handler.index('StartPersonalExport(request,response,account)'))
        self.assertIn('account == null || account.PrincipalID == UUID.Zero',handler)
        self.assertIn('NexScopes.InventoryRead',handler)
        self.assertIn('NexScopes.RegionsManage',API)

    def test_server_derived_region_not_client_supplied(self):
        for needle in ('m_GridUsers?.GetGridUserInfo(owner.PrincipalID.ToString())',
                       'info.UserID,owner.PrincipalID.ToString()',
                       'new[]{info.LastRegionID,info.HomeRegionID}',
                       'm_Nodes.FindNodeForRegion(candidate.ToString())',
                       'found.State,"online"', '!found.MaintenanceMode', '!found.Draining',
                       'm_Features.Permitted(found.NodeId,OglArchiveFeatureGate.IarExport)'):
            self.assertIn(needle,self.export)
        self.assertIn('featureControl, gridUsers',CONNECTOR)
        self.assertNotIn('"region_id",out',self.export)
        self.assertNotIn('"user_id",out',self.export)

    def test_explicit_confirmation_bounded_json_and_safe_filename(self):
        for needle in ('request.HttpMethod.Equals("POST"',
                       '"application/json"', 'char[] buffer=new char[257]',
                       'read>256', 'JsonDocument.Parse(new string(buffer,0,read))',
                       'OnlyConfirmation(root)', 'if(!confirm)',
                       'Guid id=Guid.NewGuid()', 'id.ToString("N")+".iar"'):
            self.assertIn(needle,self.export)

    def test_owned_job_has_simulator_target_and_no_download_claim(self):
        for needle in ('RegisterOperation("archive.iar.export"',
                       '["personal_iar_owner"]=userId', '["user_id"]=userId',
                       '["inventory_path"]="/"', 'm_Bus.Publish(new NexEvent("archive.iar.requested"',
                       '["target_node_id"]=node.NodeId', 'HttpStatusCode.Accepted',
                       'Der private Download ist noch nicht verfügbar'):
            self.assertIn(needle,self.export)
        for needle in ('archive.iar.requested', 'operations.StartExport(operationId, user, inventoryPath, fileName,'):
            self.assertIn(needle,NODE)
        self.assertIn('new(File.Create(path), CompressionLevel.Optimal)',ARCHIVER)
        self.assertNotIn('File.Create(',self.export)

    def test_incomplete_file_handoff_cannot_claim_export_complete(self):
        self.assertIn('"personal_iar_backend_unavailable"',API)
        self.assertIn('"Unbekannter persönlicher IAR-Endpunkt."',API)
        self.assertIn('"queued"',self.export)
        self.assertNotIn('status="completed"',self.export)


if __name__=='__main__':
    unittest.main()
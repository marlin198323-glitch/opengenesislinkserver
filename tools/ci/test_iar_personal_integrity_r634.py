#!/usr/bin/env python3
"""Fail-closed static contract for R634; actual C# build and E2E remain required."""
from pathlib import Path
import unittest

ROOT=Path(__file__).resolve().parents[2]
MANAGER=(ROOT/'NexVerse/RegionModules/Archives/OglIarOperationManager.cs').read_text()
NODE=(ROOT/'NexVerse/RegionModules/NodeAgent/NexVerseNodeAgentModule.cs').read_text()
API=(ROOT/'NexVerse/Server/Api/OglIarApi.cs').read_text()
BRIDGE=(ROOT/'NexVerse/Core/Jobs/OglJobEventBridge.cs').read_text()

class IntegrityR634(unittest.TestCase):
    def test_owner_marking_and_restricted_archive_name(self):
        for clause in ('["personal_iar_owner"]=userId',
                       '"resident-"+personalId.ToString("N")+".iar"',
                       '"invalid_personal_iar_command"',
                       'string.Equals(inventoryPath,"/",StringComparison.Ordinal)',
                       'string.Equals(personalOwner,userRaw,StringComparison.OrdinalIgnoreCase)'):
            self.assertIn(clause,API+NODE)
        self.assertIn('"personal_iar",true',NODE)
        self.assertIn('"personal_iar_owner"',API)

    def test_actual_closed_archive_is_hashed_only_after_validation(self):
        saved=MANAGER.split('private void HandleSaved(',1)[1].split('private void HandleLoaded(',1)[0]
        for needle in ('stream?.Dispose()', 'OglIarInspector.Inspect(operation.ArchivePath)',
                       'if (operation.IsPersonalExport)', 'new(operation.ArchivePath)',
                       'FileAttributes.ReparsePoint', 'info.Length > MaxPersonalArchiveBytes',
                       'new(operation.ArchivePath,FileMode.Open,FileAccess.Read',
                       'FileShare.Read,81920,FileOptions.SequentialScan',
                       'SHA256.Create()', 'hasher.ComputeHash(archive)',
                       'operation.ArchiveBytes=info.Length',
                       'operation.ArchiveSha256=digest', 'Complete(operation)'):
            self.assertIn(needle,saved)
        self.assertLess(saved.index('if (!operation.Inspection.Valid)'),saved.index('hasher.ComputeHash(archive)'))
        self.assertLess(saved.index('hasher.ComputeHash(archive)'),saved.index('Complete(operation)'))

    def test_limit_and_integrity_fail_closed(self):
        self.assertIn('512L * 1024 * 1024',MANAGER)
        for marker in ('personal_iar_archive_size_or_path_invalid',
                       'personal_iar_archive_integrity_failed',
                       'personal_iar_archive_integrity_unavailable'):
            self.assertIn(marker,MANAGER)

    def test_only_small_manifest_ever_uses_nexbus(self):
        fragment=NODE.split('private void PublishIarState(',1)[1].split('private void HandleOarCommand(',1)[0]
        for field in ('archive_bytes', 'archive_sha256', 'archiveBytes<=512L*1024*1024',
                      'archiveSha256.Length==64', 'char.IsAsciiHexDigit(c)'):
            self.assertIn(field,fragment)
        for forbidden in ('archive_content', 'base64', 'File.ReadAllBytes', 'ArchivePath', 'private_file'):
            self.assertNotIn(forbidden,fragment)
        self.assertIn('CopyResult(e.Data)',BRIDGE)

    def test_completed_job_cannot_fake_downloadable_backup(self):
        personal=API.split('private void HandlePersonal(',1)[1].split('private static bool OnlyConfirmation(',1)[0]
        self.assertIn('status=job.State==OglJobState.Completed ? "awaiting_transfer"',personal)
        self.assertIn('progress=job.State==OglJobState.Completed ? 99',personal)
        self.assertNotIn('"completed"',personal)
        self.assertIn('"Unbekannter persönlicher IAR-Endpunkt."',personal)
        self.assertNotIn('Json(response,HttpStatusCode.OK,job)',personal)

if __name__=='__main__':
    unittest.main()
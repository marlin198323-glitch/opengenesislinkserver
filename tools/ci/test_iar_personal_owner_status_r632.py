#!/usr/bin/env python3
"""R632 source-level fail-closed guards. The full C# build runs separately."""
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
API = (ROOT / 'NexVerse/Server/Api/OglIarApi.cs').read_text()
AUTH = (ROOT / 'NexVerse/Server/Api/NexVerseUserApi.cs').read_text()
JOB = (ROOT / 'NexVerse/Core/Jobs/OglJobStore.cs').read_text()


class PersonalIarOwnerStatusR632(unittest.TestCase):
    def test_owner_router_before_administrator_gate(self):
        self.assertLess(API.index('path.StartsWith("/api/v1/iar/me/"'),
                        API.index('if(!Authenticate(request,response)) return;'))
        self.assertIn('HandlePersonal(request,response,path)', API)
        self.assertIn('NexScopes.RegionsManage', API)
        self.assertIn('NexScopes.InventoryRead', API)
        self.assertIn('NexScopes.InventoryWrite', API)

    def test_requires_real_owner_not_service_key(self):
        body = API.split('private void HandlePersonal(', 1)[1].split('private bool Authenticate(', 1)[0]
        for token in ('m_Authenticator.TryAuthenticate(request,scope',
                      'account == null', 'account.PrincipalID == UUID.Zero',
                      'principal.Subject,account.PrincipalID.ToString()',
                      'job.Metadata.TryGetValue("user_id",out string ownerId)',
                      'ownerId,account.PrincipalID.ToString()',
                      'job.Metadata.TryGetValue("personal_iar_owner",out string personalOwnerId)',
                      'personalOwnerId,account.PrincipalID.ToString()'):
            self.assertIn(token, body)
        self.assertIn('if (!string.IsNullOrWhiteSpace(apiKey))', AUTH)
        self.assertIn('account = null;', AUTH)

    def test_status_never_returns_job_snapshots_or_secrets(self):
        body = API.split('private void HandlePersonal(', 1)[1].split('private bool Authenticate(', 1)[0]
        self.assertIn('m_Jobs.Get(operationId.ToString())', body)
        self.assertIn('archive.iar.export', body)
        self.assertIn('archive.iar.import', body)
        self.assertIn('HttpStatusCode.NotFound,"operation_not_found"', body)
        self.assertIn('status=job.State.ToString().ToLowerInvariant()', body)
        self.assertIn('Math.Clamp(job.ProgressPercent,0,100)', body)
        self.assertNotIn('Json(response,HttpStatusCode.OK,job)', body)
        for dangerous in ('job.Message', 'job.Logs', 'job.Metadata)', 'job.Result', 'job.Actor'):
            self.assertNotIn(dangerous, body)
        for field in ('public string Type', 'public IReadOnlyDictionary<string,string> Metadata',
                      'public int ProgressPercent'):
            self.assertIn(field, JOB)

    def test_import_still_fails_closed_until_restore_backend_is_ready(self):
        body = API.split('private void HandlePersonal(', 1)[1].split('private static bool OnlyConfirmation(', 1)[0]
        self.assertIn('"personal_iar_backend_unavailable"', body)
        self.assertIn('HttpStatusCode.ServiceUnavailable', body)
        self.assertIn('Es wurde kein Backup erstellt.', body)
        self.assertNotIn('RegisterOperation(', body)
        self.assertNotIn('m_Bus.Publish(', body)
        self.assertNotIn('File.Create(', body)


if __name__ == '__main__':
    unittest.main()
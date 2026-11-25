import io,json,tempfile,time,unittest
from pathlib import Path
from unittest.mock import patch
from petforge.server import create_app
from fixtures import FakeProvider,cat,png


class ServerTests(unittest.TestCase):
    def setUp(self):
        self.tmp=tempfile.TemporaryDirectory();root=Path(self.tmp.name);self.runtime=root/'runtime.exe';self.runtime.write_bytes(b'MZ-fixture')
        self.app=create_app(root/'work',provider_factory=FakeProvider,runtime_factory=lambda:self.runtime);self.client=self.app.test_client()
        self.token=self.client.get('/api/config').json['csrf'];self.headers={'X-PetForge-Token':self.token}
    def tearDown(self):self.app.extensions['petforge_jobs'].close();self.tmp.cleanup()
    def data(self):return dict(name='团团',api_key='private-fixture-key',consent='true',files=(io.BytesIO(png(cat())),'../../cat.png'))
    def wait(self,identity):
        for _ in range(300):
            value=self.client.get('/api/jobs/'+identity).json
            if value['state'] in ('completed','failed','quality_failed'):return value
            time.sleep(.02)
        self.fail('job timed out')
    def test_upload_to_download_and_no_key_persistence(self):
        r=self.client.post('/api/jobs',data=self.data(),headers=self.headers);self.assertEqual(r.status_code,202)
        identity=r.json['id'];self.assertEqual(self.wait(identity)['state'],'completed')
        response=self.client.get('/api/jobs/'+identity+'/package');self.assertEqual(response.status_code,200);response.close()
        for file in Path(self.tmp.name).rglob('*.json'):self.assertNotIn('private-fixture-key',file.read_text(encoding="utf-8"))
    def test_csrf_and_foreign_origins_blocked(self):
        self.assertEqual(self.client.post('/api/jobs',data=self.data()).status_code,403)
        self.assertEqual(self.client.post('/api/jobs',data=self.data(),headers={**self.headers,'Origin':'https://evil.invalid'}).status_code,403)
    def test_dns_rebinding_host_blocked(self):
        self.assertEqual(self.client.get('/api/config',headers={'Host':'evil.invalid'}).status_code,403)
    def test_missing_consent_and_unsupported_files_rejected(self):
        value=self.data();value['consent']='false';self.assertEqual(self.client.post('/api/jobs',data=value,headers=self.headers).status_code,400)
        value=self.data();value['files']=(io.BytesIO(b'bad'),'pet.exe');self.assertEqual(self.client.post('/api/jobs',data=value,headers=self.headers).status_code,400)
    def test_cannot_read_arbitrary_files(self):
        self.assertEqual(self.client.get('/api/jobs/../package').status_code,400)
        self.assertNotEqual(self.client.get('/api/jobs/'+'a'*32+'/credentials').status_code,200)
    def test_completed_order_cannot_run_again(self):
        identity=self.client.post('/api/jobs',data=self.data(),headers=self.headers).json['id'];self.wait(identity)
        r=self.client.post('/api/jobs/'+identity+'/retry',json={'api_key':'fixture'},headers=self.headers)
        self.assertEqual(r.status_code,400)
    def test_api_key_never_echoed_in_config_or_status(self):
        with patch.dict('os.environ',{'OPENAI_API_KEY':'private-environment-key'}):
            r=self.client.get('/api/config');self.assertTrue(r.json['hasEnvironmentKey']);self.assertNotIn('private-environment-key',r.text)

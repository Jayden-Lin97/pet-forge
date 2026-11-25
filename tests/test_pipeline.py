import io,json,os,tempfile,unittest,zipfile
from pathlib import Path
from unittest.mock import Mock
from petforge.errors import AmbiguousRequest,ForgeError,QualityError
from petforge.pipeline import Pipeline,write_json
from fixtures import FakeProvider,cat,png,gif


class PipelineTests(unittest.TestCase):
    def setUp(self):
        self.tmp=tempfile.TemporaryDirectory();self.root=Path(self.tmp.name);(self.root/'inputs').mkdir()
        (self.root/'inputs/0.png').write_bytes(png(cat()));self.runtime=self.root/'runtime.exe';self.runtime.write_bytes(b'MZ-test-fixture-not-a-real-app')
        self.spec=dict(id='a'*32,name='团团',inputs=['inputs/0.png'],demo=True)
        self.provider=FakeProvider();self.progress=[];self.pipeline=Pipeline(self.provider,runtime_factory=lambda:self.runtime)
    def tearDown(self):self.tmp.cleanup()
    def run_pipeline(self):self.pipeline.generate(self.root,self.spec,lambda *v:self.progress.append(v))
    def test_entire_delivery_is_complete_and_has_no_reference_or_key(self):
        self.run_pipeline();self.assertEqual(self.progress[-1][0],'completed')
        with zipfile.ZipFile(self.root/'desktop-pet.zip') as z:
            self.assertEqual(len(z.namelist()),8);self.assertFalse(any('inputs' in p for p in z.namelist()))
            self.assertEqual(json.loads(z.read('桌宠资料/pet.json'))['name'],'团团')
            self.assertEqual(len(json.loads(z.read('桌宠资料/motions.json'))['clips']),3)
        self.assertFalse(json.loads((self.root/'delivery.json').read_text(encoding="utf-8"))['providerLiveVerified'])
    def test_resume_does_not_repeat_paid_generations(self):
        self.run_pipeline();first=self.provider.calls.count('gif');self.run_pipeline()
        self.assertEqual(self.provider.calls.count('gif'),first);self.assertEqual(self.provider.calls.count('png'),1)
    def test_quality_failure_never_produces_package(self):
        self.provider.review=lambda *a:{'passed':False,'reason':'floating hind leg'}
        with self.assertRaises(QualityError):self.run_pipeline()
        self.assertFalse((self.root/'desktop-pet.zip').exists());self.assertEqual(self.provider.calls.count('png'),2)
    def test_runtime_failure_before_paid_api(self):
        self.pipeline.runtime_factory=Mock(side_effect=ForgeError('runtime missing'))
        with self.assertRaises(ForgeError):self.run_pipeline()
        self.assertEqual(self.provider.calls,[])
    def test_modified_reviewed_asset_not_delivered(self):
        self.run_pipeline();(self.root/'assets/motion-groom.png').write_bytes(b'changed')
        with self.assertRaises(QualityError):self.run_pipeline()
    def test_uncertain_checkpoint_requires_explicit_retry(self):
        self.provider.edit=Mock(side_effect=AmbiguousRequest('timeout'))
        with self.assertRaises(AmbiguousRequest):self.run_pipeline()
        with self.assertRaises(AmbiguousRequest):self.run_pipeline()
        self.assertEqual(self.provider.edit.call_count,1)
    def test_five_action_mode(self):
        self.spec['extended']=True;self.run_pipeline()
        self.assertEqual(len(json.loads((self.root/'assets/motions.json').read_text(encoding="utf-8"))['clips']),5)

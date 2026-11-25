import os,tempfile,unittest,json
from pathlib import Path
from petforge.packaging import runtime_path
from petforge.pipeline import Pipeline
from fixtures import FakeProvider,cat,png


@unittest.skipUnless(os.name=='nt','Native Windows asset loader')
class NativeTests(unittest.TestCase):
    def test_generated_gifs_load_in_real_windows_runtime(self):
        runtime=runtime_path()
        self.assertTrue((runtime.parent/'ValidateBundle.exe').exists())
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp);(root/'input.png').write_bytes(png(cat()))
            Pipeline(FakeProvider(),runtime_factory=lambda:runtime).generate(root,dict(id='b'*32,name='Fixture',inputs=['input.png'],demo=True,extended=True),lambda *a:None)
            self.assertTrue(json.loads((root/'delivery.json').read_text(encoding="utf-8"))['automatedQualityPassed'])

import subprocess,tempfile,unittest
from pathlib import Path
import imageio_ffmpeg
from petforge.media import references,decode_image
from petforge.errors import ForgeError
from fixtures import cat,png


class MediaTests(unittest.TestCase):
    def test_video_only_input_extracts_real_decoded_reference(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp);source=root/'cat.png';source.write_bytes(png(cat()))
            video=root/'clip.mp4';r=subprocess.run([imageio_ffmpeg.get_ffmpeg_exe(),'-loglevel','error','-loop','1','-i',str(source),'-t','1','-pix_fmt','yuv420p',str(video)],capture_output=True)
            self.assertEqual(r.returncode,0)
            refs=references([video],root/'frames');self.assertGreaterEqual(len(refs),1);self.assertTrue(refs[0].startswith(b'\x89PNG'))
    def test_bad_and_tiny_image_rejected(self):
        from PIL import Image
        for raw in (b'not image',png(Image.new('RGBA',(1,1)))):
            with self.assertRaises(ForgeError):decode_image(raw)
    def test_more_than_thirty_inputs_rejected(self):
        with self.assertRaises(ForgeError):references([Path('x')]*31,Path('unused'))

import io,tempfile,unittest
from pathlib import Path
from PIL import Image
from petforge.errors import QualityError
from petforge.quality import canonical,decode_animation,pack_atlas,contact_sheet
from fixtures import cat,gif,png


class QualityTests(unittest.TestCase):
    def setUp(self):self.base=canonical(png(cat()))
    def test_transparent_canonical_has_grounded_margin(self):
        self.assertEqual(self.base.size,(256,256));self.assertEqual(self.base.getbbox()[3],235)
    def test_opaque_background_rejected(self):
        with self.assertRaises(QualityError):canonical(png(cat().convert('RGB')))
    def test_clipped_subject_rejected(self):
        image=cat();image.paste((90,90,90,255),(0,0,30,512))
        with self.assertRaises(QualityError):canonical(png(image))
    def test_all_native_frames_pack_in_original_order(self):
        frames,fps,report=decode_animation(gif(self.base),self.base)
        self.assertEqual(len(frames),16);self.assertAlmostEqual(fps,1000/120)
        with tempfile.TemporaryDirectory() as tmp:
            spec=pack_atlas(frames,self.base,'groom',Path(tmp),fps)
            atlas=Image.open(Path(tmp)/spec['sheet'])
            self.assertEqual(atlas.crop((4*256,0,5*256,256)).tobytes(),frames[0].tobytes())
            self.assertEqual(spec['transitionSource'],'generated-gif');self.assertFalse(report['reversePlayback'])
        self.assertTrue(contact_sheet(frames).startswith(b'\x89PNG'))
    def test_floating_frame_rejected(self):
        with self.assertRaises(QualityError):decode_animation(gif(self.base,'floating'),self.base)
    def test_inconsistent_endpoint_rejected(self):
        with self.assertRaises(QualityError):decode_animation(gif(self.base,'bad_end'),self.base)
    def test_static_image_is_not_motion(self):
        with self.assertRaises(QualityError):decode_animation(png(self.base),self.base)
    def test_unchanging_gif_is_not_motion(self):
        with self.assertRaises(QualityError):decode_animation(gif(self.base,'static'),self.base)

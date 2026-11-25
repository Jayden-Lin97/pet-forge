import base64,unittest
from unittest.mock import Mock
import requests
from petforge.errors import ForgeError,AmbiguousRequest
from petforge.provider import OpenAIProvider


class ProviderTests(unittest.TestCase):
    def setUp(self):self.p=OpenAIProvider('fixture-secret');self.request=Mock();self.p.session.request=self.request
    def reply(self,value,status=200):self.request.return_value=Mock(status_code=status,json=Mock(return_value=value))
    def test_gif_contract_and_no_redirect(self):
        self.reply({'data':[{'b64_json':base64.b64encode(b'GIF89a').decode()}]})
        self.assertEqual(self.p.edit([b'reference'],'move',animated=True),b'GIF89a')
        _,kwargs=self.request.call_args;self.assertFalse(kwargs['allow_redirects']);self.assertEqual(kwargs['data']['frame_count'],'16');self.assertEqual(kwargs['data']['frame_duration_ms'],'120')
    def test_auth_error_never_exposes_provider_body(self):
        self.reply({'error':'fixture-secret'},401)
        with self.assertRaises(ForgeError) as result:self.p.preflight()
        self.assertNotIn('fixture-secret',str(result.exception));self.assertIn('鉴权',str(result.exception))
    def test_timeout_has_no_automatic_paid_retry(self):
        self.request.side_effect=requests.Timeout()
        with self.assertRaises(AmbiguousRequest):self.p.edit([b'reference'],'move',animated=True)
        self.assertEqual(self.request.call_count,1)
    def test_png_is_not_accepted_as_animation(self):
        self.reply({'data':[{'b64_json':base64.b64encode(b'\x89PNG').decode()}]})
        with self.assertRaises(ForgeError):self.p.edit([b'r'],'move',animated=True)
    def test_missing_vision_verdict_not_passed(self):
        self.reply({'output':[{'content':[{'type':'output_text','text':'{"reason":"looks okay"}'}]}]})
        with self.assertRaises(ForgeError):self.p.review([],'review')
    def test_host_and_header_injection_blocked(self):
        for base in ('http://example.com/v1','https://user:pass@example.com/v1','https://example.com/v1?key=x'):
            with self.subTest(base=base),self.assertRaises(ForgeError):OpenAIProvider('fixture',base_url=base)
        with self.assertRaises(ForgeError):OpenAIProvider('fixture\nvalue')
    def test_close_removes_authentication_header(self):
        self.p.close();self.assertNotIn('Authorization',self.p.session.headers)

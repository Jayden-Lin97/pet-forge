"""OpenAI Images GIF + Responses vision, without keys in logs or checkpoints."""
from __future__ import annotations
import base64
import contextlib
import io
import json
import re
from urllib.parse import urlsplit
from PIL import Image
import requests
from .errors import AmbiguousRequest, ForgeError


class OpenAIProvider:
    def __init__(self, key: str, *, base_url="https://api.openai.com/v1",
                 image_model="gpt-image-2.6-sunburst", review_model="gpt-4.1-mini"):
        parsed = urlsplit(base_url)
        if parsed.scheme != "https" or not parsed.hostname or parsed.username or parsed.query or parsed.fragment:
            raise ForgeError("API 地址必须是受信任的 HTTPS 地址。")
        if not key or any(c in key for c in "\r\n"):
            raise ForgeError("请填写有效的 API Key。")
        for model in (image_model, review_model):
            if not re.fullmatch(r"[A-Za-z0-9_.:/-]{1,100}", model):
                raise ForgeError("模型名称无效。")
        self.base_url = base_url.rstrip("/")
        self.image_model, self.review_model = image_model, review_model
        self.session = requests.Session()
        self.session.headers.update(Authorization="Bearer " + key)
        self.request_count = 0

    def close(self):
        self.session.headers.pop("Authorization", None)
        self.session.close()

    def _request(self, method, path, **kwargs):
        # Redirects must never transfer a credential or a reference photo.
        try:
            self.request_count += 1
            response = self.session.request(method, self.base_url + path, allow_redirects=False,
                                           timeout=(15, 240), **kwargs)
        except requests.RequestException:
            if method != "GET":
                raise AmbiguousRequest("生成请求的返回结果不明，已停止。重试可能产生重复费用，请先检查 API 用量。") from None
            raise ForgeError("无法连接 API，请检查网络和 API 地址。") from None
        if response.status_code != 200:
            status = response.status_code
            messages = {401: "API Key 鉴权失败，请更新 Key。", 403: "当前项目没有所选模型的使用权限。",
                        404: "API 地址或模型不可用，请检查配置。", 429: "API 额度不足或达到限流，请检查账单与限制。"}
            raise ForgeError(messages.get(status, f"API 请求失败（HTTP {status}），未自动重复请求。"))
        try:
            result = response.json()
        except ValueError:
            raise AmbiguousRequest("API 返回内容无法读取，请先检查用量，避免重复生成。") from None
        if not isinstance(result, dict):
            raise ForgeError("API 返回格式不正确。")
        return result

    def preflight(self):
        self._request("GET", "/models/" + self.image_model)
        self._request("GET", "/models/" + self.review_model)

    def edit(self, references: list[bytes], prompt: str, *, animated=False) -> bytes:
        data = dict(model=self.image_model, prompt=prompt, n="1", quality="high",
                    size="512x512" if animated else "1024x1024",
                    output_format="gif" if animated else "png", background="transparent")
        if animated:
            data.update(frame_count="16", frame_duration_ms="120")
        files = [("image[]", (f"reference-{i}.png", raw, "image/png")) for i, raw in enumerate(references)]
        result = self._request("POST", "/images/edits", data=data, files=files)
        try:
            raw = base64.b64decode(result["data"][0]["b64_json"], validate=True)
        except (KeyError, IndexError, TypeError, ValueError):
            raise ForgeError("模型没有返回图像。请确认所选模型支持参考图和 GIF 输出。") from None
        if len(raw) > 50 * 1024**2:
            raise ForgeError("模型返回文件过大。")
        expected = b"GIF8" if animated else b"\x89PNG"
        if not raw.startswith(expected):
            raise ForgeError("模型没有返回指定的动画/透明 PNG 格式，未用静态图冒充动作。")
        return raw

    def review(self, images: list[bytes], instruction: str) -> dict:
        content = [{"type": "input_text", "text": instruction}]
        for raw in images:
            content.append({"type": "input_image", "image_url": "data:image/png;base64," + base64.b64encode(raw).decode()})
        result = self._request("POST", "/responses", json={
            "model": self.review_model, "store": False,
            "input": [{"role": "user", "content": content}],
            "text": {"format": {"type": "json_object"}}, "max_output_tokens": 1000,
        })
        try:
            text = "".join(c["text"] for item in result["output"] for c in item.get("content", []) if c.get("type") == "output_text")
            value = json.loads(text)
        except (KeyError, TypeError, ValueError):
            raise ForgeError("视觉检查未返回有效结果，未自动放行。") from None
        if not isinstance(value, dict) or type(value.get("passed")) is not bool:
            raise ForgeError("视觉检查结果缺少明确的通过状态。")
        return {"passed": value["passed"], "reason": str(value.get("reason", ""))[:500]}

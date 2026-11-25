from __future__ import annotations
from concurrent.futures import ThreadPoolExecutor
import json
from pathlib import Path
import re
import threading
import time
import uuid
from .errors import AmbiguousRequest, ForgeError, QualityError
from .pipeline import Pipeline, safe_reason, write_json


class Jobs:
    def __init__(self, root, provider_factory, runtime_factory):
        self.root = Path(root).resolve()
        self.root.mkdir(parents=True, exist_ok=True)
        self.provider_factory, self.runtime_factory = provider_factory, runtime_factory
        self.lock = threading.RLock()
        self.executor = ThreadPoolExecutor(max_workers=1, thread_name_prefix="petforge")
        self.active = set()
        for path in self.root.glob("*/job.json"):
            value = json.loads(path.read_text(encoding="utf-8"))
            if value["state"] in ("queued", "preparing", "generating", "packaging"):
                value.update(state="interrupted",message="程序已重启。重新提供 Key 后可恢复已完成的步骤。")
                write_json(path, value)

    def folder(self, identity):
        if not re.fullmatch(r"[a-f0-9]{32}", identity):
            raise ForgeError("任务编号无效。")
        return self.root / identity

    def get(self, identity):
        path = self.folder(identity) / "job.json"
        if not path.is_file():
            raise ForgeError("任务不存在。")
        with self.lock:
            return json.loads(path.read_text(encoding="utf-8"))

    def list(self):
        values = [json.loads(p.read_text(encoding="utf-8")) for p in self.root.glob("*/job.json")]
        return sorted(values, key=lambda v: v["createdAt"], reverse=True)[:20]

    def create(self, name, uploads, *, extended=False, demo=False):
        name = name.strip()
        if not 1 <= len(name) <= 24 or any(ord(c) < 32 for c in name):
            raise ForgeError("宠物名字需为1–24个正常字符。")
        identity = uuid.uuid4().hex
        folder = self.folder(identity)
        folder.mkdir(mode=0o700)
        (folder / "inputs").mkdir()
        value = dict(id=identity,name=name,createdAt=time.time(),updatedAt=time.time(),state="uploading",
                     message="接收素材",progress=0,extended=extended,demo=demo,inputs=[])
        from .media import IMAGE_EXTENSIONS, VIDEO_EXTENSIONS, MAX_FILES, MAX_TOTAL
        if not 1 <= len(uploads) <= MAX_FILES:
            raise ForgeError("请上传1–30个照片或视频。")
        total = 0
        for i, upload in enumerate(uploads):
            ext = Path(upload.filename).suffix.lower()
            if ext not in IMAGE_EXTENSIONS | VIDEO_EXTENSIONS:
                raise ForgeError("请上传 JPG、PNG、WebP、MP4、MOV、WebM 或 MKV。")
            limit = (80 if ext in VIDEO_EXTENSIONS else 20) * 1024**2
            name_on_disk = f"inputs/{i:02d}{ext}"
            count = 0
            with (folder/name_on_disk).open("wb") as target:
                while chunk := upload.stream.read(1024*1024):
                    count += len(chunk)
                    total += len(chunk)
                    if count > limit or total > MAX_TOTAL:
                        raise ForgeError("文件过大：图片20MB、视频80MB、合计200MB。")
                    target.write(chunk)
            if count == 0:
                raise ForgeError("上传文件为空。")
            value["inputs"].append(name_on_disk)
        write_json(folder/"job.json", value)
        return value

    def update(self, identity, state, message, progress):
        with self.lock:
            value = self.get(identity)
            value.update(state=state,message=safe_reason(message),progress=progress,updatedAt=time.time())
            write_json(self.folder(identity)/"job.json", value)

    def submit(self, identity, key, models, *, allow_uncertain_retry=False):
        with self.lock:
            spec = self.get(identity)
            if identity in self.active or spec["state"] == "completed":
                raise ForgeError("任务已在进行或已经完成，未重复提交。")
            if len(self.active) >= 4:
                raise ForgeError("已有4个任务在排队，请稍后再试。")
            self.active.add(identity)
            self.update(identity,"queued","已排队，等待制作",0)
            self.executor.submit(self._run, identity, key, models, allow_uncertain_retry)

    def _run(self, identity, key, models, allow_retry):
        provider = None
        try:
            provider = self.provider_factory(key, **models)
            Pipeline(provider, runtime_factory=self.runtime_factory).generate(
                self.folder(identity),self.get(identity),lambda *a:self.update(identity,*a),allow_uncertain_retry=allow_retry)
        except AmbiguousRequest as error:
            self.update(identity,"uncertain",str(error),0)
        except QualityError as error:
            self.update(identity,"quality_failed",str(error),0)
        except ForgeError as error:
            self.update(identity,"failed",str(error),0)
        except Exception:
            self.update(identity,"failed","制作未完成，请检查运行环境。已完成步骤保留，Key 不写入磁盘。",0)
        finally:
            if provider:
                provider.close()
            key = None
            with self.lock:
                self.active.discard(identity)

    def close(self):
        self.executor.shutdown(wait=True, cancel_futures=True)

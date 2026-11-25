from __future__ import annotations
import json
import os
from pathlib import Path
import secrets
from flask import Flask, jsonify, request, send_file
from .errors import ForgeError
from .jobs import Jobs
from .packaging import runtime_path
from .provider import OpenAIProvider


def create_app(work=None, *, provider_factory=OpenAIProvider, runtime_factory=runtime_path, demo=False):
    web = Path(__file__).parent / "web"
    app = Flask(__name__, static_folder=str(web), static_url_path="/static")
    app.config.update(MAX_CONTENT_LENGTH=201*1024**2, MAX_FORM_PARTS=40)
    csrf = secrets.token_urlsafe(32)
    jobs = Jobs(work or Path.cwd()/".work", provider_factory, runtime_factory)
    app.extensions["petforge_jobs"] = jobs
    app.extensions["petforge_csrf"] = csrf

    @app.before_request
    def protect_local_workshop():
        if request.host.split(":",1)[0] not in ("localhost","127.0.0.1"):
            return jsonify(error="只允许从本机访问工作台。"),403
        if request.method != "GET":
            if not secrets.compare_digest(request.headers.get("X-PetForge-Token",""),csrf):
                return jsonify(error="页面已过期，请刷新后重试。"),403
            origin = request.headers.get("Origin")
            if origin and origin.rstrip("/") != request.host_url.rstrip("/"):
                return jsonify(error="不允许其他网页提交任务。"),403

    @app.after_request
    def headers(response):
        response.headers["Cache-Control"] = "no-store"
        response.headers["X-Content-Type-Options"] = "nosniff"
        response.headers["Referrer-Policy"] = "no-referrer"
        response.headers["Content-Security-Policy"] = "default-src 'self'; img-src 'self' blob: data:; style-src 'self'; script-src 'self'; connect-src 'self'; frame-ancestors 'none'"
        return response

    @app.errorhandler(ForgeError)
    def user_error(error):
        return jsonify(error=str(error)),400

    @app.errorhandler(413)
    def too_large(error):
        return jsonify(error="上传总量不能超过200MB。"),413

    @app.get("/")
    def index():
        return (web/"index.html").read_text(encoding="utf-8")

    @app.get("/api/config")
    def config():
        return jsonify(csrf=csrf,hasEnvironmentKey=bool(os.environ.get("OPENAI_API_KEY")),demo=demo,
                       imageModel=os.environ.get("PETFORGE_IMAGE_MODEL","gpt-image-2.6-sunburst"),
                       reviewModel=os.environ.get("PETFORGE_REVIEW_MODEL","gpt-4.1-mini"))

    @app.get("/api/jobs")
    def listing():
        return jsonify(jobs=jobs.list())

    def key_and_models(source):
        key = str(source.get("api_key") or os.environ.get("OPENAI_API_KEY", "")).strip()
        if not key and not demo:
            raise ForgeError("请输入 API Key，或配置 OPENAI_API_KEY 环境变量。")
        return key, dict(base_url=os.environ.get("PETFORGE_API_BASE","https://api.openai.com/v1"),
                         image_model=source.get("image_model") or os.environ.get("PETFORGE_IMAGE_MODEL","gpt-image-2.6-sunburst"),
                         review_model=source.get("review_model") or os.environ.get("PETFORGE_REVIEW_MODEL","gpt-4.1-mini"))

    @app.post("/api/jobs")
    def create():
        key, models = key_and_models(request.form)
        if request.form.get("consent") != "true":
            raise ForgeError("请确认你有权使用这些素材，并同意上传至配置的模型服务。")
        spec = jobs.create(request.form.get("name",""),request.files.getlist("files"),
                           extended=request.form.get("extended")=="true",demo=demo)
        jobs.submit(spec["id"],key,models)
        return jsonify(id=spec["id"]),202

    @app.get("/api/jobs/<identity>")
    def status(identity):
        value = jobs.get(identity)
        if value["state"] == "completed":
            delivery = json.loads((jobs.folder(identity)/"delivery.json").read_text(encoding="utf-8"))
            value["delivery"] = delivery
        return jsonify(value)

    @app.post("/api/jobs/<identity>/retry")
    def retry(identity):
        data = request.get_json(silent=True) or {}
        key, models = key_and_models(data)
        jobs.submit(identity,key,models,allow_uncertain_retry=data.get("allow_uncertain_retry") is True)
        return jsonify(ok=True),202

    @app.get("/api/jobs/<identity>/<kind>")
    def artifact(identity, kind):
        value = jobs.get(identity)
        names = {"package":"desktop-pet.zip","preview":"preview.gif","portrait":"assets/pet.png"}
        if kind not in names or value["state"] != "completed":
            return jsonify(error="素材尚未通过完整检查。"),404
        path = jobs.folder(identity)/names[kind]
        if not path.is_file():
            return jsonify(error="文件不存在。"),404
        return send_file(path,as_attachment=kind=="package",download_name="PetForge-DesktopPet.zip" if kind=="package" else None)

    return app

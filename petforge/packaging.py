from __future__ import annotations
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import zipfile
from .errors import ForgeError

ROOT = Path(__file__).resolve().parents[1]
BUILD = ROOT / "build"


def runtime_executable():
    if os.name != "nt":
        raise ForgeError("生成素材可在本系统进行；完整 Windows 程序需要在 Windows 上构建，或设置 PETFORGE_RUNTIME 指向自行构建的程序。")
    script = ROOT / "tools/build-runtime.ps1"
    sources = sorted((ROOT / "runtime/src").glob("*"))
    digest = hashlib.sha256(b"".join(p.read_bytes() for p in sources if p.is_file())).hexdigest()
    receipt = BUILD / "runtime-source.sha256"
    runtime = BUILD / "PetForge.exe"
    if runtime.exists() and receipt.exists() and receipt.read_text(encoding="utf-8") == digest:
        return runtime
    shell = shutil.which("powershell") or shutil.which("pwsh")
    if not shell:
        raise ForgeError("需要 PowerShell 和 Windows .NET Framework 4.8。")
    try:
        result = subprocess.run([shell, "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", str(script)],
                                capture_output=True, timeout=90, creationflags=getattr(subprocess,"CREATE_NO_WINDOW",0))
    except (OSError, subprocess.TimeoutExpired):
        raise ForgeError("Windows 桌宠程序构建未完成，请运行 tools/build-runtime.ps1 检查。") from None
    if result.returncode or not runtime.exists():
        raise ForgeError("Windows 桌宠构建失败，请检查系统的 .NET Framework 4.8。")
    receipt.write_text(digest)
    return runtime


def runtime_path():
    custom = os.environ.get("PETFORGE_RUNTIME")
    path = Path(custom).resolve() if custom else runtime_executable()
    if not path.is_file() or not path.read_bytes().startswith(b"MZ"):
        raise ForgeError("桌宠程序不存在或不是 Windows 可执行文件。")
    return path


def package(output: Path, assets: Path, name: str, identity: str, runtime: Path, *, demo=False):
    motions = json.loads((assets / "motions.json").read_text(encoding="utf-8"))
    if not motions.get("clips"):
        raise ForgeError("缺少通过质量检查的动作，未生成成品包。")
    pet = dict(version=1,name=name,sheet="pet.png",columns=1,rows=1,animation="ragdoll-live-v2",
               customerEdition=True,disableGreenSpill=True,profileId=identity,
               description="在电脑上陪着你的小伙伴。")
    instructions = """PetForge 桌宠

在 Windows 10 / 11 的64位电脑上，完整解压，再双击 PetForge.exe。
请保留“桌宠资料”文件夹。首次启动显示设置，可以修改名字、大小、陪伴偏好。
鼠标放到宠物身上会变成小手；抚摸头、下巴、背部，或点鼻尖和爪子。
右键宠物可拿起小毛刷、小零食、毛线球和逗猫棒。可用动作随本次生成的素材变化。
没有操作时也会自动活动；设置面板只放日常偏好。
Ctrl+Alt+P 隐藏/显示，Ctrl+Alt+Q 切换勿扰；在托盘图标里退出。
开机启动默认关闭。程序离线运行，不包含生成时使用的 API Key 和原始照片。
支持与问题反馈见 PetForge 开源仓库。
"""
    if demo:
        instructions = "这是技术流程演示角色，不是真实照片定制结果。\n\n" + instructions
    with zipfile.ZipFile(output, "w", zipfile.ZIP_DEFLATED) as archive:
        archive.write(runtime, "PetForge.exe")
        archive.writestr("使用说明.txt", instructions)
        archive.writestr("桌宠资料/pet.json", json.dumps(pet, ensure_ascii=False))
        archive.write(assets/"pet.png", "桌宠资料/pet.png")
        archive.write(assets/"motions.json", "桌宠资料/motions.json")
        for clip in motions["clips"]:
            file = clip["sheet"]
            if Path(file).name != file or file != f"motion-{clip['id']}.png":
                raise ForgeError("素材包路径无效。")
            archive.write(assets/file, "桌宠资料/"+file)
    with zipfile.ZipFile(output) as archive:
        if archive.testzip() is not None:
            raise ForgeError("成品包完整性检查未通过。")
    return hashlib.sha256(output.read_bytes()).hexdigest()

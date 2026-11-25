"""Bounded local media decoding. Video is a reference, not a generated result."""
from __future__ import annotations
import io
import subprocess
from pathlib import Path
import imageio_ffmpeg
from PIL import Image, ImageOps
from .errors import ForgeError

IMAGE_EXTENSIONS = {".jpg", ".jpeg", ".png", ".webp"}
VIDEO_EXTENSIONS = {".mp4", ".mov", ".webm", ".mkv"}
MAX_FILES = 30
MAX_TOTAL = 200 * 1024**2
Image.MAX_IMAGE_PIXELS = 36_000_000


def png(image: Image.Image) -> bytes:
    output = io.BytesIO()
    image.save(output, "PNG")
    return output.getvalue()


def decode_image(raw: bytes) -> Image.Image:
    try:
        with Image.open(io.BytesIO(raw)) as image:
            image.load()
            result = ImageOps.exif_transpose(image).convert("RGBA")
    except (OSError, ValueError, Image.DecompressionBombError):
        raise ForgeError("图片无法读取，或像素尺寸过大。请使用 JPG、PNG 或 WebP。") from None
    if min(result.size) < 32:
        raise ForgeError("参考图片太小。")
    return result


def video_frames(path: Path, output: Path) -> list[bytes]:
    output.mkdir(exist_ok=True, parents=True)
    # Decoding is bounded to the first 12 seconds; no shell or network protocols.
    format_name="mov" if path.suffix.lower() in (".mp4", ".mov") else "matroska"
    for index, seconds in enumerate((0, 4, 8)):
        args = [imageio_ffmpeg.get_ffmpeg_exe(), "-hide_banner", "-loglevel", "error", "-nostdin",
                "-protocol_whitelist", "file,pipe", "-ss", str(seconds), "-f", format_name, "-i", str(path),
                "-vf", "scale=768:768:force_original_aspect_ratio=decrease", "-frames:v", "1",
                "-y", str(output / f"reference-{index:02d}.png")]
        try:
            run = subprocess.run(args, capture_output=True, timeout=20)
        except (OSError, subprocess.TimeoutExpired):
            raise ForgeError("视频读取失败或超过时间限制。") from None
        if index==0 and run.returncode:
            raise ForgeError("视频无法解码，请换成常见的 MP4 文件。")
    files = sorted(output.glob("reference-*.png"))
    if not files:
        raise ForgeError("视频无法解码，请换成常见的 MP4 文件。")
    return [png(decode_image(file.read_bytes())) for file in files]


def references(paths: list[Path], work: Path) -> list[bytes]:
    if not 1 <= len(paths) <= MAX_FILES:
        raise ForgeError("请上传 1–30 个照片或视频。")
    result = []
    total = 0
    for index, path in enumerate(paths):
        size = path.stat().st_size
        total += size
        ext = path.suffix.lower()
        if ext not in IMAGE_EXTENSIONS | VIDEO_EXTENSIONS or size > (80 if ext in VIDEO_EXTENSIONS else 20) * 1024**2 or total > MAX_TOTAL:
            raise ForgeError("文件格式或容量超出限制：图片20MB、视频80MB、合计200MB。")
        if ext in VIDEO_EXTENSIONS:
            candidates = video_frames(path, work / f"video-{index}")
        else:
            candidates = [path.read_bytes()]
        for candidate in candidates:
            image = decode_image(candidate)
            image.thumbnail((768, 768))
            result.append(png(image))  # Re-encode strips EXIF and other metadata.
    # All uploaded files are read; deterministic stratified selection bounds API input.
    if len(result) > 12:
        result = [result[round(i * (len(result) - 1) / 11)] for i in range(12)]
    return result

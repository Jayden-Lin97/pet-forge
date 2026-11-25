"""Reject obvious artifacts instead of morphing different poses together."""
from __future__ import annotations
import io
import statistics
import numpy as np
from PIL import Image, ImageDraw, ImageSequence
from .errors import QualityError
from .media import decode_image, png


def canonical(raw: bytes) -> Image.Image:
    image = decode_image(raw)
    alpha = image.getchannel("A")
    box = alpha.point(lambda a: 255 if a >= 96 else 0).getbbox()
    if not box or np.mean(np.asarray(alpha) >= 96) > .90:
        raise QualityError("角色没有有效透明背景。")
    if min(box) < 3 or box[2] >= image.width - 2 or box[3] >= image.height - 2:
        raise QualityError("角色碰到画布边缘，可能被裁切。")
    subject = image.crop(box)
    subject.thumbnail((224, 210), Image.Resampling.LANCZOS)
    result = Image.new("RGBA", (256, 256))
    result.alpha_composite(subject, ((256 - subject.width) // 2, 235 - subject.height))
    return result


def similarity(a, b):
    aa = np.asarray(a.convert("RGBA"))
    bb = np.asarray(b.convert("RGBA"))
    ma, mb = aa[:, :, 3] >= 96, bb[:, :, 3] >= 96
    union, intersection = ma | mb, ma & mb
    iou = float(intersection.sum() / max(1, union.sum()))
    color = float(np.abs(aa[:, :, :3].astype(float) - bb[:, :, :3]).mean(axis=2)[intersection].mean()) if intersection.any() else 255.0
    return iou, color


def decode_animation(raw, base):
    try:
        with Image.open(io.BytesIO(raw)) as movie:
            if movie.format != "GIF" or not 8 <= movie.n_frames <= 32:
                raise QualityError("动作需要 8–32 帧的真实动画，不能用一张图代替。")
            if movie.width > 1024 or movie.height > 1024 or movie.width != movie.height:
                raise QualityError("动画画布尺寸不匹配。")
            frames, durations = [], []
            for item in ImageSequence.Iterator(movie):
                frames.append(item.convert("RGBA").resize((256, 256), Image.Resampling.LANCZOS))
                durations.append(int(item.info.get("duration", 120)))
    except (OSError, ValueError):
        raise QualityError("生成的动画无法解码。") from None
    if min(durations) < 34 or min(durations) > 125 or max(durations)>2000 or sum(durations)>4000:
        raise QualityError("动画帧时长不适合桌宠播放，请生成均匀且不低于8fps的动作。")
    # GIF encoders merge identical adjacent frames into a longer duration.
    # Reconstruct that timeline with held native frames, never optical flow.
    interval=min(durations)
    originals=frames;frames=[];elapsed=0;at=0
    for tick in np.arange(0,sum(durations),interval):
        while at<len(originals)-1 and tick>=elapsed+durations[at]:
            elapsed+=durations[at];at+=1
        frames.append(originals[at])
    bm = np.asarray(base)[:, :, 3] >= 96
    base_area = max(1, bm.sum())
    contact = base.getchannel("A").point(lambda a: 255 if a >= 96 else 0).getbbox()[3]
    motion = []
    for index, frame in enumerate(frames):
        array = np.asarray(frame)
        mask = array[:, :, 3] >= 96
        area = mask.sum()
        box = frame.getchannel("A").point(lambda a: 255 if a >= 96 else 0).getbbox()
        if not box or not .65 <= area / base_area <= 1.45:
            raise QualityError(f"第 {index+1} 帧的体积或透明度变化过大。")
        if box[0] < 2 or box[1] < 2 or box[2] > 254 or box[3] > 254:
            raise QualityError(f"第 {index+1} 帧碰到边缘，可能缺少耳朵或爪子。")
        if abs(box[3] - contact) > 8:
            raise QualityError(f"第 {index+1} 帧的地面接触点漂移，可能悬空。")
        if index:
            prev = np.asarray(frames[index-1])
            delta = float(np.abs(array.astype(float) - prev.astype(float)).mean() / 255)
            motion.append(delta)
            if delta > .18:
                raise QualityError("相邻帧变化过大，疑似跳切或变形。")
    if max(motion, default=0) < .0008:
        raise QualityError("动作几乎完全静止。")
    endpoints = [similarity(base, frames[i]) for i in (0, -1)]
    if any(iou < .88 or color > 30 for iou, color in endpoints):
        raise QualityError("动作起止姿态与基础角色不一致，不能直接拼接。")
    return frames, 1000.0 / interval, {
        "frameCount": len(frames), "endpointIou": [round(x[0], 4) for x in endpoints],
        "endpointColorError": [round(x[1], 2) for x in endpoints], "maximumFrameChange": round(max(motion), 4),
        "perFrameBodyWarp": False, "reversePlayback": False,
    }


def contact_sheet(frames):
    sheet = Image.new("RGB", (768, ((len(frames) + 3) // 4) * 204), "#f5f4f0")
    draw = ImageDraw.Draw(sheet)
    for i, frame in enumerate(frames):
        tile = frame.resize((192, 192), Image.Resampling.LANCZOS)
        sheet.paste(tile, ((i % 4) * 192, (i // 4) * 204), tile)
        draw.text(((i % 4) * 192 + 6, (i // 4) * 204 + 190), str(i+1), fill="#404040")
    return png(sheet)


def pack_atlas(frames, base, action, output, fps):
    # Only exact neutral padding. No body morph, pose interpolation or reversed gait.
    timeline = [base] * 4 + frames + [base] * 4
    atlas = Image.new("RGBA", (8 * 256, ((len(timeline) + 7) // 8) * 256))
    for i, frame in enumerate(timeline):
        atlas.paste(frame, ((i % 8) * 256, (i // 8) * 256))
    name = f"motion-{action}.png"
    atlas.save(output / name)
    spec = dict(id=action, sheet=name, columns=8, rows=atlas.height // 256,
                frames=len(timeline), fps=fps, size=256, originX=0, originY=0,
                anchorFrames=4, reviewed=True, physicalTransitionReviewed=True,
                transitionSource="generated-gif", reviewSource="automated-technical-and-vision")
    if action == "sleep":
        spec.update(holdAt=4+len(frames)//2, holdSeconds=20)
    return spec

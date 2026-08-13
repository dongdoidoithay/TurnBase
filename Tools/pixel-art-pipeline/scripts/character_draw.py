#!/usr/bin/env python3
"""
character_draw.py — task-animation-pilot.md. Vẽ THỦ TỤC (không AI) 1 nhân vật chibi 32×32 bằng
Pillow, xuất nhiều frame animation (idle/attack) với TÍNH NHẤT QUÁN TUYỆT ĐỐI giữa các frame (cùng
1 hàm vẽ, chỉ đổi tham số pose) — giải quyết vấn đề ComfyUI không đảm bảo nhân vật giống nhau giữa
các lần sinh riêng lẻ.

Phong cách tham khảo: _Reference/UI_SAMPLE/Character_01.png — khối màu PHẲNG (không gradient), ÍT
MÀU (~6 màu + outline), viền đen 1px quanh toàn silhouette. Màu lấy từ Tools/palette.json (48 màu
dự án) — chỉ dùng 1 tập con nhỏ, không dùng cả 48.

Dùng:
    python3 character_draw.py --hero hero_ember_knight --state idle --frames 4 --out clean/hero_ember_knight/idle/
    python3 character_draw.py --hero hero_ember_knight --state attack --frames 4 --out clean/hero_ember_knight/attack/
"""

import argparse
from pathlib import Path

try:
    from PIL import Image, ImageDraw
except ImportError:
    raise SystemExit("Cần Pillow: pip install Pillow")

SIZE = 32

# Tập màu con cho hero_ember_knight — lấy từ Tools/palette.json (index ghi chú để dễ đối chiếu).
OUTLINE = (13, 8, 14, 255)       # #0D080E (palette[8]) — gần đen, không dùng đen tuyệt đối #000
ROBE = (163, 35, 53, 255)        # #A32335 (palette[17]) — đỏ thân/robe
ROBE_SHADE = (92, 18, 32, 255)   # #5C1220 (palette[16]) — đỏ tối, viền/bóng robe
HELMET = (69, 123, 157, 255)     # #457B9D (palette[22]) — xanh mũ giáp
HELMET_SHADE = (42, 90, 128, 255)# #2A5A80 (palette[21])
SKIN = (244, 162, 89, 255)       # #F4A259 (palette[14])
STEEL = (154, 154, 154, 255)     # #9A9A9A (palette[10]) — vũ khí
STEEL_SHADE = (92, 92, 92, 255)  # #5C5C5C (palette[9])
TRIM = (184, 92, 30, 255)        # #B85C1E (palette[13]) — viền/giày cam


def draw_silhouette(pose):
    """Vẽ nhân vật (chưa outline) lên canvas RGBA trong suốt.
    pose: dict{head_bob, arm_angle, lean} — head_bob dịch đầu+thân lên/xuống (idle bob),
    arm_angle 0..1 nội suy vị trí cánh tay+vũ khí từ 'thu về' (0) sang 'vươn ra trước' (1),
    lean dịch toàn thân ngang (thêm cảm giác lao tới khi tấn công)."""
    img = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    hb = pose.get("head_bob", 0)
    lean = pose.get("lean", 0)
    arm = pose.get("arm_angle", 0.0)

    cx = SIZE // 2 + lean  # tâm ngang nhân vật, dịch theo lean

    # ---- Chân (cố định, không theo head_bob — giữ "bám đất") ----
    d.rectangle([cx - 6, 24, cx - 2, 29], fill=ROBE_SHADE)
    d.rectangle([cx + 2, 24, cx + 6, 29], fill=ROBE_SHADE)
    d.rectangle([cx - 6, 28, cx - 2, 29], fill=TRIM)
    d.rectangle([cx + 2, 28, cx + 6, 29], fill=TRIM)

    # ---- Thân robe (theo head_bob) ----
    by = 14 + hb
    d.rectangle([cx - 7, by, cx + 7, by + 9], fill=ROBE)
    d.rectangle([cx - 7, by + 8, cx + 7, by + 9], fill=ROBE_SHADE)  # viền đai bụng

    # ---- Đầu/mũ giáp (theo head_bob) ----
    hy = 3 + hb
    d.rectangle([cx - 6, hy, cx + 6, hy + 9], fill=HELMET)
    d.rectangle([cx - 6, hy + 7, cx + 6, hy + 9], fill=HELMET_SHADE)  # viền cổ mũ
    d.rectangle([cx - 4, hy + 4, cx + 4, hy + 6], fill=SKIN)          # dải mặt
    d.rectangle([cx - 3, hy + 5, cx - 2, hy + 5], fill=OUTLINE)       # mắt trái
    d.rectangle([cx + 2, hy + 5, cx + 3, hy + 5], fill=OUTLINE)       # mắt phải

    # ---- Khiên tay trái (cố định) ----
    d.rectangle([cx - 10, by + 2, cx - 8, by + 7], fill=STEEL)
    d.rectangle([cx - 10, by + 6, cx - 8, by + 7], fill=STEEL_SHADE)

    # ---- Tay phải + vũ khí (nội suy theo arm_angle: 0=thu về sát thân, 1=vươn ra trước) ----
    reach = int(round(arm * 8))
    wy = by + 2 - int(round(arm * 3))  # vươn ra thì hơi nâng lên
    d.rectangle([cx + 7, by + 3, cx + 9, by + 6], fill=ROBE_SHADE)  # bắp tay
    d.line([(cx + 9, wy), (cx + 9 + reach, wy - reach // 2)], fill=STEEL, width=2)
    d.point((cx + 9 + reach, wy - reach // 2), fill=(255, 255, 255, 255))  # ánh kim mũi kiếm

    return img


def add_outline(img):
    """Viền đen 1px quanh TOÀN silhouette — dilate alpha 4 hướng rồi tô OUTLINE ở phần dilate-only,
    đặt LÀM NỀN (dưới) nhân vật gốc."""
    alpha = img.split()[3]
    w, h = img.size
    dilated = Image.new("L", (w, h), 0)
    src = alpha.load()
    dst = dilated.load()
    for y in range(h):
        for x in range(w):
            if src[x, y] > 0:
                dst[x, y] = 255
                continue
            hit = False
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + dx, y + dy
                if 0 <= nx < w and 0 <= ny < h and src[nx, ny] > 0:
                    hit = True
                    break
            if hit:
                dst[x, y] = 255

    outline_layer = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    for y in range(h):
        for x in range(w):
            if dst[x, y] > 0 and src[x, y] == 0:
                outline_layer.putpixel((x, y), OUTLINE)

    result = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    result = Image.alpha_composite(result, outline_layer)
    result = Image.alpha_composite(result, img)
    return result


IDLE_POSES = [
    {"head_bob": 0, "arm_angle": 0.0, "lean": 0},
    {"head_bob": -1, "arm_angle": 0.0, "lean": 0},
    {"head_bob": -1, "arm_angle": 0.0, "lean": 0},
    {"head_bob": 0, "arm_angle": 0.0, "lean": 0},
]

ATTACK_POSES = [
    {"head_bob": 0, "arm_angle": 0.0, "lean": 0},   # thu người
    {"head_bob": 0, "arm_angle": 0.3, "lean": 0},   # bắt đầu vươn
    {"head_bob": 0, "arm_angle": 1.0, "lean": 1},   # vươn hết + lao người tới (đòn đánh)
    {"head_bob": 0, "arm_angle": 0.5, "lean": 0},   # thu về (follow-through)
]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--hero", required=True)
    ap.add_argument("--state", required=True, choices=["idle", "attack"])
    ap.add_argument("--frames", type=int, default=4)
    ap.add_argument("--out", required=True)
    args = ap.parse_args()

    poses = IDLE_POSES if args.state == "idle" else ATTACK_POSES
    poses = poses[: args.frames]

    out_dir = Path(args.out)
    out_dir.mkdir(parents=True, exist_ok=True)
    for i, pose in enumerate(poses):
        img = draw_silhouette(pose)
        img = add_outline(img)
        path = out_dir / f"{args.hero}_{args.state}_{i:02d}.png"
        img.save(path)
        print(f"  ✓ {path} ({img.size[0]}×{img.size[1]})")


if __name__ == "__main__":
    main()

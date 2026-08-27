#!/usr/bin/env python3
"""
draw_battle_backdrop.py — nền khung cảnh trận đấu, vẽ thủ tục bằng Pillow (không AI, không crop
ảnh mẫu) thay `battle_arena_ember.png` cũ (tường gạch phẳng + bầu trời xanh lơ, không có khí quyển)
bằng khung cảnh rừng/hang mờ tối kiểu _Reference/Art_Sample/Screen_combat.jpg: bầu trời gradient
tím-mận tối, rặng cây silhouette 2 lớp ở đường chân trời, nền đất ấm có quầng sáng giữa nơi 2 phe
đứng, vignette tối 4 góc.

Ghi đè ĐÚNG file cũ (Assets/_Project/Resources/Art/Backgrounds/battle_arena_ember.png), giữ
NGUYÊN kích thước 512×288 và mọi thiết lập import (PPU 32, Point filter, spriteMeshType Tight) —
không cần sửa .meta hay code C# nào (SpriteRenderer đã tham chiếu đúng sprite name này).

Dùng: python3 draw_battle_backdrop.py --out battle_arena_ember.png
"""
import argparse
import math
import random

from PIL import Image, ImageDraw

W, H = 512, 288
# Đường chân trời — trên là bầu trời, dưới là nền đất. Tính từ vị trí world-Y THẬT của unit
# trong trận (BattleSceneInstaller đặt hero/enemy quanh Y=-1.0..-1.8, camera ortho tâm Y=-1.6
# size 9 → quy đổi world→pixel: row=(2.9-worldY)/9*288 → unit rơi vào khoảng row 125–150).
# HORIZON phải NHỎ hơn hẳn khoảng đó để unit đứng rõ trong vùng "đất", không lơ lửng giữa "trời"
# (bug thật gặp phải ở bản vẽ đầu tiên, HORIZON=168 — xem task-ui-chrome-popups.md §3.11).
HORIZON = 95

# Tools/palette.json (TurnBase 48) — họ tím/mận + cam/vàng lửa trại
SKY_TOP = (13, 8, 14)
SKY_HORIZON = (58, 32, 51)
TREE_FAR = (42, 27, 46)
TREE_NEAR = (26, 15, 28)
GROUND_EDGE = (43, 27, 46)
GROUND_WARM = (92, 18, 32)
GLOW_CORE = (244, 162, 89)
STAR = (154, 154, 154)


def lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


def draw_sky(img):
    d = ImageDraw.Draw(img)
    for y in range(HORIZON):
        t = y / HORIZON
        d.line([(0, y), (W, y)], fill=lerp(SKY_TOP, SKY_HORIZON, t))

    rnd = random.Random(20260828)
    for _ in range(40):
        x = rnd.randint(0, W - 1)
        y = rnd.randint(0, int(HORIZON * 0.7))
        a = rnd.choice([90, 130, 170])
        d.point((x, y), fill=STAR + (a,)) if img.mode == "RGBA" else d.point((x, y), fill=STAR)


def draw_tree_row(img, base_y, color, count, min_h, max_h, min_w, max_w, seed):
    d = ImageDraw.Draw(img, "RGBA")
    rnd = random.Random(seed)
    x = -20
    while x < W + 20:
        w = rnd.randint(min_w, max_w)
        h = rnd.randint(min_h, max_h)
        peak = rnd.randint(1, 3)
        cx = x + w // 2
        if peak == 1:
            d.polygon([(x, base_y), (cx, base_y - h), (x + w, base_y)], fill=color)
        else:
            step = h // peak
            pts = [(x, base_y)]
            for i in range(peak):
                sub_cx = x + w * (i + 0.5) / peak
                pts.append((sub_cx, base_y - (step * (i % 2 + 1))))
            pts.append((x + w, base_y))
            d.polygon(pts, fill=color)
        x += int(w * rnd.uniform(0.5, 0.8))


def draw_ground(img):
    d = ImageDraw.Draw(img, "RGBA")
    for y in range(HORIZON, H):
        t = (y - HORIZON) / (H - HORIZON)
        d.line([(0, y), (W, y)], fill=lerp(GROUND_EDGE, GROUND_WARM, t * 0.6))

    # quầng sáng ấm giữa nền — nơi 2 phe đứng đối mặt (unit rơi vào khoảng row 125-150, xem
    # HORIZON ở trên), thay campfire cũ bằng glow mềm hơn
    glow = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    gd = ImageDraw.Draw(glow)
    cx, cy = W // 2, 140
    for r in range(90, 0, -6):
        a = int(70 * (1 - r / 90) ** 2)
        gd.ellipse([cx - r, cy - r * 0.45, cx + r, cy + r * 0.45], fill=GLOW_CORE + (a,))
    img.alpha_composite(glow) if img.mode == "RGBA" else img.paste(
        Image.alpha_composite(img.convert("RGBA"), glow).convert("RGB"))


def draw_vignette(img):
    vig = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    vd = ImageDraw.Draw(vig)
    cx, cy = W / 2, H / 2
    maxd = math.hypot(cx, cy)
    for y in range(0, H, 4):
        for x in range(0, W, 4):
            d = math.hypot(x - cx, y - cy) / maxd
            a = int(max(0, (d - 0.55)) / 0.45 * 130)
            if a > 0:
                vd.rectangle([x, y, x + 4, y + 4], fill=(5, 2, 6, min(a, 130)))
    img.alpha_composite(vig)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", required=True)
    args = ap.parse_args()

    img = Image.new("RGBA", (W, H), (0, 0, 0, 255))
    draw_sky(img)
    draw_tree_row(img, HORIZON + 4, TREE_FAR, 10, 30, 55, 50, 90, seed=1)
    draw_tree_row(img, HORIZON + 10, TREE_NEAR, 8, 45, 80, 60, 110, seed=2)
    draw_ground(img)
    draw_vignette(img)

    img.convert("RGB").save(args.out)
    print(f"saved {args.out} {img.size}")


if __name__ == "__main__":
    main()

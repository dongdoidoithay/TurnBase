#!/usr/bin/env python3
"""
nav_icons.py — Icon nhỏ (24x24) cho TopBar (task-ui-vfx-polish.md §4 — 7 nút chữ hiện không đủ
chỗ trong 780px, đo thật cần ~880px). Icon-only thay text-pill dài, đúng hướng UI_01 tham khảo.
NEUTRAL (trắng, alpha shape) — cùng triết lý với ui_glow_frame.py: nhân Image.color tại mỗi
Button để tự phối màu, không bake màu cứng.

Dùng: python3 nav_icons.py --out-dir out/
"""
import argparse
from pathlib import Path

from PIL import Image, ImageDraw

W = (255, 255, 255, 255)


def _new():
    return Image.new("RGBA", (24, 24), (0, 0, 0, 0))


def icon_tower():
    img = _new()
    d = ImageDraw.Draw(img)
    d.rectangle([8, 10, 15, 21], outline=W, width=2)
    # răng cưa đỉnh tháp
    for x in (7, 10, 13, 16):
        d.rectangle([x, 5, x + 2, 10], outline=W, width=1)
    d.line([11, 21, 11, 17], fill=W)  # cửa nhỏ
    return img


def icon_swords():
    img = _new()
    d = ImageDraw.Draw(img)
    d.line([5, 5, 19, 19], fill=W, width=2)
    d.line([5, 19, 19, 5], fill=W, width=2)
    # chuôi kiếm 2 đầu
    for (x, y) in ((5, 5), (19, 19), (5, 19), (19, 5)):
        d.ellipse([x - 2, y - 2, x + 2, y + 2], outline=W, width=1)
    return img


def icon_dungeon():
    img = _new()
    d = ImageDraw.Draw(img)
    d.arc([6, 4, 18, 16], 180, 360, fill=W, width=2)
    d.line([6, 10, 6, 20], fill=W, width=2)
    d.line([18, 10, 18, 20], fill=W, width=2)
    d.line([6, 20, 18, 20], fill=W, width=2)
    return img


def icon_book():
    img = _new()
    d = ImageDraw.Draw(img)
    d.rectangle([5, 5, 19, 19], outline=W, width=2)
    d.line([12, 5, 12, 19], fill=W, width=1)
    d.line([7, 9, 10, 9], fill=W)
    d.line([14, 9, 17, 9], fill=W)
    d.line([7, 13, 10, 13], fill=W)
    d.line([14, 13, 17, 13], fill=W)
    return img


def icon_mail():
    img = _new()
    d = ImageDraw.Draw(img)
    d.rectangle([4, 6, 20, 18], outline=W, width=2)
    d.line([4, 6, 12, 13], fill=W, width=2)
    d.line([20, 6, 12, 13], fill=W, width=2)
    return img


def icon_chest():
    img = _new()
    d = ImageDraw.Draw(img)
    d.rectangle([4, 11, 20, 20], outline=W, width=2)
    d.arc([4, 3, 20, 15], 180, 360, fill=W, width=2)
    d.ellipse([10, 13, 14, 17], outline=W, width=1)
    return img


def icon_scroll():
    img = _new()
    d = ImageDraw.Draw(img)
    d.rectangle([7, 5, 17, 19], outline=W, width=1)
    d.ellipse([5, 3, 9, 7], outline=W, width=1)
    d.ellipse([5, 17, 9, 21], outline=W, width=1)
    d.ellipse([15, 3, 19, 7], outline=W, width=1)
    d.ellipse([15, 17, 19, 21], outline=W, width=1)
    d.line([9, 9, 15, 9], fill=W)
    d.line([9, 12, 15, 12], fill=W)
    d.line([9, 15, 15, 15], fill=W)
    return img


def icon_gear():
    img = _new()
    d = ImageDraw.Draw(img)
    d.ellipse([8, 8, 15, 15], outline=W, width=1)
    # 8 răng bánh răng quanh vòng ngoài
    teeth = [(11, 3, 12, 6), (11, 17, 12, 20), (3, 11, 6, 12), (17, 11, 20, 12),
             (5, 5, 7, 7), (16, 16, 18, 18), (5, 16, 7, 18), (16, 5, 18, 7)]
    for (x0, y0, x1, y1) in teeth:
        d.rectangle([x0, y0, x1, y1], fill=W)
    return img


def icon_summon():
    img = _new()
    d = ImageDraw.Draw(img)
    # ngôi sao 4 cánh (kiểu "triệu hồi/phép thuật") + 2 tia lấp lánh nhỏ
    cx, cy = 12, 12
    d.polygon([(cx, cy - 9), (cx + 2, cy - 2), (cx + 9, cy), (cx + 2, cy + 2),
               (cx, cy + 9), (cx - 2, cy + 2), (cx - 9, cy), (cx - 2, cy - 2)], fill=W)
    d.polygon([(5, 4, ), (6, 6), (8, 5), (6, 7), (5, 9), (4, 7), (2, 5), (4, 6)], fill=W)
    return img


ICONS = {
    "icon_tower": icon_tower,
    "icon_trial": icon_swords,
    "icon_dungeon": icon_dungeon,
    "icon_codex": icon_book,
    "icon_mail": icon_mail,
    "icon_items": icon_chest,
    "icon_quest": icon_scroll,
    "icon_gear": icon_gear,
    "icon_summon": icon_summon,
}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out-dir", required=True)
    args = ap.parse_args()
    out = Path(args.out_dir)
    out.mkdir(parents=True, exist_ok=True)
    for name, fn in ICONS.items():
        img = fn()
        img.save(out / f"{name}.png")
        print(f"  ✓ {name}.png")


if __name__ == "__main__":
    main()

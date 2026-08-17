#!/usr/bin/env python3
"""
item_icons.py — 32x32 icon cho các loại vật phẩm/vật liệu chưa có asset (task-list "Inventory
grid icon"). Cùng phong cách phẳng+viền tối với prop_potion_red/prop_feather đã có sẵn trong
Assets/_Project/Art/UI/Icons/Items/ — không sinh lại 5 loại đã có prop (Potion/Ether/Revive
Feather/Gold/Gem), chỉ bù 8 loại còn thiếu: Antidote, SmokeBomb, ElementalBomb, EssenceI/II/III,
Core, EnhanceStone. Màu lấy từ Tools/palette.json (TurnBase 48).

Dùng: python3 item_icons.py --out-dir out/
"""
import argparse
from pathlib import Path

from PIL import Image, ImageDraw

# Tools/palette.json (TurnBase 48) — chỉ lấy các tông cần dùng
GREEN_DARK, GREEN_MID, GREEN_LIGHT = (27, 61, 31, 255), (61, 122, 46, 255), (123, 201, 80, 255)
GRAY_DARK, GRAY_MID, GRAY_LIGHT = (13, 8, 14, 255), (92, 92, 92, 255), (154, 154, 154, 255)
ORANGE_MID, ORANGE_LIGHT = (184, 92, 30, 255), (244, 162, 89, 255)
BLUE_MID, BLUE_LIGHT = (69, 123, 157, 255), (143, 192, 217, 255)
PURPLE_MID, PURPLE_LIGHT = (155, 93, 229, 255), (203, 165, 240, 255)
GOLD_MID, GOLD_LIGHT = (184, 144, 30, 255), (255, 209, 102, 255)
CYAN_MID, CYAN_LIGHT = (78, 195, 217, 255), (165, 232, 240, 255)
TAN_MID, TAN_LIGHT = (166, 113, 66, 255), (212, 165, 116, 255)
OUTLINE = (13, 8, 14, 255)


def _new():
    return Image.new("RGBA", (32, 32), (0, 0, 0, 0))


def icon_antidote():
    img = _new()
    d = ImageDraw.Draw(img)
    # Lọ thuốc bo tròn + dấu cộng — khác hẳn silhouette potion hình giọt nước đã có
    d.rounded_rectangle([10, 12, 22, 26], radius=3, fill=GREEN_MID, outline=OUTLINE, width=1)
    d.rectangle([12, 8, 20, 13], fill=GRAY_MID, outline=OUTLINE, width=1)
    d.line([16, 16, 16, 22], fill=GREEN_LIGHT, width=2)
    d.line([13, 19, 19, 19], fill=GREEN_LIGHT, width=2)
    return img


def icon_smoke_bomb():
    img = _new()
    d = ImageDraw.Draw(img)
    d.ellipse([7, 12, 25, 28], fill=GRAY_DARK, outline=OUTLINE, width=1)
    d.ellipse([10, 15, 18, 21], fill=GRAY_MID)
    d.line([18, 12, 22, 6], fill=(90, 60, 30, 255), width=2)
    d.ellipse([20, 4, 24, 8], fill=ORANGE_LIGHT, outline=OUTLINE, width=1)
    return img


def icon_elemental_bomb():
    img = _new()
    d = ImageDraw.Draw(img)
    d.ellipse([7, 12, 25, 28], fill=(60, 30, 60, 255), outline=OUTLINE, width=1)
    d.polygon([(16, 6), (19, 14), (26, 15), (20, 19), (22, 26), (16, 22), (10, 26), (12, 19),
               (6, 15), (13, 14)], fill=ORANGE_LIGHT, outline=OUTLINE)
    return img


def _essence(tier_color_mid, tier_color_light, size):
    img = _new()
    d = ImageDraw.Draw(img)
    cx, cy = 16, 16
    d.polygon([(cx, cy - size), (cx + size * 0.6, cy - size * 0.2), (cx, cy + size),
               (cx - size * 0.6, cy - size * 0.2)], fill=tier_color_mid, outline=OUTLINE)
    d.polygon([(cx, cy - size), (cx + size * 0.25, cy - size * 0.3), (cx, cy + size * 0.3),
               (cx - size * 0.25, cy - size * 0.3)], fill=tier_color_light)
    return img


def icon_essence_1():
    return _essence(BLUE_MID, BLUE_LIGHT, 8)


def icon_essence_2():
    return _essence(PURPLE_MID, PURPLE_LIGHT, 10)


def icon_essence_3():
    return _essence(GOLD_MID, GOLD_LIGHT, 12)


def icon_core():
    img = _new()
    d = ImageDraw.Draw(img)
    d.ellipse([8, 8, 24, 24], fill=CYAN_MID, outline=OUTLINE, width=1)
    d.ellipse([12, 11, 20, 18], fill=CYAN_LIGHT)
    d.ellipse([13, 12, 16, 15], fill=(255, 255, 255, 200))
    return img


def icon_enhance_stone():
    img = _new()
    d = ImageDraw.Draw(img)
    d.polygon([(9, 20), (13, 10), (21, 9), (24, 18), (19, 25), (11, 24)],
               fill=TAN_MID, outline=OUTLINE)
    d.polygon([(13, 10), (21, 9), (18, 14), (14, 15)], fill=TAN_LIGHT)
    return img


ICONS = {
    "prop_antidote": icon_antidote,
    "prop_smoke_bomb": icon_smoke_bomb,
    "prop_elemental_bomb": icon_elemental_bomb,
    "prop_essence_1": icon_essence_1,
    "prop_essence_2": icon_essence_2,
    "prop_essence_3": icon_essence_3,
    "prop_core": icon_core,
    "prop_enhance_stone": icon_enhance_stone,
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

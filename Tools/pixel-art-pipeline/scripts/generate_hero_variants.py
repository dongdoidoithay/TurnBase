#!/usr/bin/env python3
"""
generate_hero_variants.py — Giai đoạn 3 (task-animation-pilot.md §4): sinh 18 hero còn lại bằng
cách TÁI DÙNG silhouette 6 kit đã dựng (character_rig.py), chỉ đổi bảng màu theo Element — không
thiết kế lại. Mọi class dùng CHUNG cấu trúc mã màu ("4"/"8"=đầu, "5"/"7"=thân, "t"=viền sáng — xem
character_rig.CharacterKit) nên 1 bộ (primary, shade, bright) là đủ tạo 1 hero mới cho BẤT KỲ class
nào qua `element_colors()`.

Dùng:
    python3 generate_hero_variants.py --out-root Tools/pixel-art-pipeline/clean
"""

import argparse
import importlib.util
import os
import sys

_HERE = os.path.dirname(os.path.abspath(__file__))
_spec = importlib.util.spec_from_file_location("character_rig", os.path.join(_HERE, "character_rig.py"))
cr = importlib.util.module_from_spec(_spec)
sys.modules["character_rig"] = cr
_spec.loader.exec_module(cr)


def element_colors(primary, shade, bright):
    """Bảng màu hợp lệ cho BẤT KỲ kit nào — mọi _XXX_COLORS trong character_rig.py đều cùng 1 cấu
    trúc mã (xem docstring module)."""
    return {
        "0": (0, 0, 0, 0), "1": cr.OUTLINE, "2": cr.SKIN, "3": cr.STEEL, "9": cr.STEEL_SHADE,
        "4": primary, "8": bright, "5": primary, "7": shade, "t": bright,
    }


FIRE = element_colors((163, 35, 53, 255), (92, 18, 32, 255), (244, 162, 89, 255))
WATER = element_colors((69, 123, 157, 255), (42, 90, 128, 255), (143, 192, 217, 255))
EARTH = element_colors((61, 122, 46, 255), (27, 61, 31, 255), (185, 232, 154, 255))
WIND = element_colors((196, 184, 199, 255), (154, 139, 158, 255), (242, 232, 207, 255))
LIGHT = element_colors((255, 209, 102, 255), (184, 144, 30, 255), (255, 240, 184, 255))
DARK = element_colors((90, 48, 128, 255), (42, 27, 58, 255), (203, 165, 240, 255))
# Biến thể ALT — dùng khi 2 hero CÙNG class trùng element (né trông giống hệt nhau).
WIND_ALT = element_colors((123, 201, 80, 255), (58, 116, 46, 255), (217, 242, 184, 255))
LIGHT_ALT = element_colors((203, 165, 240, 255), (90, 48, 128, 255), (242, 232, 207, 255))

# (hero_id, kit_name (silhouette tái dùng), bảng màu)
HEROES = [
    ("hero_iron_bastion", "vanguard", EARTH),
    ("hero_tide_warden", "vanguard", WATER),
    ("hero_stormguard", "vanguard", WIND),
    ("hero_pyromancer", "arcanist", FIRE),
    ("hero_terra_seer", "arcanist", EARTH),
    ("hero_void_scholar", "arcanist", DARK),
    ("hero_night_stalker", "trickster", DARK),
    ("hero_spark_runner", "trickster", WIND_ALT),   # trùng Wind với hero_gale_thief
    ("hero_mirage_fox", "trickster", LIGHT),
    ("hero_grove_keeper", "warden", EARTH),
    ("hero_moon_priestess", "warden", LIGHT_ALT),   # trùng Light với hero_dawn_cleric
    ("hero_spring_medic", "warden", WATER),
    ("hero_blade_dancer", "slayer", WIND),
    ("hero_crimson_reaver", "slayer", FIRE),
    ("hero_stone_breaker", "slayer", EARTH),
    ("hero_beast_tamer", "summoner", EARTH),
    ("hero_flame_binder", "summoner", FIRE),
    ("hero_star_weaver", "summoner", LIGHT),
]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out-root", required=True)
    ap.add_argument("--only", help="chỉ sinh 1 hero (debug)")
    args = ap.parse_args()

    for hero_id, kit_name, colors in HEROES:
        if args.only and hero_id != args.only:
            continue
        base_kit = cr.KITS[kit_name]
        kit = cr.recolor_kit(base_kit, colors, base_kit.get_head, base_kit.get_torso,
                             base_kit.get_weapon,
                             base_kit.get_shield if base_kit.get_shield is None else base_kit.get_shield)
        # base_kit.get_head/.get_torso/... đã bị CharacterKit.__init__ bọc thành lambda: fn(colors_cũ)
        # — cần hàm THÔ (chưa bọc) để recolor. Lấy lại qua bảng tra cứu THÔ riêng bên dưới.
        raw = cr.RAW_PART_FNS[kit_name]
        kit = cr.CharacterKit(colors, raw["head"], raw["torso"], raw["weapon"], raw["shield"])

        for state in ("idle", "attack", "move", "damage", "die"):
            frames = 3 if state == "damage" else (6 if state == "die" else 4)
            poses = cr.POSE_SETS[state][:frames]
            out_dir = os.path.join(args.out_root, f"{hero_id}_rig", state)
            os.makedirs(out_dir, exist_ok=True)
            for i, pose in enumerate(poses):
                kwargs = dict(pose)
                if state == "attack" and i > 0:
                    kwargs["prev_arm_angle_deg"] = poses[i - 1]["arm_angle_deg"]
                frame = cr.build_frame(kit=kit, **kwargs)
                frame.save(os.path.join(out_dir, f"{hero_id}_rig_{state}_{i:02d}.png"))
        print(f"  ✓ {hero_id} (kit={kit_name})")


if __name__ == "__main__":
    main()

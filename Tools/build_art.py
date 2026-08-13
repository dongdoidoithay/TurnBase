#!/usr/bin/env python3
"""
build_art.py — Chạy trọn pipeline art cho TurnBase bằng một lệnh.

    python3 Tools/build_art.py              # sinh + xử lý toàn bộ catalog
    python3 Tools/build_art.py --process    # chỉ xử lý lại raw/ đã có
    python3 Tools/build_art.py --ui         # chỉ dựng UI bằng Pillow

Luồng (skill pixel-art-pipeline):
    ComfyUI  →  Tools/raw/            ảnh gốc 512px, nền phẳng, 4 frame/ảnh
    Pillow   →  Assets/_Project/Art/  32×32 nhân vật, nền trong, khoá palette

Kích thước chuẩn (plan.md §2.2):
    hero / enemy  32×32     enemy lớn 48×48     boss 64×64
    icon 32×32    prop 32×32    vfx 64×64
"""

import argparse
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SKILL = Path.home() / ".claude/skills/pixel-art-pipeline/scripts"
RAW = ROOT / "Tools/raw"
ART = ROOT / "Assets/_Project/Art"
PALETTE = ROOT / "Tools/palette.json"
CATALOG = ROOT / "Tools/art_catalog.json"

# category → (chiều cao sprite, canvas, thư mục đích, số frame trong sheet)
SPEC = {
    "character": (30, 32, ART / "Characters/Heroes", 4),
    "monster":   (30, 32, ART / "Characters/Enemies", 4),
    "boss":      (60, 64, ART / "Characters/Bosses", 4),
    "vfx":       (60, 64, ART / "VFX", 4),
    "prop":      (28, 32, ART / "UI/Icons/Items", 4),
    "tile":      (32, 32, ART / "Environment/Tilesets", 1),
    "background": (0, 0,  ART / "Environment/Backgrounds", 1),
}


def run(cmd, **kw):
    print(f"  $ {' '.join(str(c) for c in cmd)}")
    return subprocess.run(cmd, check=False, **kw)


def generate():
    print("\n=== 1/3 SINH bằng ComfyUI ===")
    run([sys.executable, str(SKILL / "comfy_gen.py"),
         "--catalog", str(CATALOG), "--out", str(RAW)])


def process():
    print("\n=== 2/3 HẬU XỬ LÝ bằng Pillow ===")
    for category, (height, canvas, dest, cols) in SPEC.items():
        src_dir = RAW / category
        if not src_dir.exists():
            continue

        files = sorted(src_dir.glob("*.png"))
        print(f"\n-- {category}: {len(files)} ảnh gốc → {dest.relative_to(ROOT)}")

        for f in files:
            # Bỏ hậu tố _v1/_v2 khi đặt tên thư mục đích
            stem = f.stem
            base = stem.rsplit("_v", 1)[0] if "_v" in stem else stem
            out_dir = dest / base

            if category == "background":
                # Background giữ nguyên tỉ lệ, chỉ khoá palette
                run([sys.executable, str(SKILL / "post_process.py"),
                     "--in", str(f), "--out", str(dest),
                     "--target-height", "180",
                     "--palette", str(PALETTE), "--key", "auto"])
                continue

            cmd = [sys.executable, str(SKILL / "post_process.py"),
                   "--slice", str(f), "--cols", str(cols),
                   "--out", str(out_dir),
                   "--target-height", str(height),
                   "--canvas", str(canvas),
                   "--palette", str(PALETTE),
                   "--anchor", "bottom"]
            run(cmd)


def build_ui():
    """Dựng HUD/frame/button/bar bằng code — không dùng AI (skill §Bước 3)."""
    print("\n=== 3/3 DỰNG UI bằng Pillow ===")
    compose = SKILL / "compose.py"
    frames = ART / "UI/Frames"
    buttons = ART / "UI/Buttons"

    jobs = [
        # Khung panel — 3 cỡ cho 3 mục đích
        ["frame", "--w", "48", "--h", "48", "--border", "4",
         "--color", "#F4A259", "--fill", "#2B1B2E", "--inner", "#5A4A5E",
         "--out", str(frames / "frame_panel.png")],
        ["frame", "--w", "40", "--h", "40", "--border", "3",
         "--color", "#5A4A5E", "--fill", "#1A0F1C",
         "--out", str(frames / "frame_slot.png")],
        ["frame", "--w", "64", "--h", "48", "--border", "3",
         "--color", "#736377", "--fill", "#3A2233", "--corner", "double",
         "--out", str(frames / "frame_tooltip.png")],

        # Nút — 4 trạng thái trong 1 sheet
        ["button", "--w", "96", "--h", "28",
         "--color", "#F4A259", "--fill", "#5A3A2E",
         "--out", str(buttons / "btn_primary.png")],
        ["button", "--w", "72", "--h", "24",
         "--color", "#736377", "--fill", "#3A2233",
         "--out", str(buttons / "btn_secondary.png")],
        ["button", "--w", "120", "--h", "32",
         "--color", "#FFD166", "--fill", "#6B5210",
         "--out", str(buttons / "btn_endturn.png")],

        # Thanh chỉ số — 3 lớp (khung / fill / trail)
        ["healthbar", "--w", "96", "--h", "10", "--fill", "#E63946",
         "--bg", "#3A2233", "--border", "#F4A259",
         "--out", str(frames / "bar_hp.png")],
        ["healthbar", "--w", "96", "--h", "8", "--fill", "#457B9D",
         "--bg", "#12304A", "--border", "#F4A259",
         "--out", str(frames / "bar_sp.png")],
        ["healthbar", "--w", "64", "--h", "5", "--fill", "#FFD166",
         "--bg", "#6B5210", "--border", "#B8901E", "--segments", "4",
         "--out", str(frames / "bar_poise.png")],
        ["healthbar", "--w", "120", "--h", "6", "--fill", "#7BC950",
         "--bg", "#1B3D1F", "--border", "#3D7A2E",
         "--out", str(frames / "bar_exp.png")],
        ["healthbar", "--w", "140", "--h", "12", "--fill", "#FFB703",
         "--bg", "#6B5210", "--border", "#FFD166", "--style", "gloss",
         "--out", str(frames / "bar_ultimate.png")],
        # HP bar nổi trên đầu unit (theo game_2.jpg)
        ["healthbar", "--w", "32", "--h", "4", "--fill", "#E63946",
         "--bg", "#1A0F1C", "--border", "#0D080E", "--style", "flat",
         "--out", str(frames / "bar_unit_hp.png")],
    ]

    # Khung độ hiếm cho icon
    for rarity in ("common", "rare", "epic", "legendary", "mythic"):
        jobs.append(["icon-frame", "--rarity", rarity, "--size", "40",
                     "--out", str(frames / f"frame_rarity_{rarity}.png")])

    for j in jobs:
        run([sys.executable, str(compose)] + j)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--process", action="store_true", help="Chỉ xử lý lại raw/ đã có")
    ap.add_argument("--ui", action="store_true", help="Chỉ dựng UI")
    ap.add_argument("--generate", action="store_true", help="Chỉ sinh ảnh")
    args = ap.parse_args()

    only = args.process or args.ui or args.generate

    if args.generate or not only:
        generate()
    if args.process or not only:
        process()
    if args.ui or not only:
        build_ui()

    print(f"\nXong. Asset ở {ART.relative_to(ROOT)}")
    print("Bước tiếp: refresh Unity để import (Point filter, PPU 32).")


if __name__ == "__main__":
    main()

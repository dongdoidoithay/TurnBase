#!/usr/bin/env python3
"""
draw_pill_bar.py — khung/fill thanh HP/SP/ULT dạng "viên thuốc" (2 đầu bo tròn hoàn toàn) thay cho
thanh chữ nhật viền vuông hiện có (`healthbar_hp_frame.png`) — theo đúng silhouette
_Reference/Art_Sample/Screen_combat.jpg (task "UI Screen Battle chưa giống sample", redesign layout
đợt 2). Vẽ thủ tục bằng Pillow, KHÔNG crop ảnh mẫu — cùng nguyên tắc dự án đã áp dụng cho mọi chrome
khác (xem task-ui-chrome-popups.md §3.6).

2 file:
  bar_pill_frame.png — khung ngoài (viền vàng đậm + nền tối), border 9-slice = bán kính (12,12,4,4)
                        để 2 đầu tròn giữ nguyên khi kéo giãn theo chiều ngang.
  bar_pill_fill.png   — khối fill trắng phẳng cùng silhouette viên thuốc (kéo hơi hẹp hơn frame) —
                        dùng với Image.Type.Filled Horizontal + tint màu runtime (HP xanh/vàng/đỏ
                        theo %, SP xanh dương, ULT vàng) y hệt cách Fill cũ đã hoạt động.

Dùng: python3 draw_pill_bar.py --out-dir out/
"""
import argparse
from pathlib import Path

from PIL import Image, ImageDraw

W, H = 64, 24
R = H // 2  # bán kính đầu tròn = nửa chiều cao

OUTLINE = (13, 8, 14, 255)
GOLD_BORDER = (244, 162, 89, 255)
DARK_FILL = (43, 27, 46, 255)


def _new():
    return Image.new("RGBA", (W, H), (0, 0, 0, 0))


def bar_pill_frame():
    img = _new()
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([0, 0, W - 1, H - 1], radius=R, fill=GOLD_BORDER, outline=OUTLINE, width=1)
    bt = 3  # border thickness
    d.rounded_rectangle([bt, bt, W - 1 - bt, H - 1 - bt], radius=R - bt, fill=DARK_FILL)
    return img


def bar_pill_fill():
    img = _new()
    d = ImageDraw.Draw(img)
    inset = 3
    d.rounded_rectangle([inset, inset, W - 1 - inset, H - 1 - inset],
                         radius=R - inset, fill=(255, 255, 255, 255))
    return img


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out-dir", required=True)
    args = ap.parse_args()
    out = Path(args.out_dir)
    out.mkdir(parents=True, exist_ok=True)
    bar_pill_frame().save(out / "bar_pill_frame.png")
    bar_pill_fill().save(out / "bar_pill_fill.png")
    print(f"  ✓ bar_pill_frame.png  ✓ bar_pill_fill.png  ({W}x{H}, radius={R})")


if __name__ == "__main__":
    main()

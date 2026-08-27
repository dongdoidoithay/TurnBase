#!/usr/bin/env python3
"""Ve tay (khong crop JPEG) 1 the bai scalloped-top sach, cung silhouette voi Screen_combat.jpg
nhung render crisp (khong nhieu nen JPEG). Dung ImageDraw thuan, giong ky thuat outline-dilate
da dung cho character_draw.py/monster_draw.py."""
import sys
from PIL import Image, ImageDraw

W, H = 96, 128
SCALE = 1  # ve o do phan giai nay luon, khong AA

def draw_card(border_color, fill_color, inner_color, scallop_count=5):
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    body_top = 26      # noi than the bai thang bat dau (duoi hang scallop)
    left, right = 4, W - 5
    bottom = H - 6
    body_w = right - left
    bt = 4  # border thickness

    # ---- lop 1: outline mau (than + vom scallop lien mach) ----
    d.rounded_rectangle([left, body_top, right, bottom], radius=6, fill=border_color)
    # hang scallop dinh — cac hinh tron CHONG LAN nhau (spacing < 2r) va LUN xuong than
    # (tam tron nam DUOI body_top) de khong co khe ho giua scallop va than.
    scallop_r = body_w / (scallop_count * 1.55)
    spacing = body_w / scallop_count
    cx0 = left + spacing / 2
    cy = body_top + scallop_r * 0.25
    for i in range(scallop_count):
        cx = cx0 + i * spacing
        d.ellipse([cx - scallop_r, cy - scallop_r, cx + scallop_r, cy + scallop_r], fill=border_color)

    # ---- lop 2: fill trong (inset border_thickness), CHONG LAN tuong tu ----
    d.rounded_rectangle([left + bt, body_top + bt, right - bt, bottom - bt],
                         radius=4, fill=fill_color)
    scallop_r2 = scallop_r - bt
    for i in range(scallop_count):
        cx = cx0 + i * spacing
        d.ellipse([cx - scallop_r2, cy - scallop_r2, cx + scallop_r2, cy + scallop_r2],
                   fill=fill_color)

    # ---- lop 3: inner highlight line (1px, sat border) ----
    d.rounded_rectangle([left + bt, body_top + bt, right - bt, bottom - bt],
                         radius=4, outline=inner_color, width=1)

    # ---- chan the (4 nhon nho duoi, kieu tua/fringe) ----
    fringe_n = 6
    fringe_w = body_w / fringe_n
    for i in range(fringe_n):
        fx = left + i * fringe_w
        d.polygon([(fx, bottom), (fx + fringe_w * 0.5, bottom + 5), (fx + fringe_w, bottom)],
                   fill=border_color)

    return img


def main():
    variant = sys.argv[1] if len(sys.argv) > 1 else "normal"
    out = sys.argv[2] if len(sys.argv) > 2 else f"card_{variant}.png"

    palettes = {
        "normal":   ((244, 162, 89, 255), (43, 27, 46, 255), (255, 217, 160, 200)),
        "selected": ((255, 209, 102, 255), (58, 32, 51, 255), (255, 240, 184, 230)),
        "disabled": ((90, 74, 94, 255), (26, 15, 28, 255), (115, 99, 119, 160)),
    }
    border, fill, inner = palettes[variant]
    img = draw_card(border, fill, inner)
    img.save(out)
    print(f"saved {out} {img.size}")


if __name__ == "__main__":
    main()

#!/usr/bin/env python3
"""
ui_glow_frame.py — Panel 9-slice "gloss trên nền pixel-art" (task-ui-vfx-polish.md Giai đoạn 2).

compose.py `make_frame` gốc chỉ vẽ 1 màu fill + 1 màu viền phẳng — quá thô cho mục tiêu kết hợp
UI_01 (gradient/glow/rim sáng) lên trên UI_02/pixel-art hiện có. Script MỚI riêng (không sửa
compose.py, giữ nguyên hệ generator cũ đang chạy tốt cho các asset khác), vẽ thêm:
  - Fill gradient dọc BĂNG (banded, không blur) — 4 tông từ 1 family màu palette.json.
  - Viền vàng 2 lớp (tối ngoài + sáng trong) tạo cảm giác "glow rim" thay vì viền đơn sắc.
  - 1 dòng highlight trắng-ngà mỏng NGAY dưới cạnh trên — ánh sáng hắt từ trên, kiểu UI_01.
  - Góc có ngoặc vàng nhỏ (bracket) — giữ tinh thần khung pixel-art góc vát của UI_02.
Mọi màu lấy thẳng từ palette.json, không bịa màu mới.

Dùng:
    python3 ui_glow_frame.py --w 64 --h 64 --border 14 \
        --family teal --accent gold --out out/ui_rounded_panel.png
"""
import argparse
import json
from pathlib import Path

from PIL import Image, ImageDraw

PALETTE_FAMILIES = {
    # dark -> light, đúng thứ tự trong palette.json
    "teal": ["#1B3A42", "#2E6B78", "#4EC3D9", "#A5E8F0"],
    "blue": ["#12304A", "#2A5A80", "#457B9D", "#8FC0D9"],
    "brown": ["#3A2416", "#6B4526", "#A67142", "#D4A574"],
    "violet": ["#2A1B3A", "#5A3080", "#9B5DE5", "#CBA5F0"],
}
GOLD = {"dark": "#6B5210", "bright": "#FFD166", "pale": "#FFF0B8"}
CREAM = "#F2E8CF"


def hexrgb(s):
    s = s.lstrip("#")
    return (int(s[0:2], 16), int(s[2:4], 16), int(s[4:6], 16), 255)


def lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(4))


def gradient_color(stops, u):
    """stops: [(t, color), ...] tăng dần theo t. Nội suy tuyến tính giữa 2 mốc kề nhau."""
    for (t0, c0), (t1, c1) in zip(stops, stops[1:]):
        if t0 <= u <= t1:
            local = 0 if t1 == t0 else (u - t0) / (t1 - t0)
            return lerp(c0, c1, local)
    return stops[-1][1]


NEUTRAL = {
    "white": (255, 255, 255, 255),
    "bright": (238, 238, 238, 255),
    "mid": (196, 196, 196, 255),
    "shade": (120, 120, 120, 255),
    "dark": (40, 40, 40, 255),
}


def make_glow_frame(w, h, border, family_key=None, corner_bracket=10):
    """QUAN TRỌNG: sprite này dùng chung cho Panel/Button/Fill/CloseButton... ở MỌI màn (10/11
    màn UI + 2 widget) — mỗi nơi tự nhân màu riêng qua `Image.color` (đã xác nhận: bản gốc fill
    trung tâm là (255,255,255,255) THUẦN TRẮNG, không màu — cả hệ thống dựa vào việc base neutral
    để tint nhân đúng màu mong muốn). Vẽ MÀU THẬT (teal/gold) vào đây sẽ phá vỡ tint của TẤT CẢ
    prefab cùng lúc (VD Panel tint cam × gradient teal của tôi = ra màu bùn, sai hẳn ý đồ màu cam
    parchment gốc). Vì vậy bản sửa ĐÚNG là vẽ SHAPE (gradient sáng-tối + bevel + góc) bằng
    GRAYSCALE thuần — nhân với BẤT KỲ tint nào ở Unity vẫn ra đúng hue mong muốn, chỉ thêm chiều
    sâu/gloss/rim mà bản trắng phẳng cũ không có. `family_key` giữ lại tham số nhưng KHÔNG dùng
    màu palette nữa — chỉ neutral.
    """
    white, bright, mid, shade, dark = (NEUTRAL[k] for k in ("white", "bright", "mid", "shade", "dark"))

    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    # 1) Fill nền — gradient dọc trắng->xám nhạt (KHÔNG xuống quá tối, giữ tint nhân màu vẫn rõ
    #    ràng chứ không bị đen thui) — chỉ đủ tạo cảm giác khối tròn/gloss như UI_01.
    stops = [(0.0, white), (0.15, bright), (0.55, mid), (1.0, shade)]
    for y in range(h):
        u = y / (h - 1)
        d.line([(0, y), (w - 1, y)], fill=gradient_color(stops, u))

    # 2) Viền bevel 2 lớp — tối ngoài (neo khối) + sáng trong (rim) — dưới BẤT KỲ tint nào cũng
    #    đọc đúng là "viền nổi khối", không áp đặt hue riêng.
    d.rectangle([0, 0, w - 1, h - 1], outline=dark, width=1)
    d.rectangle([1, 1, w - 2, h - 2], outline=white, width=1)

    # 3) Highlight hắt sáng cạnh TRÊN — ánh sáng từ trên xuống, nhất quán 1 hướng như UI_01.
    d.line([(3, 2), (w - 4, 2)], fill=white)

    # 4) Bóng đổ nhẹ cạnh dưới (đối trọng highlight trên — cảm giác khối nổi).
    d.line([(3, h - 3), (w - 4, h - 3)], fill=shade)

    # 5) Ngoặc góc sáng (bracket) dày 2px — 4 góc, thay cho notch đơn thuần, rõ hơn hẳn viền.
    cb = corner_bracket
    corners = [(0, 0, 1, 1), (w - 1, 0, -1, 1), (0, h - 1, 1, -1), (w - 1, h - 1, -1, -1)]
    for (cx, cy, sx, sy) in corners:
        d.line([(cx, cy + sy * 2), (cx, cy + sy * cb)], fill=white, width=2)
        d.line([(cx + sx * 2, cy), (cx + sx * cb, cy)], fill=white, width=2)
        for k in range(2):
            img.putpixel((cx + sx * k, cy), (0, 0, 0, 0))
            img.putpixel((cx, cy + sy * k), (0, 0, 0, 0))

    meta = {
        "left": border, "right": border, "top": border, "bottom": border,
        "note": "Biên 9-slice khớp sprite hiện có (giữ nguyên GUID/import settings khi ghi đè).",
    }
    return img, meta


# ============================================================== BUTTON (cùng ngôn ngữ với panel)

BUTTON_STATES = ["normal", "hover", "pressed", "disabled"]


def _shade(c, f):
    return (min(255, int(c[0] * f)), min(255, int(c[1] * f)), min(255, int(c[2] * f)), c[3])


def make_glow_button(w, h, family_key=None):
    """Nút bấm cùng ngôn ngữ thị giác với make_glow_frame: gradient dọc + viền bevel 2 lớp +
    highlight cạnh trên. NEUTRAL (trắng/xám) — xem lý do ở make_glow_frame. 4 trạng thái xếp dọc
    trong 1 sheet (đúng quy ước compose.make_button cũ) để tái dùng logic cắt/slice quen thuộc."""
    base_bright, base_mid, base_dark = NEUTRAL["white"], NEUTRAL["mid"], NEUTRAL["shade"]
    rim_dark = NEUTRAL["dark"]

    sheet = Image.new("RGBA", (w, h * len(BUTTON_STATES)), (0, 0, 0, 0))
    variants = {
        "normal":   (1.0, 0),
        "hover":    (1.18, 0),
        "pressed":  (0.82, 1),
        "disabled": (0.5, 0),
    }

    for i, state in enumerate(BUTTON_STATES):
        f, offset = variants[state]
        btn = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        d = ImageDraw.Draw(btn)
        top = offset
        stops = [(0.0, _shade(base_bright, f)), (0.4, _shade(base_mid, f)), (1.0, _shade(base_dark, f))]
        for y in range(top, h):
            u = (y - top) / max(1, (h - 1 - top))
            d.line([(0, y), (w - 1, y)], fill=gradient_color(stops, u))
        d.rectangle([0, top, w - 1, h - 1], outline=_shade(rim_dark, f), width=1)
        if state != "pressed":
            d.line([2, top + 1, w - 3, top + 1], fill=_shade(NEUTRAL["white"], f))
        for (cx, cy) in ((0, top), (w - 1, top), (0, h - 1), (w - 1, h - 1)):
            btn.putpixel((cx, cy), (0, 0, 0, 0))
        sheet.paste(btn, (0, i * h), btn)

    meta = {"states": BUTTON_STATES, "frame_w": w, "frame_h": h,
            "left": 6, "right": 6, "top": 6, "bottom": 6}
    return sheet, meta


BRONZE = {
    "outline": (13, 8, 5, 255),
    "shadow": (90, 52, 24, 255),
    "mid": (139, 90, 43, 255),
    "highlight": (216, 154, 92, 255),
    "rivet": (21, 16, 10, 255),
    "inner_line": (36, 26, 16, 255),
}
SLATE = {
    "top": (74, 85, 104, 255),
    "bottom": (51, 59, 74, 255),
}


def make_bronze_frame(w, h, border, rivet_inset=4):
    """Khung bronze-riveted style UI_02 (task-ui-vfx-polish.md — yêu cầu '100% giống UI_02').
    KHÁC HẲN make_glow_frame: ở đây bake MÀU THẬT (bronze+slate), không neutral — vì UI_02 không
    biến thiên hue giữa các panel (mọi khung đều bronze/navy đồng nhất), nên mọi nơi dùng sprite
    này phải đặt Image.color TRẮNG (không tint) để màu bake hiển thị đúng — xem ghi chú trong
    task file về việc reset tint hàng loạt ở các prefab dùng chung sprite này."""
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    # Fill — gradient slate dọc, nhạt hơn ở trên (ánh sáng hắt xuống, nhất quán các asset khác).
    stops = [(0.0, SLATE["top"]), (1.0, SLATE["bottom"])]
    for y in range(h):
        u = y / (h - 1)
        d.line([(0, y), (w - 1, y)], fill=gradient_color(stops, u))

    # Viền bronze bevel — outline tối ngoài cùng, rồi MID phẳng làm nền viền, sau đó bevel THẬT
    # (chỉ 2 cạnh sáng trên/trái + 2 cạnh tối dưới/phải, không phải rectangle đều) mô phỏng ánh
    # sáng 1 hướng như tấm kim loại dập nổi, cuối cùng 1 đường phân tách tối trước khi vào fill.
    d.rectangle([0, 0, w - 1, h - 1], outline=BRONZE["outline"], width=1)
    d.rectangle([1, 1, w - 2, h - 2], fill=None, outline=BRONZE["mid"], width=3)
    # bevel sáng: cạnh TRÊN + TRÁI
    d.line([(1, 1), (w - 2, 1)], fill=BRONZE["highlight"], width=1)
    d.line([(1, 1), (1, h - 2)], fill=BRONZE["highlight"], width=1)
    # bevel tối: cạnh DƯỚI + PHẢI
    d.line([(1, h - 2), (w - 2, h - 2)], fill=BRONZE["shadow"], width=1)
    d.line([(w - 2, 1), (w - 2, h - 2)], fill=BRONZE["shadow"], width=1)
    d.rectangle([4, 4, w - 5, h - 5], outline=BRONZE["inner_line"], width=1)

    # Rivet (đinh tán) 4 góc — 1 chấm tròn nhỏ mỗi góc, chữ ký nhận diện khung kim loại UI_02.
    ri = rivet_inset
    for (cx, cy) in ((ri, ri), (w - 1 - ri, ri), (ri, h - 1 - ri), (w - 1 - ri, h - 1 - ri)):
        d.ellipse([cx - 1, cy - 1, cx + 1, cy + 1], fill=BRONZE["rivet"], outline=BRONZE["highlight"])

    meta = {"left": border, "right": border, "top": border, "bottom": border}
    return img, meta


def make_glow_button_flat(w, h, border, family_key=None):
    """1 sprite duy nhất (không phải sheet 4 trạng thái) — dùng cho Button.Transition=ColorTint
    có sẵn của Unity (multiply màu lúc hover/pressed). NEUTRAL (trắng/xám) — xem lý do ở
    make_glow_frame: nhân với Image.color mới ra đúng hue mong muốn ở nơi dùng, không bịa màu."""
    white, mid, shade = NEUTRAL["white"], NEUTRAL["mid"], NEUTRAL["shade"]

    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    stops = [(0.0, white), (0.45, mid), (1.0, shade)]
    for y in range(h):
        u = y / (h - 1)
        d.line([(0, y), (w - 1, y)], fill=gradient_color(stops, u))
    d.rectangle([0, 0, w - 1, h - 1], outline=NEUTRAL["dark"], width=1)
    d.line([2, 1, w - 3, 1], fill=white)
    for (cx, cy) in ((0, 0), (w - 1, 0), (0, h - 1), (w - 1, h - 1)):
        img.putpixel((cx, cy), (0, 0, 0, 0))

    meta = {"left": border, "right": border, "top": border, "bottom": border}
    return img, meta


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--mode", choices=["frame", "button", "button-flat", "bronze"], default="frame")
    ap.add_argument("--w", type=int, default=64)
    ap.add_argument("--h", type=int, default=64)
    ap.add_argument("--border", type=int, default=14)
    ap.add_argument("--family", default="teal")
    ap.add_argument("--out", required=True)
    args = ap.parse_args()

    if args.mode == "button":
        img, meta = make_glow_button(args.w, args.h, args.family)
    elif args.mode == "button-flat":
        img, meta = make_glow_button_flat(args.w, args.h, args.border, args.family)
    elif args.mode == "bronze":
        img, meta = make_bronze_frame(args.w, args.h, args.border)
    else:
        img, meta = make_glow_frame(args.w, args.h, args.border, args.family)

    out = Path(args.out)
    out.parent.mkdir(parents=True, exist_ok=True)
    img.save(out)
    out.with_suffix(".9.json").write_text(json.dumps(meta, indent=2))
    print(f"  ✓ {out} ({img.size[0]}x{img.size[1]})")


if __name__ == "__main__":
    main()

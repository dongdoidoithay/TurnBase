#!/usr/bin/env python3
"""
compose.py — Lắp ráp asset "kỹ thuật" bằng Pillow, KHÔNG dùng AI.

Những thứ cần đối xứng tuyệt đối và co giãn đúng thì phải vẽ bằng code:
frame 9-slice, button, health bar, icon frame, HUD, minimap, tileset,
spritesheet, atlas.

Dùng:
    python3 compose.py frame --w 64 --h 64 --border 6 --out ui/frame.png
    python3 compose.py button --w 96 --h 32 --out ui/btn.png
    python3 compose.py healthbar --w 96 --h 10 --out ui/bar_hp.png
    python3 compose.py icon-frame --icon icons/fire.png --rarity epic --out out.png
    python3 compose.py spritesheet --in frames/ --cols 8 --out sheet.png --meta
    python3 compose.py tileset --tile grass.png --out tileset.png --make-seamless
    python3 compose.py atlas --in sprites/ --out atlas.png
    python3 compose.py minimap --nodes map.json --out minimap.png
"""

import argparse
import json
import math
from pathlib import Path

try:
    from PIL import Image, ImageDraw
except ImportError:
    raise SystemExit("Cần Pillow: pip install Pillow")


def hex2rgba(s, alpha=255):
    if isinstance(s, (tuple, list)):
        return tuple(s) if len(s) == 4 else (*s, alpha)
    s = s.lstrip("#")
    if len(s) == 8:
        return tuple(int(s[i:i + 2], 16) for i in (0, 2, 4, 6))
    return (int(s[0:2], 16), int(s[2:4], 16), int(s[4:6], 16), alpha)


def save(img, path, meta=None):
    p = Path(path)
    p.parent.mkdir(parents=True, exist_ok=True)
    img.save(p)
    if meta:
        p.with_suffix(".9.json").write_text(json.dumps(meta, indent=2))
    print(f"  ✓ {p} ({img.size[0]}×{img.size[1]})")


# ============================================================== FRAME 9-SLICE

def make_frame(w, h, border, color, fill, inner=None, corner_style="notch"):
    """
    Khung 9-slice. Biên phải < nửa kích thước, nếu không engine co giãn sẽ méo.
    corner_style: 'notch' (vát góc, kiểu retro) | 'square' | 'double'
    """
    border = min(border, (min(w, h) - 2) // 2)
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    c_border = hex2rgba(color)
    c_fill = hex2rgba(fill)
    c_inner = hex2rgba(inner) if inner else None

    d.rectangle([0, 0, w - 1, h - 1], fill=c_fill, outline=c_border, width=border)

    if c_inner and border >= 3:
        gap = border - 1
        d.rectangle([gap, gap, w - 1 - gap, h - 1 - gap], outline=c_inner, width=1)

    if corner_style == "notch":
        # Vát 1 pixel ở 4 góc — dấu hiệu nhận biết của UI pixel-art
        for (cx, cy) in ((0, 0), (w - 1, 0), (0, h - 1), (w - 1, h - 1)):
            img.putpixel((cx, cy), (0, 0, 0, 0))
    elif corner_style == "double":
        d.rectangle([0, 0, w - 1, h - 1], outline=c_border, width=1)
        d.rectangle([2, 2, w - 3, h - 3], outline=c_border, width=1)

    meta = {"left": border + 1, "right": border + 1,
            "top": border + 1, "bottom": border + 1,
            "note": "Biên 9-slice. Unity: đặt vào Sprite Border, bật Full Rect."}
    return img, meta


# ============================================================== BUTTON

BUTTON_STATES = ["normal", "hover", "pressed", "disabled"]


def make_button(w, h, color, fill, text_area=True):
    """Sinh 4 trạng thái trong 1 sheet dọc + meta."""
    sheet = Image.new("RGBA", (w, h * len(BUTTON_STATES)), (0, 0, 0, 0))

    base_border = hex2rgba(color)
    base_fill = hex2rgba(fill)

    def shade(c, f):
        return (min(255, int(c[0] * f)), min(255, int(c[1] * f)),
                min(255, int(c[2] * f)), c[3])

    variants = {
        "normal":   (base_border, base_fill, 0),
        "hover":    (shade(base_border, 1.25), shade(base_fill, 1.30), 0),
        "pressed":  (shade(base_border, 0.80), shade(base_fill, 0.75), 1),  # dịch xuống 1px
        "disabled": (shade(base_border, 0.45), shade(base_fill, 0.55), 0),
    }

    for i, state in enumerate(BUTTON_STATES):
        bcol, fcol, offset = variants[state]
        btn = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        d = ImageDraw.Draw(btn)
        top = offset
        d.rectangle([0, top, w - 1, h - 1], fill=fcol, outline=bcol, width=2)
        if state != "pressed":
            # Highlight cạnh trên = khối nổi
            d.line([2, top + 1, w - 3, top + 1],
                   fill=(min(255, fcol[0] + 40), min(255, fcol[1] + 40),
                         min(255, fcol[2] + 40), 255))
        for (cx, cy) in ((0, top), (w - 1, top), (0, h - 1), (w - 1, h - 1)):
            btn.putpixel((cx, cy), (0, 0, 0, 0))
        sheet.paste(btn, (0, i * h), btn)

    meta = {"states": BUTTON_STATES, "frame_w": w, "frame_h": h,
            "left": 3, "right": 3, "top": 3, "bottom": 3}
    return sheet, meta


# ============================================================== HEALTH BAR

def make_healthbar(w, h, fill, bg, border, segments=0, style="flat"):
    """
    Sinh 3 lớp trong 1 sheet dọc: [khung rỗng, lớp fill đầy, lớp fill mờ (damage trail)].
    Engine chỉ cần scale lớp fill theo % máu.
    """
    sheet = Image.new("RGBA", (w, h * 3), (0, 0, 0, 0))
    c_fill = hex2rgba(fill)
    c_bg = hex2rgba(bg)
    c_border = hex2rgba(border)

    # Lớp 0 — khung + nền
    frame = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(frame)
    d.rectangle([0, 0, w - 1, h - 1], fill=c_bg, outline=c_border, width=1)
    for (cx, cy) in ((0, 0), (w - 1, 0), (0, h - 1), (w - 1, h - 1)):
        frame.putpixel((cx, cy), (0, 0, 0, 0))

    # Lớp 1 — fill đầy
    bar = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d2 = ImageDraw.Draw(bar)
    d2.rectangle([1, 1, w - 2, h - 2], fill=c_fill)
    if style == "gloss" and h >= 5:
        gloss = (min(255, c_fill[0] + 55), min(255, c_fill[1] + 55),
                 min(255, c_fill[2] + 55), 255)
        d2.line([1, 1, w - 2, 1], fill=gloss)
        dark = (int(c_fill[0] * 0.7), int(c_fill[1] * 0.7), int(c_fill[2] * 0.7), 255)
        d2.line([1, h - 2, w - 2, h - 2], fill=dark)

    if segments > 1:
        step = (w - 2) / segments
        for s in range(1, segments):
            x = int(1 + step * s)
            d2.line([x, 1, x, h - 2], fill=c_border)

    # Lớp 2 — damage trail (trắng mờ, hiện khi mất máu)
    trail = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    ImageDraw.Draw(trail).rectangle([1, 1, w - 2, h - 2], fill=(255, 255, 255, 160))

    for i, layer in enumerate((frame, bar, trail)):
        sheet.paste(layer, (0, i * h), layer)

    meta = {"layers": ["frame", "fill", "trail"], "frame_w": w, "frame_h": h,
            "fill_rect": [1, 1, w - 2, h - 2]}
    return sheet, meta


# ============================================================== ICON FRAME

RARITY_COLORS = {
    "common":    ("#9A9A9A", "#2E2E33"),
    "rare":      ("#4EA8DE", "#1B2A3A"),
    "epic":      ("#9B5DE5", "#2A1B3A"),
    "legendary": ("#F4A259", "#3A2A1B"),
    "mythic":    ("#E63946", "#3A1B22"),
}


def make_icon_frame(icon_path, rarity, size, border=2):
    border_col, bg_col = RARITY_COLORS.get(rarity, RARITY_COLORS["common"])
    frame, _ = make_frame(size, size, border, border_col, bg_col)

    if icon_path and Path(icon_path).exists():
        icon = Image.open(icon_path).convert("RGBA")
        inner = size - border * 2 - 2
        if icon.size[0] != inner:
            icon = icon.resize((inner, inner), Image.NEAREST)
        frame.paste(icon, (border + 1, border + 1), icon)
    return frame


# ============================================================== SPRITESHEET

def make_spritesheet(frame_dir, cols, out, write_meta=True, padding=0):
    files = sorted(Path(frame_dir).glob("*.png"))
    if not files:
        raise SystemExit(f"Không có frame nào trong {frame_dir}")

    frames = [Image.open(f).convert("RGBA") for f in files]
    fw = max(f.size[0] for f in frames)
    fh = max(f.size[1] for f in frames)
    cols = min(cols, len(frames))
    rows = math.ceil(len(frames) / cols)

    sheet = Image.new("RGBA",
                      (cols * (fw + padding) - padding if padding else cols * fw,
                       rows * (fh + padding) - padding if padding else rows * fh),
                      (0, 0, 0, 0))

    entries = []
    for i, fr in enumerate(frames):
        c, r = i % cols, i // cols
        x = c * (fw + padding)
        y = r * (fh + padding)
        # Căn đáy giữa để nhân vật không nhảy giữa các frame
        ox = x + (fw - fr.size[0]) // 2
        oy = y + (fh - fr.size[1])
        sheet.paste(fr, (ox, oy), fr)
        entries.append({"name": files[i].stem, "index": i,
                        "x": x, "y": y, "w": fw, "h": fh})

    meta = {"frame_w": fw, "frame_h": fh, "cols": cols, "rows": rows,
            "count": len(frames), "frames": entries}
    save(sheet, out)
    if write_meta:
        Path(out).with_suffix(".json").write_text(json.dumps(meta, indent=2))
        print(f"  ✓ {Path(out).with_suffix('.json')}")
    return sheet, meta


# ============================================================== TILESET

def make_seamless(tile: Image.Image) -> Image.Image:
    """Mirror 4 góc để biên luôn khớp — cách đơn giản nhất luôn cho tile liền mạch."""
    w, h = tile.size
    out = Image.new("RGBA", (w, h))
    half_w, half_h = w // 2, h // 2
    tl = tile.crop((0, 0, half_w, half_h))
    out.paste(tl, (0, 0))
    out.paste(tl.transpose(Image.FLIP_LEFT_RIGHT), (half_w, 0))
    out.paste(tl.transpose(Image.FLIP_TOP_BOTTOM), (0, half_h))
    out.paste(tl.transpose(Image.FLIP_LEFT_RIGHT).transpose(Image.FLIP_TOP_BOTTOM),
              (half_w, half_h))
    return out


# 16-tile blob: bitmask 4 hướng (bắc=1, đông=2, nam=4, tây=8)
def make_tileset_16(base_tile: Image.Image, edge_color, out):
    """Sinh bộ 16 tile auto-tiling từ 1 tile nền + màu viền."""
    ts = base_tile.size[0]
    sheet = Image.new("RGBA", (ts * 4, ts * 4), (0, 0, 0, 0))
    ec = hex2rgba(edge_color)

    for mask in range(16):
        tile = base_tile.copy()
        d = ImageDraw.Draw(tile)
        # Vẽ viền ở cạnh KHÔNG có hàng xóm
        if not mask & 1:  d.line([0, 0, ts - 1, 0], fill=ec, width=2)            # bắc
        if not mask & 2:  d.line([ts - 1, 0, ts - 1, ts - 1], fill=ec, width=2)  # đông
        if not mask & 4:  d.line([0, ts - 1, ts - 1, ts - 1], fill=ec, width=2)  # nam
        if not mask & 8:  d.line([0, 0, 0, ts - 1], fill=ec, width=2)            # tây
        sheet.paste(tile, ((mask % 4) * ts, (mask // 4) * ts))

    meta = {"tile_size": ts, "layout": "16-blob",
            "bitmask": {"north": 1, "east": 2, "south": 4, "west": 8},
            "index": "row = mask // 4, col = mask % 4"}
    save(sheet, out, None)
    Path(out).with_suffix(".json").write_text(json.dumps(meta, indent=2))
    return sheet


# ============================================================== ATLAS

def make_atlas(src_dir, out, max_size=2048, padding=2):
    """Đóng gói sprite vào atlas bằng thuật toán shelf (đủ tốt cho pixel-art)."""
    files = sorted(Path(src_dir).rglob("*.png"))
    if not files:
        raise SystemExit(f"Không có sprite nào trong {src_dir}")

    imgs = [(f, Image.open(f).convert("RGBA")) for f in files]
    imgs.sort(key=lambda t: -t[1].size[1])  # cao trước, giảm khoảng trống

    entries, x, y, shelf_h, width = [], 0, 0, 0, 0
    for f, im in imgs:
        w, h = im.size
        if x + w + padding > max_size:
            x, y = 0, y + shelf_h + padding
            shelf_h = 0
        entries.append({"name": f.stem, "x": x, "y": y, "w": w, "h": h, "_img": im})
        x += w + padding
        shelf_h = max(shelf_h, h)
        width = max(width, x)

    height = y + shelf_h
    atlas = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    for e in entries:
        atlas.paste(e["_img"], (e["x"], e["y"]), e["_img"])
        del e["_img"]

    save(atlas, out)
    Path(out).with_suffix(".json").write_text(
        json.dumps({"size": [width, height], "sprites": entries}, indent=2))
    print(f"  ✓ {len(entries)} sprite → atlas {width}×{height}")


# ============================================================== MINIMAP

def make_minimap(nodes_json, out, cell=16, node_r=5):
    """Minimap từ node graph — dùng cho bản đồ phân nhánh kiểu roguelite."""
    data = json.loads(Path(nodes_json).read_text())
    nodes = data["nodes"]
    edges = data.get("edges", [])

    xs = [n["x"] for n in nodes]
    ys = [n["y"] for n in nodes]
    w = (max(xs) - min(xs) + 2) * cell
    h = (max(ys) - min(ys) + 2) * cell
    ox, oy = -min(xs) + 1, -min(ys) + 1

    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    type_colors = {
        "battle": "#E63946", "elite": "#9B5DE5", "boss": "#F4A259",
        "shop": "#4EA8DE", "rest": "#7BC950", "treasure": "#FFD166",
        "event": "#F2E8CF", "start": "#9A9A9A",
    }

    for a, b in edges:
        na, nb = nodes[a], nodes[b]
        d.line([(na["x"] + ox) * cell, (na["y"] + oy) * cell,
                (nb["x"] + ox) * cell, (nb["y"] + oy) * cell],
               fill=hex2rgba("#5A4A5E"), width=1)

    for n in nodes:
        cx, cy = (n["x"] + ox) * cell, (n["y"] + oy) * cell
        col = hex2rgba(type_colors.get(n.get("type", "battle"), "#E63946"))
        d.rectangle([cx - node_r, cy - node_r, cx + node_r, cy + node_r],
                    fill=col, outline=hex2rgba("#2B1B2E"))

    save(img, out)


# ============================================================== HUD

def make_hud(layout_json, out):
    """Ghép HUD từ các mảnh đã có theo file layout — dùng để xem trước bố cục."""
    layout = json.loads(Path(layout_json).read_text())
    w, h = layout["size"]
    img = Image.new("RGBA", (w, h), hex2rgba(layout.get("background", "#00000000")))

    for part in layout["parts"]:
        src = Path(part["image"])
        if not src.exists():
            print(f"  ! thiếu {src}")
            continue
        piece = Image.open(src).convert("RGBA")
        if "size" in part:
            piece = piece.resize(tuple(part["size"]), Image.NEAREST)
        img.paste(piece, tuple(part["pos"]), piece)

    save(img, out)


# ============================================================== CLI

def main():
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest="cmd", required=True)

    p = sub.add_parser("frame")
    p.add_argument("--w", type=int, default=64); p.add_argument("--h", type=int, default=64)
    p.add_argument("--border", type=int, default=4)
    p.add_argument("--color", default="#F4A259"); p.add_argument("--fill", default="#2B1B2E")
    p.add_argument("--inner"); p.add_argument("--corner", default="notch",
                                              choices=["notch", "square", "double"])
    p.add_argument("--out", required=True)

    p = sub.add_parser("button")
    p.add_argument("--w", type=int, default=96); p.add_argument("--h", type=int, default=32)
    p.add_argument("--color", default="#F4A259"); p.add_argument("--fill", default="#5A3A2E")
    p.add_argument("--out", required=True)

    p = sub.add_parser("healthbar")
    p.add_argument("--w", type=int, default=96); p.add_argument("--h", type=int, default=10)
    p.add_argument("--fill", default="#E63946"); p.add_argument("--bg", default="#3A2233")
    p.add_argument("--border", default="#F4A259"); p.add_argument("--segments", type=int, default=0)
    p.add_argument("--style", default="gloss", choices=["flat", "gloss"])
    p.add_argument("--out", required=True)

    p = sub.add_parser("icon-frame")
    p.add_argument("--icon"); p.add_argument("--rarity", default="common",
                                             choices=list(RARITY_COLORS))
    p.add_argument("--size", type=int, default=40); p.add_argument("--out", required=True)

    p = sub.add_parser("spritesheet")
    p.add_argument("--in", dest="src", required=True); p.add_argument("--cols", type=int, default=8)
    p.add_argument("--padding", type=int, default=0)
    p.add_argument("--meta", action="store_true"); p.add_argument("--out", required=True)

    p = sub.add_parser("tileset")
    p.add_argument("--tile", required=True); p.add_argument("--edge", default="#1A0F1C")
    p.add_argument("--make-seamless", action="store_true"); p.add_argument("--out", required=True)

    p = sub.add_parser("atlas")
    p.add_argument("--in", dest="src", required=True); p.add_argument("--max-size", type=int, default=2048)
    p.add_argument("--out", required=True)

    p = sub.add_parser("minimap")
    p.add_argument("--nodes", required=True); p.add_argument("--cell", type=int, default=16)
    p.add_argument("--out", required=True)

    p = sub.add_parser("hud")
    p.add_argument("--layout", required=True); p.add_argument("--out", required=True)

    a = ap.parse_args()

    if a.cmd == "frame":
        img, meta = make_frame(a.w, a.h, a.border, a.color, a.fill, a.inner, a.corner)
        save(img, a.out, meta)
    elif a.cmd == "button":
        img, meta = make_button(a.w, a.h, a.color, a.fill)
        save(img, a.out, meta)
    elif a.cmd == "healthbar":
        img, meta = make_healthbar(a.w, a.h, a.fill, a.bg, a.border, a.segments, a.style)
        save(img, a.out, meta)
    elif a.cmd == "icon-frame":
        save(make_icon_frame(a.icon, a.rarity, a.size), a.out)
    elif a.cmd == "spritesheet":
        make_spritesheet(a.src, a.cols, a.out, a.meta, a.padding)
    elif a.cmd == "tileset":
        tile = Image.open(a.tile).convert("RGBA")
        if a.make_seamless:
            tile = make_seamless(tile)
        make_tileset_16(tile, a.edge, a.out)
    elif a.cmd == "atlas":
        make_atlas(a.src, a.out, a.max_size)
    elif a.cmd == "minimap":
        make_minimap(a.nodes, a.out, a.cell)
    elif a.cmd == "hud":
        make_hud(a.layout, a.out)


if __name__ == "__main__":
    main()

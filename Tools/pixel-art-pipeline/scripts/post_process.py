#!/usr/bin/env python3
"""
post_process.py — Biến ảnh AI thành pixel-art đúng chuẩn.

Thứ tự xử lý BẮT BUỘC (đảo là hỏng):
  1. tách nền  2. trim  3. hạ pixel (NEAREST)  4. khoá palette
  5. alpha nhị phân  6. canvas chuẩn, căn đáy giữa

Dùng:
    python3 post_process.py --in raw/ --out clean/ --target-height 48 --key magenta
    python3 post_process.py --in raw/ --out clean/ --palette palette.json
    python3 post_process.py --verify-palette clean/ --palette palette.json
    python3 post_process.py --slice raw/hero.png --cols 4 --out frames/
"""

import argparse
import json
from pathlib import Path

try:
    from PIL import Image
except ImportError:
    raise SystemExit("Cần Pillow: pip install Pillow")

KEY_COLORS = {
    "magenta": (255, 0, 255),
    "green":   (0, 255, 0),
    "black":   (0, 0, 0),
}


# ---------------------------------------------------------------- 1. tách nền

def remove_background(img: Image.Image, key: str = "magenta",
                      tolerance: int = 60, flood_corners: bool = True) -> Image.Image:
    """Tách nền theo màu key; nếu không khớp thì flood-fill từ 4 góc."""
    img = img.convert("RGBA")
    px = img.load()
    w, h = img.size

    if key in KEY_COLORS:
        kr, kg, kb = KEY_COLORS[key]
        hit = 0
        for y in range(h):
            for x in range(w):
                r, g, b, a = px[x, y]
                if abs(r - kr) <= tolerance and abs(g - kg) <= tolerance and abs(b - kb) <= tolerance:
                    px[x, y] = (0, 0, 0, 0)
                    hit += 1
        # Nếu key gần như không trúng gì thì nền không phải màu key
        if hit > w * h * 0.02:
            return img

    if flood_corners:
        return _flood_remove_corners(img, tolerance)
    return img


def _flood_remove_corners(img: Image.Image, tolerance: int) -> Image.Image:
    """Flood-fill trong suốt từ 4 góc — dùng khi nền là màu phẳng bất kỳ."""
    px = img.load()
    w, h = img.size
    seeds = [(0, 0), (w - 1, 0), (0, h - 1), (w - 1, h - 1)]

    for sx, sy in seeds:
        base = px[sx, sy]
        if base[3] == 0:
            continue
        stack = [(sx, sy)]
        seen = set()
        while stack:
            x, y = stack.pop()
            if (x, y) in seen or not (0 <= x < w and 0 <= y < h):
                continue
            seen.add((x, y))
            r, g, b, a = px[x, y]
            if a == 0:
                continue
            if (abs(r - base[0]) <= tolerance and abs(g - base[1]) <= tolerance
                    and abs(b - base[2]) <= tolerance):
                px[x, y] = (0, 0, 0, 0)
                stack.extend([(x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)])
    return img


# ---------------------------------------------------------------- 2. trim

def trim(img: Image.Image) -> Image.Image:
    bbox = img.getbbox()
    return img.crop(bbox) if bbox else img


# ---------------------------------------------------------------- 3. hạ pixel

def downscale(img: Image.Image, target_height: int) -> Image.Image:
    """NEAREST luôn — LANCZOS/BICUBIC làm mờ pixel, phá hỏng pixel-art."""
    w, h = img.size
    if h <= target_height:
        return img
    ratio = target_height / h
    new_w = max(1, round(w * ratio))
    return img.resize((new_w, target_height), Image.NEAREST)


# ---------------------------------------------------------------- 4. palette

def load_palette(path) -> list:
    data = json.loads(Path(path).read_text())
    colors = data["colors"] if isinstance(data, dict) else data
    out = []
    for c in colors:
        if isinstance(c, str):
            c = c.lstrip("#")
            out.append((int(c[0:2], 16), int(c[2:4], 16), int(c[4:6], 16)))
        else:
            out.append(tuple(c[:3]))
    return out


def quantize_to_palette(img: Image.Image, palette: list) -> Image.Image:
    """Ánh xạ mọi pixel về màu gần nhất trong palette. Giữ nguyên alpha."""
    img = img.convert("RGBA")
    px = img.load()
    w, h = img.size
    cache = {}

    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a == 0:
                continue
            key = (r, g, b)
            if key not in cache:
                best, bd = palette[0], 1 << 30
                for pr, pg, pb in palette:
                    d = (r - pr) ** 2 + (g - pg) ** 2 + (b - pb) ** 2
                    if d < bd:
                        bd, best = d, (pr, pg, pb)
                cache[key] = best
            nr, ng, nb = cache[key]
            px[x, y] = (nr, ng, nb, a)
    return img


def verify_palette(img: Image.Image, palette: list) -> set:
    """Trả về tập màu KHÔNG nằm trong palette."""
    allowed = set(palette)
    bad = set()
    for r, g, b, a in img.convert("RGBA").getdata():
        if a > 0 and (r, g, b) not in allowed:
            bad.add((r, g, b))
    return bad


# ---------------------------------------------------------------- 5. alpha

def binarize_alpha(img: Image.Image, threshold: int = 128) -> Image.Image:
    """Alpha chỉ 0 hoặc 255 — pixel-art không có bán trong suốt ở viền."""
    img = img.convert("RGBA")
    px = img.load()
    w, h = img.size
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            px[x, y] = (r, g, b, 255 if a >= threshold else 0)
    return img


def remove_orphan_pixels(img: Image.Image, min_neighbors: int = 2) -> Image.Image:
    """Xoá pixel đơn độc do nhiễu resize."""
    img = img.convert("RGBA")
    px = img.load()
    w, h = img.size
    kill = []
    for y in range(h):
        for x in range(w):
            if px[x, y][3] == 0:
                continue
            n = 0
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + dx, y + dy
                if 0 <= nx < w and 0 <= ny < h and px[nx, ny][3] > 0:
                    n += 1
            if n < min_neighbors:
                kill.append((x, y))
    for x, y in kill:
        px[x, y] = (0, 0, 0, 0)
    return img


# ---------------------------------------------------------------- 6. canvas

def fit_canvas(img: Image.Image, size: int, anchor: str = "bottom") -> Image.Image:
    """
    Đặt sprite vào canvas vuông bội số 2.
    anchor='bottom': căn ĐÁY giữa — nhân vật đứng đúng mặt đất giữa các sprite.
    """
    canvas = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    w, h = img.size
    if w > size or h > size:
        ratio = min(size / w, size / h)
        img = img.resize((max(1, int(w * ratio)), max(1, int(h * ratio))), Image.NEAREST)
        w, h = img.size
    x = (size - w) // 2
    y = size - h if anchor == "bottom" else (size - h) // 2
    canvas.paste(img, (x, y), img)
    return canvas


# ---------------------------------------------------------------- cắt frame

def slice_sheet(img: Image.Image, cols: int, rows: int = 1) -> list:
    """Cắt spritesheet thành từng frame."""
    w, h = img.size
    fw, fh = w // cols, h // rows
    frames = []
    for r in range(rows):
        for c in range(cols):
            frames.append(img.crop((c * fw, r * fh, (c + 1) * fw, (r + 1) * fh)))
    return frames


# ---------------------------------------------------------------- pipeline

def process_file(src: Path, dst: Path, target_height, palette, key,
                 canvas_size, anchor, tolerance):
    img = Image.open(src).convert("RGBA")
    img = remove_background(img, key, tolerance)
    img = trim(img)
    if target_height:
        img = downscale(img, target_height)
    if palette:
        img = quantize_to_palette(img, palette)
    img = binarize_alpha(img)
    img = remove_orphan_pixels(img)
    if canvas_size:
        img = fit_canvas(img, canvas_size, anchor)
    dst.parent.mkdir(parents=True, exist_ok=True)
    img.save(dst)
    return img.size


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--in", dest="src", help="File hoặc thư mục nguồn")
    ap.add_argument("--out", dest="dst", default="clean")
    ap.add_argument("--target-height", type=int, default=48)
    ap.add_argument("--canvas", type=int, default=0, help="Kích thước canvas vuông (0 = bỏ qua)")
    ap.add_argument("--anchor", default="bottom", choices=["bottom", "center"])
    ap.add_argument("--palette", help="File palette JSON")
    ap.add_argument("--key", default="magenta", choices=list(KEY_COLORS) + ["auto"])
    ap.add_argument("--tolerance", type=int, default=60)
    ap.add_argument("--verify-palette", help="Chỉ kiểm tra palette của thư mục này")
    ap.add_argument("--slice", help="Cắt spritesheet này thành frame")
    ap.add_argument("--cols", type=int, default=4)
    ap.add_argument("--rows", type=int, default=1)
    args = ap.parse_args()

    palette = load_palette(args.palette) if args.palette else None

    if args.verify_palette:
        if not palette:
            ap.error("--verify-palette cần --palette")
        bad_total = 0
        for f in sorted(Path(args.verify_palette).rglob("*.png")):
            bad = verify_palette(Image.open(f), palette)
            if bad:
                bad_total += 1
                print(f"✗ {f.name}: {len(bad)} màu ngoài palette, ví dụ "
                      f"{list(bad)[:3]}")
        print("✓ Tất cả đều đúng palette" if bad_total == 0
              else f"✗ {bad_total} file sai palette")
        return

    if args.slice:
        src = Path(args.slice)
        frames = slice_sheet(Image.open(src).convert("RGBA"), args.cols, args.rows)
        out = Path(args.dst)
        out.mkdir(parents=True, exist_ok=True)
        for i, fr in enumerate(frames):
            fr = trim(fr)
            if args.canvas:
                fr = fit_canvas(fr, args.canvas, args.anchor)
            fr.save(out / f"{src.stem}_{i:02d}.png")
        print(f"✓ Cắt {len(frames)} frame → {out}")
        return

    if not args.src:
        ap.error("Cần --in")

    src = Path(args.src)
    dst_root = Path(args.dst)
    files = [src] if src.is_file() else sorted(src.rglob("*.png"))
    if not files:
        print(f"Không có ảnh PNG nào trong {src}")
        return

    for f in files:
        rel = f.name if src.is_file() else f.relative_to(src)
        out = dst_root / rel
        size = process_file(f, out, args.target_height, palette, args.key,
                            args.canvas, args.anchor, args.tolerance)
        print(f"  ✓ {rel} → {size[0]}×{size[1]}")

    print(f"\nXong {len(files)} ảnh → {dst_root}")


if __name__ == "__main__":
    main()

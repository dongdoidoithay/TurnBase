#!/usr/bin/env python3
"""Cắt sheet nhiều lớp (button 4-state, healthbar 3-layer) do compose.py sinh ra
thành từng file PNG riêng, dùng tên lớp từ file .9.json meta đi kèm."""
import json
import sys
from pathlib import Path
from PIL import Image

SRC_DIR = Path(sys.argv[1])
OUT_DIR = Path(sys.argv[2])
OUT_DIR.mkdir(parents=True, exist_ok=True)

for png in sorted(SRC_DIR.glob("*.png")):
    meta_path = png.with_suffix(".9.json")
    if not meta_path.exists():
        continue
    meta = json.loads(meta_path.read_text())
    names = meta.get("states") or meta.get("layers")
    if not names:
        continue
    fh = meta["frame_h"]
    fw = meta["frame_w"]
    img = Image.open(png).convert("RGBA")
    stem = png.stem
    for i, name in enumerate(names):
        frame = img.crop((0, i * fh, fw, (i + 1) * fh))
        out_path = OUT_DIR / f"{stem}_{name}.png"
        frame.save(out_path)
        print(f"  cut {out_path.name} ({fw}x{fh})")

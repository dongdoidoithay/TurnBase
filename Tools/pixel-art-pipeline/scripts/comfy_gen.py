#!/usr/bin/env python3
"""
comfy_gen.py — Sinh ảnh gốc bằng ComfyUI cho pipeline pixel-art.

Chỉ sinh phần "vẽ tay": nhân vật, quái, boss, background, VFX.
KHÔNG dùng để sinh HUD/button/frame/health bar — những thứ đó dựng bằng compose.py.

Dùng:
    python3 comfy_gen.py --catalog catalog.json --out raw/
    python3 comfy_gen.py --prompt "fire knight" --name hero_01 --category character
    python3 comfy_gen.py --list-checkpoints
"""

import argparse
import json
import sys
import time
import urllib.parse
import urllib.request
from pathlib import Path

COMFY = "http://127.0.0.1:8188"

# ---------------------------------------------------------------- style presets

BASE_STYLE = ("pixel art, pixelart, 16-bit sprite, crisp hard pixels, "
              "clean dark outline, limited palette, high contrast")

# Nền magenta tách sạch hơn xám rất nhiều — không màu nào của chủ thể trùng nó
BG_KEY = "flat solid magenta background, uniform background, no gradient"

CATEGORY_STYLE = {
    "character": f"{BASE_STYLE}, {BG_KEY}, full body, centered, side view, "
                 "standing neutral pose, game character sprite",
    "monster":   f"{BASE_STYLE}, {BG_KEY}, full body, centered, side view, "
                 "menacing creature, game enemy sprite",
    "boss":      f"{BASE_STYLE}, {BG_KEY}, full body, centered, side view, "
                 "large imposing boss creature, detailed, game boss sprite",
    "background": f"{BASE_STYLE}, parallax background layer, no characters, "
                  "seamless horizontal tiling, wide landscape, atmospheric depth",
    "vfx":       f"{BASE_STYLE}, on pure black background, additive glow effect, "
                 "no character, energy burst, particle effect",
    "prop":      f"{BASE_STYLE}, {BG_KEY}, single object, centered, game item",
    "tile":      f"{BASE_STYLE}, seamless tileable texture, top down, "
                 "repeating pattern, no border",
}

NEGATIVE = ("blurry, antialiased, smooth gradient, soft shading, 3d render, "
            "photo, photorealistic, text, letters, watermark, signature, "
            "jpeg artifacts, extra limbs, deformed, drop shadow, vignette, "
            "multiple characters, cropped, out of frame")


# ---------------------------------------------------------------- comfy client

def _get(path):
    with urllib.request.urlopen(f"{COMFY}{path}", timeout=30) as r:
        return json.loads(r.read())


def _post(path, payload):
    req = urllib.request.Request(
        f"{COMFY}{path}", data=json.dumps(payload).encode(),
        headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=30) as r:
        return json.loads(r.read())


def check_server():
    try:
        _get("/system_stats")
        return True
    except Exception as e:
        print(f"ComfyUI không phản hồi ở {COMFY}: {e}", file=sys.stderr)
        print("Khởi động: cd ~/AI/ComfyUI && ./venv/bin/python main.py --port 8188 &",
              file=sys.stderr)
        return False


def list_checkpoints():
    info = _get("/object_info/CheckpointLoaderSimple")
    return info["CheckpointLoaderSimple"]["input"]["required"]["ckpt_name"][0]


def pick_checkpoint(preferred=None):
    ckpts = list_checkpoints()
    if not ckpts:
        raise RuntimeError("Không có checkpoint nào trong ComfyUI.")
    if preferred:
        for c in ckpts:
            if preferred.lower() in c.lower():
                return c
    # Ưu tiên checkpoint chuyên pixel-art
    for kw in ("pixel", "sprite", "8bit", "16bit"):
        for c in ckpts:
            if kw in c.lower():
                return c
    print(f"! Không tìm thấy checkpoint chuyên pixel-art, dùng {ckpts[0]} "
          f"— chất lượng pixel sẽ kém hơn.", file=sys.stderr)
    return ckpts[0]


def build_workflow(ckpt, positive, negative, seed, w, h, steps, cfg, sampler, scheduler):
    return {
        "1": {"class_type": "CheckpointLoaderSimple",
              "inputs": {"ckpt_name": ckpt}},
        "2": {"class_type": "CLIPTextEncode",
              "inputs": {"text": positive, "clip": ["1", 1]}},
        "3": {"class_type": "CLIPTextEncode",
              "inputs": {"text": negative, "clip": ["1", 1]}},
        "4": {"class_type": "EmptyLatentImage",
              "inputs": {"width": w, "height": h, "batch_size": 1}},
        "5": {"class_type": "KSampler",
              "inputs": {"seed": seed, "steps": steps, "cfg": cfg,
                         "sampler_name": sampler, "scheduler": scheduler,
                         "denoise": 1.0, "model": ["1", 0],
                         "positive": ["2", 0], "negative": ["3", 0],
                         "latent_image": ["4", 0]}},
        "6": {"class_type": "VAEDecode",
              "inputs": {"samples": ["5", 0], "vae": ["1", 2]}},
        "7": {"class_type": "SaveImage",
              "inputs": {"filename_prefix": "pixelgen/out", "images": ["6", 0]}},
    }


def download(img, dest: Path):
    q = urllib.parse.urlencode({
        "filename": img["filename"],
        "subfolder": img.get("subfolder", ""),
        "type": img.get("type", "output")})
    with urllib.request.urlopen(f"{COMFY}/view?{q}", timeout=120) as r:
        dest.parent.mkdir(parents=True, exist_ok=True)
        dest.write_bytes(r.read())


def generate_one(ckpt, prompt, category, name, seed, size, out_dir: Path,
                 steps=30, cfg=8.0, sampler="dpmpp_2m", scheduler="karras",
                 extra_negative=""):
    style = CATEGORY_STYLE.get(category, BASE_STYLE)
    positive = f"{prompt}, {style}"
    negative = NEGATIVE + (", " + extra_negative if extra_negative else "")

    wf = build_workflow(ckpt, positive, negative, seed, size[0], size[1],
                        steps, cfg, sampler, scheduler)
    pid = _post("/prompt", {"prompt": wf})["prompt_id"]

    for _ in range(900):
        time.sleep(1)
        hist = _get(f"/history/{pid}")
        if pid not in hist:
            continue
        imgs = hist[pid].get("outputs", {}).get("7", {}).get("images", [])
        if not imgs:
            print(f"  ! {name}: không có ảnh trả về")
            return None
        dest = out_dir / category / f"{name}.png"
        download(imgs[0], dest)
        print(f"  ✓ {dest}")
        return dest

    print(f"  ! {name}: hết thời gian chờ")
    return None


# ---------------------------------------------------------------- main

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--catalog", help="File JSON mô tả danh sách asset cần sinh")
    ap.add_argument("--out", default="raw", help="Thư mục xuất ảnh gốc")
    ap.add_argument("--checkpoint", help="Tên (một phần) checkpoint muốn dùng")
    ap.add_argument("--prompt", help="Sinh một ảnh đơn lẻ")
    ap.add_argument("--name", default="output")
    ap.add_argument("--category", default="character", choices=list(CATEGORY_STYLE))
    ap.add_argument("--seed", type=int, default=20260807)
    ap.add_argument("--w", type=int, default=768)
    ap.add_argument("--h", type=int, default=768)
    ap.add_argument("--steps", type=int, default=30)
    ap.add_argument("--cfg", type=float, default=8.0)
    ap.add_argument("--variants", type=int, default=1)
    ap.add_argument("--list-checkpoints", action="store_true")
    args = ap.parse_args()

    if not check_server():
        sys.exit(1)

    if args.list_checkpoints:
        for c in list_checkpoints():
            print(c)
        return

    ckpt = pick_checkpoint(args.checkpoint)
    print(f"Checkpoint: {ckpt}")
    out_dir = Path(args.out)

    if args.prompt:
        for v in range(args.variants):
            name = args.name if args.variants == 1 else f"{args.name}_v{v+1}"
            generate_one(ckpt, args.prompt, args.category, name,
                         args.seed + v * 7919, (args.w, args.h), out_dir,
                         args.steps, args.cfg)
        return

    if not args.catalog:
        ap.error("Cần --catalog hoặc --prompt")

    catalog = json.loads(Path(args.catalog).read_text())
    items = catalog if isinstance(catalog, list) else catalog.get("items", [])
    print(f"\n{len(items)} mục trong catalog\n")

    for i, item in enumerate(items, 1):
        name = item["id"]
        cat = item.get("category", "character")
        size = tuple(item.get("size", [args.w, args.h]))
        seed = item.get("seed", args.seed + i * 7919)
        variants = item.get("variants", args.variants)
        print(f"[{i}/{len(items)}] {name} ({cat}, {size[0]}×{size[1]})")

        for v in range(variants):
            vname = name if variants == 1 else f"{name}_v{v+1}"
            generate_one(ckpt, item["prompt"], cat, vname, seed + v * 104729,
                         size, out_dir,
                         item.get("steps", args.steps),
                         item.get("cfg", args.cfg),
                         extra_negative=item.get("negative", ""))

    print(f"\nXong. Bước tiếp theo:\n"
          f"  python3 post_process.py --in {args.out}/ --out clean/ --key magenta")


if __name__ == "__main__":
    main()

#!/usr/bin/env python3
"""
monster_draw.py — task-combat-dungeon-redesign.md Phase A. Vẽ THỦ TỤC (không AI) quái vật kiểu mới,
chi tiết hơn hero chibi hiện có (32x32) — tham khảo phong cách _Reference/Art_Sample/Monters.png
(viền đen dày, silhouette chunky, nhiều dải màu hơn). Canvas 48x48.

Lý do KHÔNG dùng ComfyUI ở đây: checkpoint duy nhất có trong máy (PixelartSpritesheet_V.1) thiên vị
mạnh về xuất lưới nhiều-frame lặp lại, không theo prompt loài/nền đơn lẻ dù đã thử 2 prompt khác
nhau (7 ảnh) — cùng lý do dự án đã chọn vẽ thủ tục cho hero_ember_knight trước đây (character_draw.py).
Kỹ thuật giống hệt: khối màu PHẲNG + outline dilate-alpha, tham số hoá theo pose để nhất quán tuyệt
đối giữa các frame.

Dùng:
    python3 monster_draw.py --monster enemy_goblin_v2 --state idle --frames 4 --out ../clean/enemy_goblin_v2/idle/
    python3 monster_draw.py --monster enemy_goblin_v2 --state attack --frames 4 --out ../clean/enemy_goblin_v2/attack/
"""

import argparse
from pathlib import Path

try:
    from PIL import Image, ImageDraw
except ImportError:
    raise SystemExit("Cần Pillow: pip install Pillow")

SIZE = 48

# Tập màu con cho goblin — lấy đúng từ references/palette.md (nhóm Xanh lá + Nâu + Đỏ + Vàng + Trung tính).
OUTLINE = (13, 8, 14, 255)        # #0D080E — viền đen (không dùng đen tuyệt đối)
SKIN_DARK = (27, 61, 31, 255)     # #1B3D1F — bóng da
SKIN_MID = (61, 122, 46, 255)     # #3D7A2E — da chính
SKIN_LIGHT = (123, 201, 80, 255)  # #7BC950 — highlight da
LEATHER_DARK = (58, 36, 22, 255)  # #3A2416 — bóng da thuộc/gỗ
LEATHER_MID = (107, 69, 38, 255)  # #6B4526 — da thuộc/gỗ chính
LEATHER_LIGHT = (166, 113, 66, 255)  # #A67142 — highlight da thuộc/gỗ
EYE = (230, 57, 70, 255)          # #E63946 — mắt đỏ
TOOTH = (255, 240, 184, 255)      # #FFF0B8 — răng nanh vàng nhạt
CLUB = (154, 154, 154, 255)       # #9A9A9A — đầu chuỳ kim loại (tái dùng STEEL của knight)


def draw_silhouette(pose):
    """pose: dict{head_bob, arm_swing, lean} — head_bob nhấp nhô lúc idle; arm_swing nội suy chuỳ 2
    tay từ 'nằm ngang trước bụng' (0) sang 'vung cao qua đầu' (1); lean dịch ngang toàn thân lúc lao
    tới. Bố cục bám sát _Reference/Art_Sample/Monters.png: đầu to hơn hẳn thân (tỉ lệ chibi-quái),
    tai to xệ, đỉnh đầu nhọn lệch, 2 tay cùng cầm 1 chuỳ ngang trước bụng, thân trần lộ da chỉ có 1
    dây da vắt chéo vai — KHÔNG mặc yếm kín như bản nháp đầu."""
    img = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    hb = pose.get("head_bob", 0)
    lean = pose.get("lean", 0)
    arm = pose.get("arm_swing", 0.0)
    cx = SIZE // 2 + lean

    # ---- Chân (cố định, bám đất — KHÔNG theo head_bob) — ngắn, cong vòng kiềng ----
    d.rectangle([cx - 8, 39, cx - 3, 45], fill=SKIN_DARK)
    d.rectangle([cx + 3, 39, cx + 8, 45], fill=SKIN_DARK)
    d.rectangle([cx - 8, 43, cx - 3, 45], fill=LEATHER_DARK)   # bàn chân
    d.rectangle([cx + 3, 43, cx + 8, 45], fill=LEATHER_DARK)

    # ---- Thân — trần, lộ da, KHÔNG yếm kín (theo head_bob) ----
    by = 26 + hb
    d.rectangle([cx - 9, by, cx + 9, by + 13], fill=SKIN_MID)           # ngực/bụng trần
    d.rectangle([cx - 9, by + 10, cx + 9, by + 13], fill=SKIN_DARK)     # bóng bụng dưới
    d.rectangle([cx - 6, by + 1, cx + 6, by + 3], fill=SKIN_LIGHT)      # highlight ngực trên
    d.rectangle([cx - 9, by + 12, cx + 9, by + 14], fill=LEATHER_DARK)  # đai khố
    # Dây da vắt chéo vai trái → hông phải (1 dây duy nhất, KHÔNG che kín thân)
    d.line([(cx - 7, by - 1), (cx + 6, by + 11)], fill=LEATHER_MID, width=3)

    # ---- Chuỳ 2 tay cầm ngang trước bụng, nội suy theo arm_swing (0=ngang thấp, 1=vung cao) ----
    lift = int(round(arm * 18))
    tilt = int(round(arm * 6))
    gy = by + 4 - lift
    left_hand = (cx - 11, gy + tilt)
    right_hand = (cx + 9, gy - tilt)
    d.line([(cx - 8, by + 3), left_hand], fill=SKIN_MID, width=3)   # cẳng tay trái
    d.line([(cx + 6, by + 3), right_hand], fill=SKIN_MID, width=3)  # cẳng tay phải
    d.line([left_hand, right_hand], fill=LEATHER_MID, width=3)      # cán chuỳ gỗ
    d.rectangle([right_hand[0] - 1, right_hand[1] - 3, right_hand[0] + 4, right_hand[1] + 3],
                fill=CLUB)  # đầu chuỳ kim loại đầu phải
    d.ellipse([left_hand[0] - 2, left_hand[1] - 2, left_hand[0] + 2, left_hand[1] + 2],
              fill=SKIN_DARK)  # nắm tay trái

    # ---- Đầu — TO nhưng TRÒN (không nhọn kiểu mũ phù thuỷ), tai bầu nhỏ áp sát, 2 tông da ----
    hy = 8 + hb
    # Hộp sọ: bo tròn qua ellipse thay vì hình chữ nhật cứng
    d.ellipse([cx - 10, hy, cx + 10, hy + 18], fill=SKIN_MID)
    d.ellipse([cx - 10, hy, cx + 10, hy + 9], fill=SKIN_LIGHT)          # tông sáng nửa trên (bo tròn)
    d.rectangle([cx - 10, hy + 13, cx + 10, hy + 18], fill=SKIN_DARK)   # bóng hàm dưới
    d.pieslice([cx - 10, hy + 9, cx + 10, hy + 18], 0, 180, fill=SKIN_DARK)  # bo lại đáy hàm
    # Mào nhỏ bất đối xứng lệch trái — chỉ 1 gợn nhô nhẹ, KHÔNG phải mũ nhọn to
    d.polygon([(cx - 5, hy + 1), (cx - 2, hy - 3), (cx + 1, hy + 1)], fill=SKIN_LIGHT)
    # Tai bầu nhỏ, áp sát đầu (ellipse, không phải cánh xoè to)
    d.ellipse([cx - 15, hy + 3, cx - 9, hy + 11], fill=SKIN_MID)
    d.ellipse([cx - 14, hy + 4, cx - 11, hy + 9], fill=SKIN_DARK)
    d.ellipse([cx + 9, hy + 4, cx + 15, hy + 12], fill=SKIN_DARK)
    d.ellipse([cx + 10, hy + 5, cx + 13, hy + 10], fill=SKIN_LIGHT)
    # Mắt đỏ — HÌNH CHỮ NHẬT nhỏ (ellipse ở size này bị dilate outline làm méo thành hình sao)
    d.rectangle([cx - 6, hy + 8, cx - 3, hy + 9], fill=EYE)
    d.rectangle([cx + 3, hy + 8, cx + 6, hy + 9], fill=EYE)
    # Mũi nhỏ
    d.rectangle([cx - 1, hy + 10, cx + 1, hy + 11], fill=SKIN_DARK)
    # Miệng nhe răng dưới
    d.rectangle([cx - 6, hy + 13, cx + 6, hy + 16], fill=OUTLINE)
    d.polygon([(cx - 5, hy + 13), (cx - 4, hy + 16), (cx - 3, hy + 13)], fill=TOOTH)
    d.polygon([(cx - 1, hy + 13), (cx, hy + 16), (cx + 1, hy + 13)], fill=TOOTH)
    d.polygon([(cx + 3, hy + 13), (cx + 4, hy + 16), (cx + 5, hy + 13)], fill=TOOTH)

    return img


def add_outline(img):
    """Viền đen 1px quanh TOÀN silhouette — dilate alpha 4 hướng, đặt LÀM NỀN dưới nhân vật gốc.
    Kỹ thuật giống hệt character_draw.py.add_outline."""
    alpha = img.split()[3]
    w, h = img.size
    dilated = Image.new("L", (w, h), 0)
    src = alpha.load()
    dst = dilated.load()
    for y in range(h):
        for x in range(w):
            if src[x, y] > 0:
                dst[x, y] = 255
                continue
            hit = False
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + dx, y + dy
                if 0 <= nx < w and 0 <= ny < h and src[nx, ny] > 0:
                    hit = True
                    break
            if hit:
                dst[x, y] = 255

    outline_layer = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    for y in range(h):
        for x in range(w):
            if dst[x, y] > 0 and src[x, y] == 0:
                outline_layer.putpixel((x, y), OUTLINE)

    result = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    result = Image.alpha_composite(result, outline_layer)
    result = Image.alpha_composite(result, img)
    return result


IDLE_POSES = [
    {"head_bob": 0, "arm_swing": 0.1, "lean": 0},
    {"head_bob": -1, "arm_swing": 0.15, "lean": 0},
    {"head_bob": -1, "arm_swing": 0.1, "lean": 0},
    {"head_bob": 0, "arm_swing": 0.05, "lean": 0},
]

ATTACK_POSES = [
    {"head_bob": 0, "arm_swing": 0.0, "lean": 0},    # thu chuỳ
    {"head_bob": 0, "arm_swing": 0.6, "lean": -1},   # vung lên
    {"head_bob": 1, "arm_swing": 1.0, "lean": 2},     # bổ xuống + lao người
    {"head_bob": 0, "arm_swing": 0.3, "lean": 0},    # thu về
]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--monster", required=True)
    ap.add_argument("--state", required=True, choices=["idle", "attack"])
    ap.add_argument("--frames", type=int, default=4)
    ap.add_argument("--out", required=True)
    args = ap.parse_args()

    poses = IDLE_POSES if args.state == "idle" else ATTACK_POSES
    poses = poses[: args.frames]

    out_dir = Path(args.out)
    out_dir.mkdir(parents=True, exist_ok=True)
    for i, pose in enumerate(poses):
        img = draw_silhouette(pose)
        img = add_outline(img)
        path = out_dir / f"{args.monster}_{args.state}_{i:02d}.png"
        img.save(path)
        print(f"  ✓ {path} ({img.size[0]}×{img.size[1]})")


if __name__ == "__main__":
    main()

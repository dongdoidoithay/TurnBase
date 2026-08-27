#!/usr/bin/env python3
"""
draw_skill_icons.py — vẽ lại 9 icon skill (Art/UI/Icons/Skills/icon_skill_*.png) đang là glyph
trắng phẳng 1 màu (đặt tạm khi build SkillSlotView, chưa bao giờ có art thật) bằng pixel art có
màu, cùng phong cách phẳng+viền tối 1px như item_icons.py/nav_icons.py đã dùng cho Inventory —
không dùng AI/crop, vẽ trực tiếp bằng ImageDraw đúng quy ước dự án (xem
Tools/pixel-art-pipeline/SKILL.md §"Nguyên tắc cốt lõi": hình học/icon → Pillow, không phải AI).

Bối cảnh: task-ui-chrome-popups.md đã nâng cấp KHUNG thẻ kỹ năng (card_gold 9-slice, scalloped)
nhưng để lại ghi chú "chưa đụng" — icon BÊN TRONG khung vẫn là glyph trắng đơn sắc (sword-check,
tia sét, ngôi sao...) từ SkillSlotView.IconKeyFor(), tạo cảm giác lệch tông ngay khi khung đã lên
màu vàng-mận đồng bộ toàn màn Combat HUD. Đây là phần còn thiếu được người dùng chỉ ra qua "UI
Screen Battle chưa giống sample".

Màu lấy từ Tools/palette.json (TurnBase 48), 1 tông riêng mỗi loại icon để vừa đẹp vừa phân biệt
nhanh bằng mắt (không thay cho ElementColor tint theo nguyên tố vẫn phủ lên trên trong game).

Dùng: python3 draw_skill_icons.py --out-dir out/
"""
import argparse
from pathlib import Path

from PIL import Image, ImageDraw

# Tools/palette.json (TurnBase 48)
OUTLINE = (13, 8, 14, 255)
STEEL_DARK, STEEL_MID, STEEL_LIGHT = (92, 92, 92, 255), (154, 154, 154, 255), (242, 232, 207, 255)
ORANGE_DARK, ORANGE_MID, ORANGE_LIGHT, ORANGE_PALE = (122, 61, 20, 255), (184, 92, 30, 255), (244, 162, 89, 255), (255, 217, 160, 255)
RED_DARK, RED_MID, RED_LIGHT = (92, 18, 32, 255), (163, 35, 53, 255), (230, 57, 70, 255)
BLUE_DARK, BLUE_MID, BLUE_LIGHT, BLUE_PALE = (18, 48, 74, 255), (42, 90, 128, 255), (69, 123, 157, 255), (143, 192, 217, 255)
GREEN_DARK, GREEN_MID, GREEN_LIGHT, GREEN_PALE = (27, 61, 31, 255), (61, 122, 46, 255), (123, 201, 80, 255), (185, 232, 154, 255)
GOLD_DARK, GOLD_MID, GOLD_LIGHT, GOLD_PALE = (107, 82, 16, 255), (184, 144, 30, 255), (255, 209, 102, 255), (255, 240, 184, 255)
PURPLE_DARK, PURPLE_MID, PURPLE_LIGHT = (42, 27, 58, 255), (90, 48, 128, 255), (155, 93, 229, 255)
CYAN_DARK, CYAN_MID, CYAN_LIGHT, CYAN_PALE = (27, 58, 66, 255), (46, 107, 120, 255), (78, 195, 217, 255), (165, 232, 240, 255)


def _new():
    return Image.new("RGBA", (32, 32), (0, 0, 0, 0))


def icon_slash():
    """Kiếm chéo đơn — đòn đánh thường, tông thép trung tính."""
    img = _new()
    d = ImageDraw.Draw(img)
    d.polygon([(6, 24), (20, 8), (24, 10), (10, 26)], fill=STEEL_MID, outline=OUTLINE)
    d.line([(9, 23), (19, 11)], fill=STEEL_LIGHT, width=1)
    d.rectangle([(6, 22), (10, 27)], fill=ORANGE_DARK, outline=OUTLINE)
    d.line([(4, 27), (12, 20)], fill=STEEL_DARK, width=2)
    return img


def icon_power_strike():
    """Kiếm lớn hơn + 2 vệt motion cam-đỏ phía sau — đòn mạnh/breaker."""
    img = _new()
    d = ImageDraw.Draw(img)
    d.line([(9, 27), (25, 6)], fill=RED_MID, width=2)
    d.line([(6, 22), (22, 3)], fill=RED_LIGHT, width=2)
    d.polygon([(10, 26), (23, 9), (27, 11), (14, 28)], fill=ORANGE_LIGHT, outline=OUTLINE)
    d.line([(13, 25), (24, 11)], fill=ORANGE_PALE, width=1)
    d.rectangle([(8, 24), (13, 30)], fill=ORANGE_DARK, outline=OUTLINE)
    return img


def icon_magic_bolt():
    """Tia sét xanh — đòn phép."""
    img = _new()
    d = ImageDraw.Draw(img)
    d.polygon([(19, 4), (11, 16), (16, 16), (10, 28), (23, 13), (17, 13)],
               fill=BLUE_LIGHT, outline=OUTLINE)
    d.polygon([(18, 6), (13, 15), (16, 15), (12, 23), (20, 14), (16, 14)], fill=BLUE_PALE)
    d.ellipse([(4, 6), (8, 10)], fill=BLUE_PALE, outline=OUTLINE)
    d.ellipse([(24, 20), (28, 24)], fill=BLUE_PALE, outline=OUTLINE)
    return img


def icon_heal():
    """Dấu cộng xanh lá + hào quang — hồi máu."""
    img = _new()
    d = ImageDraw.Draw(img)
    d.ellipse([(4, 4), (28, 28)], fill=(0, 0, 0, 0), outline=GREEN_PALE, width=1)
    d.rectangle([(13, 7), (19, 25)], fill=GREEN_MID, outline=OUTLINE)
    d.rectangle([(7, 13), (25, 19)], fill=GREEN_MID, outline=OUTLINE)
    d.rectangle([(14, 9), (18, 23)], fill=GREEN_LIGHT)
    d.rectangle([(9, 14), (23, 18)], fill=GREEN_LIGHT)
    d.rectangle([(15, 11), (17, 21)], fill=GREEN_PALE)
    d.rectangle([(11, 15), (21, 17)], fill=GREEN_PALE)
    return img


def icon_shield():
    """Khiên bạc-lam — buff phòng thủ."""
    img = _new()
    d = ImageDraw.Draw(img)
    d.polygon([(16, 4), (26, 8), (25, 18), (16, 29), (7, 18), (6, 8)],
               fill=BLUE_MID, outline=OUTLINE)
    d.polygon([(16, 7), (23, 10), (22, 17), (16, 25), (10, 17), (9, 10)], fill=BLUE_LIGHT)
    d.polygon([(16, 7), (16, 25), (10, 17), (9, 10)], fill=BLUE_PALE)
    d.line([(16, 10), (16, 21)], fill=STEEL_LIGHT, width=1)
    d.line([(12, 14), (20, 14)], fill=STEEL_LIGHT, width=1)
    return img


def icon_haste():
    """3 vệt gió vàng — tăng tốc độ."""
    img = _new()
    d = ImageDraw.Draw(img)
    for i, (y, w) in enumerate([(9, 3), (16, 3), (23, 3)]):
        x2 = 27 - i * 2
        d.line([(4, y), (x2, y)], fill=GOLD_MID, width=w)
        d.line([(4, y - 1), (x2 - 4, y - 1)], fill=GOLD_LIGHT, width=1)
    d.polygon([(24, 6), (29, 9), (24, 12)], fill=GOLD_LIGHT, outline=OUTLINE)
    d.polygon([(22, 20), (27, 23), (22, 26)], fill=GOLD_LIGHT, outline=OUTLINE)
    return img


def icon_cleanse():
    """Tia lấp lánh xanh cyan — giải trừ hiệu ứng xấu."""
    img = _new()
    d = ImageDraw.Draw(img)

    def star(cx, cy, r, color):
        pts = []
        for i in range(8):
            rr = r if i % 2 == 0 else r * 0.4
            ang = i * 3.14159 / 4
            pts.append((cx + rr * __import__("math").sin(ang), cy - rr * __import__("math").cos(ang)))
        d.polygon(pts, fill=color, outline=OUTLINE)

    star(16, 16, 11, CYAN_MID)
    star(16, 16, 6, CYAN_LIGHT)
    d.ellipse([(14, 14), (18, 18)], fill=CYAN_PALE)
    return img


def icon_aoe_burst():
    """Nổ cam nhiều tia — kỹ năng diện rộng."""
    img = _new()
    d = ImageDraw.Draw(img)
    import math
    cx, cy = 16, 17
    pts = []
    for i in range(10):
        r = 13 if i % 2 == 0 else 6
        ang = i * math.pi / 5
        pts.append((cx + r * math.sin(ang), cy - r * math.cos(ang)))
    d.polygon(pts, fill=ORANGE_MID, outline=OUTLINE)
    pts2 = []
    for i in range(10):
        r = 8 if i % 2 == 0 else 3.5
        ang = i * math.pi / 5
        pts2.append((cx + r * math.sin(ang), cy - r * math.cos(ang)))
    d.polygon(pts2, fill=ORANGE_LIGHT)
    d.ellipse([(13, 14), (19, 20)], fill=ORANGE_PALE)
    return img


def icon_ultimate():
    """Ngôi sao vàng lớn + hào quang — kỹ năng ULT, phải nổi bật nhất trong 9 icon."""
    img = _new()
    d = ImageDraw.Draw(img)
    import math
    cx, cy = 16, 16
    pts = []
    for i in range(10):
        r = 14 if i % 2 == 0 else 6
        ang = i * math.pi / 5 - math.pi / 2
        pts.append((cx + r * math.cos(ang), cy + r * math.sin(ang)))
    d.polygon(pts, fill=GOLD_MID, outline=OUTLINE)
    pts2 = []
    for i in range(10):
        r = 9 if i % 2 == 0 else 3.5
        ang = i * math.pi / 5 - math.pi / 2
        pts2.append((cx + r * math.cos(ang), cy + r * math.sin(ang)))
    d.polygon(pts2, fill=GOLD_LIGHT)
    d.ellipse([(13, 13), (19, 19)], fill=GOLD_PALE)
    for ang_deg in (20, 160, 260):
        ang = math.radians(ang_deg)
        x, y = cx + 15 * math.cos(ang), cy + 15 * math.sin(ang)
        d.ellipse([(x - 1.5, y - 1.5), (x + 1.5, y + 1.5)], fill=GOLD_PALE)
    return img


ICONS = {
    "slash": icon_slash,
    "power_strike": icon_power_strike,
    "magic_bolt": icon_magic_bolt,
    "heal": icon_heal,
    "shield": icon_shield,
    "haste": icon_haste,
    "cleanse": icon_cleanse,
    "aoe_burst": icon_aoe_burst,
    "ultimate": icon_ultimate,
}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out-dir", required=True)
    args = ap.parse_args()
    out = Path(args.out_dir)
    out.mkdir(parents=True, exist_ok=True)
    for name, fn in ICONS.items():
        img = fn()
        img.save(out / f"icon_skill_{name}.png")
        print(f"  ✓ icon_skill_{name}.png")


if __name__ == "__main__":
    main()

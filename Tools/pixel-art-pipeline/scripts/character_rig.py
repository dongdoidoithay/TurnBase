#!/usr/bin/env python3
"""
character_rig.py — PILOT so sánh, KHÔNG thay thế character_draw.py.

Port kỹ thuật "skeletal rig" thật sự đang tồn tại trong
Tools/pixel-art-pipeline/charator/pixel_character_generator_v2.py (bộ phận đầu/thân/tay/chân/vũ
khí tách rời, mỗi bộ phận có điểm neo (pivot) và XOAY (PIL Image.rotate + resample NEAREST) trước
khi ghép vào frame) — kỹ thuật này khác character_draw.py (khối hình chữ nhật cố định, dịch theo
tham số, KHÔNG xoay khớp thật). Không sửa file gốc trong charator/ hay Art_python — bản port này
thu nhỏ về 32×32 (đúng PPU dự án, bản gốc 64×64) và dùng LẠI đúng bảng màu hero_ember_knight từ
character_draw.py để so sánh công bằng: chỉ đổi KỸ THUẬT dựng hình, không đổi màu/nhận diện nhân vật.

Lưu ý minh bạch: "Smooth_Walk_x4.png"/Warrior rig walk-cycle quan sát được trong thư mục
Art_python KHÔNG đến từ script v2 hiện có trên đĩa (grep xác nhận không có hàm generate_smooth_walk/
get_warrior_* nào trong v2) — có thể từ 1 bản script trung gian đã bị ghi đè, không còn tồn tại để
port trực tiếp. Bản port này dựa trên kỹ thuật THẬT SỰ còn trên đĩa (rig Mage của v2), không phải
suy đoán lại phần đã mất.

Dùng:
    python3 character_rig.py --state idle --frames 4 --out clean/hero_ember_knight_rig/idle/
    python3 character_rig.py --state attack --frames 4 --out clean/hero_ember_knight_rig/attack/
    python3 character_rig.py --vfx ember_burst --frames 6 --out clean/vfx_pilot/
"""

import argparse
from pathlib import Path

try:
    from PIL import Image, ImageDraw, ImageFilter
except ImportError:
    raise SystemExit("Cần Pillow: pip install Pillow")

SIZE = 32

# Đúng bảng màu character_draw.py (chỉ đổi kỹ thuật dựng hình, không đổi màu).
OUTLINE = (13, 8, 14, 255)
ROBE = (163, 35, 53, 255)
ROBE_SHADE = (92, 18, 32, 255)
HELMET = (69, 123, 157, 255)
HELMET_SHADE = (42, 90, 128, 255)
SKIN = (244, 162, 89, 255)
STEEL = (154, 154, 154, 255)
STEEL_SHADE = (92, 92, 92, 255)
TRIM = (184, 92, 30, 255)

# Màu riêng cho VFX pilot (lửa/ember — khác tông cyan/tím của bản gốc v2, khớp chủ đề Ember Knight).
EMBER_CORE = (255, 220, 120, 255)
EMBER_MID = (240, 140, 40, 200)
EMBER_OUT = (163, 35, 53, 110)


def render_part(template, colors):
    """char-grid -> ảnh RGBA trong suốt, giống render_part của v2."""
    h = len(template)
    w = len(template[0])
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    px = img.load()
    for y in range(h):
        for x in range(w):
            c = template[y][x]
            if c in colors:
                px[x, y] = colors[c]
    return img


def rotate_part(img, angle, pivot):
    """Xoay 1 bộ phận quanh pivot — thuật toán y hệt v2.rotate_image (pad canvas 3x, rotate NEAREST
    để giữ pixel cứng, không blur/AA)."""
    w, h = img.size
    pad = max(w, h) * 3
    canvas = Image.new("RGBA", (pad, pad), (0, 0, 0, 0))
    cx, cy = pad // 2, pad // 2
    canvas.paste(img, (cx - pivot[0], cy - pivot[1]))
    rotated = canvas.rotate(angle, resample=Image.NEAREST)
    return rotated, (cx, cy)


def paste_part(canvas, img, pivot, angle, target_pos, alpha=255):
    rot, rot_center = rotate_part(img, angle, pivot)
    if alpha < 255:
        rot.putalpha(rot.getchannel("A").point(lambda p: p * (alpha / 255.0)))
    tx = target_pos[0] - rot_center[0]
    ty = target_pos[1] - rot_center[1]
    canvas.paste(rot, (tx, ty), rot)


def add_silhouette_outline(img):
    """Viền đen 1px quanh TOÀN silhouette đã ghép — y hệt character_draw.py.add_outline (dilate
    alpha 4 hướng, tô OUTLINE ở phần dilate-only, đặt LÀM NỀN dưới nhân vật). Thay cho việc mỗi
    bộ phận tự vẽ viền riêng (tạo seam đen giữa các khớp — lỗi thấy ở lần review đầu)."""
    alpha = img.split()[3]
    w, h = img.size
    src = alpha.load()
    dilated = Image.new("L", (w, h), 0)
    dst = dilated.load()
    for y in range(h):
        for x in range(w):
            if src[x, y] > 0:
                dst[x, y] = 255
                continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + dx, y + dy
                if 0 <= nx < w and 0 <= ny < h and src[nx, ny] > 0:
                    dst[x, y] = 255
                    break

    outline_layer = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    for y in range(h):
        for x in range(w):
            if dst[x, y] > 0 and src[x, y] == 0:
                outline_layer.putpixel((x, y), OUTLINE)

    result = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    result = Image.alpha_composite(result, outline_layer)
    result = Image.alpha_composite(result, img)
    return result


def draw_drop_shadow(canvas, cx, foot_y):
    """Bóng đổ mờ dưới chân — LỚP RIÊNG, dán TRƯỚC/DƯỚI nhân vật. Đây là kỹ thuật hậu kỳ DUY NHẤT
    được giữ nguyên GaussianBlur — bóng đất mờ là quy ước bình thường ngay cả trong pixel art
    cứng nét (không đụng tới pixel nhân vật, không phá quy tắc "phẳng màu, viền cứng")."""
    shadow = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(shadow)
    d.ellipse([cx - 6, foot_y - 1, cx + 6, foot_y + 2], fill=(0, 0, 0, 90))
    shadow = shadow.filter(ImageFilter.GaussianBlur(0.8))
    canvas.paste(shadow, (0, 0), shadow)


# ---- Bộ phận Ember Knight, thu nhỏ về ngân sách 32×32 (bản gốc v2 ở 64×64) ----
# render_part chỉ nhận 1 ký tự/pixel.
# _COLORS: dùng khi cần xem riêng 1 part (debug). _PART_COLORS: dùng khi LẮP RÁP frame thật — map
# "1" (viền) -> TRONG SUỐT để KHÔNG vẽ viền riêng từng bộ phận (lý do sửa: viền từng phần tạo seam
# đen xấu giữa đầu/thân/tay/chân/khiên — xem lần review đầu). Viền THẬT quanh TOÀN silhouette được
# vẽ 1 LẦN DUY NHẤT sau khi ghép xong frame, bằng add_silhouette_outline() (kỹ thuật dilate giống
# character_draw.py.add_outline).
_COLORS = {
    "0": (0, 0, 0, 0), "1": OUTLINE, "2": SKIN, "3": STEEL, "9": STEEL_SHADE,
    "4": HELMET, "8": HELMET_SHADE, "5": ROBE, "7": ROBE_SHADE, "t": TRIM,
}
_PART_COLORS = dict(_COLORS)
_PART_COLORS["1"] = (0, 0, 0, 0)


def get_head(colors=None):
    colors = colors or _PART_COLORS
    t = [
        "011111110",
        "144444441",
        "144444441",
        "142222241",
        "142222241",
        "148888841",
        "011111110",
    ]
    return render_part(t, colors), (4, 6)  # pivot: gốc cổ (hàng cuối, giữa)


def get_torso(colors=None):
    colors = colors or _PART_COLORS
    t = [
        "01111111110",
        "15555555551",
        "15555555551",
        "15555555551",
        "15555555551",
        "15555555551",
        "15555555551",
        "15555555551",
        "17777777771",
        "01111111110",
    ]
    return render_part(t, colors), (5, 0)  # pivot: đỉnh thân (nối cổ)


def get_leg(colors=None):
    colors = colors or _PART_COLORS
    t = ["01110", "15551", "15551", "15551", "15551", "17t71", "17t71", "17t71", "01110"]
    return render_part(t, colors), (2, 0)  # pivot: gốc hông — DÙNG CHUNG mọi class (chân/ủng
                                            # tương tự nhau, chỉ đổi màu qua tham số colors)


def get_arm(colors=None):
    colors = colors or _PART_COLORS
    t = ["111", "757", "757", "757", "757", "111"]
    return render_part(t, colors), (1, 0)  # pivot: khớp vai — XOAY thật quanh điểm này, DÙNG CHUNG


def get_shield(colors=None):
    colors = colors or _PART_COLORS
    t = ["11111", "39993", "39993", "39993", "39993", "11111"]
    return render_part(t, colors), (2, 2)


def get_weapon(colors=None):
    colors = colors or _PART_COLORS
    t = ["1", "3", "3", "3", "3", "9", "9"]
    return render_part(t, colors), (0, 6)  # pivot: chuôi kiếm — gắn vào cuối tay khi xoay


class CharacterKit:
    """Gói bộ phận + màu cho 1 class — mọi class DÙNG CHUNG get_leg/get_arm (chỉ đổi màu qua
    `colors`), chỉ đầu/thân/vũ khí/khiên (nếu có) là hàm RIÊNG cho từng class (silhouette khác
    nhau thật, không chỉ đổi màu)."""

    def __init__(self, colors, get_head, get_torso, get_weapon, get_shield=None):
        self.colors = colors
        self.get_head = lambda: get_head(colors)
        self.get_torso = lambda: get_torso(colors)
        self.get_weapon = lambda: get_weapon(colors)
        # None = không mang khiên (lớp phép thuật/nhanh nhẹn) — giữ None nguyên vẹn, không lambda hoá.
        self.get_shield = (lambda: get_shield(colors)) if get_shield is not None else None
        self.get_leg = lambda: get_leg(colors)
        self.get_arm = lambda: get_arm(colors)


def recolor_kit(base_kit_colors_source, new_colors, get_head, get_torso, get_weapon,
                get_shield=None):
    """Tạo 1 CharacterKit MỚI tái dùng silhouette (hàm vẽ) của kit gốc nhưng đổi bảng màu —
    dùng cho recolor 18 hero còn lại trong Giai đoạn 3 (mỗi hero = 1 element khác trong cùng
    class, cùng silhouette, không cần thiết kế lại). `base_kit_colors_source` chỉ để đọc code hiểu
    kit gốc nào — không thực sự dùng trong hàm, tham số giữ lại cho rõ ý khi gọi."""
    return CharacterKit(new_colors, get_head, get_torso, get_weapon, get_shield)


VANGUARD_KIT = CharacterKit(_PART_COLORS, get_head, get_torso, get_weapon, get_shield)

# ---- Arcanist (Frost Sage) — áo choàng dài, mũ trùm nhọn, gậy phép, KHÔNG khiên ----
FROST_ROBE = (46, 107, 120, 255)        # #2E6B78
FROST_ROBE_SHADE = (27, 58, 66, 255)    # #1B3A42
FROST_HOOD = (18, 48, 74, 255)          # #12304A
FROST_ICE = (78, 195, 217, 255)         # #4EC3D9
FROST_ICE_BRIGHT = (165, 232, 240, 255) # #A5E8F0

_ARCANIST_COLORS = {
    "0": (0, 0, 0, 0), "1": OUTLINE, "2": SKIN, "3": STEEL, "9": STEEL_SHADE,
    "4": FROST_HOOD, "8": FROST_ICE_BRIGHT, "5": FROST_ROBE, "7": FROST_ROBE_SHADE, "t": FROST_ICE,
}
_ARCANIST_PART_COLORS = dict(_ARCANIST_COLORS)
_ARCANIST_PART_COLORS["1"] = (0, 0, 0, 0)


def get_arcanist_head(colors=None):
    """Mũ trùm nhọn thay vì mũ giáp phẳng — silhouette khác Vanguard rõ ràng."""
    colors = colors or _ARCANIST_PART_COLORS
    t = [
        "0001110000",
        "0011111000",
        "0144444100",
        "0144444100",
        "0144444100",
        "0142222410",
        "0148888410",
        "0011111000",
    ]
    return render_part(t, colors), (5, 7)


def get_arcanist_torso(colors=None):
    """Áo choàng — vai hẹp hơn hem để tạo dáng loe xuống dưới, đúng silhouette robe."""
    colors = colors or _ARCANIST_PART_COLORS
    t = [
        "00111111000",
        "00155555100",
        "01555555510",
        "01555555510",
        "15555555551",
        "15555555551",
        "15555555551",
        "15555555551",
        "17777777771",
        "01111111110",
    ]
    return render_part(t, colors), (5, 0)


def get_arcanist_staff(colors=None):
    """Gậy dài + đá phép ở đầu — khác hẳn kiếm ngắn của Vanguard."""
    colors = colors or _ARCANIST_PART_COLORS
    t = ["1", "8", "8", "3", "3", "3", "3", "3", "9", "9"]
    return render_part(t, colors), (0, 9)


ARCANIST_KIT = CharacterKit(_ARCANIST_PART_COLORS, get_arcanist_head, get_arcanist_torso,
                            get_arcanist_staff, get_shield=None)

# ---- Trickster (Gale Thief) — khăn trùm thấp/mặt nạ, áo gọn nhẹ, dao ngắn, KHÔNG khiên (né đòn
# nhanh, không đứng chắn) ----
GALE_HOOD = (27, 61, 31, 255)         # #1B3D1F
GALE_TUNIC = (61, 122, 46, 255)       # #3D7A2E
GALE_TUNIC_SHADE = (27, 61, 31, 255)  # #1B3D1F (đồng màu mũ — thân/mũ cùng tông vải)
GALE_SCARF = (185, 232, 154, 255)     # #B9E89A
GALE_TRIM = (255, 209, 102, 255)      # #FFD166

_TRICKSTER_COLORS = {
    "0": (0, 0, 0, 0), "1": OUTLINE, "2": SKIN, "3": STEEL, "9": STEEL_SHADE,
    "4": GALE_HOOD, "8": GALE_SCARF, "5": GALE_TUNIC, "7": GALE_TUNIC_SHADE, "t": GALE_TRIM,
}
_TRICKSTER_PART_COLORS = dict(_TRICKSTER_COLORS)
_TRICKSTER_PART_COLORS["1"] = (0, 0, 0, 0)


def get_trickster_head(colors=None):
    """Khăn trùm thấp + khăn che mặt — chỉ hở 1 dải mắt, thấp/gọn hơn mũ nhọn Arcanist rõ rệt."""
    colors = colors or _TRICKSTER_PART_COLORS
    t = [
        "011111110",
        "144444441",
        "144444441",
        "148888841",
        "144444441",
        "011111110",
    ]
    return render_part(t, colors), (4, 5)


def get_trickster_torso(colors=None):
    """Áo gọn nhẹ, hẹp hơn robe Arcanist lẫn giáp Vanguard — thân thoi hẹp dần xuống hông, có khăn
    quàng vai bay (mã 8) tạo cảm giác nhanh nhẹn."""
    colors = colors or _TRICKSTER_PART_COLORS
    t = [
        "01111111110",
        "81555555518",
        "01555555510",
        "01555555510",
        "01555555510",
        "01555555510",
        "01555555510",
        "01555555510",
        "01777777710",
        "00111111000",
    ]
    return render_part(t, colors), (5, 0)


def get_trickster_dagger(colors=None):
    """Dao ngắn — khác hẳn gậy dài Arcanist lẫn kiếm thẳng Vanguard. Mọi hàng PHẢI cùng độ rộng
    (render_part lấy width từ hàng đầu) — 2 cột: lưỡi (3/9) + viền trong suốt (0)."""
    colors = colors or _TRICKSTER_PART_COLORS
    t = ["30", "39", "39", "30", "90"]
    return render_part(t, colors), (0, 4)  # pivot: chuôi dao (hàng cuối)


TRICKSTER_KIT = CharacterKit(_TRICKSTER_PART_COLORS, get_trickster_head, get_trickster_torso,
                             get_trickster_dagger, get_shield=None)

# ---- Warden (Dawn Cleric) — vệ thần/hộ giáo: áo tu sĩ vàng kim, khiên TRÒN (khác khiên chữ nhật
# Vanguard), gậy phép đầu cầu sáng thay vì vũ khí sát thương ----
DAWN_ROBE = (184, 144, 30, 255)       # #B8901E
DAWN_ROBE_SHADE = (107, 82, 16, 255)  # #6B5210
DAWN_TRIM = (255, 240, 184, 255)      # #FFF0B8
DAWN_HOOD = (242, 232, 207, 255)      # #F2E8CF

_WARDEN_COLORS = {
    "0": (0, 0, 0, 0), "1": OUTLINE, "2": SKIN, "3": STEEL, "9": STEEL_SHADE,
    "4": DAWN_HOOD, "8": DAWN_TRIM, "5": DAWN_ROBE, "7": DAWN_ROBE_SHADE, "t": DAWN_TRIM,
}
_WARDEN_PART_COLORS = dict(_WARDEN_COLORS)
_WARDEN_PART_COLORS["1"] = (0, 0, 0, 0)


def get_warden_head(colors=None):
    """Khăn tu sĩ trắng ngà quấn quanh đầu, dải trán vàng kim — khác mũ nhọn Arcanist/khăn che mặt
    Trickster/mũ giáp Vanguard."""
    colors = colors or _WARDEN_PART_COLORS
    t = [
        "011111110",
        "144444441",
        "148888841",
        "142222241",
        "144444441",
        "011111110",
    ]
    return render_part(t, colors), (4, 5)


def get_warden_robe(colors=None):
    """Áo tu sĩ dài, loe nhẹ như Arcanist nhưng tông vàng kim + dải viền sáng giữa ngực."""
    colors = colors or _WARDEN_PART_COLORS
    t = [
        "00111111000",
        "01555555510",
        "01585585510",
        "01555555510",
        "15555555551",
        "15555555551",
        "15555555551",
        "15555555551",
        "17777777771",
        "01111111110",
    ]
    return render_part(t, colors), (5, 0)


def get_warden_shield(colors=None):
    """Khiên TRÒN — silhouette khác khiên chữ nhật của Vanguard (Warden = hộ giáo, phòng thủ nhưng
    không cùng kiểu khiên chiến binh)."""
    colors = colors or _WARDEN_PART_COLORS
    t = ["01110", "18881", "18881", "18881", "01110"]
    return render_part(t, colors), (2, 2)


def get_warden_mace(colors=None):
    """Gậy đầu cầu sáng — vũ khí hỗ trợ/phép, không phải lưỡi sát thương."""
    colors = colors or _WARDEN_PART_COLORS
    t = ["080", "888", "080", "030", "030", "090"]
    return render_part(t, colors), (1, 5)


WARDEN_KIT = CharacterKit(_WARDEN_PART_COLORS, get_warden_head, get_warden_robe,
                          get_warden_mace, get_shield=get_warden_shield)

# ---- Slayer (Shadow Fang) — sát thủ cận chiến: giáp da mỏng tối màu, mắt/viền tím phát sáng, đại
# đao dài thay vì kiếm ngắn, KHÔNG khiên (né/xả sát thương, không phòng thủ) ----
FANG_ARMOR = (58, 34, 51, 255)        # #3A2233
FANG_ARMOR_SHADE = (26, 15, 28, 255)  # #1A0F1C
FANG_TRIM = (230, 57, 70, 255)        # #E63946
FANG_GLOW = (155, 93, 229, 255)       # #9B5DE5

_SLAYER_COLORS = {
    "0": (0, 0, 0, 0), "1": OUTLINE, "2": SKIN, "3": STEEL, "9": STEEL_SHADE,
    "4": FANG_ARMOR_SHADE, "8": FANG_GLOW, "5": FANG_ARMOR, "7": FANG_ARMOR_SHADE, "t": FANG_TRIM,
}
_SLAYER_PART_COLORS = dict(_SLAYER_COLORS)
_SLAYER_PART_COLORS["1"] = (0, 0, 0, 0)


def get_slayer_head(colors=None):
    """Mũ trùm sát gọn, mắt tím phát sáng — dữ dằn hơn khăn Trickster, không có vành/đỉnh nhọn."""
    colors = colors or _SLAYER_PART_COLORS
    t = [
        "011111110",
        "144444441",
        "144444441",
        "148888841",
        "144444441",
        "011111110",
    ]
    return render_part(t, colors), (4, 5)


def get_slayer_armor(colors=None):
    """Giáp da mỏng, hẹp hơn giáp tấm Vanguard rõ rệt — dải viền đỏ chéo ngực."""
    colors = colors or _SLAYER_PART_COLORS
    t = [
        "01111111110",
        "15555555551",
        "15577775551",
        "15555555551",
        "15555555551",
        "15555555551",
        "15555555551",
        "15555555551",
        "17777777771",
        "01111111110",
    ]
    return render_part(t, colors), (5, 0)


def get_slayer_blade(colors=None):
    """Đại đao dài — dài hơn hẳn kiếm Vanguard/dao Trickster/gậy Arcanist, thân rộng 2 cột."""
    colors = colors or _SLAYER_PART_COLORS
    t = ["30", "39", "39", "39", "39", "39", "t0", "90"]
    return render_part(t, colors), (0, 7)


SLAYER_KIT = CharacterKit(_SLAYER_PART_COLORS, get_slayer_head, get_slayer_armor,
                          get_slayer_blade, get_shield=None)

# ---- Summoner (Bone Caller) — pháp sư triệu hồi: áo choàng tím thẫm, khăn trùm có dấu xương
# trán, totem đầu lâu thay gậy đá phép của Arcanist — KHÔNG khiên ----
BONE_ROBE = (90, 48, 128, 255)        # #5A3080
BONE_ROBE_SHADE = (50, 27, 58, 255)   # #321B3A
BONE_TRIM = (242, 232, 207, 255)      # #F2E8CF (trắng xương)
BONE_GLOW = (203, 165, 240, 255)      # #CBA5F0

_SUMMONER_COLORS = {
    "0": (0, 0, 0, 0), "1": OUTLINE, "2": SKIN, "3": STEEL, "9": STEEL_SHADE,
    "4": BONE_ROBE_SHADE, "8": BONE_TRIM, "5": BONE_ROBE, "7": BONE_ROBE_SHADE, "t": BONE_GLOW,
}
_SUMMONER_PART_COLORS = dict(_SUMMONER_COLORS)
_SUMMONER_PART_COLORS["1"] = (0, 0, 0, 0)


def get_summoner_head(colors=None):
    """Khăn trùm tím thẫm + dấu xương trắng hình chữ thập trên trán — khác hẳn mũ nhọn băng giá
    của Arcanist dù cùng là caster."""
    colors = colors or _SUMMONER_PART_COLORS
    t = [
        "011111110",
        "144444441",
        "144484441",
        "144888441",
        "144484441",
        "011111110",
    ]
    return render_part(t, colors), (4, 5)


def get_summoner_robe(colors=None):
    """Áo choàng loe rộng hơn Arcanist (thầy triệu hồi mặc rộng rãi hơn thầy phép chiến đấu), viền
    xương trắng ở gấu áo."""
    colors = colors or _SUMMONER_PART_COLORS
    t = [
        "00111111000",
        "00155555100",
        "01555555510",
        "15555555551",
        "15555555551",
        "15555555551",
        "15555555551",
        "18888888881",
        "17777777771",
        "01111111110",
    ]
    return render_part(t, colors), (5, 0)


def get_summoner_totem(colors=None):
    """Totem đầu lâu — silhouette khác hẳn gậy-đá-phép Arcanist dù cùng vai trò gậy phép."""
    colors = colors or _SUMMONER_PART_COLORS
    t = ["898", "888", "080", "030", "030", "030", "090"]
    return render_part(t, colors), (1, 6)


SUMMONER_KIT = CharacterKit(_SUMMONER_PART_COLORS, get_summoner_head, get_summoner_robe,
                            get_summoner_totem, get_shield=None)


def build_frame(head_bob, arm_angle_deg, lean, prev_arm_angle_deg=None, leg_angle_deg=0,
                flash=False, kit=None):
    """1 frame nhân vật lắp từ bộ phận rig — arm_angle_deg XOAY THẬT (không dịch tay bằng tay
    như character_draw.py), đây là điểm khác biệt kỹ thuật chính cần so sánh. Mọi bộ phận neo vào
    CÙNG 1 điểm cổ (neck) chia sẻ — pivot đầu (đáy đầu) và pivot thân (đỉnh thân) phải trùng nhau
    tại đúng 1 toạ độ, không lệch tay. `kit` (CharacterKit) chọn class — mặc định Vanguard/Ember
    Knight, giữ hành vi cũ 100% khi không truyền.

    prev_arm_angle_deg (tuỳ chọn): nếu có, vẽ THÊM 1 bản vũ khí mờ (alpha=90, KHÔNG blur/BICUBIC —
    vẫn NEAREST cứng nét) ở góc của frame TRƯỚC đó — vệt mờ tốc độ (motion-blur trail), kỹ thuật
    giữ lại từ pixel_character_generator_modern.py NHƯNG không làm nhoè pixel gốc (khác bản gốc:
    bản gốc dùng BICUBIC cho toàn bộ khớp xoay + GaussianBlur cho bloom, làm nhoè cả đầu/thân)."""
    import math

    kit = kit or VANGUARD_KIT
    cx = SIZE // 2 + lean
    neck_y = 6 + head_bob          # đầu/thân nối tại đây
    hip_y = neck_y + 9              # đáy thân (10 hàng, viền cuối) = gốc hông
    shoulder = (cx + 5, neck_y + 3)

    def hand_pos(angle_deg):
        rad = math.radians(angle_deg)
        return shoulder[0] + int(5 * math.sin(rad)), shoulder[1] + int(5 * math.cos(rad))

    # ---- Lớp nhân vật thuần (không shadow) — để viền silhouette chỉ bọc đúng nhân vật ----
    char_layer = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    head_img, head_pv = kit.get_head()
    torso_img, torso_pv = kit.get_torso()
    leg_img, leg_pv = kit.get_leg()
    arm_img, arm_pv = kit.get_arm()
    weapon_img, weapon_pv = kit.get_weapon()

    paste_part(char_layer, leg_img, leg_pv, -leg_angle_deg, (cx - 3, hip_y))
    paste_part(char_layer, leg_img, leg_pv, leg_angle_deg, (cx + 3, hip_y))
    if kit.get_shield is not None:
        shield_img, shield_pv = kit.get_shield()
        paste_part(char_layer, shield_img, shield_pv, 0, (cx - 7, neck_y + 5))
    paste_part(char_layer, torso_img, torso_pv, 0, (cx, neck_y))
    paste_part(char_layer, head_img, head_pv, 0, (cx, neck_y))

    if prev_arm_angle_deg is not None:
        paste_part(char_layer, weapon_img, weapon_pv, prev_arm_angle_deg,
                   hand_pos(prev_arm_angle_deg), alpha=90)

    # Tay phải + vũ khí — XOAY QUANH VAI, vũ khí xoay CÙNG góc để dính chặt vào tay (đúng cấu trúc
    # skeletal thật: vũ khí là "con" của tay, không tính toạ độ tay lẫn kiếm riêng như bản cũ).
    paste_part(char_layer, arm_img, arm_pv, arm_angle_deg, shoulder)
    paste_part(char_layer, weapon_img, weapon_pv, arm_angle_deg, hand_pos(arm_angle_deg))

    char_layer = add_silhouette_outline(char_layer)

    if flash:
        # Chớp trắng khi trúng đòn — giữ nguyên alpha (silhouette), đổi RGB toàn bộ pixel đặc thành
        # trắng. Đây là hiệu ứng TĨNH trên frame PNG; khác với UnitView.PlayHit (chớp màu runtime
        # trên _sprite.color, không đụng) — dùng khi muốn 1 frame "dam" tự thân đã trắng sẵn.
        r, g, b, a = char_layer.split()
        white = Image.new("RGBA", char_layer.size, (255, 255, 255, 255))
        white.putalpha(a)
        char_layer = white

    # ---- Ghép cuối: bóng đổ (dưới) + nhân vật đã viền (trên) ----
    frame = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    draw_drop_shadow(frame, cx, hip_y + 6)
    frame.paste(char_layer, (0, 0), char_layer)
    return frame


IDLE_POSES = [
    {"head_bob": 0, "arm_angle_deg": 5, "lean": 0},
    {"head_bob": -1, "arm_angle_deg": 5, "lean": 0},
    {"head_bob": -1, "arm_angle_deg": 5, "lean": 0},
    {"head_bob": 0, "arm_angle_deg": 5, "lean": 0},
]

# Vươn kiếm thật qua các góc liên tục -70°(thu về)..+50°(vươn hết) — XOAY, không nội suy toạ độ tay
# tay như character_draw.py.
ATTACK_POSES = [
    {"head_bob": 0, "arm_angle_deg": -70, "lean": 0},
    {"head_bob": 0, "arm_angle_deg": -20, "lean": 0},
    {"head_bob": 0, "arm_angle_deg": 50, "lean": 1},
    {"head_bob": 0, "arm_angle_deg": 10, "lean": 0},
]

# Chân XOAY thật quanh hông (mirror trái/phải) — chu kỳ bước 4 frame, khớp arm đung đưa ngược pha.
MOVE_POSES = [
    {"head_bob": 0, "arm_angle_deg": -15, "lean": 0, "leg_angle_deg": 20},
    {"head_bob": -1, "arm_angle_deg": 5, "lean": 1, "leg_angle_deg": 0},
    {"head_bob": 0, "arm_angle_deg": 15, "lean": 0, "leg_angle_deg": -20},
    {"head_bob": -1, "arm_angle_deg": 5, "lean": 1, "leg_angle_deg": 0},
]

# Trúng đòn — bật lùi (lean âm), đầu hất nhẹ, frame đầu chớp trắng (plan.md §2.2: 3 frame/16fps).
DAMAGE_POSES = [
    {"head_bob": 1, "arm_angle_deg": -30, "lean": -2, "flash": True},
    {"head_bob": 1, "arm_angle_deg": -30, "lean": -3},
    {"head_bob": 0, "arm_angle_deg": -10, "lean": -1},
]

# Gục ngã — nghiêng dần + đầu sụp, KHÔNG bake fade-alpha vào PNG (UnitView.PlayDeath đã tự fade
# alpha runtime, giữ nguyên — chỉ cần pose "đổ" ở đây, plan.md §2.2: 6 frame/10fps, giữ frame cuối).
DIE_POSES = [
    {"head_bob": 1, "arm_angle_deg": -20, "lean": -1},
    {"head_bob": 2, "arm_angle_deg": -40, "lean": -3},
    {"head_bob": 3, "arm_angle_deg": -60, "lean": -5},
    {"head_bob": 4, "arm_angle_deg": -80, "lean": -7},
    {"head_bob": 5, "arm_angle_deg": -90, "lean": -8},
    {"head_bob": 5, "arm_angle_deg": -90, "lean": -8},
]


def draw_ember_burst(frame, cx, cy, radius, intensity):
    """VFX pilot — port draw_aura của v2, đổi tông màu cyan/tím -> lửa/ember, giữ nguyên kỹ thuật
    alpha-blend elip + nhiễu hạt."""
    import math, random
    layer = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    px = layer.load()
    for dy in range(-radius, radius):
        for dx in range(-radius, radius):
            dist = math.sqrt(dx * dx + (dy * 1.3) ** 2)
            if dist < radius:
                alpha = int(200 * (1 - dist / radius) * intensity)
                if alpha > 0:
                    x, y = cx + dx, cy + dy
                    if 0 <= x < SIZE and 0 <= y < SIZE:
                        noise = random.randint(-15, 15)
                        r = max(0, min(255, 240 + noise))
                        g = max(0, min(255, 140 + noise))
                        b = 40
                        px[x, y] = (r, g, b, alpha)
    frame.paste(layer, (0, 0), layer)
    return frame


def draw_inferno_bulwark_frame(size, frame_idx, num_frames):
    """VFX Ultimate `skill_inferno_bulwark` (hero_ember_knight, Fire/AoE) — nổ lửa 64×64, lõi trắng
    nóng -> cam -> đỏ, vài tia ember bắn lên. Cùng kỹ thuật alpha-blend elip của `draw_ember_burst`
    nhưng: (1) canvas 64×64 khớp quy ước VFX dự án (khác 32×32 nhân vật), (2) lõi gần-trắng thay vì
    cam thuần — để khi nhân với màu HDR của `Mat_HDREmissiveSprite` (VfxPlayer gán riêng cho key
    này) tạo bloom rõ nhất đúng chỗ sáng nhất, không cần bake blur vào file."""
    import math, random
    random.seed(frame_idx * 7 + 1)
    cx, cy = size // 2, size // 2
    t = frame_idx / max(1, num_frames - 1)  # 0..1 tiến trình nổ
    frame = Image.new("RGBA", (size, size), (0, 0, 0, 0))

    # Lõi nổ giãn dần rồi hơi co lại ở frame cuối (kiểu "bùng rồi tàn").
    radius = int(10 + 22 * math.sin(t * math.pi * 0.9))
    intensity = 0.5 + 0.5 * math.sin(t * math.pi)

    layer = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    px = layer.load()
    for dy in range(-radius, radius):
        for dx in range(-radius, radius):
            dist = math.sqrt(dx * dx + dy * dy)
            if dist < radius:
                falloff = 1 - dist / radius
                alpha = int(230 * falloff * intensity)
                if alpha <= 0:
                    continue
                x, y = cx + dx, cy + dy
                if not (0 <= x < size and 0 <= y < size):
                    continue
                noise = random.randint(-10, 10)
                if falloff > 0.75:  # lõi — gần trắng nóng
                    r, g, b = 255, min(255, 235 + noise), max(120, 190 + noise)
                elif falloff > 0.4:  # vùng giữa — cam
                    r, g, b = 255, max(0, 150 + noise), 30
                else:  # rìa — đỏ sẫm
                    r, g, b = max(0, 190 + noise), 40, 20
                px[x, y] = (r, g, b, alpha)
    frame.paste(layer, (0, 0), layer)

    # Vài tia ember bắn lên — dùng draw_pixel_line (Bresenham) cho nét cứng, không blur.
    gen = SkeletalPixelGeneratorLike()
    num_sparks = 4 + frame_idx
    for _ in range(num_sparks):
        ang = random.uniform(-math.pi * 0.8, -math.pi * 0.2)  # bắn lên trên
        length = random.randint(6, 16) + frame_idx * 2
        x1 = cx + int(math.cos(ang) * length)
        y1 = cy + int(math.sin(ang) * length)
        gen.draw_pixel_line(frame, cx, cy, x1, y1, (255, 200, 80, 220))

    return frame


class SkeletalPixelGeneratorLike:
    """Tái dùng đúng thuật toán vẽ đường Bresenham cho tia ember — tách riêng thay vì phụ thuộc
    class Mage của v2 (không import file khác dự án)."""

    def draw_pixel_line(self, frame, x0, y0, x1, y1, color):
        w, h = frame.size
        px = frame.load()
        dx, dy = abs(x1 - x0), abs(y1 - y0)
        x, y = x0, y0
        sx = -1 if x0 > x1 else 1
        sy = -1 if y0 > y1 else 1
        if dx > dy:
            err = dx / 2.0
            while x != x1:
                if 0 <= x < w and 0 <= y < h:
                    px[x, y] = color
                err -= dy
                if err < 0:
                    y += sy
                    err += dx
                x += sx
        else:
            err = dy / 2.0
            while y != y1:
                if 0 <= x < w and 0 <= y < h:
                    px[x, y] = color
                err -= dx
                if err < 0:
                    x += sx
                    err += dy
                y += sy
        if 0 <= x < w and 0 <= y < h:
            px[x, y] = color


POSE_SETS = {
    "idle": IDLE_POSES,
    "attack": ATTACK_POSES,
    "move": MOVE_POSES,
    "damage": DAMAGE_POSES,
    "die": DIE_POSES,
}


# ============================================================
# Giai đoạn 3 — recolor 18 hero còn lại (cùng silhouette class, khác element/màu)
# ============================================================

# ---- VANGUARD recolors ----
_IRON_BASTION_PART_COLORS = {
    "0": (0,0,0,0), "1": (0,0,0,0), "2": SKIN,
    "3": (154,154,154,255), "9": (92,92,92,255),
    "4": (107,69,38,255), "8": (212,165,116,255),
    "5": (92,92,92,255), "7": (58,36,22,255), "t": (166,113,66,255),
}
IRON_BASTION_KIT = recolor_kit(_PART_COLORS, _IRON_BASTION_PART_COLORS,
                                get_head, get_torso, get_weapon, get_shield)

_TIDE_WARDEN_PART_COLORS = {
    "0": (0,0,0,0), "1": (0,0,0,0), "2": SKIN,
    "3": (154,154,154,255), "9": (92,92,92,255),
    "4": (46,107,120,255), "8": (143,192,217,255),
    "5": (42,90,128,255), "7": (18,48,74,255), "t": (165,232,240,255),
}
TIDE_WARDEN_KIT = recolor_kit(_PART_COLORS, _TIDE_WARDEN_PART_COLORS,
                               get_head, get_torso, get_weapon, get_shield)

_STORMGUARD_PART_COLORS = {
    "0": (0,0,0,0), "1": (0,0,0,0), "2": SKIN,
    "3": (242,232,207,255), "9": (154,154,154,255),
    "4": (154,154,154,255), "8": (196,184,199,255),
    "5": (92,92,92,255), "7": (43,27,46,255), "t": (154,139,158,255),
}
STORMGUARD_KIT = recolor_kit(_PART_COLORS, _STORMGUARD_PART_COLORS,
                              get_head, get_torso, get_weapon, get_shield)

# ---- ARCANIST recolors ----
_PYROMANCER_PART_COLORS = {
    "0": (0,0,0,0), "1": (0,0,0,0), "2": SKIN,
    "3": (244,162,89,255), "9": (184,92,30,255),
    "4": (92,18,32,255), "8": (244,162,89,255),
    "5": (163,35,53,255), "7": (92,18,32,255), "t": (255,217,160,255),
}
PYROMANCER_KIT = recolor_kit(_ARCANIST_PART_COLORS, _PYROMANCER_PART_COLORS,
                              get_arcanist_head, get_arcanist_torso, get_arcanist_staff)

_TERRA_SEER_PART_COLORS = {
    "0": (0,0,0,0), "1": (0,0,0,0), "2": SKIN,
    "3": (212,165,116,255), "9": (107,69,38,255),
    "4": (107,69,38,255), "8": (212,165,116,255),
    "5": (166,113,66,255), "7": (58,36,22,255), "t": (123,201,80,255),
}
TERRA_SEER_KIT = recolor_kit(_ARCANIST_PART_COLORS, _TERRA_SEER_PART_COLORS,
                              get_arcanist_head, get_arcanist_torso, get_arcanist_staff)

_VOID_SCHOLAR_PART_COLORS = {
    "0": (0,0,0,0), "1": (0,0,0,0), "2": SKIN,
    "3": (155,93,229,255), "9": (90,48,128,255),
    "4": (42,27,58,255), "8": (155,93,229,255),
    "5": (90,48,128,255), "7": (42,27,58,255), "t": (203,165,240,255),
}
VOID_SCHOLAR_KIT = recolor_kit(_ARCANIST_PART_COLORS, _VOID_SCHOLAR_PART_COLORS,
                                get_arcanist_head, get_arcanist_torso, get_arcanist_staff)

# ---- TRICKSTER recolors ----
_NIGHT_STALKER_PART_COLORS = {
    "0": (0,0,0,0), "1": (0,0,0,0), "2": SKIN,
    "3": (90,74,94,255), "9": (43,27,46,255),
    "4": (26,15,28,255), "8": (155,93,229,255),
    "5": (58,34,51,255), "7": (26,15,28,255), "t": (90,48,128,255),
}
NIGHT_STALKER_KIT = recolor_kit(_TRICKSTER_PART_COLORS, _NIGHT_STALKER_PART_COLORS,
                                 get_trickster_head, get_trickster_torso, get_trickster_dagger)

_SPARK_RUNNER_PART_COLORS = {
    "0": (0,0,0,0), "1": (0,0,0,0), "2": SKIN,
    "3": (255,209,102,255), "9": (184,144,30,255),
    "4": (27,61,31,255), "8": (255,209,102,255),
    "5": (184,144,30,255), "7": (107,82,16,255), "t": (255,217,160,255),
}
SPARK_RUNNER_KIT = recolor_kit(_TRICKSTER_PART_COLORS, _SPARK_RUNNER_PART_COLORS,
                                get_trickster_head, get_trickster_torso, get_trickster_dagger)

_MIRAGE_FOX_PART_COLORS = {
    "0": (0,0,0,0), "1": (0,0,0,0), "2": SKIN,
    "3": (242,232,207,255), "9": (154,154,154,255),
    "4": (242,232,207,255), "8": (255,209,102,255),
    "5": (154,154,154,255), "7": (92,92,92,255), "t": (255,240,184,255),
}
MIRAGE_FOX_KIT = recolor_kit(_TRICKSTER_PART_COLORS, _MIRAGE_FOX_PART_COLORS,
                               get_trickster_head, get_trickster_torso, get_trickster_dagger)

# ---- WARDEN recolors ----
_GROVE_KEEPER_PART_COLORS = {
    "0": (0,0,0,0), "1": (0,0,0,0), "2": SKIN,
    "3": (123,201,80,255), "9": (61,122,46,255),
    "4": (27,61,31,255), "8": (185,232,154,255),
    "5": (61,122,46,255), "7": (27,61,31,255), "t": (123,201,80,255),
}
GROVE_KEEPER_KIT = recolor_kit(_WARDEN_PART_COLORS, _GROVE_KEEPER_PART_COLORS,
                                get_warden_head, get_warden_robe, get_warden_mace,
                                get_warden_shield)

_MOON_PRIESTESS_PART_COLORS = {
    "0": (0,0,0,0), "1": (0,0,0,0), "2": SKIN,
    "3": (242,232,207,255), "9": (154,154,154,255),
    "4": (154,139,158,255), "8": (242,232,207,255),
    "5": (115,99,119,255), "7": (74,47,66,255), "t": (143,192,217,255),
}
MOON_PRIESTESS_KIT = recolor_kit(_WARDEN_PART_COLORS, _MOON_PRIESTESS_PART_COLORS,
                                  get_warden_head, get_warden_robe, get_warden_mace,
                                  get_warden_shield)

_SPRING_MEDIC_PART_COLORS = {
    "0": (0,0,0,0), "1": (0,0,0,0), "2": SKIN,
    "3": (123,201,80,255), "9": (61,122,46,255),
    "4": (46,107,120,255), "8": (165,232,240,255),
    "5": (78,195,217,255), "7": (27,58,66,255), "t": (123,201,80,255),
}
SPRING_MEDIC_KIT = recolor_kit(_WARDEN_PART_COLORS, _SPRING_MEDIC_PART_COLORS,
                                get_warden_head, get_warden_robe, get_warden_mace,
                                get_warden_shield)

# ---- SLAYER recolors ----
_BLADE_DANCER_PART_COLORS = {
    "0": (0,0,0,0), "1": (0,0,0,0), "2": SKIN,
    "3": (154,154,154,255), "9": (92,92,92,255),
    "4": (27,61,31,255), "8": (123,201,80,255),
    "5": (61,122,46,255), "7": (27,61,31,255), "t": (185,232,154,255),
}
BLADE_DANCER_KIT = recolor_kit(_SLAYER_PART_COLORS, _BLADE_DANCER_PART_COLORS,
                                get_slayer_head, get_slayer_armor, get_slayer_blade)

_CRIMSON_REAVER_PART_COLORS = {
    "0": (0,0,0,0), "1": (0,0,0,0), "2": SKIN,
    "3": (154,154,154,255), "9": (92,92,92,255),
    "4": (92,18,32,255), "8": (244,162,89,255),
    "5": (163,35,53,255), "7": (92,18,32,255), "t": (255,217,160,255),
}
CRIMSON_REAVER_KIT = recolor_kit(_SLAYER_PART_COLORS, _CRIMSON_REAVER_PART_COLORS,
                                  get_slayer_head, get_slayer_armor, get_slayer_blade)

_STONE_BREAKER_PART_COLORS = {
    "0": (0,0,0,0), "1": (0,0,0,0), "2": SKIN,
    "3": (212,165,116,255), "9": (107,69,38,255),
    "4": (58,36,22,255), "8": (166,113,66,255),
    "5": (154,154,154,255), "7": (92,92,92,255), "t": (184,92,30,255),
}
STONE_BREAKER_KIT = recolor_kit(_SLAYER_PART_COLORS, _STONE_BREAKER_PART_COLORS,
                                 get_slayer_head, get_slayer_armor, get_slayer_blade)

# ---- SUMMONER recolors ----
_BEAST_TAMER_PART_COLORS = {
    "0": (0,0,0,0), "1": (0,0,0,0), "2": SKIN,
    "3": (212,165,116,255), "9": (107,69,38,255),
    "4": (107,69,38,255), "8": (212,165,116,255),
    "5": (166,113,66,255), "7": (58,36,22,255), "t": (123,201,80,255),
}
BEAST_TAMER_KIT = recolor_kit(_SUMMONER_PART_COLORS, _BEAST_TAMER_PART_COLORS,
                               get_summoner_head, get_summoner_robe, get_summoner_totem)

_FLAME_BINDER_PART_COLORS = {
    "0": (0,0,0,0), "1": (0,0,0,0), "2": SKIN,
    "3": (244,162,89,255), "9": (184,92,30,255),
    "4": (92,18,32,255), "8": (244,162,89,255),
    "5": (163,35,53,255), "7": (92,18,32,255), "t": (255,217,160,255),
}
FLAME_BINDER_KIT = recolor_kit(_SUMMONER_PART_COLORS, _FLAME_BINDER_PART_COLORS,
                                get_summoner_head, get_summoner_robe, get_summoner_totem)

_STAR_WEAVER_PART_COLORS = {
    "0": (0,0,0,0), "1": (0,0,0,0), "2": SKIN,
    "3": (255,209,102,255), "9": (184,144,30,255),
    "4": (42,27,58,255), "8": (255,209,102,255),
    "5": (42,90,128,255), "7": (18,48,74,255), "t": (184,144,30,255),
}
STAR_WEAVER_KIT = recolor_kit(_SUMMONER_PART_COLORS, _STAR_WEAVER_PART_COLORS,
                               get_summoner_head, get_summoner_robe, get_summoner_totem)


KITS = {
    "vanguard":       VANGUARD_KIT,
    "arcanist":       ARCANIST_KIT,
    "trickster":      TRICKSTER_KIT,
    "warden":         WARDEN_KIT,
    "slayer":         SLAYER_KIT,
    "summoner":       SUMMONER_KIT,
    # Giai đoạn 3 recolors
    "iron_bastion":   IRON_BASTION_KIT,
    "tide_warden":    TIDE_WARDEN_KIT,
    "stormguard":     STORMGUARD_KIT,
    "pyromancer":     PYROMANCER_KIT,
    "terra_seer":     TERRA_SEER_KIT,
    "void_scholar":   VOID_SCHOLAR_KIT,
    "night_stalker":  NIGHT_STALKER_KIT,
    "spark_runner":   SPARK_RUNNER_KIT,
    "mirage_fox":     MIRAGE_FOX_KIT,
    "grove_keeper":   GROVE_KEEPER_KIT,
    "moon_priestess": MOON_PRIESTESS_KIT,
    "spring_medic":   SPRING_MEDIC_KIT,
    "blade_dancer":   BLADE_DANCER_KIT,
    "crimson_reaver": CRIMSON_REAVER_KIT,
    "stone_breaker":  STONE_BREAKER_KIT,
    "beast_tamer":    BEAST_TAMER_KIT,
    "flame_binder":   FLAME_BINDER_KIT,
    "star_weaver":    STAR_WEAVER_KIT,
}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--state", choices=list(POSE_SETS.keys()))
    ap.add_argument("--vfx", choices=["ember_burst", "inferno_bulwark"])
    ap.add_argument("--frames", type=int, default=4)
    ap.add_argument("--kit", choices=list(KITS.keys()), default="vanguard")
    ap.add_argument("--hero", default="hero_ember_knight",
                    help="defId dùng làm tiền tố tên file xuất ra")
    ap.add_argument("--out", required=True)
    args = ap.parse_args()

    out_dir = Path(args.out)
    out_dir.mkdir(parents=True, exist_ok=True)

    if args.vfx == "ember_burst":
        for i in range(args.frames):
            frame = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
            intensity = 0.4 + 0.6 * (i / max(1, args.frames - 1))
            radius = 6 + i * 2
            draw_ember_burst(frame, SIZE // 2, SIZE // 2, radius, intensity)
            path = out_dir / f"vfx_ember_burst_{i:02d}.png"
            frame.save(path)
            print(f"  ✓ {path}")
        return

    if args.vfx == "inferno_bulwark":
        VFX_SIZE = 64  # khớp quy ước VFX dự án (Art/VFX/vfx_*, khác 32×32 nhân vật)
        n = args.frames
        for i in range(n):
            frame = draw_inferno_bulwark_frame(VFX_SIZE, i, n)
            path = out_dir / f"vfx_inferno_bulwark_{i:02d}.png"
            frame.save(path)
            print(f"  ✓ {path} ({frame.size[0]}×{frame.size[1]})")
        return

    kit = KITS[args.kit]
    poses = POSE_SETS[args.state][: args.frames]
    for i, pose in enumerate(poses):
        kwargs = dict(pose)
        if args.state == "attack" and i > 0:
            kwargs["prev_arm_angle_deg"] = poses[i - 1]["arm_angle_deg"]
        frame = build_frame(kit=kit, **kwargs)
        path = out_dir / f"{args.hero}_rig_{args.state}_{i:02d}.png"
        frame.save(path)
        print(f"  ✓ {path}")


if __name__ == "__main__":
    main()

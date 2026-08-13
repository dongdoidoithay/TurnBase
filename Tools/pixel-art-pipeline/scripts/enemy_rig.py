#!/usr/bin/env python3
"""
enemy_rig.py — Sinh sprite 32×32 enemy/boss cho TurnBase Aether Legion.
Cùng kỹ thuật skeletal rig với character_rig.py cho humanoid; PIL draw trực tiếp cho creature.

Humanoid archetypes (dùng build_frame từ character_rig):
  goblin | skeleton | zombie | brute | caster | knight

Creature archetypes (PIL draw):
  wolf | bat | slime | wisp | golem | serpent | spider | horror | toad | swarm

Boss: boss_wolf | boss_goblin | boss_lich | boss_void | boss_drake

Dùng:
  python3 enemy_rig.py --kit goblin --state idle --frames 4 \
      --enemy enemy_goblin --out clean/enemy_goblin_rig/idle

  # Liệt kê tất cả enemy → kit mapping:
  python3 enemy_rig.py --list
"""

import argparse, math, sys
from pathlib import Path

try:
    from PIL import Image, ImageDraw
except ImportError:
    raise SystemExit("Cần Pillow: pip install Pillow")

sys.path.insert(0, str(Path(__file__).parent))
from character_rig import (
    SIZE, OUTLINE, SKIN,
    render_part, paste_part, add_silhouette_outline, draw_drop_shadow,
    get_leg, get_arm, get_shield, get_head, get_torso, get_weapon,
    get_arcanist_head, get_arcanist_torso, get_arcanist_staff,
    get_summoner_head, get_summoner_robe, get_summoner_totem,
    CharacterKit, recolor_kit,
    build_frame,
    IDLE_POSES, ATTACK_POSES, MOVE_POSES, DAMAGE_POSES, DIE_POSES, POSE_SETS,
    _PART_COLORS, _ARCANIST_PART_COLORS, _SUMMONER_PART_COLORS,
)

# ================================================================
# BẢNG MÀU DÙNG CHUNG
# ================================================================

BONE_WHITE  = (242, 232, 207, 255)
BONE_SHADOW = (154, 139, 158, 255)
BONE_DARK   = (90, 74, 94, 255)
BONE_EYE    = (120, 50, 180, 255)

# ================================================================
# HUMANOID ENEMY KITS — dùng build_frame() từ character_rig
# ================================================================

# ---- Goblin ----
_GOBLIN_PC = {
    "0": (0,0,0,0), "1": (0,0,0,0), "2": (220,170,100,255),
    "3": (120,80,30,255), "9": (70,45,15,255),
    "4": (62,130,55,255), "8": (220,60,50,255),
    "5": (46,100,42,255), "7": (22,55,20,255), "t": (180,120,40,255),
}

def get_goblin_head(colors=None):
    colors = colors or _GOBLIN_PC
    t = [
        "0181810",
        "1444441",
        "1488441",
        "1442241",
        "1444441",
        "0111110",
    ]
    return render_part(t, colors), (3, 5)

def get_goblin_torso(colors=None):
    colors = colors or _GOBLIN_PC
    t = [
        "011111110",
        "155555551",
        "155555551",
        "155555551",
        "155555551",
        "177777771",
        "011111110",
    ]
    return render_part(t, colors), (4, 0)

def get_goblin_club(colors=None):
    colors = colors or _GOBLIN_PC
    t = ["03", "03", "33", "30", "30", "90"]
    return render_part(t, colors), (0, 5)

GOBLIN_KIT = CharacterKit(_GOBLIN_PC, get_goblin_head, get_goblin_torso, get_goblin_club)

# Goblin Shaman (Dark) — mắt tím, thân tối
_GOBLIN_DARK_PC = dict(_GOBLIN_PC)
_GOBLIN_DARK_PC.update({
    "4": (42,27,58,255), "8": (155,93,229,255),
    "5": (58,34,51,255), "7": (26,15,28,255), "t": (203,165,240,255),
    "3": (155,93,229,255), "9": (90,48,128,255),
})
GOBLIN_DARK_KIT = recolor_kit(_GOBLIN_PC, _GOBLIN_DARK_PC,
                               get_goblin_head, get_goblin_torso, get_goblin_club)

# Goblin Archer (Wind) — xanh lá nhạt
_GOBLIN_WIND_PC = dict(_GOBLIN_PC)
_GOBLIN_WIND_PC.update({
    "4": (55,120,40,255), "8": (185,230,100,255),
    "5": (35,90,25,255), "7": (18,55,12,255), "t": (150,210,70,255),
    "3": (100,70,20,255), "9": (60,40,10,255),
})
GOBLIN_WIND_KIT = recolor_kit(_GOBLIN_PC, _GOBLIN_WIND_PC,
                               get_goblin_head, get_goblin_torso, get_goblin_club)

# Boss Goblin King — vương miện vàng, giáp xám
_GOBLIN_KING_PC = dict(_GOBLIN_PC)
_GOBLIN_KING_PC.update({
    "4": (80,160,65,255), "8": (255,209,102,255),
    "5": (92,92,92,255), "7": (58,58,58,255), "t": (255,209,102,255),
    "3": (154,154,154,255), "9": (92,92,92,255),
})

def get_goblin_king_head(colors=None):
    colors = colors or _GOBLIN_KING_PC
    t = [
        "0ttttt0",
        "0181810",
        "1444441",
        "1488441",
        "1442241",
        "1444441",
        "0111110",
    ]
    return render_part(t, colors), (3, 6)

GOBLIN_KING_KIT = CharacterKit(_GOBLIN_KING_PC, get_goblin_king_head,
                                get_goblin_torso, get_goblin_club, get_shield)

# ---- Skeleton ----
_SKEL_PC = {
    "0": (0,0,0,0), "1": (0,0,0,0), "2": BONE_WHITE,
    "3": BONE_SHADOW, "9": BONE_DARK,
    "4": BONE_WHITE, "8": BONE_EYE,
    "5": BONE_WHITE, "7": BONE_SHADOW, "t": BONE_SHADOW,
}

def get_skull_head(colors=None):
    colors = colors or _SKEL_PC
    t = [
        "011111110",
        "144444441",
        "148888841",
        "144444441",
        "141414141",
        "011111110",
    ]
    return render_part(t, colors), (4, 5)

def get_skeleton_torso(colors=None):
    colors = colors or _SKEL_PC
    t = [
        "011111110",
        "155555551",
        "157575751",
        "155555551",
        "157575751",
        "155555551",
        "177777771",
        "011111110",
    ]
    return render_part(t, colors), (4, 0)

def get_bone_sword(colors=None):
    colors = colors or _SKEL_PC
    t = ["2", "2", "3", "3", "3", "9"]
    return render_part(t, colors), (0, 5)

SKELETON_KIT = CharacterKit(_SKEL_PC, get_skull_head, get_skeleton_torso, get_bone_sword)

# Mummy Guardian (Neutral) — quấn vải nâu
_MUMMY_PC = dict(_SKEL_PC)
_MUMMY_PC.update({
    "5": (212,165,116,255), "7": (107,69,38,255),
    "4": (192,145,96,255), "t": BONE_WHITE,
})
MUMMY_KIT = recolor_kit(_SKEL_PC, _MUMMY_PC, get_skull_head, get_skeleton_torso, get_bone_sword)

# ---- Zombie ----
_ZOMBIE_PC = {
    "0": (0,0,0,0), "1": (0,0,0,0), "2": (92,122,62,255),
    "3": (92,92,92,255), "9": (58,58,58,255),
    "4": (78,108,50,255), "8": (163,35,53,255),
    "5": (72,95,48,255), "7": (40,60,25,255), "t": (55,85,35,255),
}

def get_zombie_head(colors=None):
    colors = colors or _ZOMBIE_PC
    t = [
        "011111110",
        "144444441",
        "148444841",
        "144444441",
        "181818181",
        "011111110",
    ]
    return render_part(t, colors), (4, 5)

def get_zombie_torso(colors=None):
    colors = colors or _ZOMBIE_PC
    t = [
        "011111110",
        "155555551",
        "155855551",
        "155555551",
        "158555851",
        "155555551",
        "177777771",
        "011111110",
    ]
    return render_part(t, colors), (4, 0)

ZOMBIE_KIT = CharacterKit(_ZOMBIE_PC, get_zombie_head, get_zombie_torso, get_bone_sword)

# Charred Zombie (Fire)
_ZOMBIE_FIRE_PC = dict(_ZOMBIE_PC)
_ZOMBIE_FIRE_PC.update({
    "4": (58,22,12,255), "8": (244,100,30,255),
    "5": (45,20,10,255), "7": (30,12,5,255),
    "2": (90,50,20,255), "t": (163,35,53,255),
})
ZOMBIE_FIRE_KIT = recolor_kit(_ZOMBIE_PC, _ZOMBIE_FIRE_PC,
                               get_zombie_head, get_zombie_torso, get_bone_sword)

# ---- Brute / Ogre ----
_BRUTE_PC = {
    "0": (0,0,0,0), "1": (0,0,0,0), "2": (166,113,66,255),
    "3": (92,92,92,255), "9": (58,58,58,255),
    "4": (150,100,58,255), "8": (220,60,45,255),
    "5": (58,36,22,255), "7": (30,18,10,255), "t": (92,55,22,255),
}

def get_brute_head(colors=None):
    colors = colors or _BRUTE_PC
    t = [
        "01111110",
        "14444441",
        "14848441",
        "14444441",
        "01111110",
    ]
    return render_part(t, colors), (3, 4)

def get_brute_torso(colors=None):
    colors = colors or _BRUTE_PC
    t = [
        "011111111110",
        "155555555551",
        "155555555551",
        "155555555551",
        "155555555551",
        "155555555551",
        "177777777771",
        "011111111110",
    ]
    return render_part(t, colors), (5, 0)

def get_club(colors=None):
    colors = colors or _BRUTE_PC
    t = ["333", "393", "393", "333", "030", "090"]
    return render_part(t, colors), (1, 5)

BRUTE_KIT = CharacterKit(_BRUTE_PC, get_brute_head, get_brute_torso, get_club)

# Brute Dark (shadow_knight, void_reaver, nightmare_fiend)
_BRUTE_DARK_PC = dict(_BRUTE_PC)
_BRUTE_DARK_PC.update({
    "4": (26,15,28,255), "8": (155,93,229,255),
    "5": (58,34,51,255), "7": (26,15,28,255), "t": (90,48,128,255),
    "3": (90,74,94,255), "9": (43,27,46,255),
})
BRUTE_DARK_KIT = recolor_kit(_BRUTE_PC, _BRUTE_DARK_PC, get_brute_head, get_brute_torso, get_club)

# Brute Fire (ember_knight, molten_brute)
_BRUTE_FIRE_PC = dict(_BRUTE_PC)
_BRUTE_FIRE_PC.update({
    "4": (92,18,32,255), "8": (244,162,89,255),
    "5": (58,22,12,255), "7": (30,10,5,255), "t": (163,35,53,255),
    "3": (154,154,154,255), "9": (92,92,92,255),
})
BRUTE_FIRE_KIT = recolor_kit(_BRUTE_PC, _BRUTE_FIRE_PC, get_brute_head, get_brute_torso, get_club)

# Brute Water (swamp_troll, drowned_knight)
_BRUTE_WATER_PC = dict(_BRUTE_PC)
_BRUTE_WATER_PC.update({
    "4": (46,107,120,255), "8": (143,192,217,255),
    "5": (42,90,128,255), "7": (18,48,74,255), "t": (165,232,240,255),
    "3": (154,154,154,255), "9": (92,92,92,255),
})
BRUTE_WATER_KIT = recolor_kit(_BRUTE_PC, _BRUTE_WATER_PC, get_brute_head, get_brute_torso, get_club)

# Brute Earth (ogre_brute, shield_bearer heavy armor)
_BRUTE_EARTH_PC = dict(_BRUTE_PC)
_BRUTE_EARTH_PC.update({
    "4": (107,69,38,255), "8": (212,165,116,255),
    "5": (92,92,92,255), "7": (58,36,22,255), "t": (166,113,66,255),
    "3": (154,154,154,255), "9": (92,92,92,255),
})
BRUTE_EARTH_KIT = recolor_kit(_BRUTE_PC, _BRUTE_EARTH_PC, get_brute_head, get_brute_torso, get_club)

# Boss Void King — to hơn brute, mắt tím sáng
_BOSS_VOID_PC = dict(_BRUTE_DARK_PC)
_BOSS_VOID_PC.update({
    "4": (10,5,20,255), "8": (200,140,255,255),
    "5": (30,15,45,255), "7": (10,5,20,255), "t": (155,93,229,255),
})
BOSS_VOID_KIT = CharacterKit(_BOSS_VOID_PC, get_brute_head, get_brute_torso, get_club, get_shield)

# ---- Enemy Caster (robed mage) — reuse Arcanist silhouette ----
_CASTER_DARK_PC = {
    "0": (0,0,0,0), "1": (0,0,0,0), "2": SKIN,
    "3": (155,93,229,255), "9": (90,48,128,255),
    "4": (42,27,58,255), "8": (155,93,229,255),
    "5": (90,48,128,255), "7": (42,27,58,255), "t": (203,165,240,255),
}
CASTER_DARK_KIT = recolor_kit(_ARCANIST_PART_COLORS, _CASTER_DARK_PC,
                               get_arcanist_head, get_arcanist_torso, get_arcanist_staff)

_CASTER_FIRE_PC = {
    "0": (0,0,0,0), "1": (0,0,0,0), "2": SKIN,
    "3": (244,162,89,255), "9": (184,92,30,255),
    "4": (92,18,32,255), "8": (244,162,89,255),
    "5": (163,35,53,255), "7": (92,18,32,255), "t": (255,217,160,255),
}
CASTER_FIRE_KIT = recolor_kit(_ARCANIST_PART_COLORS, _CASTER_FIRE_PC,
                               get_arcanist_head, get_arcanist_torso, get_arcanist_staff)

_CASTER_WATER_PC = {
    "0": (0,0,0,0), "1": (0,0,0,0), "2": SKIN,
    "3": (78,195,217,255), "9": (27,58,66,255),
    "4": (18,48,74,255), "8": (165,232,240,255),
    "5": (46,107,120,255), "7": (18,48,74,255), "t": (165,232,240,255),
}
CASTER_WATER_KIT = recolor_kit(_ARCANIST_PART_COLORS, _CASTER_WATER_PC,
                                get_arcanist_head, get_arcanist_torso, get_arcanist_staff)

# Caster Undead (death_priest, soul_reaper) — Summoner silhouette + xương
_CASTER_UNDEAD_PC = {
    "0": (0,0,0,0), "1": (0,0,0,0), "2": BONE_WHITE,
    "3": BONE_SHADOW, "9": BONE_DARK,
    "4": BONE_WHITE, "8": BONE_EYE,
    "5": (90,74,94,255), "7": (43,27,46,255), "t": BONE_WHITE,
}
CASTER_UNDEAD_KIT = recolor_kit(_SUMMONER_PART_COLORS, _CASTER_UNDEAD_PC,
                                 get_summoner_head, get_summoner_robe, get_summoner_totem)

# Caster Neutral healer (star_priest)
_CASTER_LIGHT_PC = {
    "0": (0,0,0,0), "1": (0,0,0,0), "2": SKIN,
    "3": (255,209,102,255), "9": (184,140,40,255),
    "4": (242,232,207,255), "8": (255,209,102,255),
    "5": (200,185,240,255), "7": (130,110,180,255), "t": (255,240,180,255),
}
CASTER_LIGHT_KIT = recolor_kit(_SUMMONER_PART_COLORS, _CASTER_LIGHT_PC,
                                get_summoner_head, get_summoner_robe, get_summoner_totem)

# Boss Lich — đầu lâu to + áo Summoner tối
_BOSS_LICH_PC = {
    "0": (0,0,0,0), "1": (0,0,0,0), "2": BONE_WHITE,
    "3": (155,93,229,255), "9": (90,48,128,255),
    "4": BONE_WHITE, "8": (155,93,229,255),
    "5": (42,27,58,255), "7": (26,15,28,255), "t": BONE_WHITE,
}
BOSS_LICH_KIT = recolor_kit(_SUMMONER_PART_COLORS, _BOSS_LICH_PC,
                             get_skull_head, get_summoner_robe, get_summoner_totem)

# ---- Enemy Knight (armored humanoid) — reuse Vanguard silhouette ----
_KNIGHT_DARK_PC = {
    "0": (0,0,0,0), "1": (0,0,0,0), "2": SKIN,
    "3": (90,74,94,255), "9": (43,27,46,255),
    "4": (58,34,51,255), "8": (155,93,229,255),
    "5": (58,34,51,255), "7": (26,15,28,255), "t": (90,48,128,255),
}
KNIGHT_DARK_KIT = recolor_kit(_PART_COLORS, _KNIGHT_DARK_PC,
                               get_head, get_torso, get_weapon, get_shield)

_KNIGHT_WATER_PC = {
    "0": (0,0,0,0), "1": (0,0,0,0), "2": SKIN,
    "3": (154,154,154,255), "9": (92,92,92,255),
    "4": (46,107,120,255), "8": (143,192,217,255),
    "5": (42,90,128,255), "7": (18,48,74,255), "t": (165,232,240,255),
}
KNIGHT_WATER_KIT = recolor_kit(_PART_COLORS, _KNIGHT_WATER_PC,
                                get_head, get_torso, get_weapon, get_shield)

_KNIGHT_EARTH_PC = {
    "0": (0,0,0,0), "1": (0,0,0,0), "2": SKIN,
    "3": (154,154,154,255), "9": (92,92,92,255),
    "4": (107,69,38,255), "8": (212,165,116,255),
    "5": (92,92,92,255), "7": (58,36,22,255), "t": (166,113,66,255),
}
KNIGHT_EARTH_KIT = recolor_kit(_PART_COLORS, _KNIGHT_EARTH_PC,
                                get_head, get_torso, get_weapon, get_shield)

_KNIGHT_DARK2_PC = {
    "0": (0,0,0,0), "1": (0,0,0,0), "2": SKIN,
    "3": (90,74,94,255), "9": (43,27,46,255),
    "4": (26,15,28,255), "8": (200,140,255,255),
    "5": (30,15,45,255), "7": (10,5,20,255), "t": (155,93,229,255),
}
KNIGHT_DARK2_KIT = recolor_kit(_PART_COLORS, _KNIGHT_DARK2_PC,
                                get_head, get_torso, get_weapon, get_shield)

# ================================================================
# CREATURE ARCHETYPES — PIL draw trực tiếp
# ================================================================

def _new():
    return Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))


def _final(img):
    img = add_silhouette_outline(img)
    canvas = _new()
    draw_drop_shadow(canvas, SIZE // 2, SIZE - 4)
    canvas.paste(img, (0, 0), img)
    return canvas


def draw_wolf(state, frame_idx, colors):
    fur   = colors.get("fur",   (155,120,80,255))
    shade = colors.get("shade", (100,70,40,255))
    eye   = colors.get("eye",   (255,220,60,255))
    nose  = (40,25,20,255)

    img = _new()
    d = ImageDraw.Draw(img)

    # State offsets [dx, dy] per frame
    if state == "idle":
        ox = [0,0,0,0]; oy = [0,-1,-1,0]
    elif state == "attack":
        ox = [0,2,4,2]; oy = [0,-2,-3,-1]
    elif state == "move":
        ox = [0,2,0,-2]; oy = [0,-1,0,-1]
    elif state == "damage":
        ox = [-3,-2,-1]; oy = [2,1,0]
    else:  # die
        ox = [0,0,-1,-2,-2,-2]; oy = [2,4,6,8,10,10]

    n = len(ox)
    fi = min(frame_idx, n - 1)
    dx, dy = ox[fi], oy[fi]

    # Body
    bx, by, bw, bh = 10+dx, 12+dy, 14, 8
    d.ellipse([bx, by, bx+bw, by+bh], fill=fur)
    d.ellipse([bx+3, by+2, bx+bw-3, by+bh-2], fill=shade)

    # Head
    hx, hy = 3+dx, 9+dy
    d.ellipse([hx, hy, hx+9, hy+7], fill=fur)
    # Snout
    d.ellipse([hx-3, hy+3, hx+2, hy+7], fill=fur)
    d.ellipse([hx-3, hy+4, hx+1, hy+7], fill=shade)
    d.ellipse([hx-3, hy+2, hx, hy+5], fill=nose)
    # Eye
    d.ellipse([hx+4, hy+1, hx+7, hy+4], fill=eye)
    d.point([hx+5, hy+2], fill=(0,0,0,255))
    # Ear
    d.polygon([(hx+4,hy),(hx+6,hy-3),(hx+8,hy)], fill=fur)

    # Tail — arc via polygon
    tx, ty = bx+bw-2+dx, by-4+dy
    tail_pts = [(tx,by-1+dy),(tx+3,by-4+dy),(tx+6,by-6+dy),(tx+7,by-4+dy),(tx+5,by-1+dy)]
    d.polygon(tail_pts, fill=fur)

    # Legs
    if state == "die":
        angle_off = fi * 3
        d.line([bx+2,by+bh, bx-angle_off,by+bh+5], fill=shade, width=2)
        d.line([bx+5,by+bh, bx+5+angle_off,by+bh+5], fill=shade, width=2)
        d.line([bx+9,by+bh, bx+9-angle_off,by+bh+5], fill=shade, width=2)
        d.line([bx+12,by+bh, bx+12+angle_off,by+bh+5], fill=shade, width=2)
    elif state == "move":
        la = [(0,0,3,-2),(3,-2,0,0),(0,0,3,-2),(3,-2,0,0)]
        pair = la[frame_idx % 4]
        d.line([bx+2+pair[0],by+bh+pair[1], bx+2+pair[0],by+bh+6+pair[1]], fill=shade, width=2)
        d.line([bx+5+pair[2],by+bh+pair[3], bx+5+pair[2],by+bh+6+pair[3]], fill=shade, width=2)
        la2 = [(3,-2,0,0),(0,0,3,-2),(3,-2,0,0),(0,0,3,-2)]
        pair2 = la2[frame_idx % 4]
        d.line([bx+9+pair2[0],by+bh+pair2[1], bx+9+pair2[0],by+bh+6+pair2[1]], fill=shade, width=2)
        d.line([bx+12+pair2[2],by+bh+pair2[3], bx+12+pair2[2],by+bh+6+pair2[3]], fill=shade, width=2)
    else:
        d.line([bx+2,by+bh, bx+2,by+bh+6], fill=shade, width=2)
        d.line([bx+5,by+bh, bx+5,by+bh+6], fill=shade, width=2)
        d.line([bx+9,by+bh, bx+9,by+bh+6], fill=shade, width=2)
        d.line([bx+12,by+bh, bx+12,by+bh+6], fill=shade, width=2)

    if state == "damage" and frame_idx == 0:
        r,g,b,a = img.split(); white = Image.new("RGBA",img.size,(255,255,255,255)); white.putalpha(a); img = white
    return _final(img)


def draw_bat(state, frame_idx, colors):
    body  = colors.get("body",  (60,30,90,255))
    wing  = colors.get("wing",  (40,15,65,255))
    eye   = colors.get("eye",   (255,60,60,255))

    img = _new()
    d = ImageDraw.Draw(img)

    # Wing spread per frame
    if state == "idle":
        spread = [14,12,12,14][frame_idx % 4]
        cy = 14
    elif state == "attack":
        spread = [14,16,18,14][frame_idx % 4]
        cy = [14,12,10,12][frame_idx % 4]
    elif state == "move":
        spread = [16,12,16,12][frame_idx % 4]
        cy = [12,14,12,14][frame_idx % 4]
    elif state == "damage":
        spread = [10,12,14][frame_idx % 3]
        cy = [16,15,14][frame_idx % 3]
    else:  # die
        spread = 10; cy = [14,16,18,20,22,22][min(frame_idx,5)]

    cx = SIZE // 2
    # Wings
    for sign in (-1, 1):
        pts = [
            (cx, cy),
            (cx + sign*spread, cy - 5),
            (cx + sign*spread, cy + 3),
            (cx + sign*(spread//2), cy + 6),
        ]
        d.polygon(pts, fill=wing)
    # Body
    d.ellipse([cx-4, cy-3, cx+4, cy+5], fill=body)
    # Ears
    d.polygon([(cx-3,cy-3),(cx-2,cy-7),(cx,cy-3)], fill=body)
    d.polygon([(cx+3,cy-3),(cx+2,cy-7),(cx,cy-3)], fill=body)
    # Eyes
    d.point([cx-2, cy-1], fill=eye)
    d.point([cx+2, cy-1], fill=eye)

    if state == "damage" and frame_idx == 0:
        r,g,b,a = img.split(); white = Image.new("RGBA",img.size,(255,255,255,255)); white.putalpha(a); img = white
    return _final(img)


def draw_slime(state, frame_idx, colors):
    body = colors.get("body", (46,190,80,255))
    mid  = colors.get("mid",  (100,230,120,255))
    eye  = (255,255,255,255)

    img = _new()
    d = ImageDraw.Draw(img)
    cx = SIZE // 2

    if state == "idle":
        cy,w,h = [20,19,19,20][frame_idx%4], 13, [10,9,9,10][frame_idx%4]
    elif state == "attack":
        cx_off = [0,2,4,2][frame_idx%4]
        cx += cx_off
        cy,w,h = [20,19,18,19][frame_idx%4], 13, 10
    elif state == "move":
        cy = 20; w = [13,15,13,11][frame_idx%4]; h = [10,8,10,12][frame_idx%4]
    elif state == "damage":
        cy,w,h = 22, 16, [7,8,9][frame_idx%3]
    else:  # die
        spread = min(frame_idx, 5)
        cy,w,h = 24, min(14+spread*2,28), max(4,10-spread*2)

    d.ellipse([cx-w, cy-h, cx+w, cy+h], fill=body)
    d.ellipse([cx-w//2, cy-h//2, cx+w//3, cy+h//3], fill=mid)
    # Eyes
    if not (state=="die" and frame_idx >= 4):
        d.ellipse([cx-5,cy-3,cx-2,cy], fill=eye)
        d.point([cx-4,cy-2], fill=(0,0,0,255))
        d.ellipse([cx+2,cy-3,cx+5,cy], fill=eye)
        d.point([cx+3,cy-2], fill=(0,0,0,255))

    if state == "damage" and frame_idx == 0:
        r,g,b,a = img.split(); white = Image.new("RGBA",img.size,(255,255,255,255)); white.putalpha(a); img = white
    return _final(img)


def draw_wisp(state, frame_idx, colors):
    core  = colors.get("core",  (200,120,255,255))
    glow  = colors.get("glow",  (130,60,200,180))
    trail = colors.get("trail", (80,20,140,120))

    img = _new()
    d = ImageDraw.Draw(img)
    cx = SIZE // 2

    if state == "idle":
        cy = [14,13,13,14][frame_idx%4]; cr = [5,6,6,5][frame_idx%4]
    elif state == "attack":
        cy = [14,12,10,12][frame_idx%4]; cr = 6
    elif state == "move":
        cy = [14,13,14,15][frame_idx%4]; cr = 5
    elif state == "damage":
        cy = [14,15,16][frame_idx%3]; cr = [3,4,5][frame_idx%3]
    else:  # die
        cy = 14; cr = max(1, 6 - frame_idx)

    # Glow ring
    d.ellipse([cx-cr-3,cy-cr-3,cx+cr+3,cy+cr+3], fill=glow)
    # Core
    d.ellipse([cx-cr,cy-cr,cx+cr,cy+cr], fill=core)
    # Trail wisps below
    for i,ty in enumerate([cy+cr+2, cy+cr+5]):
        alpha = 120 - i*40
        r,g,b,_ = trail
        d.ellipse([cx-2,ty,cx+2,ty+3], fill=(r,g,b,alpha))

    if state == "damage" and frame_idx == 0:
        r,g,b,a = img.split(); white = Image.new("RGBA",img.size,(255,255,255,255)); white.putalpha(a); img = white
    return _final(img)


def draw_golem(state, frame_idx, colors):
    stone  = colors.get("stone",  (110,95,80,255))
    crack  = colors.get("crack",  (70,55,45,255))
    eye    = colors.get("eye",    (255,200,50,255))

    img = _new()
    d = ImageDraw.Draw(img)
    cx = SIZE // 2

    if state == "idle":
        lean = [0,0,1,0][frame_idx%4]
    elif state == "attack":
        lean = [0,2,4,2][frame_idx%4]
    elif state == "move":
        lean = [0,1,0,-1][frame_idx%4]
    elif state == "damage":
        lean = [-2,-1,0][frame_idx%3]
    else:  # die
        lean = [0,-1,-2,-3,-4,-4][min(frame_idx,5)]

    # Body (large rectangle)
    bx = cx-6+lean; by = 10
    d.rectangle([bx,by,bx+12,by+12], fill=stone)
    # Crack pattern on body
    d.line([bx+3,by+2,bx+5,by+7], fill=crack, width=1)
    d.line([bx+8,by+4,bx+7,by+9], fill=crack, width=1)
    # Head (smaller square)
    d.rectangle([bx+1,by-6,bx+11,by], fill=stone)
    # Eyes
    d.rectangle([bx+2,by-4,bx+4,by-2], fill=eye)
    d.rectangle([bx+8,by-4,bx+10,by-2], fill=eye)
    # Arms
    if state == "attack":
        arm_raise = [0,-3,-6,-3][frame_idx%4]
        d.rectangle([bx-4,by+arm_raise,bx-1,by+5+arm_raise], fill=stone)
        d.rectangle([bx+13,by,bx+16,by+5], fill=stone)
    else:
        d.rectangle([bx-4,by+2,bx-1,by+7], fill=stone)
        d.rectangle([bx+13,by+2,bx+16,by+7], fill=stone)
    # Legs
    d.rectangle([bx+1,by+12,bx+5,by+18], fill=stone)
    d.rectangle([bx+7,by+12,bx+11,by+18], fill=stone)

    if state == "damage" and frame_idx == 0:
        r,g,b,a = img.split(); white = Image.new("RGBA",img.size,(255,255,255,255)); white.putalpha(a); img = white
    return _final(img)


def draw_serpent(state, frame_idx, colors):
    sc    = colors.get("scale", (42,120,60,255))
    belly = colors.get("belly", (160,200,130,255))
    eye   = colors.get("eye",   (255,220,50,255))

    img = _new()
    d = ImageDraw.Draw(img)

    # S-curve anchor points change per state/frame
    if state == "idle":
        phase = [0,1,2,1][frame_idx%4]
        sway = phase * 2
    elif state == "attack":
        sway = [0,3,6,3][frame_idx%4]
    elif state == "move":
        sway = [0,2,4,2][frame_idx%4]
    elif state == "damage":
        sway = [-4,-2,0][frame_idx%3]
    else:  # die
        sway = [0,1,2,3,4,4][min(frame_idx,5)]

    # Head at top, body snaking down
    hx,hy = 14+sway, 5
    d.ellipse([hx,hy,hx+6,hy+5], fill=sc)  # head
    d.ellipse([hx+1,hy+1,hx+3,hy+3], fill=eye)
    d.point([hx+2,hy+2], fill=(0,0,0,255))
    # Tongue
    d.line([hx+6,hy+2,hx+9,hy+2], fill=(200,40,40,255), width=1)

    # Body segments — S-curve via manually placed ovals
    segs = [
        (14+sway,  10, 5, 4),
        (13-sway//2, 15, 5, 4),
        (14+sway//2, 20, 5, 4),
        (13,        25, 4, 3),
    ]
    for sx,sy,sw,sh in segs:
        d.ellipse([sx,sy,sx+sw,sy+sh], fill=sc)
        d.ellipse([sx+1,sy+1,sx+sw-1,sy+sh-1], fill=belly)

    if state == "damage" and frame_idx == 0:
        r,g,b,a = img.split(); white = Image.new("RGBA",img.size,(255,255,255,255)); white.putalpha(a); img = white
    return _final(img)


def draw_spider(state, frame_idx, colors):
    body  = colors.get("body",  (60,45,70,255))
    leg   = colors.get("leg",   (40,30,50,255))
    eye   = colors.get("eye",   (255,50,50,255))

    img = _new()
    d = ImageDraw.Draw(img)
    cx, cy = SIZE//2, SIZE//2 - 2

    # Sway/position shift
    if state == "idle":
        cy += [0,0,-1,0][frame_idx%4]
    elif state == "attack":
        cy += [0,-2,-4,-2][frame_idx%4]
    elif state == "damage":
        cy += [2,1,0][frame_idx%3]

    # Abdomen
    d.ellipse([cx-5,cy+3,cx+5,cy+10], fill=body)
    # Cephalothorax
    d.ellipse([cx-4,cy-3,cx+4,cy+5], fill=body)
    # Eyes (cluster of 4 small dots)
    for ex,ey in [(cx-2,cy-1),(cx,cy-1),(cx-2,cy+1),(cx,cy+1)]:
        d.point([ex,ey], fill=eye)

    # Legs — 4 pairs radiating
    leg_spread = [14,12,12,14][frame_idx%4] if state in ("idle","move") else 14
    for i,angle in enumerate([-50,-25,25,50]):
        for sign in (-1,1):
            rad = math.radians(angle * sign)
            lx = cx + int(leg_spread * math.cos(rad))
            ly = cy + int(leg_spread * 0.5 * math.sin(rad)) + 2
            d.line([cx,cy,lx,ly], fill=leg, width=1)

    if state == "die":
        # Curl legs in
        for i in range(8):
            rad = math.radians(i*45)
            fold = min(frame_idx,5)*1.5
            lx = cx + int((6+fold)*math.cos(rad))
            ly = cy + int((4+fold)*0.5*math.sin(rad)) + 2
            d.line([cx,cy,(cx+lx)//2,(cy+ly)//2], fill=leg, width=1)

    if state == "damage" and frame_idx == 0:
        r,g,b,a = img.split(); white = Image.new("RGBA",img.size,(255,255,255,255)); white.putalpha(a); img = white
    return _final(img)


def draw_horror(state, frame_idx, colors):
    """Blob hữu cơ với xúc tu — fungal_horror, void_horror, nightmare_fiend."""
    body  = colors.get("body",  (90,60,120,255))
    mid   = colors.get("mid",   (140,80,180,255))
    tent  = colors.get("tent",  (60,40,80,255))
    eye   = colors.get("eye",   (255,100,100,255))

    img = _new()
    d = ImageDraw.Draw(img)
    cx = SIZE//2

    pulse = [0,1,1,0][frame_idx%4]
    cy = 16

    # Body blob
    bw,bh = 10+pulse, 8+pulse
    d.ellipse([cx-bw,cy-bh,cx+bw,cy+bh], fill=body)
    d.ellipse([cx-bw//2,cy-bh//2,cx+bw//2,cy+bh//2], fill=mid)
    # Eye(s)
    d.ellipse([cx-2,cy-2,cx+2,cy+2], fill=eye)
    d.point([cx,cy], fill=(0,0,0,255))

    # Tentacles
    tent_configs = [(-6,-3,-12,-8),(6,-3,12,-8),(-4,4,-8,12),(4,4,8,12),(-2,-4,0,-12),(2,-4,0,-12)]
    wave = math.sin(frame_idx * math.pi / 2)
    for tx,ty,tx2,ty2 in tent_configs:
        ex = cx + tx2 + int(wave*2)
        ey = cy + ty2 + int(wave*2)
        d.line([cx+tx,cy+ty,ex,ey], fill=tent, width=2)
        d.ellipse([ex-1,ey-1,ex+1,ey+1], fill=tent)

    if state == "damage" and frame_idx == 0:
        r,g,b,a = img.split(); white = Image.new("RGBA",img.size,(255,255,255,255)); white.putalpha(a); img = white
    return _final(img)


def draw_toad(state, frame_idx, colors):
    body  = colors.get("body",  (60,140,60,255))
    belly = colors.get("belly", (150,200,100,255))
    eye   = colors.get("eye",   (255,255,50,255))

    img = _new()
    d = ImageDraw.Draw(img)
    cx = SIZE//2

    squat = [0,1,1,0][frame_idx%4]
    cy = 18

    # Body — wide squat
    d.ellipse([cx-9,cy-5+squat,cx+9,cy+5-squat], fill=body)
    d.ellipse([cx-7,cy,cx+7,cy+5-squat], fill=belly)
    # Eye bumps on top
    for ex in [cx-4, cx+4]:
        d.ellipse([ex-2,cy-7,ex+2,cy-3], fill=body)
        d.ellipse([ex-1,cy-6,ex+1,cy-4], fill=eye)
    # Mouth
    d.arc([cx-5,cy-2,cx+5,cy+2], start=0, end=180, fill=(30,80,30,255), width=1)
    # Back legs
    if state == "attack" or state == "move":
        jump = [0,3,6,3][frame_idx%4]
        d.line([cx-8,cy+5,cx-12,cy+5+jump], fill=body, width=3)
        d.line([cx+8,cy+5,cx+12,cy+5+jump], fill=body, width=3)
    else:
        d.line([cx-8,cy+5,cx-12,cy+8], fill=body, width=3)
        d.line([cx+8,cy+5,cx+12,cy+8], fill=body, width=3)

    if state == "damage" and frame_idx == 0:
        r,g,b,a = img.split(); white = Image.new("RGBA",img.size,(255,255,255,255)); white.putalpha(a); img = white
    return _final(img)


def draw_swarm(state, frame_idx, colors):
    """Đàn sinh vật nhỏ — dot cluster."""
    dot = colors.get("dot", (120,80,60,255))
    img = _new()
    d = ImageDraw.Draw(img)
    # Scatter of 15-20 small dots
    import random; random.seed(42 + frame_idx)
    count = 18
    for i in range(count):
        bx = random.randint(4,27); by = random.randint(16,26)
        sz = random.randint(1,2)
        alpha = 200 if state!="die" else max(40,200-frame_idx*35)
        r,g,b,_ = dot
        d.ellipse([bx,by,bx+sz,by+sz], fill=(r,g,b,alpha))
    return _final(img)


def draw_boss_drake(state, frame_idx, colors):
    """Magma Drake — thân rồng lửa lớn."""
    scale  = colors.get("scale",  (120,30,20,255))
    belly  = colors.get("belly",  (200,120,50,255))
    fire   = colors.get("fire",   (255,180,40,255))
    eye    = colors.get("eye",    (255,255,100,255))

    img = _new()
    d = ImageDraw.Draw(img)
    cx = SIZE//2

    if state == "idle":
        head_up = [0,-1,-1,0][frame_idx%4]
    elif state == "attack":
        head_up = [0,-2,-4,-2][frame_idx%4]
    elif state == "damage":
        head_up = [2,1,0][frame_idx%3]
    else:
        head_up = 0

    # Body — large oval
    by = 14
    d.ellipse([cx-10,by,cx+10,by+12], fill=scale)
    d.ellipse([cx-7,by+3,cx+7,by+10], fill=belly)
    # Neck
    d.polygon([(cx-3,by),(cx+3,by),(cx+2,by-5),(cx-2,by-5)], fill=scale)
    # Head
    hx,hy = cx-4, by-10+head_up
    d.ellipse([hx,hy,hx+9,hy+7], fill=scale)
    # Snout + teeth
    d.ellipse([hx+7,hy+3,hx+12,hy+7], fill=scale)
    d.line([hx+9,hy+5,hx+13,hy+5], fill=(200,200,200,255), width=1)
    # Eye
    d.ellipse([hx+2,hy+1,hx+5,hy+4], fill=eye)
    d.point([hx+3,hy+2], fill=(0,0,0,255))
    # Wing stubs
    d.polygon([(cx+8,by+2),(cx+15,by-4),(cx+16,by+5),(cx+10,by+7)], fill=scale)
    d.polygon([(cx-8,by+2),(cx-15,by-4),(cx-16,by+5),(cx-10,by+7)], fill=scale)
    # Tail
    d.line([cx+10,by+10, cx+16,by+14, cx+18,by+18], fill=scale, width=3)
    # Fire breath on attack
    if state == "attack" and frame_idx >= 1:
        fb = [2,4,6][min(frame_idx-1,2)]
        for fi in range(fb):
            d.ellipse([hx+12+fi*3,hy+3-fi,hx+15+fi*3,hy+6-fi], fill=fire)
    # Legs
    d.rectangle([cx-8,by+11,cx-4,by+18], fill=scale)
    d.rectangle([cx+4,by+11,cx+8,by+18], fill=scale)

    if state == "damage" and frame_idx == 0:
        r,g,b,a = img.split(); white = Image.new("RGBA",img.size,(255,255,255,255)); white.putalpha(a); img = white
    return _final(img)


# ================================================================
# CREATURE PALETTE VARIANTS
# ================================================================

CREATURE_PALETTES = {
    # Wolf / Hound
    "wolf_neutral":    {"fur":(155,120,80,255),  "shade":(100,70,40,255),  "eye":(255,220,60,255)},
    "wolf_fire":       {"fur":(160,60,30,255),   "shade":(100,30,10,255),  "eye":(255,180,40,255)},
    "wolf_dark":       {"fur":(60,40,80,255),    "shade":(35,20,55,255),   "eye":(200,130,255,255)},
    # Bat
    "bat_dark":        {"body":(60,30,90,255),   "wing":(40,15,65,255),    "eye":(255,60,60,255)},
    "bat_fire":        {"body":(100,30,20,255),  "wing":(70,15,10,255),    "eye":(255,160,40,255)},
    # Slime
    "slime_water":     {"body":(46,190,80,255),  "mid":(100,230,120,255)},
    "slime_fire":      {"body":(200,80,30,255),  "mid":(240,140,60,255)},
    "slime_dark":      {"body":(80,40,120,255),  "mid":(130,60,180,255)},
    # Wisp / Wraith
    "wisp_dark":       {"core":(200,120,255,255),"glow":(130,60,200,180), "trail":(80,20,140,120)},
    "wisp_fire":       {"core":(255,200,80,255), "glow":(220,130,40,180), "trail":(180,80,20,120)},
    "wisp_water":      {"core":(80,200,255,255), "glow":(40,130,200,180), "trail":(20,80,160,120)},
    "wisp_neutral":    {"core":(220,220,220,255),"glow":(160,160,160,180),"trail":(100,100,100,120)},
    # Golem
    "golem_earth":     {"stone":(110,95,80,255), "crack":(70,55,45,255),  "eye":(255,200,50,255)},
    "golem_dark":      {"stone":(60,40,80,255),  "crack":(35,20,55,255),  "eye":(200,130,255,255)},
    "golem_water":     {"stone":(55,90,120,255), "crack":(30,55,80,255),  "eye":(100,220,255,255)},
    # Serpent
    "serpent_water":   {"scale":(42,120,60,255), "belly":(160,200,130,255),"eye":(255,220,50,255)},
    "serpent_fire":    {"scale":(150,50,20,255), "belly":(220,130,60,255), "eye":(255,200,40,255)},
    "serpent_dark":    {"scale":(60,30,80,255),  "belly":(140,80,180,255), "eye":(200,130,255,255)},
    # Spider
    "spider_dark":     {"body":(60,45,70,255),   "leg":(40,30,50,255),    "eye":(255,50,50,255)},
    "spider_neutral":  {"body":(90,70,50,255),   "leg":(60,45,30,255),    "eye":(220,180,50,255)},
    # Horror
    "horror_dark":     {"body":(90,60,120,255),  "mid":(140,80,180,255),  "tent":(60,40,80,255),  "eye":(255,100,100,255)},
    "horror_fire":     {"body":(120,50,20,255),  "mid":(200,100,40,255),  "tent":(80,25,10,255),  "eye":(255,200,50,255)},
    "horror_neutral":  {"body":(80,100,60,255),  "mid":(140,180,80,255),  "tent":(50,70,35,255),  "eye":(180,220,100,255)},
    # Toad
    "toad_water":      {"body":(60,140,60,255),  "belly":(150,200,100,255),"eye":(255,255,50,255)},
    "toad_neutral":    {"body":(80,120,40,255),  "belly":(160,200,80,255), "eye":(255,220,50,255)},
    # Swarm
    "swarm_neutral":   {"dot":(120,80,60,255)},
    # Drake boss
    "drake_fire":      {"scale":(120,30,20,255), "belly":(200,120,50,255),"fire":(255,180,40,255),"eye":(255,255,100,255)},
}

# ================================================================
# HUMANOID KITS REGISTRY
# ================================================================

HUMANOID_KITS = {
    "goblin":         GOBLIN_KIT,
    "goblin_dark":    GOBLIN_DARK_KIT,
    "goblin_wind":    GOBLIN_WIND_KIT,
    "goblin_king":    GOBLIN_KING_KIT,
    "skeleton":       SKELETON_KIT,
    "mummy":          MUMMY_KIT,
    "zombie":         ZOMBIE_KIT,
    "zombie_fire":    ZOMBIE_FIRE_KIT,
    "brute":          BRUTE_KIT,
    "brute_dark":     BRUTE_DARK_KIT,
    "brute_fire":     BRUTE_FIRE_KIT,
    "brute_water":    BRUTE_WATER_KIT,
    "brute_earth":    BRUTE_EARTH_KIT,
    "caster_dark":    CASTER_DARK_KIT,
    "caster_fire":    CASTER_FIRE_KIT,
    "caster_water":   CASTER_WATER_KIT,
    "caster_undead":  CASTER_UNDEAD_KIT,
    "caster_light":   CASTER_LIGHT_KIT,
    "knight_dark":    KNIGHT_DARK_KIT,
    "knight_water":   KNIGHT_WATER_KIT,
    "knight_earth":   KNIGHT_EARTH_KIT,
    "knight_dark2":   KNIGHT_DARK2_KIT,
    "boss_lich":      BOSS_LICH_KIT,
    "boss_void":      BOSS_VOID_KIT,
}

# Creature archetypes: (draw_fn, palette_key)
CREATURE_KITS = {
    "wolf_neutral":   (draw_wolf,   "wolf_neutral"),
    "wolf_fire":      (draw_wolf,   "wolf_fire"),
    "wolf_dark":      (draw_wolf,   "wolf_dark"),
    "bat_dark":       (draw_bat,    "bat_dark"),
    "bat_fire":       (draw_bat,    "bat_fire"),
    "slime_water":    (draw_slime,  "slime_water"),
    "slime_fire":     (draw_slime,  "slime_fire"),
    "slime_dark":     (draw_slime,  "slime_dark"),
    "wisp_dark":      (draw_wisp,   "wisp_dark"),
    "wisp_fire":      (draw_wisp,   "wisp_fire"),
    "wisp_water":     (draw_wisp,   "wisp_water"),
    "wisp_neutral":   (draw_wisp,   "wisp_neutral"),
    "golem_earth":    (draw_golem,  "golem_earth"),
    "golem_dark":     (draw_golem,  "golem_dark"),
    "golem_water":    (draw_golem,  "golem_water"),
    "serpent_water":  (draw_serpent,"serpent_water"),
    "serpent_fire":   (draw_serpent,"serpent_fire"),
    "serpent_dark":   (draw_serpent,"serpent_dark"),
    "spider_dark":    (draw_spider, "spider_dark"),
    "spider_neutral": (draw_spider, "spider_neutral"),
    "horror_dark":    (draw_horror, "horror_dark"),
    "horror_fire":    (draw_horror, "horror_fire"),
    "horror_neutral": (draw_horror, "horror_neutral"),
    "toad_water":     (draw_toad,   "toad_water"),
    "toad_neutral":   (draw_toad,   "toad_neutral"),
    "swarm_neutral":  (draw_swarm,  "swarm_neutral"),
    "boss_drake":     (draw_boss_drake,"drake_fire"),
}

ALL_KITS = set(HUMANOID_KITS.keys()) | set(CREATURE_KITS.keys())

# ================================================================
# ENEMY → KIT MAPPING (66 entries)
# ================================================================

ENEMY_KIT = {
    # Chapter 1
    "enemy_goblin":           "goblin",
    "enemy_goblin_archer":    "goblin_wind",
    "enemy_goblin_shaman":    "goblin_dark",
    "enemy_slime":            "slime_water",
    "enemy_wolf":             "wolf_neutral",
    "enemy_bat":              "bat_dark",
    "enemy_bomb_slime":       "slime_fire",
    "enemy_ogre_brute":       "brute_earth",
    "enemy_shield_bearer":    "knight_earth",
    "enemy_skeleton":         "skeleton",
    "boss_alpha_wolf":        "wolf_neutral",
    # Chapter 2
    "boss_goblin_king":       "goblin_king",
    "enemy_bog_zombie":       "zombie",
    "enemy_swamp_troll":      "brute_water",
    "enemy_will_o_wisp":      "wisp_neutral",
    "enemy_giant_leech":      "serpent_water",
    "enemy_venomous_spider":  "spider_neutral",
    "enemy_bog_witch":        "caster_dark",
    "enemy_mud_crawler":      "golem_water",
    "enemy_mire_serpent":     "serpent_water",
    "enemy_goblin_archer":    "goblin_wind",
    "enemy_poison_toad":      "toad_water",
    "enemy_swamp_rat_swarm":  "swarm_neutral",
    # Chapter 3
    "boss_lich":              "boss_lich",
    "enemy_skeleton":         "skeleton",
    "enemy_bone_swordsman":   "skeleton",
    "enemy_bone_archer":      "skeleton",
    "enemy_crypt_wraith":     "wisp_dark",
    "enemy_crypt_spider":     "spider_dark",
    "enemy_grave_golem":      "golem_earth",
    "enemy_mummy_guardian":   "mummy",
    "enemy_death_priest":     "caster_undead",
    "enemy_cursed_gargoyle":  "brute_dark",
    "enemy_necrotic_hound":   "wolf_dark",
    "enemy_soul_reaper":      "caster_undead",
    "enemy_phantom_knight":   "knight_dark",
    # Chapter 4
    "boss_magma_drake":       "boss_drake",
    "enemy_fire_imp":         "bat_fire",
    "enemy_magma_hound":      "wolf_fire",
    "enemy_lava_slime":       "slime_fire",
    "enemy_flame_wisp":       "wisp_fire",
    "enemy_ember_knight":     "brute_fire",
    "enemy_pyroclast_mage":   "caster_fire",
    "enemy_molten_brute":     "brute_fire",
    "enemy_volcanic_crab":    "golem_earth",
    "enemy_flame_serpent":    "serpent_fire",
    "enemy_obsidian_golem":   "golem_earth",
    "enemy_cinder_bat":       "bat_fire",
    "enemy_charred_zombie":   "zombie_fire",
    # Chapter 5 (Dark/Void)
    "boss_void_king":         "boss_void",
    "enemy_void_cultist":     "caster_dark",
    "enemy_shadow_stalker":   "wolf_dark",
    "enemy_abyssal_wraith":   "wisp_dark",
    "enemy_shadow_knight":    "knight_dark2",
    "enemy_void_reaver":      "brute_dark",
    "enemy_chaos_spawn":      "horror_dark",
    "enemy_dark_sentinel":    "knight_dark2",
    "enemy_nether_hound":     "wolf_dark",
    "enemy_corrupted_golem":  "golem_dark",
    "enemy_void_serpent":     "serpent_dark",
    "enemy_nightmare_fiend":  "horror_dark",
    "enemy_ash_wraith":       "wisp_dark",
    "enemy_fungal_horror":    "horror_neutral",
    "enemy_void_horror":      "horror_dark",
    "enemy_drowned_knight":   "knight_water",
    "enemy_star_priest":      "caster_light",
    "enemy_abyss_stalker":    "wolf_dark",
    "boss_trial_champion":    "boss_void",
}

# ================================================================
# FRAME COUNTS PER STATE
# ================================================================

STATE_FRAMES = {
    "idle":   4,
    "attack": 4,
    "move":   4,
    "damage": 3,
    "die":    6,
}

CREATURE_STATE_FRAMES = STATE_FRAMES  # same


# ================================================================
# GENERATION
# ================================================================

def gen_humanoid_frames(kit, state, n_frames):
    poses = POSE_SETS[state][:n_frames]
    frames = []
    for i, pose in enumerate(poses):
        kwargs = dict(pose)
        if state == "attack" and i > 0:
            kwargs["prev_arm_angle_deg"] = poses[i-1]["arm_angle_deg"]
        frames.append(build_frame(kit=kit, **kwargs))
    return frames


def gen_creature_frames(draw_fn, palette_key, state, n_frames):
    palette = CREATURE_PALETTES.get(palette_key, {})
    frames = []
    for i in range(n_frames):
        frames.append(draw_fn(state, i, palette))
    return frames


def generate(kit_name, state, enemy_id, out_dir, n_frames=0):
    out_dir = Path(out_dir)
    out_dir.mkdir(parents=True, exist_ok=True)

    if kit_name in HUMANOID_KITS:
        kit = HUMANOID_KITS[kit_name]
        n = n_frames or STATE_FRAMES[state]
        frames = gen_humanoid_frames(kit, state, n)
    elif kit_name in CREATURE_KITS:
        draw_fn, palette_key = CREATURE_KITS[kit_name]
        n = n_frames or STATE_FRAMES[state]
        frames = gen_creature_frames(draw_fn, palette_key, state, n)
    else:
        raise ValueError(f"Kit không tồn tại: {kit_name}")

    for i, frame in enumerate(frames):
        path = out_dir / f"{enemy_id}_rig_{state}_{i:02d}.png"
        frame.save(path)
        print(f"  ✓ {path}")


# ================================================================
# CLI
# ================================================================

def main():
    ap = argparse.ArgumentParser(description="Sinh sprite enemy/boss 32×32")
    ap.add_argument("--kit",    help="Tên kit (humanoid hoặc creature)")
    ap.add_argument("--state",  choices=list(STATE_FRAMES.keys()))
    ap.add_argument("--frames", type=int, default=0, help="Override số frame (0=dùng mặc định theo state)")
    ap.add_argument("--enemy",  default="enemy_test", help="defId, dùng làm tiền tố file")
    ap.add_argument("--out",    help="Thư mục xuất frame")
    ap.add_argument("--list",   action="store_true", help="Liệt kê ENEMY_KIT mapping")
    ap.add_argument("--all-states", action="store_true", help="Sinh tất cả 5 state")
    args = ap.parse_args()

    if args.list:
        print(f"{'Enemy ID':<35} Kit")
        print("-" * 55)
        for eid, kit in sorted(ENEMY_KIT.items()):
            print(f"{eid:<35} {kit}")
        print(f"\nTổng: {len(ENEMY_KIT)} enemy, {len(ALL_KITS)} kit")
        return

    if not args.kit or not args.out:
        ap.print_help()
        return

    if args.kit not in ALL_KITS:
        print(f"Kit không biết: {args.kit}. Xem --list để biết các kit.")
        sys.exit(1)

    states = list(STATE_FRAMES.keys()) if args.all_states else [args.state]
    for state in states:
        out = Path(args.out) / state if args.all_states else Path(args.out)
        generate(args.kit, state, args.enemy, out, n_frames=args.frames)


if __name__ == "__main__":
    main()

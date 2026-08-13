#!/usr/bin/env python3
"""
audio_gen.py — Sinh SFX và BGM chiptune cho TurnBase.

Vì sao tự tổng hợp thay vì dùng model AI:
  - Khớp thẩm mỹ pixel-art 16-bit tuyệt đối
  - Deterministic: cùng seed → cùng file, commit được vào git
  - Không vướng bản quyền
  - File nhỏ (SFX < 20KB), đúng ngân sách build 150MB
  - Sửa được từng tham số (cao độ, thời lượng, duty cycle) thay vì roll lại

Dùng:
    python3 Tools/audio_gen.py --all
    python3 Tools/audio_gen.py --sfx
    python3 Tools/audio_gen.py --bgm
    python3 Tools/audio_gen.py --one hit_fire

Xuất ra Assets/_Project/Audio/{SFX,BGM}/ theo quy ước tên ở plan.md §12.
"""

import argparse
import math
import struct
import wave
from pathlib import Path

import numpy as np

SR = 44100
ROOT = Path(__file__).resolve().parent.parent
SFX_DIR = ROOT / "Assets/_Project/Resources/Audio/SFX"
BGM_DIR = ROOT / "Assets/_Project/Resources/Audio/BGM"


# ============================================================ dao động cơ bản

def _t(dur):
    return np.linspace(0, dur, int(SR * dur), endpoint=False)


def square(freq, dur, duty=0.5):
    """Sóng vuông — kênh chủ đạo của NES/GameBoy. duty đổi màu âm rõ rệt."""
    t = _t(dur)
    phase = np.mod(freq * t, 1.0) if np.isscalar(freq) else np.mod(np.cumsum(freq) / SR, 1.0)
    return np.where(phase < duty, 1.0, -1.0)


def triangle(freq, dur):
    """Sóng tam giác — dùng cho bass, mềm hơn vuông."""
    t = _t(dur)
    phase = np.mod(freq * t, 1.0) if np.isscalar(freq) else np.mod(np.cumsum(freq) / SR, 1.0)
    return 2.0 * np.abs(2.0 * phase - 1.0) - 1.0


def saw(freq, dur):
    t = _t(dur)
    phase = np.mod(freq * t, 1.0) if np.isscalar(freq) else np.mod(np.cumsum(freq) / SR, 1.0)
    return 2.0 * phase - 1.0


def noise(dur, seed=0):
    """Nhiễu trắng — trống, tiếng vỡ, tiếng gió."""
    rng = np.random.default_rng(seed)
    return rng.uniform(-1.0, 1.0, int(SR * dur))


def sweep(f0, f1, dur, kind="square", duty=0.5, curve="lin"):
    """Quét cao độ — xương sống của mọi SFX retro."""
    n = int(SR * dur)
    if curve == "exp":
        freqs = f0 * (f1 / max(f0, 1e-6)) ** np.linspace(0, 1, n)
    else:
        freqs = np.linspace(f0, f1, n)
    phase = np.mod(np.cumsum(freqs) / SR, 1.0)
    if kind == "square":
        return np.where(phase < duty, 1.0, -1.0)
    if kind == "triangle":
        return 2.0 * np.abs(2.0 * phase - 1.0) - 1.0
    return 2.0 * phase - 1.0


# ============================================================ bao hình & hiệu ứng

def adsr(sig, a=0.005, d=0.05, s=0.6, r=0.1):
    """Bao hình ADSR. Attack ngắn = âm "đanh", hợp SFX đánh."""
    n = len(sig)
    na, nd, nr = int(SR * a), int(SR * d), int(SR * r)
    ns = max(0, n - na - nd - nr)
    env = np.concatenate([
        np.linspace(0, 1, na, endpoint=False) if na else np.array([]),
        np.linspace(1, s, nd, endpoint=False) if nd else np.array([]),
        np.full(ns, s),
        np.linspace(s, 0, nr) if nr else np.array([]),
    ])
    env = np.resize(env, n)
    return sig * env


def decay(sig, power=3.0):
    """Bao hình mũ giảm dần — dùng cho trống và tiếng va chạm."""
    return sig * (np.linspace(1, 0, len(sig)) ** power)


def bitcrush(sig, bits=6):
    """Giảm độ phân giải bit — chất "8-bit" nghe được ngay."""
    levels = 2 ** bits
    return np.round(sig * levels) / levels


def downsample(sig, factor=3):
    """Giữ mẫu theo bậc thang — thêm chất lo-fi retro."""
    if factor <= 1:
        return sig
    out = sig.copy()
    for i in range(0, len(sig), factor):
        out[i:i + factor] = sig[i]
    return out


def vibrato(sig, rate=6.0, depth=0.015):
    t = np.linspace(0, len(sig) / SR, len(sig), endpoint=False)
    mod = 1.0 + depth * np.sin(2 * np.pi * rate * t)
    idx = np.clip((np.arange(len(sig)) * mod).astype(int), 0, len(sig) - 1)
    return sig[idx]


def mix(*sigs):
    n = max(len(s) for s in sigs)
    out = np.zeros(n)
    for s in sigs:
        out[:len(s)] += s
    return out


def cat(*sigs):
    return np.concatenate(sigs)


def silence(dur):
    return np.zeros(int(SR * dur))


def normalize(sig, peak=0.85):
    m = np.max(np.abs(sig))
    return sig * (peak / m) if m > 1e-9 else sig


def save_wav(sig, path: Path, mono=True):
    path.parent.mkdir(parents=True, exist_ok=True)
    data = (normalize(sig) * 32767).astype(np.int16)
    with wave.open(str(path), "w") as w:
        w.setnchannels(1 if mono else 2)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(data.tobytes())
    kb = path.stat().st_size / 1024
    print(f"  ✓ {path.relative_to(ROOT)} ({kb:.0f}KB, {len(sig)/SR:.2f}s)")


# ============================================================ SFX chiến đấu

def sfx_hit_physical():
    """Đòn vật lý: nhiễu đanh + sweep xuống."""
    imp = decay(noise(0.06, seed=1), 4)
    body = decay(sweep(320, 90, 0.09, "square", 0.25, "exp"), 2.5)
    return mix(imp * 0.7, body * 0.8)


def sfx_hit_fire():
    crack = decay(noise(0.10, seed=2), 2.2)
    roar = decay(sweep(180, 60, 0.16, "saw"), 2.0)
    return mix(crack * 0.55, roar * 0.7)


def sfx_hit_water():
    drop = decay(sweep(900, 220, 0.14, "triangle", curve="exp"), 2.5)
    splash = decay(noise(0.08, seed=3), 3.5)
    return mix(drop * 0.8, splash * 0.35)


def sfx_hit_earth():
    thud = decay(sweep(150, 45, 0.18, "triangle"), 2.0)
    rubble = decay(noise(0.12, seed=4), 3.0)
    return mix(thud * 0.9, rubble * 0.45)


def sfx_hit_wind():
    gust = decay(noise(0.20, seed=5), 1.6)
    whistle = decay(sweep(700, 1400, 0.18, "square", 0.15), 2.0)
    return mix(gust * 0.5, whistle * 0.4)


def sfx_hit_light():
    shine = decay(sweep(1200, 2400, 0.16, "square", 0.12, "exp"), 2.2)
    bell = decay(triangle(1760, 0.20), 2.5)
    return mix(shine * 0.6, bell * 0.5)


def sfx_hit_dark():
    void = decay(sweep(260, 70, 0.22, "saw", curve="exp"), 1.8)
    hiss = decay(noise(0.16, seed=6), 2.5)
    return mix(void * 0.75, hiss * 0.35)


def sfx_crit():
    """Chí mạng: hai nhát nhanh + âm cao chói."""
    a = decay(sweep(500, 120, 0.05, "square", 0.25, "exp"), 3)
    b = decay(sweep(800, 180, 0.08, "square", 0.25, "exp"), 2.5)
    sparkle = decay(square(2093, 0.10, 0.12), 3)
    return cat(a, mix(b, sparkle * 0.4))


def sfx_perfect():
    """PERFECT — arpeggio đi lên rực rỡ, phải nghe là biết ngay mình làm đúng."""
    notes = [1046.5, 1318.5, 1568.0, 2093.0]  # C6 E6 G6 C7
    parts = [decay(square(f, 0.055, 0.25), 1.5) for f in notes]
    tail = decay(mix(square(2093, 0.22, 0.25), square(2637, 0.22, 0.12) * 0.5), 2.2)
    return cat(*parts, tail)


def sfx_good():
    notes = [783.99, 1046.5]  # G5 C6
    return cat(*[decay(square(f, 0.06, 0.3), 2) for f in notes])


def sfx_miss():
    """Trượt: sweep xuống ngắn, nghe "hụt"."""
    return decay(sweep(400, 140, 0.12, "square", 0.5, "exp"), 3)


def sfx_whoosh():
    return decay(bitcrush(noise(0.18, seed=7), 5), 2.0)


def sfx_break():
    """BREAK — kính vỡ. Sự kiện quan trọng nhất trong trận, phải nổi bật."""
    shatter = decay(noise(0.35, seed=8), 1.4)
    crack = decay(sweep(2600, 300, 0.28, "square", 0.15, "exp"), 1.8)
    boom = decay(triangle(70, 0.30), 2.0)
    ring = decay(mix(square(1568, 0.30, 0.25), square(2093, 0.30, 0.18) * 0.6), 2.5)
    return mix(shatter * 0.65, crack * 0.55, boom * 0.8, ring * 0.35)


def sfx_resist():
    return decay(mix(square(392, 0.14, 0.5), square(523.25, 0.14, 0.5) * 0.6), 2.5)


def sfx_heal():
    notes = [523.25, 659.25, 783.99, 1046.5]
    return cat(*[decay(triangle(f, 0.09), 1.6) for f in notes])


def sfx_shield():
    base = decay(sweep(300, 700, 0.20, "triangle", curve="exp"), 1.5)
    shimmer = decay(square(1568, 0.18, 0.12), 2.5)
    return mix(base * 0.8, shimmer * 0.35)


def sfx_buff():
    return cat(*[decay(square(f, 0.07, 0.35), 1.8) for f in [523.25, 659.25, 830.61]])


def sfx_debuff():
    return cat(*[decay(square(f, 0.09, 0.4), 1.8) for f in [523.25, 415.30, 311.13]])


def sfx_death():
    body = decay(sweep(440, 40, 0.45, "saw", curve="exp"), 1.5)
    crunch = decay(noise(0.20, seed=9), 3.0)
    return mix(body * 0.8, crunch * 0.3)


def sfx_summon():
    rise = decay(sweep(120, 900, 0.35, "square", 0.2, "exp"), 1.2)
    pulse = decay(triangle(220, 0.30), 1.8)
    return mix(rise * 0.7, pulse * 0.6)


def sfx_ultimate():
    """Ultimate: nạp năng lượng rồi bùng nổ."""
    charge = adsr(vibrato(sweep(200, 1200, 0.55, "saw", curve="exp")), 0.3, 0.1, 0.8, 0.05)
    boom = decay(mix(noise(0.40, seed=10) * 0.6, triangle(60, 0.40)), 1.5)
    chord = decay(mix(square(523.25, 0.45, 0.3),
                      square(659.25, 0.45, 0.3) * 0.7,
                      square(783.99, 0.45, 0.3) * 0.6), 1.8)
    return cat(charge * 0.6, mix(boom, chord * 0.7))


def sfx_levelup():
    notes = [523.25, 659.25, 783.99, 1046.5, 1318.5]
    parts = [decay(square(f, 0.08, 0.3), 1.4) for f in notes]
    fanfare = decay(mix(square(1046.5, 0.35, 0.25),
                        square(1318.5, 0.35, 0.25) * 0.7,
                        square(1568.0, 0.35, 0.25) * 0.6), 1.8)
    return cat(*parts, fanfare)


def sfx_legendary():
    """Nhận Legendary — phải cảm thấy như trúng số."""
    riser = adsr(sweep(300, 3000, 0.7, "square", 0.15, "exp"), 0.4, 0.1, 0.9, 0.05)
    sparkle = cat(*[decay(square(f, 0.05, 0.12), 2)
                    for f in [2093, 2637, 3136, 2637, 3136, 4186]])
    chord = decay(mix(square(1046.5, 0.6, 0.25),
                      square(1318.5, 0.6, 0.25) * 0.8,
                      square(1568.0, 0.6, 0.25) * 0.7,
                      triangle(261.63, 0.6) * 0.9), 1.4)
    return cat(riser * 0.5, mix(sparkle * 0.5, chord))


# ============================================================ SFX giao diện

def sfx_ui_tick():
    return decay(square(1200, 0.035, 0.25), 4)


def sfx_ui_confirm():
    return cat(decay(square(880, 0.05, 0.3), 3), decay(square(1318.5, 0.07, 0.3), 3))


def sfx_ui_cancel():
    return cat(decay(square(660, 0.05, 0.4), 3), decay(square(440, 0.07, 0.4), 3))


def sfx_ui_error():
    return decay(mix(square(220, 0.16, 0.5), square(233, 0.16, 0.5)), 2.5)


def sfx_ui_tab():
    return decay(square(1568, 0.04, 0.2), 4)


def sfx_ui_coin():
    return cat(decay(square(1318.5, 0.04, 0.2), 3), decay(square(1760, 0.10, 0.2), 2.5))


def sfx_ui_reward():
    return cat(*[decay(square(f, 0.06, 0.25), 2) for f in [784, 988, 1175, 1568]])


def sfx_ui_page():
    return decay(bitcrush(noise(0.07, seed=11), 4), 3.5)


# ============================================================ BGM

# Bảng tần số: C2..B6
NOTE = {}
for _o in range(1, 8):
    for _i, _n in enumerate(["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"]):
        NOTE[f"{_n}{_o}"] = 440.0 * (2 ** ((_o - 4) + (_i - 9) / 12))
NOTE["-"] = 0.0


def seq(pattern, bpm, wave_fn, duty=0.5, gate=0.85, vol=1.0):
    """Chuỗi nốt đơn giản. '-' = nghỉ, '.' = giữ nốt trước."""
    beat = 60.0 / bpm
    out = []
    prev = "-"
    for tok in pattern:
        name = prev if tok == "." else tok
        prev = name
        f = NOTE.get(name, 0.0)
        n_on = int(SR * beat * gate)
        n_off = int(SR * beat) - n_on
        if f <= 0:
            out.append(np.zeros(int(SR * beat)))
            continue
        d = beat * gate
        s = wave_fn(f, d, duty) if wave_fn is square else wave_fn(f, d)
        s = adsr(s, 0.004, 0.03, 0.75, 0.03) * vol
        out.append(np.concatenate([s, np.zeros(n_off)]))
    return np.concatenate(out)


def drums(pattern, bpm, vol=0.5):
    """'k'=kick 's'=snare 'h'=hihat '-'=nghỉ"""
    beat = 60.0 / bpm
    out = []
    for tok in pattern:
        n = int(SR * beat)
        if tok == "k":
            s = decay(sweep(160, 45, min(beat, 0.14), "triangle", curve="exp"), 2.5)
        elif tok == "s":
            s = mix(decay(noise(min(beat, 0.12), seed=21), 3),
                    decay(sweep(320, 180, min(beat, 0.10), "triangle"), 3) * 0.5)
        elif tok == "h":
            s = decay(bitcrush(noise(min(beat, 0.05), seed=22), 4), 5) * 0.55
        else:
            s = np.zeros(n)
        buf = np.zeros(n)
        buf[:min(len(s), n)] = s[:n]
        out.append(buf * vol)
    return np.concatenate(out)


def bgm_menu():
    """Menu: nhẹ, lặp được, không gây mệt khi nghe lâu."""
    bpm = 96
    lead = ["C5", "-", "E5", "-", "G5", "-", "E5", "-",
            "F5", "-", "A5", "-", "G5", ".", "-", "-"] * 2
    bass = ["C3", ".", "-", ".", "G2", ".", "-", ".",
            "F2", ".", "-", ".", "G2", ".", "-", "."] * 2
    hats = list("h-h-h-h-h-h-h-h-") * 2
    return normalize(mix(
        seq(lead, bpm, square, 0.25, vol=0.42),
        seq(bass, bpm, triangle, vol=0.55),
        drums(hats, bpm, 0.22)), 0.7)


def bgm_battle():
    """Trận thường: nhịp thúc, giữ được sự tập trung."""
    bpm = 148
    lead = ["A4", "C5", "E5", "C5", "A4", "-", "G4", "-",
            "F4", "A4", "C5", "A4", "G4", "-", "E4", "-"] * 2
    harm = ["E4", "-", "A4", "-", "E4", "-", "D4", "-",
            "C4", "-", "F4", "-", "D4", "-", "B3", "-"] * 2
    bass = ["A2", ".", "A2", ".", "F2", ".", "F2", ".",
            "G2", ".", "G2", ".", "E2", ".", "E2", "."] * 2
    dr = list("k-h-s-h-k-h-s-h-") * 2
    return normalize(mix(
        seq(lead, bpm, square, 0.25, vol=0.40),
        seq(harm, bpm, square, 0.125, vol=0.22),
        seq(bass, bpm, triangle, vol=0.55),
        drums(dr, bpm, 0.32)), 0.75)


def bgm_boss():
    """Boss: tối, hạ tông, nhịp dồn."""
    bpm = 160
    lead = ["D4", "F4", "A4", "F4", "D4", "-", "C4", "-",
            "A#3", "D4", "F4", "D4", "C4", "-", "A3", "-"] * 2
    harm = ["A3", "-", "D4", "-", "A3", "-", "G3", "-",
            "F3", "-", "A#3", "-", "G3", "-", "E3", "-"] * 2
    bass = ["D2", "D2", "D2", "-", "A#1", "A#1", "A#1", "-",
            "C2", "C2", "C2", "-", "A1", "A1", "A1", "-"] * 2
    dr = list("k-khs-khk-khs-hh") * 2
    return normalize(mix(
        seq(lead, bpm, square, 0.5, vol=0.38),
        seq(harm, bpm, saw, vol=0.20),
        seq(bass, bpm, triangle, vol=0.60),
        drums(dr, bpm, 0.36)), 0.78)


def bgm_victory():
    bpm = 132
    lead = ["C5", "E5", "G5", "C6", "-", "G5", "C6", "-"]
    bass = ["C3", ".", "G2", ".", "C3", ".", "C3", "."]
    return normalize(mix(
        seq(lead, bpm, square, 0.25, vol=0.5),
        seq(bass, bpm, triangle, vol=0.55),
        drums(list("k-s-k-s-"), bpm, 0.3)), 0.8)


def bgm_defeat():
    bpm = 76
    lead = ["A4", "-", "G4", "-", "F4", "-", "E4", "."]
    bass = ["A2", ".", "F2", ".", "D2", ".", "E2", "."]
    return normalize(mix(
        seq(lead, bpm, triangle, vol=0.5),
        seq(bass, bpm, triangle, vol=0.5)), 0.65)


def bgm_meadow():
    bpm = 112
    lead = ["G4", "B4", "D5", "B4", "G4", "-", "A4", "-",
            "C5", "E5", "G5", "E5", "D5", "-", "B4", "-"] * 2
    bass = ["G2", ".", "-", ".", "D2", ".", "-", ".",
            "C2", ".", "-", ".", "D2", ".", "-", "."] * 2
    return normalize(mix(
        seq(lead, bpm, square, 0.25, vol=0.40),
        seq(bass, bpm, triangle, vol=0.50),
        drums(list("h-h-hh h-h-h-hh-".replace(" ", "-")), bpm, 0.20)), 0.72)


def bgm_swamp():
    bpm = 88
    lead = ["E4", "-", "G4", "-", "A4", "-", "G4", "-",
            "D4", "-", "F4", "-", "E4", ".", "-", "-"] * 2
    bass = ["E2", ".", "-", ".", "C2", ".", "-", ".",
            "D2", ".", "-", ".", "E2", ".", "-", "."] * 2
    return normalize(mix(
        seq(lead, bpm, saw, vol=0.32),
        seq(bass, bpm, triangle, vol=0.55),
        drums(list("k---s---k---s---"), bpm, 0.24)), 0.70)


def bgm_crypt():
    bpm = 92
    lead = ["A4", "-", "A#4", "-", "A4", "-", "F4", "-",
            "G4", "-", "G#4", "-", "G4", ".", "-", "-"] * 2
    bass = ["A2", ".", ".", ".", "F2", ".", ".", ".",
            "G2", ".", ".", ".", "E2", ".", ".", "."] * 2
    return normalize(mix(
        seq(lead, bpm, square, 0.125, vol=0.34),
        seq(bass, bpm, triangle, vol=0.55),
        drums(list("k-------s-------"), bpm, 0.26)), 0.70)


def bgm_volcano():
    bpm = 138
    lead = ["D5", "C5", "A#4", "C5", "D5", "-", "F5", "-",
            "D5", "C5", "A4", "C5", "A#4", "-", "G4", "-"] * 2
    bass = ["D2", "D2", "A#1", "A#1", "C2", "C2", "G1", "G1"] * 4
    return normalize(mix(
        seq(lead, bpm, square, 0.5, vol=0.38),
        seq(bass, bpm, triangle, vol=0.58),
        drums(list("k-h-s-h-k-hks-h-"), bpm, 0.34)), 0.76)


def bgm_void():
    bpm = 104
    lead = ["C5", "-", "D#5", "-", "F#5", "-", "D#5", "-",
            "A4", "-", "C5", "-", "D#5", ".", "-", "-"] * 2
    harm = ["G4", "-", "-", "-", "A#4", "-", "-", "-",
            "F4", "-", "-", "-", "G4", "-", "-", "-"] * 2
    bass = ["C2", ".", ".", ".", "F#1", ".", ".", ".",
            "A1", ".", ".", ".", "C2", ".", ".", "."] * 2
    return normalize(mix(
        seq(lead, bpm, square, 0.125, vol=0.34),
        seq(harm, bpm, saw, vol=0.18),
        seq(bass, bpm, triangle, vol=0.55),
        drums(list("k---k---s-------"), bpm, 0.28)), 0.74)


# ============================================================ danh mục

SFX = {
    # chiến đấu
    "battle/sfx_battle_hit_physical": sfx_hit_physical,
    "battle/sfx_battle_hit_fire": sfx_hit_fire,
    "battle/sfx_battle_hit_water": sfx_hit_water,
    "battle/sfx_battle_hit_earth": sfx_hit_earth,
    "battle/sfx_battle_hit_wind": sfx_hit_wind,
    "battle/sfx_battle_hit_light": sfx_hit_light,
    "battle/sfx_battle_hit_dark": sfx_hit_dark,
    "battle/sfx_battle_crit": sfx_crit,
    "battle/sfx_battle_perfect": sfx_perfect,
    "battle/sfx_battle_good": sfx_good,
    "battle/sfx_battle_miss": sfx_miss,
    "battle/sfx_battle_whoosh": sfx_whoosh,
    "battle/sfx_battle_break": sfx_break,
    "battle/sfx_battle_resist": sfx_resist,
    "battle/sfx_battle_heal": sfx_heal,
    "battle/sfx_battle_shield": sfx_shield,
    "battle/sfx_battle_buff": sfx_buff,
    "battle/sfx_battle_debuff": sfx_debuff,
    "battle/sfx_battle_death": sfx_death,
    "battle/sfx_battle_summon": sfx_summon,
    "battle/sfx_battle_ultimate": sfx_ultimate,
    # giao diện
    "ui/sfx_ui_tick": sfx_ui_tick,
    "ui/sfx_ui_confirm": sfx_ui_confirm,
    "ui/sfx_ui_cancel": sfx_ui_cancel,
    "ui/sfx_ui_error": sfx_ui_error,
    "ui/sfx_ui_tab": sfx_ui_tab,
    "ui/sfx_ui_coin": sfx_ui_coin,
    "ui/sfx_ui_reward": sfx_ui_reward,
    "ui/sfx_ui_page": sfx_ui_page,
    "ui/sfx_ui_levelup": sfx_levelup,
    "ui/sfx_ui_legendary": sfx_legendary,
}

BGM = {
    "bgm_menu": bgm_menu,
    "bgm_battle": bgm_battle,
    "bgm_boss": bgm_boss,
    "bgm_victory": bgm_victory,
    "bgm_defeat": bgm_defeat,
    "bgm_biome_meadow": bgm_meadow,
    "bgm_biome_swamp": bgm_swamp,
    "bgm_biome_crypt": bgm_crypt,
    "bgm_biome_volcano": bgm_volcano,
    "bgm_biome_void": bgm_void,
}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--all", action="store_true")
    ap.add_argument("--sfx", action="store_true")
    ap.add_argument("--bgm", action="store_true")
    ap.add_argument("--one", help="Sinh 1 file theo tên trong danh mục")
    ap.add_argument("--list", action="store_true")
    args = ap.parse_args()

    if args.list:
        print("SFX:"); [print(" ", k) for k in SFX]
        print("BGM:"); [print(" ", k) for k in BGM]
        return

    if args.one:
        for name, fn in {**SFX, **BGM}.items():
            if args.one in name:
                d = SFX_DIR if name in SFX else BGM_DIR
                save_wav(fn(), d / f"{name.split('/')[-1]}.wav")
                return
        print(f"Không tìm thấy '{args.one}'")
        return

    do_sfx = args.sfx or args.all or (not args.bgm)
    do_bgm = args.bgm or args.all

    if do_sfx:
        print(f"=== SFX ({len(SFX)}) ===")
        for name, fn in SFX.items():
            save_wav(fn(), SFX_DIR / f"{name}.wav")

    if do_bgm:
        print(f"\n=== BGM ({len(BGM)}) — loop 1 lượt, engine lặp lại ===")
        for name, fn in BGM.items():
            save_wav(fn(), BGM_DIR / f"{name}.wav")

    print("\nXong.")


if __name__ == "__main__":
    main()

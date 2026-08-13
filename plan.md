# PLAN.md — Thiết kế & Kiến trúc chi tiết

> **Dự án:** TurnBase — codename *Aether Legion*
> **Engine:** Unity 6000.5.2f1 · URP 2D · Input System 1.19 · uGUI 2.5 · MCP for Unity
> **Phiên bản tài liệu:** 2.0 — 2026-08-07
> **Bộ tài liệu:**
> - [plan.md](plan.md) — thiết kế & kiến trúc (file này)
> - [structure.md](structure.md) — cây cấu trúc project, từng file & trách nhiệm
> - [object-map.md](object-map.md) — bản đồ Scene/Prefab ↔ Script ↔ Asset + ma trận tác động khi thay đổi
> - [roadmap.md](roadmap.md) — lộ trình theo tuần

---

## MỤC LỤC

| § | Nội dung |
|---|---|
| 0 | Quyết định nền tảng |
| 1 | Tổng quan sản phẩm |
| 2 | Định hướng nghệ thuật |
| 3 | Vòng lặp cốt lõi |
| 4 | **Hệ thống chiến đấu (chi tiết đầy đủ)** |
| 5 | Hệ thống nhân vật |
| 6 | Kẻ địch & Boss |
| 7 | Trang bị & Vật phẩm |
| 8 | Tiến trình & Bản đồ node |
| 9 | Kinh tế & Gacha |
| 10 | UI/UX — đặc tả 23 màn hình |
| 11 | Kiến trúc kỹ thuật |
| 12 | Âm thanh |
| 13 | Bản địa hóa |
| 14 | Analytics |
| 15 | Rủi ro |
| 16 | Definition of Done |
| 17 | Quyết định kỹ thuật |
| 18 | Quy ước code & đặt tên |
| 19 | Bản quyền |
| 20 | Bảng thuật ngữ |

---

## 0. Quyết định nền tảng (đã chốt)

| Hạng mục | Quyết định | Hệ quả kỹ thuật |
|---|---|---|
| Nền tảng | Mobile (Android/iOS) + PC/Steam, UI **responsive** Portrait & Landscape | `LayoutProfileSwitcher`, 2 preset RectTransform, SafeArea, input abstraction |
| Combat core | Command JRPG + Skill Grid + ATB/Speed + **Action Command** | Simulation C# thuần, deterministic, tách khỏi View |
| Meta/Online | Offline v1, kiến trúc **server-ready** | Mọi truy cập dữ liệu qua `IPlayerRepository`; mọi giao dịch qua `IEconomyService` |

---

## 1. Tổng quan sản phẩm

### 1.1. Pitch
> Dẫn đội 4 anh hùng pixel-art xuyên bản đồ phân nhánh. Mỗi trận là ván cờ tốc độ: chọn kỹ năng đúng lúc, **bấm đúng nhịp** để tung đòn Perfect, phá **Poise** địch để tạo cơ hội dứt điểm.

### 1.2. Đối chiếu ảnh mẫu → tính năng cụ thể

| Ảnh | Yếu tố lấy về | Mục trong tài liệu |
|---|---|---|
| `image_UI.jpg` | Hero panel trái (portrait/HP/SP/LV/buff), Enemy panel phải (HP/ATK/DEF), **Skill Grid 5×3**, nút END TURN, Card/Item slot, panel STATS/EQ, node map `SWAMPS [1/3]`, bộ stat STR/CON/INT/DEX/AUR/LUK | §4.5, §5.3, §8.1, §10.2 |
| `Game_1.jpg` | Bottom nav 6 mục, top bar tiền tệ, side rail quest/event, **Auto ON/OFF**, **Damage Meter**, stage `4-3`, quest tracker nổi, red dot | §8.3, §9.1, §10.1, §10.6 |
| `game_4.jpg` | Menu lệnh FIGHT/MAGIC/ITEM, HP/MP đối xứng 2 phe, side-view party-vs-party, tooltip dòng phụ (`FIRE / BURN`), damage number lớn | §4.1, §5.3, §10.2 |
| `game_2.jpg` | Outline dày, glow ring dưới chân, HP bar nổi trên đầu unit | §2.1, §10.5 |

### 1.3. USP (4 trụ cột, không được cắt)
1. **Action Command** — mọi hành động có cửa sổ bấm nhịp → Perfect/Good/Miss.
2. **Skill Grid 5×3** — đọc toàn bộ lựa chọn trong 0.5 giây.
3. **Poise → Break** — mục tiêu chiến thuật rõ ràng mỗi trận.
4. **Node map phân nhánh** — mỗi run là một chuỗi quyết định.

### 1.4. KPI mục tiêu (v1.0)

| Chỉ số | Mục tiêu | Đo bằng |
|---|---|---|
| Tutorial completion | ≥ 85% | `tutorial_step` |
| D1 / D7 / D30 retention | 40% / 18% / 7% | Analytics |
| Phiên chơi TB | 12 phút (mobile) | `session_length` |
| **North Star:** trận có ≥1 Perfect / DAU | ≥ 3.5 | `action_command_accuracy` |
| Crash-free session | ≥ 99.5% | Crashlytics |

---

## 2. Định hướng nghệ thuật

### 2.1. Thông số kỹ thuật art

| Hạng mục | Giá trị |
|---|---|
| PPU (Pixels Per Unit) | **32** |
| **Kích thước hero** | **32×32 (chuẩn, canvas vuông, căn đáy giữa)** |
| **Kích thước enemy thường** | **32×32** |
| Kích thước enemy lớn | 48×48 |
| Kích thước boss | 64×64 → 96×96 |
| Portrait | 64×64 |
| Icon (skill/item/buff) | 32×32 |
| Tile | 32×32 |
| Filter Mode | **Point (no filter)** |
| Compression | None (UI) / ASTC 6×6 (build mobile) |
| Bảng màu | 48 màu cố định — xem `_Reference/palette.gpl` |

**Màu chủ đạo:** nền `#2B1B2E` · viền `#F4A259` · HP `#E63946` · SP `#457B9D` · Poise `#FFD166` · chữ `#F2E8CF` · Ultimate `#FFB703`

### 2.2. Bộ animation bắt buộc mỗi unit

| Clip | Frame | FPS | Loop | Ghi chú |
|---|---|---|---|---|
| `idle` | 4 | 8 | ✔ | Luôn chạy khi rảnh |
| `walk` | 6 | 12 | ✔ | Dùng khi tiến lên đánh |
| `attack` | 8 | 14 | ✘ | Có **event frame** `OnHit` |
| `skill` | 12 | 14 | ✘ | Có event `OnCast` + `OnHit` |
| `hit` | 3 | 16 | ✘ | Kèm flash trắng 2 frame |
| `down` | 6 | 10 | ✘ | Frame cuối giữ nguyên |
| `victory` | 6 | 8 | ✔ | Chỉ hero |

> **Quy ước tên file art:** `{loại}_{id}_{clip}.aseprite` — VD `hero_emberknight_attack.aseprite`, `enemy_goblin_idle.aseprite`, `boss_lich_skill2.aseprite`.

### 2.3. Ngân sách asset v1.0

| Loại | SL | Ghi chú |
|---|---|---|
| Hero | 24 × 7 clip = 168 | Aseprite import |
| Enemy | 60 × 4 clip = 240 | 8 boss thêm 2 phase |
| Tileset | 6 biome | Rule Tile |
| Background | 6 × 3 lớp parallax | |
| VFX | 40 sprite-sheet | 8–12 frame |
| Portrait | 44 | Hero + NPC |
| Icon | ~200 | Atlas riêng |
| UI 9-slice | ~60 | Atlas riêng |

---

## 3. Vòng lặp cốt lõi

```
[Home] → [Chọn chương] → [Node Map phân nhánh]
                              ↓ chọn node
        ┌──────────────────────────────────────────┐
        │  Battle / Elite / Boss / Shop / Rest /    │
        │  Treasure / Event / Mystery               │
        └──────────────────────────────────────────┘
                              ↓ (nếu là trận)
        [Pre-Battle: chọn 4 hero + đội hình]
                              ↓
        [BATTLE: ATB → Skill Grid → Action Command → Break]
                              ↓
        [Result: Vàng · EXP · Trang bị · Mảnh hero]
                              ↓
        [Về Node Map] ─(hết node)→ [Boss] → [Hoàn thành chương]
                              ↓
        [Meta: Level up · Gắn đồ · Ascend · Summon · Dungeon · Quest]
```

**3 tầng vòng lặp:**

| Tầng | Thời lượng | Quyết định chính | Phần thưởng |
|---|---|---|---|
| Micro | 20–60 giây | Chọn skill nào, nhắm ai, bấm nhịp | Damage, Break, sống sót |
| Mid | 6–10 phút | Đi nhánh nào trên node map | Vàng, trang bị, EXP |
| Macro | Nhiều ngày | Nuôi hero nào, tiêu Gem vào đâu | Hero mới, chương mới |

---

## 4. HỆ THỐNG CHIẾN ĐẤU — chi tiết đầy đủ

### 4.1. Sân khấu & vị trí

Side-view, party bên trái, địch bên phải (theo `game_4.jpg`).

**Slot người chơi (4 slot, 2 hàng × 2 cột):**

| Slot | Hàng | Toạ độ world (PPU 32) | Ghi chú |
|---|---|---|---|
| P0 | Front | (−4.0, −1.2) | Front-Top |
| P1 | Front | (−3.4, −2.0) | Front-Bottom |
| P2 | Back | (−5.6, −1.2) | Back-Top |
| P3 | Back | (−5.0, −2.0) | Back-Bottom |

**Slot địch (5 slot):** E0..E4 tại x = 3.0 → 6.2, so le y ∈ {−1.0, −1.8, −2.4}.
Boss chiếm 3 slot (E1–E3), đứng tại (4.6, −1.4).

**Quy tắc hàng (Row Rule):**
- Sát thương **vật lý** vào hàng sau: ×0.70
- Sát thương **phép**: không đổi
- Sát thương **AoE**: không đổi ở cả 2 hàng
- Hàng trước chết hết → hàng sau **tự động** thành hàng trước ở đầu lượt kế

### 4.2. Battle State Machine

```
BattleState:
  Init ──→ Intro ──→ RoundStart ──→ TurnStart ──→ AwaitInput ──→ ActionCommand
                          ↑                                            ↓
                          │                                        Resolve
                          │                                            ↓
                     RoundEnd ←── TurnEnd ←── AfterEffects ←──────────┘
                          │
                          └─→ (kiểm tra thắng/thua) ─→ Victory / Defeat / Escaped
```

| State | Việc làm | Sự kiện phát ra |
|---|---|---|
| `Init` | Dựng `BattleState` từ `BattleConfig`, seed RNG, tính stat | `BattleInitialized` |
| `Intro` | Camera pan, hiện tên boss, buff mở màn (aura, passive) | `BattleStarted` |
| `RoundStart` | +1 RoundNumber, kiểm tra Enrage timer, refresh intent địch | `RoundStarted` |
| `TurnStart` | Tick ATB tới khi có unit đạt 1000, DoT tick, giảm CD, hồi SP | `TurnStarted`, `StatusTicked` |
| `AwaitInput` | Người chơi chọn skill + mục tiêu (hoặc AI quyết định) | `ActionRequested` |
| `ActionCommand` | Mở cửa sổ nhịp (bỏ qua nếu Auto/tắt) | `CommandWindowOpened/Closed` |
| `Resolve` | Chạy `ActionResolver` → sinh chuỗi event | `DamageDealt`, `StatusApplied`, ... |
| `AfterEffects` | Counter, Reflect, Passive on-hit, Poise check, chết | `PoiseBroken`, `UnitDied` |
| `TurnEnd` | Giảm thời lượng buff của chủ thể, trừ ATB | `TurnEnded` |
| `RoundEnd` | Kiểm tra điều kiện thắng/thua/timeout | `BattleEnded` |

> **Bất biến (invariant) phải luôn đúng:** ở mọi thời điểm, `sum(HP) ≥ 0`, không unit nào có `ATB > 1000` sau `TurnEnd`, không status nào có `duration < 0`. Có assert trong Debug build.

### 4.3. Pipeline một lượt — 14 bước (thứ tự resolve chính xác)

Đây là thứ tự **bắt buộc**, sai thứ tự sẽ gây bug cân bằng khó tìm:

| # | Bước | Chi tiết |
|---|---|---|
| 1 | **Tick ATB** | `ATB += SPD_eff` cho mọi unit sống, tới khi ≥1000 |
| 2 | **Chọn actor** | ATB cao nhất → SPD → UnitId (ổn định, deterministic) |
| 3 | **DoT tick** | Burn/Poison/Bleed gây damage **trước** khi actor hành động |
| 4 | **Kiểm tra chết do DoT** | Nếu actor chết → bỏ lượt, sang bước 13 |
| 5 | **Kiểm tra Control** | Stun/Freeze → mất lượt, giảm duration, sang bước 13 |
| 6 | **Hồi SP** | `SP += 5 + AUR×0.3` (cap MaxSP) |
| 7 | **Giảm cooldown** | Mọi skill `CD -= 1` |
| 8 | **Nhận input** | Người chơi chọn / AI chấm điểm chọn |
| 9 | **Trừ chi phí** | Trừ SP, set CD, trừ ATB phụ nếu skill có `ExtraAtbCost` |
| 10 | **Action Command** | Mở cửa sổ nhịp → ra `CommandGrade` |
| 11 | **Resolve từng hit** | Với mỗi hit: chọn target → tính damage → áp dụng → trừ Poise → áp status |
| 12 | **Phản ứng** | Counter, Reflect, passive `OnHitTaken`, `OnKill`, hút máu |
| 13 | **Kết thúc lượt** | Giảm duration buff/debuff **của actor**, `ATB -= 1000` |
| 14 | **Dọn dẹp** | Xoá unit chết khỏi turn order, kiểm tra thắng/thua |

**Quy tắc quan trọng:**
- Damage được áp dụng **ngay lập tức** cho từng hit (không cộng dồn) → shield/counter phản ứng đúng từng nhát.
- Status áp dụng **sau khi** damage của hit đó đã trừ HP.
- Nếu target chết giữa combo → các hit còn lại **chuyển sang target khác còn sống** (nếu `TargetMode` là Single) hoặc bỏ qua.

### 4.4. ATB rời rạc (tick-based)

```csharp
const int ATB_THRESHOLD = 1000;

void TickUntilActor() {
    while (true) {
        var ready = units.Where(u => u.Alive && u.Atb >= ATB_THRESHOLD).ToList();
        if (ready.Count > 0) { currentActor = SortStable(ready)[0]; return; }
        foreach (var u in units.Where(u => u.Alive))
            u.Atb += u.SpdEffective;   // 1 tick
        ticks++;
        if (ticks > 100_000) throw new BattleStalemateException(); // an toàn
    }
}
// SortStable: ATB desc → SPD desc → UnitId asc
```

**Ví dụ số:** hero SPD 140, địch SPD 100 → hero hành động ~1.4 lần mỗi lần địch hành động.
Buff `Haste` +30% → SPD 182 → gần **2 lượt/1 lượt địch**. Đây là lý do Haste rất mạnh, phải giới hạn 2 stack.

**Turn Order Bar** hiển thị **8 lượt kế tiếp** bằng cách chạy mô phỏng ATB thuần (không side effect) — hàm `TurnScheduler.PreviewOrder(8)`.

### 4.5. Chỉ số (Stats)

**Chỉ số gốc (Primary)** — đúng bộ trong `image_UI.jpg`:

| Mã | Tên | Công thức dẫn xuất |
|---|---|---|
| STR | Sức mạnh | `ATK_phys = 5 + STR × 2.2` |
| CON | Thể chất | `MaxHP = 50 + CON × 12` · `DEF = 2 + CON × 1.4` |
| INT | Trí tuệ | `ATK_mag = 5 + INT × 2.4` · `MaxSP = 20 + INT × 3` |
| DEX | Nhanh nhẹn | `SPD = 80 + DEX × 3` · `ACC = 90 + DEX × 0.5` |
| AUR | Linh khí | `RES = DEX × 0.2 + AUR × 0.8` · `SP_regen = 5 + AUR × 0.3` |
| LUK | May mắn | `CRIT% = 5 + LUK × 0.6` · `DropBonus% = LUK × 0.3` |

**Chỉ số dẫn xuất (Derived) — danh sách đầy đủ:**

| Mã | Mặc định | Cap | Nguồn tăng |
|---|---|---|---|
| `MaxHP` | theo CON | — | CON, trang bị, buff |
| `MaxSP` | theo INT | — | INT, trang bị |
| `ATK` | theo STR/INT | — | trang bị, buff ATK Up |
| `DEF` | theo CON | — | trang bị, buff DEF Up |
| `SPD` | theo DEX | — | Haste/Slow |
| `ACC` (chính xác) | 90 + DEX×0.5 | 150 | trang bị |
| `EVA` (né) | 3 | **40** | trang bị, buff |
| `CRIT%` | theo LUK | **95** | trang bị |
| `CRIT_DMG%` | 50 | 300 | trang bị |
| `RES` (kháng hiệu ứng) | theo AUR | **85** | trang bị |
| `EFF_ACC` (chính xác hiệu ứng) | 0 | 100 | trang bị |
| `POISE_MAX` | theo loại unit | — | cố định |
| `LIFESTEAL%` | 0 | 40 | trang bị |
| `DMG_BONUS%` | 0 | — | buff |
| `DMG_REDUCT%` | 0 | **75** | buff, DEF Up |

> **Thứ tự áp dụng modifier stat:** `Base → +Flat (trang bị) → ×(1 + Σ%Trang bị) → ×(1 + Σ%Set) → ×(1 + Σ%Buff) → ×(1 − Σ%Debuff) → Clamp cap`.
> Buff cùng loại **không nhân dồn** mà cộng %: 2 stack ATK Up = +50%, không phải ×1.25².

### 4.6. Pipeline sát thương (thứ tự chính xác)

```csharp
int CalculateDamage(CombatUnit atk, CombatUnit def, SkillDefinition skill,
                    CommandGrade grade, IRandomSource rng)
{
    // 1. Chọn ATK theo loại sát thương
    float atkStat = skill.DamageType == DamageType.Physical ? atk.Atk_Phys : atk.Atk_Mag;

    // 2. Cơ sở
    float dmg = atkStat * skill.PowerMultiplier + skill.FlatDamage;

    // 3. Kiểm tra né (chỉ vật lý; phép không né được)
    if (skill.DamageType == DamageType.Physical) {
        float hitChance = Clamp((atk.Acc - def.Eva) / 100f, 0.10f, 1.00f);
        if (rng.NextFloat() > hitChance) return DAMAGE_MISS;   // −1 = MISS
    }

    // 4. Giảm theo giáp (bão hòa, không bao giờ về 0)
    float defEff = def.Def * (1f - skill.DefIgnore);            // DefIgnore 0..1
    dmg *= 100f / (100f + defEff);

    // 5. Nguyên tố
    dmg *= ElementTable.Multiplier(skill.Element, def.Element); // 1.4 / 1.3 / 1.0 / 0.7

    // 6. Chí mạng
    bool crit = rng.NextFloat() < Clamp(atk.Crit - def.CritResist, 0.01f, 0.95f);
    if (crit) dmg *= 1f + atk.CritDmg;

    // 7. Modifier tổng
    dmg *= 1f + atk.DmgBonus;
    dmg *= 1f - Clamp(def.DmgReduct, 0f, 0.75f);
    dmg *= grade.Multiplier;               // Perfect 1.30 / Good 1.10 / Miss 0.80
    dmg *= def.IsBroken ? 1.5f : 1f;
    dmg *= (skill.DamageType == DamageType.Physical && def.Row == Row.Back
            && !skill.IsAoe) ? 0.7f : 1f;

    // 8. Dao động deterministic
    dmg *= rng.NextFloat(0.95f, 1.05f);

    // 9. Chia đều nếu multi-hit đã nhân sẵn PowerMultiplier
    return Math.Max(1, (int)dmg);
}
```

**Thứ tự trừ HP:** `Shield` hấp thụ trước → HP còn lại chịu phần dư. Nếu `Shield` vỡ đúng nhát này → phát `ShieldBroken`.

### 4.7. Bảng nguyên tố (6×6 đầy đủ)

| Attacker \ Defender | Fire | Water | Earth | Wind | Light | Dark |
|---|---|---|---|---|---|---|
| **Fire** | 1.0 | 0.7 | 1.0 | **1.3** | 1.0 | 1.0 |
| **Water** | **1.3** | 1.0 | 0.7 | 1.0 | 1.0 | 1.0 |
| **Earth** | 1.0 | **1.3** | 1.0 | 0.7 | 1.0 | 1.0 |
| **Wind** | 0.7 | 1.0 | **1.3** | 1.0 | 1.0 | 1.0 |
| **Light** | 1.0 | 1.0 | 1.0 | 1.0 | 1.0 | **1.4** |
| **Dark** | 1.0 | 1.0 | 1.0 | 1.0 | **1.4** | 1.0 |
| **Neutral** | 1.0 ở mọi ô | | | | | |

Hiển thị: mũi tên ▲ xanh (khắc) / ▼ đỏ (bị khắc) cạnh HP bar mục tiêu khi hover skill.

### 4.8. ACTION COMMAND — đặc tả đầy đủ

Đây là tính năng rủi ro cao nhất. Cần spec chính xác từng ms.

#### 4.8.1. Bốn loại

| Loại | Skill dùng | Cách chơi | Cửa sổ Perfect | Cửa sổ Good |
|---|---|---|---|---|
| `SingleTap` | Đánh thường, skill 1 hit | Vòng tròn thu về tâm trong 900 ms, nhấn khi trùng vòng đích | ±80 ms | ±180 ms |
| `Combo(n)` | Skill nhiều hit (n=3..5) | n lần nhấn theo nhịp metronome 500 ms | ±90 ms mỗi nhịp | ±200 ms |
| `Charge` | Skill nặng, Ultimate | Giữ, thanh chạy 0→100 trong 1400 ms, nhả trong vùng xanh (85–100) | vùng 92–100 | vùng 80–92 |
| `Guard` | Khi **bị** tấn công | Nhấn ngay trước impact | ±100 ms → giảm **50%** dmg | ±220 ms → giảm 20% |

#### 4.8.2. Hệ số kết quả

| Grade | Damage | SP | Poise | Ultimate gauge | Phản hồi |
|---|---|---|---|---|---|
| `Perfect` | ×1.30 | +5 | −8 thêm | +8 | Chớp vàng, "PERFECT!", shake mạnh, SFX riêng |
| `Good` | ×1.10 | +2 | 0 | +4 | Chớp trắng nhẹ, SFX thường |
| `Miss` | ×0.80 | 0 | 0 | +1 | Không hiệu ứng, SFX hụt |

Combo n nhịp: mỗi nhịp chấm riêng, damage tổng = Σ từng hit; **đủ n Perfect liên tiếp → bonus `All Perfect` ×1.15 lên tổng**.

#### 4.8.3. Xử lý độ trễ (quan trọng cho mobile — rủi ro R1)

```
effectiveInputTime = rawInputTime − platformLatencyOffset − userCalibrationOffset
```
- `platformLatencyOffset`: đo tự động lúc khởi động (Android ~40–90 ms, iOS ~25 ms, PC ~8 ms).
- `userCalibrationOffset`: màn hình hiệu chỉnh trong Settings (người chơi bấm theo 8 nhịp metronome → lấy trung vị).
- **Input buffer 100 ms**: nhấn sớm trước khi cửa sổ mở vẫn được ghi nhận.
- Trên mobile, cửa sổ Perfect **nới thêm 20 ms** so với PC.

#### 4.8.4. Chế độ thay thế
| Chế độ | Hành vi |
|---|---|
| Auto-battle | Luôn tính `Good` (×1.10) |
| Tắt Action Command (Accessibility) | Luôn `Good`, không hiện UI, **không bị phạt** |
| Tốc độ ×2 / ×3 | Cửa sổ giữ nguyên thời gian thực, chỉ animation nhanh hơn |

### 4.9. Poise & Break

| Loại unit | POISE_MAX | Hồi |
|---|---|---|
| Trash | 30 | Đầy sau 2 lượt kể từ khi Break kết thúc |
| Elite | 80 | 3 lượt |
| Boss | 150–200 | 3 lượt, phase mới reset |
| Hero (chỉ boss phá được) | 60 | 2 lượt |

**Nguồn trừ Poise:**

| Nguồn | Trừ |
|---|---|
| Hit thường | −3 |
| Hit trúng hệ khắc chế | −15 |
| Hit `Perfect` | −8 thêm |
| Skill có tag `Breaker` | −25 |
| Đòn `Heavy` (PowerMultiplier ≥ 2.0) | −12 |

**Khi Poise = 0 → BREAK:**
- Bỏ lượt tiếp theo của mục tiêu (ATB reset về 0).
- Nhận **×1.5** damage trong suốt trạng thái Break.
- Mọi debuff đang có **+1 lượt**.
- Kéo dài 1 lượt của mục tiêu, sau đó Poise hồi dần.
- VFX: freeze 120 ms, chớp trắng toàn màn, chữ "BREAK!", shatter particle, SFX kính vỡ.

### 4.10. Tài nguyên hành động

| Tài nguyên | Phạm vi | Hồi | Dùng cho |
|---|---|---|---|
| **SP** | Mỗi hero riêng | `5 + AUR×0.3`/lượt, +5 khi Perfect | Skill (cost 8–40) |
| **Cooldown** | Mỗi skill riêng | −1 mỗi lượt của chủ thể | Skill mạnh (CD 2–5) |
| **Ultimate Gauge** | **Chung cả đội**, 0→100 | +4 khi gây dmg, +6 khi nhận dmg, +8 khi Perfect | Ultimate của 1 hero |

Ultimate: khi gauge = 100, viền vàng nhấp nháy quanh ô số 5 của Skill Grid. Dùng → gauge về 0, có cutscene ngắn 1.5 giây (bỏ qua được).

### 4.11. Trạng thái (Status Effects) — bảng đầy đủ 20 hiệu ứng

| ID | Tên | Nhóm | Hiệu ứng | Tick | Stack | Duration | Dispel |
|---|---|---|---|---|---|---|---|
| `burn` | Thiêu đốt | DoT | −5% MaxHP/lượt, −20% DEF | Đầu lượt | 3 | 3 | Cleanse |
| `poison` | Độc | DoT | −8% HP hiện tại/lượt | Đầu lượt | 3 | 4 | Cleanse |
| `bleed` | Chảy máu | DoT | −0.5×ATK người gây/lượt | Đầu lượt | 5 | 3 | Cleanse |
| `stun` | Choáng | Control | Mất lượt | — | ✘ | 1–2 | Cleanse |
| `freeze` | Đóng băng | Control | Mất lượt, +30% dmg nhận, **tan khi trúng Fire** | — | ✘ | 2 | Cleanse |
| `paralyze` | Tê liệt | Control | 50% mất lượt | — | ✘ | 3 | Cleanse |
| `silence` | Câm lặng | Control | Chỉ đánh thường | — | ✘ | 2 | Cleanse |
| `taunt` | Khiêu khích | Control | Buộc nhắm unit taunt | — | ✘ | 2 | Cleanse |
| `sleep` | Ngủ | Control | Mất lượt, **tỉnh khi bị đánh** | — | ✘ | 3 | Cleanse |
| `atk_down` | Giảm công | Debuff | −25% ATK | — | 2 | 2 | Cleanse |
| `def_down` | Giảm thủ | Debuff | −25% DEF | — | 2 | 2 | Cleanse |
| `spd_down` (Slow) | Chậm | Debuff | −30% SPD | — | 2 | 3 | Cleanse |
| `blind` | Mù | Debuff | −40% ACC | — | ✘ | 2 | Cleanse |
| `curse` | Nguyền rủa | Debuff | Không hồi máu | — | ✘ | 3 | Cleanse |
| `atk_up` | Tăng công | Buff | +25% ATK | — | 2 | 3 | Dispel |
| `def_up` | Tăng thủ | Buff | +25% DEF | — | 2 | 3 | Dispel |
| `spd_up` (Haste) | Nhanh | Buff | +30% SPD | — | 2 | 3 | Dispel |
| `shield` | Khiên | Buff | Hấp thụ N damage | — | Cộng giá trị | 3 | Dispel |
| `regen` | Hồi phục | Buff | +6% MaxHP/lượt | Đầu lượt | 2 | 3 | Dispel |
| `immunity` | Miễn nhiễm | Buff | Chặn debuff mới | — | ✘ | 2 | Dispel |
| `counter` | Phản đòn | Buff | Phản 60% ATK khi bị đánh cận chiến | — | ✘ | 2 | Dispel |
| `reflect` | Phản chiếu | Buff | Dội 30% dmg phép | — | ✘ | 2 | Dispel |

**Luật chung — phải cài đúng để không sót logic:**

| Luật | Chi tiết |
|---|---|
| Tỉ lệ áp dụng | `applied = rng < (skill.ApplyChance + atk.EffAcc − def.Res)`, kẹp `[0.05, 0.95]` |
| `immunity` | Chặn **hoàn toàn** mọi debuff mới, không tiêu hao stack |
| Refresh vs Stack | Áp lại status đã có: nếu chưa đầy stack → +1 stack **và** reset duration; nếu đầy stack → chỉ reset duration |
| Thời điểm tick DoT | **Đầu lượt của unit mang status** (bước 3 trong §4.3) |
| Thời điểm giảm duration | **Cuối lượt của unit mang status** (bước 13) |
| Chết do DoT | Vẫn tính là bị giết bởi người gây status (cho passive `OnKill`) |
| Break kéo dài debuff | Khi Break, mọi debuff `duration += 1` (một lần duy nhất) |
| Hết hạn | Phát `StatusExpired`, gỡ icon, tính lại stat |

### 4.12. Chế độ nhắm mục tiêu

| `TargetMode` | Mô tả | Auto-suggest |
|---|---|---|
| `SingleEnemy` | 1 địch | HP thấp nhất, hoặc bị khắc chế |
| `AllEnemies` | Tất cả địch | — |
| `RandomEnemy(n)` | n lần chọn ngẫu nhiên (có thể trùng) | — |
| `FrontRowEnemies` | Hàng trước địch | — |
| `BackRowEnemies` | Hàng sau địch | — |
| `LowestHpEnemy` | Tự chọn | — |
| `SingleAlly` | 1 đồng minh | HP% thấp nhất |
| `AllAllies` | Toàn đội | — |
| `Self` | Bản thân | — |
| `DeadAlly` | Đồng minh đã gục (hồi sinh) | Gục sớm nhất |
| `Taunted` | Ghi đè: nếu có unit taunt thì buộc chọn | — |

**Thứ tự ưu tiên ghi đè:** `Taunt > người chơi chọn > Auto-suggest`.

### 4.13. AI kẻ địch

#### 4.13.1. Utility scoring

```csharp
// AIProfileDefinition (ScriptableObject)
class AIRule {
    Condition   When;        // enum + tham số
    string      SkillId;
    float       Weight;
    float       Cooldown;    // số lượt không lặp lại rule này
}

int ChooseAction() {
    var scored = rules
        .Where(r => r.When.Evaluate(state) && SkillReady(r.SkillId))
        .Select(r => (r, score: r.Weight + rng.NextFloat(-10f, 10f)));
    return scored.OrderByDescending(x => x.score).First().r;
}
```

**Điều kiện (`Condition`) hỗ trợ:** `Always · SelfHpBelow(x%) · AllyHpBelow(x%) · AllyCountAlive(≥n) · EnemyCountAlive(≥n) · SelfHasStatus(id) · EnemyHasStatus(id) · RoundNumber(≥n) · SelfSpAbove(x) · TargetIsBroken · PhaseIs(n)`

**Ví dụ `AIProfile_GoblinShaman`:**

| Điều kiện | Skill | Weight |
|---|---|---|
| `SelfHasStatus(silence)` | `basic_attack` | 100 |
| `AllyHpBelow(40)` | `heal_wave` | 90 |
| `EnemyCountAlive(≥3)` | `aoe_bolt` | 70 |
| `RoundNumber(≥5)` | `curse` | 55 |
| `Always` | `bolt` | 40 |

#### 4.13.2. Intent Preview
Icon trên đầu địch báo trước hành động lượt sau: ⚔ tấn công (kèm số damage ước tính) · 🛡 phòng thủ · ✨ buff · ☠ ultimate · ❓ (độ khó cao thì ẩn).
Tính bằng cách chạy `ChooseAction()` với RNG **peek** (không tiêu hao seed) ở cuối `TurnEnd`.

#### 4.13.3. Boss
- `BossPhaseController`: đổi bộ rule khi HP < 60% / 30%. Phase mới → reset Poise, phát `PhaseChanged`, có animation chuyển pha.
- `SignatureMove`: đếm ngược **3 lượt hiển thị công khai** trên đầu boss. Có counterplay bắt buộc (Break / dispel / burst).
- **Enrage**: sau `EnrageRound` (mặc định 12) → +50% ATK, +30% SPD mỗi 3 lượt tiếp theo, cộng dồn.

### 4.14. Bảng xử lý tình huống biên (Edge Cases) — chống sót logic

| # | Tình huống | Xử lý bắt buộc |
|---|---|---|
| E01 | Target chết giữa combo multi-hit | Chuyển sang target sống khác cùng phe; hết target → dừng combo, không phí SP |
| E02 | Actor chết do DoT ngay đầu lượt | Bỏ lượt, không hoàn SP, ATB reset |
| E03 | Actor chết do Counter/Reflect giữa hành động | Hoàn tất chuỗi hit hiện tại, rồi mới xử lý chết |
| E04 | Cả 2 phe chết cùng lúc | **Người chơi thua** (quy ước) |
| E05 | Unit bị Freeze rồi trúng Fire | Freeze tan ngay, damage Fire vẫn ×1.3 do Freeze |
| E06 | Unit đang Sleep bị đánh | Tỉnh sau khi damage đã áp dụng (vẫn ăn full damage) |
| E07 | Taunt lên unit đã chết | Status bị gỡ, mục tiêu chọn lại tự do |
| E08 | Shield lớn hơn damage | HP không đổi, Shield giảm, không tính là "bị đánh" cho passive `OnDamaged` |
| E09 | Hồi máu vượt MaxHP | Cắt tại MaxHP, phần dư **không** thành shield (trừ khi skill ghi rõ) |
| E10 | Hồi sinh unit đang có debuff | Xoá toàn bộ status, hồi HP theo % ghi trong skill |
| E11 | Hero gục quá 3 lượt | `PermanentlyDown` — không hồi sinh được trong trận đó |
| E12 | Toàn đội gục nhưng có `AutoRevive` passive | Kích hoạt trước khi kiểm tra thua |
| E13 | Trận vượt 30 lượt (có timer) | Thua theo timeout; trận không timer thì vượt 200 lượt → hòa, tính là thua |
| E14 | Hàng trước chết hết | Hàng sau thành hàng trước **ở đầu lượt kế**, không phải ngay lập tức |
| E15 | Ultimate gauge đầy đúng lúc hero dùng ultimate chết | Gauge giữ nguyên, chuyển cho hero khác |
| E16 | Skill AoE nhưng chỉ còn 1 địch | Vẫn tính là AoE (không có bonus single-target) |
| E17 | Người chơi thoát app giữa trận | Lưu `BattleSnapshot` (seed + intent list) → vào lại replay tới đúng trạng thái |
| E18 | Escape ở node Boss | Bị chặn, nút Escape ẩn |
| E19 | SP không đủ nhưng skill được chọn qua Auto | AI/Auto chỉ chọn skill khả dụng — có filter ở `ChooseAction` |
| E20 | Minion của Summoner khi chủ chết | Minion biến mất sau 1 lượt |
| E21 | 2 status cùng ID từ 2 nguồn khác nhau | Cùng slot stack, giá trị lấy theo **nguồn mạnh hơn** |
| E22 | `spd_down` khiến SPD < 1 | Sàn SPD = 10, không bao giờ 0 (chống deadlock ATB) |
| E23 | Đổi tốc độ ×3 giữa Action Command | Cửa sổ nhịp giữ nguyên thời gian thực |
| E24 | Damage âm do modifier lỗi | `Math.Max(1, dmg)` — luôn ≥ 1 |

> Mỗi dòng ở bảng này **phải có 1 unit test tương ứng** trong `Tests/EditMode/Combat/EdgeCaseTests.cs`. Xem [object-map.md](object-map.md) §7.

### 4.15. Kết thúc trận

| Kết quả | Điều kiện | Xử lý |
|---|---|---|
| `Victory` | Mọi enemy HP = 0 | Tính thưởng, animation victory, màn hình Result |
| `Defeat` | Mọi hero HP = 0 | Màn Defeat: `Thử lại` / `Về map (mất run)` / `Hồi sinh bằng Gem` |
| `Escaped` | Người chơi dùng Escape thành công | Về node map, mất 50% thưởng, node coi như đã đi qua |
| `Timeout` | Vượt giới hạn lượt | Tính như `Defeat` |

**Công thức thưởng:**
```
gold  = (30 + 12 × stageLevel) × nodeMult × (1 + LUK_team × 0.003)
exp   = (25 + 10 × stageLevel) × nodeMult
nodeMult: Battle 1.0 · Elite 2.2 · Boss 4.0 · Treasure 0 (cho item)
Bonus không ai gục: ×1.15   ·   Bonus ≤ N lượt: ×1.10
```

### 4.16. Auto-battle

| Cấp | Hành vi |
|---|---|
| `Off` | Người chơi điều khiển hoàn toàn |
| `Auto` | AI chọn skill theo policy dưới, Action Command = Good |

**Policy Auto (thứ tự ưu tiên):**
1. Đồng minh HP < 35% và có skill heal khả dụng → heal
2. Đồng minh gục và có Revive → revive
3. Địch đang Break → dùng skill damage cao nhất
4. Ultimate đầy và có ≥2 địch → Ultimate
5. Có skill khắc chế hệ mục tiêu → dùng
6. Skill damage cao nhất mà đủ SP
7. Đánh thường

Ghi nhớ lựa chọn Auto/tốc độ vào `SettingsDto`.

### 4.17. Determinism & Replay

- Mọi RNG qua `IRandomSource` (`XorShiftRandom` seed 64-bit).
- **`ReplayData` = `{ seed, battleConfigId, List<ActionIntent> }`** — đủ để tái tạo 100% trận đấu.
- `ReplayVerifier.Verify(replay, expectedResult)` — dùng cho: golden test, chống gian lận khi lên server, tính năng "xem lại trận" ở v1.2.
- **Cấm tuyệt đối** trong `Game.Combat`: `UnityEngine.Random`, `Time.*`, `DateTime.Now`, `float` không xác định thứ tự (dùng `List` thay `HashSet`/`Dictionary` khi duyệt).

---

## 5. HỆ THỐNG NHÂN VẬT

### 5.1. Sáu lớp

| Class | Vai trò | Stat chính | Cơ chế riêng | POISE dmg |
|---|---|---|---|---|
| **Vanguard** | Tank | CON, STR | Taunt, Shield, giảm dmg cho hàng sau | Trung bình |
| **Slayer** | DPS vật lý | STR, LUK | Combo 5 nhịp, Crit build, hút máu | Cao (Breaker) |
| **Arcanist** | DPS phép | INT, AUR | Element Charge, AoE | **Rất cao** |
| **Warden** | Hỗ trợ | AUR, CON | Heal, Cleanse, Revive, Regen | Thấp |
| **Trickster** | Debuff/Tốc | DEX, LUK | Steal, Slow, đánh 2 lần, luôn đi trước | Trung bình |
| **Summoner** | Triệu hồi | INT, DEX | Minion có slot ATB riêng | Thấp (minion cao) |

### 5.2. Bảng stat cơ sở theo class (level 1, ★3)

| Class | STR | CON | INT | DEX | AUR | LUK | → MaxHP | ATK | DEF | SPD |
|---|---|---|---|---|---|---|---|---|---|---|
| Vanguard | 12 | 20 | 6 | 8 | 10 | 6 | 290 | 31 | 30 | 104 |
| Slayer | 20 | 10 | 6 | 14 | 6 | 14 | 170 | 49 | 16 | 122 |
| Arcanist | 6 | 9 | 20 | 10 | 12 | 8 | 158 | 53 | 15 | 110 |
| Warden | 8 | 14 | 12 | 9 | 18 | 8 | 218 | 34 | 22 | 107 |
| Trickster | 14 | 9 | 8 | 20 | 8 | 16 | 158 | 36 | 15 | 140 |
| Summoner | 7 | 11 | 18 | 12 | 12 | 9 | 182 | 48 | 17 | 116 |

**Tăng trưởng mỗi level:** `stat += GrowthPerLevel × (1 + 0.02 × (star − 3))`, `GrowthPerLevel` ≈ 8–12% stat gốc.

### 5.3. Đường cong Level & chi phí

```
EXP cần cho level n:      EXP(n) = round(40 × n^1.85)
Tổng EXP tới level 60:    ≈ 1.42 triệu
Vàng cần mỗi level:       gold(n) = round(60 × n^1.35)
```

| Mốc | Level | Cần thêm | Ghi chú |
|---|---|---|---|
| Cap ★3 | 40 | — | Muốn lên tiếp phải Ascend |
| Cap ★4 | 50 | — | |
| Cap ★5 | 55 | — | |
| Cap ★6 | 60 | — | |

### 5.4. Độ hiếm & Ascend (nâng sao)

| Rarity | ★ khởi điểm | ★ tối đa | Tỉ lệ summon |
|---|---|---|---|
| Common | ★1 | ★4 | 50% |
| Rare | ★2 | ★5 | 36.5% |
| Epic | ★3 | ★6 | 12% |
| Legendary | ★4 | ★6 | 1.5% |

**Chi phí Ascend:**

| Từ → Đến | Mảnh cùng hero | Vật liệu | Vàng | Hiệu quả |
|---|---|---|---|---|
| ★1→★2 | 10 | 5 Essence I | 5.000 | stat ×1.15, cap level +10 |
| ★2→★3 | 20 | 10 Essence I | 15.000 | stat ×1.15 |
| ★3→★4 | 40 | 15 Essence II | 40.000 | stat ×1.15, **mở skill slot 4** |
| ★4→★5 | 70 | 25 Essence II + 5 Core | 100.000 | stat ×1.15, **mở Ultimate** |
| ★5→★6 | 120 | 40 Essence III + 15 Core | 250.000 | stat ×1.15, **mở Awakening** |

### 5.5. Skill Grid — 15 ô (theo `image_UI.jpg`)

```
Hàng 1 — ACTIVE   [Basic]   [Skill A]  [Skill B]  [Skill C]  [ULTIMATE]
Hàng 2 — ITEM     [Potion]  [Ether]    [Antidote] [Bomb]     [Revive]
Hàng 3 — TACTIC   [Guard]   [SwapRow]  [Focus]    [Analyze]  [Escape]
```

**Trạng thái hiển thị của một ô (`SkillSlotView`) — phải cài đủ 8 trạng thái:**

| Trạng thái | Hiển thị |
|---|---|
| Khả dụng | Icon sáng, viền theo nguyên tố, badge SP |
| Đang cooldown | Overlay tối + số lượt còn lại to ở giữa |
| Không đủ SP | Icon xám, badge SP đỏ |
| Bị Silence | Icon xám + biểu tượng cấm |
| Ultimate chưa đầy | Viền xám + thanh tiến độ |
| Ultimate đầy | Viền vàng nhấp nháy 1 Hz |
| Đang chọn | Scale 1.08, viền trắng dày |
| Khắc chế mục tiêu | Mũi tên ▲ nhỏ góc phải trên |

**Tooltip** (nhấn giữ 300 ms / hover PC): tên · mô tả · cost SP · CD · hệ · sát thương ước tính · status áp dụng — theo format `game_4.jpg` (`FIRE` / dòng phụ `BURN`).

### 5.6. Bảng tactic (hàng 3) — thường bị bỏ sót

| Tactic | Cost | Hiệu ứng |
|---|---|---|
| `Guard` | 0 SP | Kết thúc lượt, +50% DEF tới lượt sau, hồi +8 SP |
| `SwapRow` | 0 SP | Đổi hàng trước/sau, không kết thúc lượt (1 lần/lượt) |
| `Focus` | 0 SP | Kết thúc lượt, lượt sau chắc chắn Crit |
| `Analyze` | 5 SP | Hiện toàn bộ stat + intent + kháng của 1 địch (vĩnh viễn trong trận) |
| `Escape` | 0 SP | Tỉ lệ `40% + (SPD_team − SPD_enemy)×0.5%`; thất bại thì mất lượt |

### 5.7. Đội hình & Synergy

**8 preset đội hình:**

| Tên | Bố trí | Bonus |
|---|---|---|
| Balanced | 2 trước / 2 sau | Không |
| Phalanx | 3 trước / 1 sau | Hàng trước +15% DEF |
| Arrowhead | 1 trước / 3 sau | Hàng sau +12% ATK |
| Vanguard Line | 4 trước | Toàn đội +10% ATK, −10% DEF |
| Siege | 4 sau | −20% dmg vật lý nhận, −15% SPD |
| Flanking | 2 trước / 2 sau lệch | +8% CRIT |
| Turtle | 3 trước / 1 sau | +20% DEF, −15% ATK |
| Blitz | 2 trước / 2 sau | +12% SPD, −8% MaxHP |

**Team Synergy (cộng dồn):**

| Điều kiện | Bonus |
|---|---|
| 2 hero cùng class | +10% stat chính của class đó |
| 3 hero cùng class | +18% |
| 2 hero cùng element | +8% dmg hệ đó |
| 4 hero khác class hoàn toàn | +10% mọi stat |
| Có đủ Tank + Heal + DPS | +5% MaxHP toàn đội |

### 5.8. Danh sách hero v1.0 (24 hero)

**6 hero mẫu chi tiết đầy đủ** (dùng làm template cho 18 hero còn lại):

#### `hero_ember_knight` — Ember Knight · Vanguard · Fire · Epic

| Ô | Skill | Cost | CD | Loại | Power | Target | Hiệu ứng |
|---|---|---|---|---|---|---|---|
| 1 | Ember Slash | 0 | 0 | Phys | 1.0 | Single | — · `SingleTap` |
| 2 | Flame Guard | 12 | 3 | Support | — | Self | `def_up` 3 lượt + `taunt` 2 lượt · `Charge` |
| 3 | Blazing Bash | 18 | 2 | Phys | 1.6 | Single | `burn` 60%, tag `Breaker` · `SingleTap` |
| 4 | Cinder Wall | 24 | 4 | Support | — | AllAllies | `shield` = 2.5×DEF · `Charge` |
| 5 | **Inferno Bulwark** (Ult) | — | — | Phys | 2.4 | AllEnemies | `burn` 100%, tự `def_up` 3 lượt · `Charge` |

Passive: *Molten Core* — khi HP < 40%, +30% DEF và phản 25% damage vật lý.
Awakening: *Eternal Flame* — `shield` không bị dispel, khi shield vỡ gây `burn` cho kẻ phá.

#### `hero_shadow_fang` — Shadow Fang · Slayer · Dark · Legendary

| Ô | Skill | Cost | CD | Loại | Power | Target | Hiệu ứng |
|---|---|---|---|---|---|---|---|
| 1 | Quick Slash | 0 | 0 | Phys | 0.9 | Single | `SingleTap` |
| 2 | Fang Rush | 14 | 1 | Phys | 0.5 ×4 | Single | `Combo(4)`, `bleed` 40%/hit |
| 3 | Shadow Step | 10 | 3 | Support | — | Self | `spd_up` + né đòn kế tiếp · `SingleTap` |
| 4 | Executioner | 26 | 3 | Phys | 2.2 | LowestHpEnemy | +100% dmg nếu target < 30% HP · `Charge` |
| 5 | **Thousand Fangs** (Ult) | — | — | Phys | 0.55 ×8 | Random(8) | `Combo(5)`, hút máu 25% |

Passive: *Bloodthirst* — mỗi lần giết, +15% ATK tới hết trận (cộng dồn tối đa 3).
Awakening: *Shadow Clone* — All Perfect ở `Fang Rush` → thêm 2 hit.

#### `hero_frost_sage` — Frost Sage · Arcanist · Water · Epic

| Ô | Skill | Cost | CD | Loại | Power | Target | Hiệu ứng |
|---|---|---|---|---|---|---|---|
| 1 | Ice Shard | 0 | 0 | Mag | 1.0 | Single | `SingleTap` |
| 2 | Frost Nova | 20 | 2 | Mag | 1.3 | AllEnemies | `spd_down` 50%, tag `Breaker` · `Charge` |
| 3 | Glacier Spike | 24 | 3 | Mag | 2.0 | Single | `freeze` 45% · `SingleTap` |
| 4 | Mana Well | 8 | 4 | Support | — | AllAllies | +20 SP toàn đội · `SingleTap` |
| 5 | **Absolute Zero** (Ult) | — | — | Mag | 2.8 | AllEnemies | `freeze` 70%, ×2 dmg lên mục tiêu Break · `Charge` |

Passive: *Frostbite* — địch bị `freeze` nhận thêm 25% damage từ mọi nguồn.

#### `hero_dawn_cleric` — Dawn Cleric · Warden · Light · Rare

| Ô | Skill | Cost | CD | Loại | Power | Target | Hiệu ứng |
|---|---|---|---|---|---|---|---|
| 1 | Light Bolt | 0 | 0 | Mag | 0.9 | Single | `SingleTap` |
| 2 | Mend | 12 | 1 | Heal | 1.4×ATK | LowestHpAlly | `regen` 2 lượt · `SingleTap` |
| 3 | Purify | 14 | 2 | Support | — | AllAllies | Cleanse 1 debuff + `immunity` 1 lượt · `Charge` |
| 4 | Sanctuary | 22 | 4 | Heal | 1.0×ATK | AllAllies | + `def_up` · `Charge` |
| 5 | **Rebirth Dawn** (Ult) | — | — | Heal | — | DeadAlly | Hồi sinh 60% HP + `shield` · `Charge` |

Passive: *Guiding Light* — mỗi lần hồi máu, nạp thêm +3 Ultimate gauge.

#### `hero_gale_thief` — Gale Thief · Trickster · Wind · Rare

| Ô | Skill | Cost | CD | Loại | Power | Target | Hiệu ứng |
|---|---|---|---|---|---|---|---|
| 1 | Gust Cut | 0 | 0 | Phys | 0.85 ×2 | Single | `Combo(2)` |
| 2 | Pilfer | 10 | 2 | Phys | 0.8 | Single | Cướp 1 buff của địch · `SingleTap` |
| 3 | Smoke Veil | 12 | 3 | Support | — | AllAllies | +25 EVA 2 lượt · `SingleTap` |
| 4 | Hamstring | 16 | 2 | Phys | 1.2 | AllEnemies | `spd_down` 70% · `Charge` |
| 5 | **Tempest Dance** (Ult) | — | — | Phys | 0.7 ×6 | Random(6) | `Combo(5)`, mỗi Perfect +1 hit |

Passive: *First Strike* — luôn hành động đầu tiên ở round 1.

#### `hero_bone_caller` — Bone Caller · Summoner · Dark · Epic

| Ô | Skill | Cost | CD | Loại | Power | Target | Hiệu ứng |
|---|---|---|---|---|---|---|---|
| 1 | Dark Bolt | 0 | 0 | Mag | 1.0 | Single | `SingleTap` |
| 2 | Raise Skeleton | 18 | 3 | Summon | — | Self | Gọi Skeleton (slot ATB riêng, 3 lượt) · `Charge` |
| 3 | Bone Spear | 16 | 1 | Mag | 1.5 | Single | `def_down` 50% · `SingleTap` |
| 4 | Soul Drain | 20 | 3 | Mag | 1.4 | AllEnemies | Hút 30% dmg thành HP cho chủ · `Charge` |
| 5 | **Legion of Bones** (Ult) | — | — | Summon | — | Self | Gọi 3 Skeleton cùng lúc, 4 lượt |

Passive: *Necromancy* — Skeleton chết → +10 SP cho chủ.

**18 hero còn lại (danh sách để sản xuất — số liệu ở CSV):**

| ID | Tên | Class | Element | Rarity |
|---|---|---|---|---|
| `hero_iron_bastion` | Iron Bastion | Vanguard | Earth | Rare |
| `hero_tide_warden` | Tide Warden | Vanguard | Water | Common |
| `hero_stormguard` | Stormguard | Vanguard | Wind | Legendary |
| `hero_blade_dancer` | Blade Dancer | Slayer | Wind | Epic |
| `hero_crimson_reaver` | Crimson Reaver | Slayer | Fire | Rare |
| `hero_stone_breaker` | Stone Breaker | Slayer | Earth | Common |
| `hero_pyromancer` | Pyromancer | Arcanist | Fire | Rare |
| `hero_terra_seer` | Terra Seer | Arcanist | Earth | Common |
| `hero_void_scholar` | Void Scholar | Arcanist | Dark | Legendary |
| `hero_grove_keeper` | Grove Keeper | Warden | Earth | Epic |
| `hero_moon_priestess` | Moon Priestess | Warden | Light | Legendary |
| `hero_spring_medic` | Spring Medic | Warden | Water | Common |
| `hero_night_stalker` | Night Stalker | Trickster | Dark | Epic |
| `hero_spark_runner` | Spark Runner | Trickster | Wind | Common |
| `hero_mirage_fox` | Mirage Fox | Trickster | Light | Legendary |
| `hero_beast_tamer` | Beast Tamer | Summoner | Earth | Rare |
| `hero_flame_binder` | Flame Binder | Summoner | Fire | Epic |
| `hero_star_weaver` | Star Weaver | Summoner | Light | Epic |

---

## 6. KẺ ĐỊCH & BOSS

### 6.1. Công thức scale stat địch

```
stat_final = stat_base × (1 + 0.085 × (stageLevel − 1)) × archetypeMult × difficultyMult

archetypeMult:  Trash 1.0 · Elite 1.9 · Boss 4.5
difficultyMult: Normal 1.0 · Hard 1.35 · Nightmare 1.8
```

### 6.2. Archetype kẻ địch (12 mẫu — mọi enemy đều thuộc 1 archetype)

| Archetype | Vai trò | Đặc điểm | Ví dụ |
|---|---|---|---|
| `Grunt` | Bia đỡ | HP thấp, đánh thường | Goblin, Slime |
| `Brute` | Damage cao | Chậm, đánh mạnh, Poise cao | Ogre, Golem |
| `Skirmisher` | Nhanh | SPD cao, đánh 2 lần | Wolf, Bat |
| `Archer` | Hàng sau | Nhắm hàng sau người chơi | Goblin Archer |
| `Healer` | Hỗ trợ | Hồi máu đồng minh — **ưu tiên giết** | Shaman |
| `Buffer` | Hỗ trợ | Buff ATK/DEF cho đồng minh | War Drummer |
| `Debuffer` | Quấy rối | Poison, Slow, Silence | Witch |
| `Caster` | AoE phép | Damage diện rộng, HP giấy | Mage |
| `Tank` | Chắn | Taunt, Shield, DEF cao | Shield Bearer |
| `Swarm` | Số đông | Xuất hiện 4–5 con, tự nhân đôi | Rat, Imp |
| `Bomber` | Cảm tử | Nổ khi chết gây AoE | Bomb Slime |
| `Elite` | Trùm nhỏ | 2 cơ chế kết hợp | Goblin Champion |

### 6.3. Bảng 8 boss v1.0

| Chương | Boss | Element | Phase | Cơ chế đặc trưng | Counterplay |
|---|---|---|---|---|---|
| 1 | **Alpha Wolf** | Wind | 2 | Gọi 2 sói con mỗi 3 lượt; Howl buff cả bầy | Giết sói con trước, dispel Howl |
| 1-E | Bandit Chief | Earth | 1 | Cướp vàng thưởng nếu không giết trong 8 lượt | Burst nhanh |
| 2 | **Goblin King** | Earth | 2 | Phase 2 toàn map `poison`; ngồi ngai được +50% DEF | Kéo khỏi ngai bằng Break |
| 2-E | Swamp Hydra | Water | 1 | Mọc lại đầu nếu không giết cả 3 đầu trong 1 lượt | AoE burst |
| 3 | **Lich** | Dark | 3 | Hồi sinh mọi enemy đã chết ở lượt 6, 12; `silence` toàn đội | Dispel, giữ Ultimate cho lượt 6 |
| 3-E | Bone Colossus | Earth | 1 | Poise 200, chỉ nhận damage khi Break | Arcanist phá Poise |
| 4 | **Magma Drake** | Fire | 2 | Enrage lượt 10; đòn Cinder Breath báo trước 3 lượt (one-shot nếu không Guard) | Guard đúng nhịp / shield |
| 5 | **Void King** | Dark | 3 | Phase 3 đảo ngược hồi máu thành damage; hút Ultimate gauge | Cleanse, dùng Ult sớm |

**Mọi boss phải có đủ 4 yếu tố:** Telegraph (báo trước ≥1 lượt) · ≥2 Counterplay · Escalation (Enrage) · Tell riêng (animation + SFX nhận diện được).

---

## 7. TRANG BỊ & VẬT PHẨM

### 7.1. Main stat theo slot (cố định)

| Slot | Main stat |
|---|---|
| Weapon | ATK (flat) |
| Armor | MaxHP (flat) |
| Helm | DEF (flat) |
| Boots | SPD (flat) |
| Ring | 1 trong {CRIT%, CRIT_DMG%, EFF_ACC} |
| Amulet | 1 trong {RES, MaxSP, LIFESTEAL%} |

### 7.2. Sub stat pool & khoảng giá trị (theo rarity)

| Sub stat | Common | Rare | Epic | Legendary | Mythic |
|---|---|---|---|---|---|
| ATK% | 2–4 | 4–7 | 6–10 | 9–14 | 12–18 |
| HP% | 3–5 | 5–8 | 7–12 | 11–16 | 14–20 |
| DEF% | 3–5 | 5–8 | 7–12 | 11–16 | 14–20 |
| SPD (flat) | 1–2 | 2–4 | 3–6 | 5–8 | 7–11 |
| CRIT% | 1–2 | 2–4 | 3–6 | 5–8 | 7–10 |
| CRIT_DMG% | 2–4 | 4–8 | 7–12 | 10–18 | 15–25 |
| RES | 2–4 | 4–7 | 6–10 | 9–14 | 12–18 |
| EFF_ACC | 2–4 | 4–7 | 6–10 | 9–14 | 12–18 |

Số sub stat khởi điểm: Common 1 · Rare 2 · Epic 2 · Legendary 3 · Mythic 4.

### 7.3. Enhance (+0 → +15)

| Mốc | Tỉ lệ thành công | Vàng | Đá | Hiệu quả |
|---|---|---|---|---|
| +1 → +3 | 100% | 200–600 | 1 | Main stat +10%/mốc |
| +4 → +6 | 100% | 900–1.800 | 2 | **+3: mở sub stat mới** |
| +7 → +9 | 100% | 2.400–4.000 | 3 | **+6: mở sub stat mới** |
| +10 → +11 | 100% | 5.000–7.000 | 5 | **+9: mở sub stat mới** |
| +12 | 70% | 9.000 | 8 | **+12: mở sub stat mới** |
| +13 | 55% | 12.000 | 10 | |
| +14 | 40% | 16.000 | 14 | |
| +15 | 25% | 22.000 | 20 | **+15: main stat ×1.5** |

Thất bại: **không mất đồ, không tụt level**, chỉ mất tài nguyên (thân thiện người chơi).

### 7.4. Set bonus (8 set v1.0)

| Set | 2 món | 4 món |
|---|---|---|
| **Ember** | +12% ATK | Đòn Perfect gây `burn` 3 lượt |
| **Bastion** | +15% DEF | Khi HP < 50%, nhận `shield` 20% MaxHP (1 lần/trận) |
| **Tempest** | +8 SPD | Hành động đầu tiên mỗi round → +20% dmg |
| **Assassin** | +10% CRIT | Crit gây thêm 15% damage đã gây dưới dạng `bleed` |
| **Sage** | +15% MaxSP | Skill tiêu SP hoàn lại 30% khi Perfect |
| **Guardian** | +12% MaxHP | Hồi 8% MaxHP mỗi khi kết thúc lượt |
| **Breaker** | +15% Poise damage | Break mục tiêu → toàn đội +15% ATK 2 lượt |
| **Vampire** | +8% LIFESTEAL | Giết địch → hồi 15% MaxHP |

### 7.5. Vật phẩm tiêu hao (mang tối đa 5 loại × 3 cái/trận)

| Item | Hiệu ứng | Giá |
|---|---|---|
| Potion | Hồi 35% MaxHP 1 hero | 200 vàng |
| Ether | Hồi 40 SP | 300 vàng |
| Antidote | Cleanse mọi DoT 1 hero | 250 vàng |
| Smoke Bomb | Escape 100% | 500 vàng |
| Revive Feather | Hồi sinh 40% HP | 1.500 vàng |
| Elemental Bomb | 2.0× dmg hệ + −20 Poise, AoE | 800 vàng |

---

## 8. TIẾN TRÌNH & BẢN ĐỒ NODE

### 8.1. Thuật toán sinh node map

Mỗi chương = 1 đồ thị có hướng, 3 tầng (`[1/3]`, `[2/3]`, `[3/3]`), 12–15 node.

```
GenerateMap(chapterDef, seed):
  1. Tạo 3 tầng; tầng i có floors[i] hàng (mặc định 4/5/5)
  2. Mỗi hàng có randomInt(2,3) node
  3. Nối node: mỗi node hàng r nối 1–2 node hàng r+1 gần nhất theo chỉ số
  4. Bảo đảm: mọi node đều có ≥1 đường vào và ≥1 đường ra (trừ start/boss)
  5. Gán loại node theo tỉ lệ + luật cứng:
       - Hàng cuối mỗi tầng: Rest hoặc Shop
       - Hàng cuối chương: BOSS (chỉ 1 node)
       - Không 2 Elite liên tiếp trên cùng một đường đi
       - Mỗi tầng có ≥1 Treasure
       - Hàng 1 luôn là Battle (dạy nhịp)
  6. Xác thực bằng BFS: tồn tại ít nhất 1 đường Start → Boss
  7. Nếu thất bại → tăng seed, lặp lại (tối đa 20 lần)
```

**Tỉ lệ loại node:**

| Loại | Tỉ lệ | Mô tả |
|---|---|---|
| `Battle` | 45% | Trận thường 3–5 địch |
| `Elite` | 15% | 2–3 địch mạnh, thưởng ×2.2 |
| `Treasure` | 10% | 1 trang bị đảm bảo ≥ Rare |
| `Event` | 12% | 2–3 lựa chọn có rủi ro |
| `Shop` | 8% | Mua item/trang bị/xoá thẻ |
| `Rest` | 8% | Hồi 30% HP **hoặc** +1 skill level |
| `Mystery` | 2% | Ngẫu nhiên trong các loại trên |

Người chơi **thấy trước 2 hàng** → quyết định có ý nghĩa.

### 8.2. Bảng 5 chương v1.0

| Ch | Biome | Node | Stage level | Boss | Cơ chế mới dạy | Enemy mới |
|---|---|---|---|---|---|---|
| 1 | Đồng Cỏ | 12 | 1–8 | Alpha Wolf | ATB, Action Command, hệ nguyên tố | 10 |
| 2 | Đầm Lầy | 13 | 9–18 | Goblin King | DoT, Break, hàng trước/sau | 12 |
| 3 | Hầm Mộ | 14 | 19–30 | Lich | Hồi sinh, Silence, dispel | 12 |
| 4 | Núi Lửa | 15 | 31–44 | Magma Drake | Enrage timer, Guard đúng nhịp | 13 |
| 5 | Thành Trì Hư Vô | 15 | 45–60 | Void King | Tổng hợp tất cả | 13 |

### 8.3. Endgame

| Chế độ | Chu kỳ | Nội dung | Thưởng |
|---|---|---|---|
| **Dungeon Vàng** | T2, T5, CN | 10 tầng | Vàng |
| **Dungeon EXP** | T3, T6, CN | 10 tầng | Sách EXP |
| **Dungeon Vật liệu** | T4, T7, CN | 10 tầng | Essence I/II/III, Core |
| **Dungeon Đá** | T2, T5, T7 | 10 tầng | Đá cường hóa |
| **Tháp Vô Tận** | Reset tuần | 100 tầng, HP không hồi giữa tầng | Gem, trang bị Mythic |
| **Trial Boss** | Tuần | Boss HP cực cao, xếp hạng theo tổng damage (**Damage Meter** như `Game_1.jpg`) | Gem, mảnh Legendary |
| **Arena** (v1.1) | Mùa 14 ngày | PvP async với snapshot đội hình do AI điều khiển | Honor |

---

## 9. KINH TẾ & GACHA

### 9.1. Bảng tiền tệ

| Loại | Faucet | Sink | Cap | Icon `Game_1.jpg` |
|---|---|---|---|---|
| **Vàng** | Mọi trận, Dungeon Vàng, bán đồ | Level up, Enhance, Reforge, Shop | ∞ | vàng tròn |
| **Gem** | Quest, thành tựu, chương mới, IAP | Summon, Energy, mở slot, hồi sinh | ∞ | lục giác tím |
| **Energy** | +1/6 phút (cap 120), item, level up | Battle −6, Elite −8, Dungeon −10, Boss −12 | 120 | tia sét |
| **Summon Ticket** | Sự kiện, quest tuần | 1 lần summon | 999 | vé |
| **Mảnh Hero** | Trùng hero, shop mảnh, Trial | Ascend | ∞ | mảnh vỡ |
| **Essence I/II/III** | Dungeon Vật liệu | Ascend | ∞ | lọ |
| **Core** | Boss chương, Tháp | Ascend ★5/★6 | ∞ | lõi |
| **Đá cường hóa** | Dungeon Đá | Enhance | ∞ | đá |
| **Honor** | Arena (v1.1) | Shop Honor | ∞ | huy hiệu |

### 9.2. Ngân sách Energy (rất quan trọng cho retention)

```
Energy hồi/ngày  = 24×60/6 = 240
+ Cap ban đầu    = 120
+ Thưởng ngày    = 60 (3 lần nhận miễn phí)
Tổng F2P/ngày    ≈ 420 energy ≈ 60 trận thường ≈ 45–60 phút chơi
```
Đây là **thời lượng chơi mục tiêu mỗi ngày** cho người F2P. Nếu playtest cho thấy người chơi hết energy trong 20 phút → tăng cap hoặc giảm cost.

### 9.3. Gacha — tỉ lệ & thuật toán pity

| Bậc | Tỉ lệ cơ bản | Soft pity | Hard pity |
|---|---|---|---|
| Legendary | 1.5% | Từ lần 45: +2%/lần | **Bảo đảm ở lần 60** |
| Epic | 12.0% | — | Bảo đảm ở lần 10 |
| Rare | 36.5% | — | — |
| Common | 50.0% | — | — |

```csharp
Rarity Roll(GachaState s, IRandomSource rng) {
    s.PullsSinceLegendary++; s.PullsSinceEpic++;
    if (s.PullsSinceLegendary >= 60) return Reset(s, Rarity.Legendary);

    float legRate = 0.015f;
    if (s.PullsSinceLegendary >= 45)
        legRate += 0.02f * (s.PullsSinceLegendary - 44);

    float r = rng.NextFloat();
    if (r < legRate)                       return Reset(s, Rarity.Legendary);
    if (s.PullsSinceEpic >= 10)            return ResetEpic(s, Rarity.Epic);
    if (r < legRate + 0.12f)               return ResetEpic(s, Rarity.Epic);
    if (r < legRate + 0.12f + 0.365f)      return Rarity.Rare;
    return Rarity.Common;
}
```

**Giá:** 300 Gem/lần · 2.700 Gem/10 lần (−10%) · 10-pull đảm bảo ≥1 Epic.
**Bắt buộc:** hiển thị tỉ lệ trong game, lưu lịch sử 100 lần gần nhất, có unit test `GachaPityTests` chứng minh tỉ lệ khớp ±0.05% trên 1 triệu roll.

### 9.4. Gói IAP & Battle Pass

| Gói | Giá (USD) | Nội dung |
|---|---|---|
| Gem S | 0.99 | 300 Gem |
| Gem M | 4.99 | 1.600 Gem (+7%) |
| Gem L | 19.99 | 7.000 Gem (+17%) |
| Gem XL | 49.99 | 18.500 Gem (+23%) |
| Gem XXL | 99.99 | 39.000 Gem (+30%) |
| Starter Pack (1 lần) | 2.99 | 1 hero Epic + 1.000 Gem + 50k vàng |
| Battle Pass (30 ngày) | 9.99 | Premium track: 3.000 Gem + trang bị + skin |

**Nguyên tắc:** không bao giờ bán **sức mạnh tuyệt đối**, chỉ bán **tốc độ**. Mọi hero Legendary đều có đường lấy F2P (mảnh từ Trial/Shop).

### 9.5. Bản PC (Steam)
Cùng codebase, bật cờ `MonetizationProfile = PremiumPC`:
- **Bỏ Energy** (chơi không giới hạn)
- **Bỏ Gacha** → hero mở khóa qua tiến trình chương + Tháp
- Bỏ quảng cáo, bỏ IAP
- Giá 14.99 USD

---

## 10. UI/UX — ĐẶC TẢ MÀN HÌNH

### 10.1. Danh sách 23 màn hình

| Nhóm | Màn hình | ScreenId | Ghi chú |
|---|---|---|---|
| Khởi động | Splash | `Splash` | Logo, 2 giây |
| | Title | `Title` | Nhấn để bắt đầu |
| | Loading | `Loading` | Overlay, có mẹo chơi |
| Meta (bottom nav) | Home / Growth | `Home` | Mặc định |
| | Hero List | `HeroList` | Lọc/sắp xếp |
| | Summon | `Summon` | Gacha |
| | Dungeon | `Dungeon` | 4 hầm + Tháp + Trial |
| | Arena | `Arena` | v1.1, v1.0 hiện "Sắp có" |
| | Shop | `Shop` | Gói + refresh ngày |
| Phụ | Hero Detail | `HeroDetail` | Stat/skill/đồ/lore |
| | Equipment | `Equipment` | Gắn/tháo/so sánh |
| | Enhance | `Enhance` | +0→+15 |
| | Inventory | `Inventory` | Lọc, bán hàng loạt |
| | Formation | `Formation` | 8 preset |
| | Quest | `Quest` | Ngày/tuần/chuỗi |
| | Achievement | `Achievement` | |
| | Collection | `Collection` | Codex hero/enemy/item |
| | Mail | `Mail` | Đền bù LiveOps |
| | Settings | `Settings` | Gồm hiệu chỉnh Action Command |
| Trận | Chapter Select | `ChapterSelect` | 5 chương |
| | Node Map | `NodeMap` | Bản đồ phân nhánh |
| | Pre-Battle | `PreBattle` | Chọn 4 hero + đội hình |
| | Battle HUD | `BattleHud` | Trong scene Battle |
| | Result | `Result` | Thắng: thưởng |
| | Defeat | `Defeat` | Thua: 3 lựa chọn |

### 10.2. Battle HUD — đặc tả từng thành phần

**Landscape (ref 960×540) — bố cục theo `image_UI.jpg`:**

```
┌────────────────────────────────────────────────────────────────┐
│ ┌HeroPanel──┐                                    ┌EnemyPanel─┐ │
│ │ Portrait  │         SÂN KHẤU CHIẾN ĐẤU        │ Tên/LV     │ │
│ │ NAME      │      P2 P0        E0 E1 E2        │ HP bar     │ │
│ │ ▬HP▬      │      P3 P1        E3 E4           │ Poise bar  │ │
│ │ ▬SP▬      │                                    │ ATK/DEF    │ │
│ │ LV10      │  ──── TurnOrderBar (8 ô) ────      │ Element    │ │
│ │ [buffs]   │                                    │ Intent     │ │
│ │ ZONE[1/3] │                                    └────────────┘ │
│ └───────────┘                                                   │
├────────────────────────────────────────────────────────────────┤
│ ┌ItemBar─┐  ┌──── SKILL GRID 5×3 ────┐   ┌── STATS / EQ ────┐ │
│ │ ▢ ▢ ▢  │  │ ◻ ◻ ◻ ◻ ◻(Ult)         │   │ HP  100/1000000  │ │
│ │        │  │ ◻ ◻ ◻ ◻ ◻              │   │ ATK 5   CON 11   │ │
│ │        │  │ ◻ ◻ ◻ ◻ ◻              │   │ DEF 10  STR 3    │ │
│ └────────┘  └────────────────────────┘   └──────────────────┘ │
│                    ▓▓ END TURN ▓▓          [Auto][×1][⚙]      │
└────────────────────────────────────────────────────────────────┘
```

**Portrait (ref 540×960):**
```
[TopBar: tiền tệ · stage 4-3 · ☰]
[EnemyRow: E0..E4 + HP/Poise bar nổi]
[SÂN KHẤU (chiếm 34% chiều cao)]
[PartyRow: P0..P3 + HP/SP bar]
[TurnOrderBar (8 ô, ngang)]
[HeroPanel thu gọn: portrait + HP/SP + buff]
[SKILL GRID 5×3 (chiếm 30% chiều cao dưới)]
[END TURN]                     [Auto][×1] (nổi bên phải)
```

**Bảng thành phần bắt buộc của Battle HUD:**

| Thành phần | Dữ liệu hiển thị | Cập nhật khi |
|---|---|---|
| `HeroPanelView` | Portrait, tên, HP/MaxHP, SP/MaxSP, LV, ≤6 icon buff | `DamageDealt`, `HealApplied`, `StatusApplied/Expired`, `TurnStarted` |
| `EnemyPanelView` | Tên, LV, HP bar, **Poise bar**, ATK/DEF, element, intent | Như trên + `PoiseBroken`, `IntentChanged` |
| `TurnOrderBar` | 8 chân dung nhỏ theo thứ tự | `TurnEnded`, `StatusApplied` (nếu ảnh hưởng SPD) |
| `SkillGridView` | 15 ô, 8 trạng thái (§5.5) | `TurnStarted`, `SpChanged`, `CooldownChanged` |
| `ItemSlotBar` | 3 ô tiêu hao + số lượng | Dùng item |
| `StatsEqPanel` | Bảng stat đầy đủ + 6 slot trang bị | `TurnStarted`, đổi hero đang chọn |
| `EndTurnButton` | Bật/tắt theo state | `AwaitInput` |
| `AutoSpeedToggle` | Auto ON/OFF, ×1/×2/×3 | Người chơi bấm |
| `DamageMeterView` | Tổng damage mỗi hero (như `Game_1.jpg`) | `DamageDealt` |
| `ZoneIndicator` | `SWAMPS [1/3]` | Vào trận |
| `ActionCommandUI` | Vòng nhịp / combo / charge / guard | `CommandWindowOpened` |
| `TargetHighlighter` | Viền mục tiêu, auto-suggest | Chọn skill |
| `FloatingTextLayer` | Damage number, MISS, RESIST, PERFECT | Mọi event damage |

### 10.3. Navigation stack (UI)

```
UIScreenStack (LIFO)
  Push(screenId, data)  → màn cũ SetInteractable(false), không destroy
  Pop()                 → quay lại, khôi phục state
  Replace(screenId)     → thay màn hiện tại
  PopToRoot()           → về Home
Nút Back (Android) / Esc (PC) → Pop() hoặc mở ConfirmDialog nếu ở root
```

**Luật:** không bao giờ Push quá 5 tầng · mọi Push có transition 200 ms · `Loading` và `ConfirmDialog` là **overlay**, không nằm trong stack.

### 10.4. Responsive — quy tắc kỹ thuật

```csharp
// Gắn trên mọi panel gốc của mỗi màn hình
public class LayoutProfileSwitcher : MonoBehaviour {
    [SerializeField] RectTransformPreset portrait;   // anchorMin/Max, pivot, sizeDelta, anchoredPos
    [SerializeField] RectTransformPreset landscape;
    [SerializeField] bool   alsoSwitchLayoutGroup;   // Horizontal ↔ Vertical
    // Nghe ScreenOrientationService.OnOrientationChanged → Apply()
}
```

| Quy tắc | Chi tiết |
|---|---|
| Breakpoint | `aspect = w/h`; `< 1.0` → Portrait, `≥ 1.0` → Landscape |
| Prefab | **1 prefab duy nhất**, 2 preset — cấm nhân đôi prefab |
| Canvas Scaler | `Scale With Screen Size`, ref 540×960 (P) / 960×540 (L), Match 0.5 |
| Safe area | `SafeAreaFitter` trên panel gốc mỗi màn |
| Vùng chạm | ≥ 44×44 dp; ô Skill Grid ≥ 56 dp |
| Tỉ lệ test bắt buộc | 9:16, 3:4, 16:9, 20:9, 21:9 |

### 10.5. Bảng "Juice" — phản hồi bắt buộc

| Sự kiện | Hình ảnh | Âm thanh | Rung |
|---|---|---|---|
| Chạm nút/skill | Scale 0.92 → 1.0 trong 60 ms | `ui_tick` | — |
| Gây damage | Hit-stop 60 ms · shake 3 px · sprite flash trắng 2 frame · damage number bay lên | `hit_{element}` | Nhẹ |
| Crit | Damage number ×1.6 màu cam · zoom camera 1.05 trong 150 ms | `crit` | Trung bình |
| **Perfect** | Chớp vàng toàn màn · chữ "PERFECT!" · shake 6 px | `perfect` (riêng biệt) | Trung bình |
| **Break** | Freeze 120 ms · chớp trắng · shatter VFX · chữ "BREAK!" | `break_glass` | Mạnh |
| Miss / Né | Chữ "MISS" xám · sprite dịch ngang | `whoosh` | — |
| Kháng debuff | Chữ "RESIST!" xanh | `resist` | — |
| Unit chết | Dissolve shader pixel bay lên 500 ms | `death_{type}` | Nhẹ |
| Ultimate | Cutscene 1.5 giây · ducking BGM · vignette | `ultimate_{hero}` | Mạnh |
| Lên level | Panel bung · confetti pixel | `levelup` | — |
| Nhận Legendary | Chớp cầu vồng · slow-mo mở thẻ | `legendary` | Mạnh |

> **Luật:** không có tương tác nào được phép "câm" — mọi hành động phải có ít nhất 1 phản hồi hình + 1 âm thanh.

### 10.6. Red Dot (chấm đỏ thông báo) — theo `Game_1.jpg`

Cây phân cấp: `Root → BottomNav.{Hero,Summon,Dungeon,Shop} → SubTab → Item`
`RedDotService.SetDirty(path)` → lan lên cha tự động. Quy tắc: chỉ hiện khi có **hành động miễn phí khả thi** (không hiện chỉ vì "có đồ bán").

### 10.7. Trợ năng
Tắt Action Command · tắt screen shake · tắt rung · chế độ mù màu (icon nguyên tố có **hình dạng khác nhau**, không chỉ khác màu) · scale chữ 100/125/150% · tốc độ text 1×/2×/tức thì · hiển thị số damage lớn.

### 10.8. Tutorial (5 bước, dạy trong trận thật)

| Bước | Dạy | Trigger | Khoá thao tác khác |
|---|---|---|---|
| 1 | Chọn skill từ Skill Grid | Trận 1, lượt 1 | Chỉ cho bấm ô skill 1 |
| 2 | Action Command | Ngay sau bước 1 | Slow-mo cửa sổ nhịp |
| 3 | Hệ khắc chế | Trận 1, lượt 3 | Highlight địch bị khắc |
| 4 | Break | Trận 2 (Elite yếu) | Highlight Poise bar |
| 5 | Ultimate | Trận 3 | Highlight ô 5 |

Có nút **Bỏ qua tutorial** cho người chơi lại (lưu cờ `TutorialCompleted`).

---

## 11. KIẾN TRÚC KỸ THUẬT

### 11.1. Năm nguyên tắc nền tảng

1. **Simulation ≠ Presentation** — `Game.Combat` là C# thuần, không `MonoBehaviour`, không Unity API.
2. **Deterministic** — cùng seed + cùng chuỗi intent = cùng kết quả, 100%.
3. **Data-driven** — hero/skill/enemy/item là dữ liệu (SO sinh từ CSV), không phải code.
4. **Event-driven presentation** — Simulation phát `CombatEvent`, View đọc và diễn.
5. **Server-ready** — mọi truy cập dữ liệu người chơi qua interface.

### 11.2. Đồ thị assembly (bắt buộc, có test kiểm tra)

```
Game.Core        ← không phụ thuộc gì (trừ UnityEngine.CoreModule)
   ↑
Game.Data        ← Core
   ↑
Game.Combat      ← Core, Data           ⛔ CẤM: UI, CombatView, UnityEngine.Random, Time
   ↑
Game.CombatView  ← Core, Data, Combat, Services
Game.Meta        ← Core, Data, Combat, Services
Game.UI          ← Core, Data, Meta, Services, Combat(read-only)
Game.Services    ← Core, Data
Game.Bootstrap   ← tất cả (composition root)
```

Chi tiết từng file: xem **[structure.md](structure.md)**.

### 11.3. Kiến trúc trận đấu

```
 Input (touch/chuột/gamepad)
        │
        ▼
 BattleController (MonoBehaviour)
        │ ActionIntent { actorId, skillId, targetIds[], commandGrade }
        ▼
 CombatSimulation (C# thuần)  ── IRandomSource(seed)
   ├─ TurnScheduler          (ATB)
   ├─ ActionResolver         (pipeline 14 bước §4.3)
   ├─ DamageCalculator       (§4.6)
   ├─ StatusProcessor        (§4.11)
   ├─ PoiseSystem            (§4.9)
   ├─ TargetSelector         (§4.12)
   ├─ AIController           (§4.13)
   └─ UltimateGauge
        │ Queue<CombatEvent>
        ▼
 CombatPresenter (đọc & diễn tuần tự, có tốc độ phát)
   ├─ UnitView / UnitAnimator
   ├─ VfxPlayer / HitStop / ScreenShake / CameraDirector
   ├─ DamageNumberPool
   └─ BattleHudScreen (cập nhật UI)
```

### 11.4. Danh mục `CombatEvent` (22 loại — bảng đầy đủ)

| Event | Payload | Ai nghe |
|---|---|---|
| `BattleInitialized` | units, config | HUD, Presenter |
| `BattleStarted` | — | CameraDirector, Audio |
| `RoundStarted` | roundNumber | HUD |
| `TurnStarted` | actorId, atb | HUD, SkillGrid, TurnOrderBar |
| `SpChanged` | unitId, old, new | HeroPanel, SkillGrid |
| `CooldownChanged` | unitId, skillId, cd | SkillGrid |
| `ActionRequested` | actorId, isPlayer | BattleController |
| `ActionDeclared` | actorId, skillId, targets | Presenter (chạy animation) |
| `CommandWindowOpened` | type, durationMs | ActionCommandUI |
| `CommandWindowClosed` | grade | ActionCommandUI, FloatingText |
| `DamageDealt` | src, dst, amount, isCrit, isMiss, element | UnitView, FloatingText, DamageMeter |
| `ShieldAbsorbed` | dst, amount, remaining | UnitView |
| `ShieldBroken` | dst | VfxPlayer |
| `HealApplied` | src, dst, amount | FloatingText, HeroPanel |
| `StatusApplied` | dst, statusId, stacks, duration | UnitStatusIcons |
| `StatusResisted` | dst, statusId | FloatingText |
| `StatusTicked` | dst, statusId, damage | FloatingText |
| `StatusExpired` | dst, statusId | UnitStatusIcons |
| `PoiseDamaged` | dst, amount, remaining | PoiseBar |
| `PoiseBroken` | dst | BreakEffect, HitStop |
| `UnitDied` | unitId, killerId | UnitView, TurnOrderBar |
| `UnitRevived` | unitId, hp | UnitView |
| `MinionSummoned` | ownerId, minionId | Presenter |
| `PhaseChanged` | bossId, phase | CameraDirector, Audio |
| `UltimateCharged` | value | SkillGrid |
| `TurnEnded` | actorId | HUD |
| `BattleEnded` | result, stats | ResultScreen |

> Khi thêm event mới: cập nhật bảng này **và** [object-map.md](object-map.md) §5.

### 11.5. Mô hình dữ liệu — ScriptableObject

```csharp
[CreateAssetMenu(menuName = "Game/Hero")]
public class HeroDefinition : ScriptableObject {
    public string           Id;               // "hero_ember_knight"
    public LocalizedKey     NameKey, LoreKey;
    public HeroClass        Class;
    public Element          Element;
    public Rarity           Rarity;
    public PrimaryStats     BaseStats;        // STR/CON/INT/DEX/AUR/LUK
    public PrimaryStats     GrowthPerLevel;
    public SkillDefinition[] Skills;          // đúng 5 (Basic..Ultimate)
    public PassiveDefinition Passive;
    public PassiveDefinition Awakening;
    public int              PoiseMax;
    public AssetReferenceSprite  Portrait;
    public AssetReferenceGameObject BattlePrefab;
}

[CreateAssetMenu(menuName = "Game/Skill")]
public class SkillDefinition : ScriptableObject {
    public string           Id;
    public LocalizedKey     NameKey, DescKey;
    public SkillType        Type;             // Physical/Magical/Heal/Support/Summon
    public DamageType       DamageType;
    public Element          Element;
    public TargetMode       Target;
    public int              SpCost, Cooldown, ExtraAtbCost;
    public float            PowerMultiplier, FlatDamage, DefIgnore;
    public int              HitCount;
    public int              PoiseDamage;
    public bool             IsAoe, IsBreaker;
    public ActionCommandType CommandType;
    public StatusApplication[] Applies;       // {statusId, chance, duration, stacks, target}
    public AssetReferenceSprite Icon;
    public string           VfxKey, SfxKey, AnimTrigger;
}
```

**Toàn bộ 24 loại SO:** xem [structure.md](structure.md) §3.2.

**Pipeline dữ liệu:**
```
Google Sheet / CSV  ──(Tools/Import Game Data)──▶  ScriptableObject  ──▶  GameDatabase (Addressables)
                     └─ DataValidator: kiểm tra id trùng, tham chiếu chết, số ngoài khoảng
```

### 11.6. Save schema (JSON v1)

```jsonc
{
  "version": 1,
  "playerId": "uuid",
  "createdAtUtc": "2026-08-07T10:00:00Z",
  "lastSavedUtc": "2026-08-07T12:31:00Z",
  "profile": { "name": "Hero", "level": 12, "exp": 3400, "avatarId": "av_01" },
  "wallet": { "gold": 125000, "gem": 3200, "energy": 88, "energyLastTickUtc": "...",
              "ticket": 4, "honor": 0,
              "materials": { "essence_1": 40, "core": 3, "enhance_stone": 120 },
              "heroShards": { "hero_shadow_fang": 25 } },
  "heroes": [
    { "uid": "h_0001", "defId": "hero_ember_knight", "level": 34, "exp": 1200,
      "star": 4, "awakened": false,
      "skillLevels": [3,2,2,1,1],
      "equipped": { "weapon": "e_0012", "armor": "e_0034", "helm": null,
                    "boots": null, "ring": null, "amulet": null } }
  ],
  "equipment": [
    { "uid": "e_0012", "defId": "eq_sword_ember", "rarity": "Epic", "level": 9,
      "mainStat": { "type": "Atk", "value": 210 },
      "subStats": [ { "type": "CritPct", "value": 5 }, { "type": "AtkPct", "value": 8 } ],
      "setId": "set_ember", "locked": true }
  ],
  "inventory": { "potion": 12, "ether": 6, "revive_feather": 2 },
  "progress": { "chapterUnlocked": 3, "stageCleared": { "1": 12, "2": 13, "3": 5 },
                "towerFloor": 24, "tutorialCompleted": true },
  "run": { "active": true, "chapterId": 3, "seed": 918273645,
           "mapNodes": [...], "currentNodeId": 7, "teamUids": ["h_0001","h_0007"],
           "battleSnapshot": null },
  "gacha": { "pullsSinceLegendary": 23, "pullsSinceEpic": 4, "history": [...] },
  "quests": { "daily": { "q_daily_battle3": 2 }, "weekly": {...}, "lastResetUtc": "..." },
  "settings": { "bgm": 0.7, "sfx": 0.9, "actionCommandEnabled": true,
                "actionCommandOffsetMs": -35, "screenShake": true,
                "autoBattle": false, "battleSpeed": 1, "language": "vi",
                "textScale": 1.0, "colorblindMode": false },
  "stats": { "battlesWon": 214, "perfectHits": 1892, "breaksTriggered": 331 },
  "checksum": "hmac-sha256..."
}
```

**Luật save:**
- Ghi atomic: `save.tmp` → flush → rename `save.json` (chống hỏng khi mất điện).
- `SaveMigrationRunner`: chạy tuần tự `ISaveMigration` từ version cũ lên version hiện tại.
- Auto-save: sau mỗi trận · mỗi giao dịch tiền tệ · `OnApplicationPause` · mỗi 60 giây.
- Backup: giữ 1 bản `save.bak` của lần lưu trước.

### 11.7. Bảng service

| Service | Interface | v1 impl | Ghi chú |
|---|---|---|---|
| Lưu game | `IPlayerRepository` | `LocalPlayerRepository` | v2 → `RemotePlayerRepository` |
| Âm thanh | `IAudioService` | `AudioService` | AudioMixer 4 group + pool |
| Asset | `IAssetService` | `AddressableAssetService` | Load/Release theo scope màn hình |
| Scene | `ISceneFlowService` | `SceneFlowService` | Additive + loading screen |
| Ngôn ngữ | `ILocalizationService` | `LocalizationService` | CSV key→value, VI/EN |
| Input | `IInputService` | `InputService` | Trừu tượng touch/chuột/gamepad |
| Đồng hồ | `IGameClock` | `SystemGameClock` | v2 → server time |
| Kinh tế | `IEconomyService` | `EconomyService` | **Mọi** giao dịch đi qua đây |
| Analytics | `IAnalyticsService` | `NullAnalyticsService` | Bật ở P8 |
| Quảng cáo | `IAdsService` | `NullAdsService` | |
| IAP | `IStoreService` | `NullStoreService` | |
| Remote config | `IRemoteConfigService` | `LocalRemoteConfig` | Hằng số cân bằng |
| Object pool | `IPoolService` | `PoolService` | Damage number, VFX |
| Cài đặt | `ISettingsService` | `SettingsService` | |

Đăng ký tại **composition root** (`ServiceInstaller` trong scene `Boot`). Cấm singleton rải rác.

### 11.8. Object pooling (bắt buộc để đạt 0 GC)

| Đối tượng | Pool size ban đầu | Grow |
|---|---|---|
| `DamageNumber` | 30 | ✔ |
| `VfxInstance` | 20/loại | ✔ |
| `StatusIcon` | 40 | ✔ |
| `CombatEvent` | struct, không cấp phát | — |
| `UnitView` | 9 (4 hero + 5 địch) | ✘ |
| Cell của list UI | 20/list | ✔ |

### 11.9. Nhóm Addressables

| Nhóm | Nội dung | Load khi |
|---|---|---|
| `Core` | UI atlas, font, SFX chung | Boot (giữ mãi) |
| `Meta` | UI meta, portrait | Vào Meta scene |
| `Battle_Common` | HUD, VFX chung, damage number | Vào Battle scene |
| `Biome_{n}` | Tileset, background, BGM biome | Vào chương n |
| `Heroes` | Prefab + sprite hero (label riêng từng hero) | Khi hero vào đội |
| `Enemies_{chapter}` | Prefab enemy theo chương | Vào trận chương đó |

Giải phóng bằng `IAssetService.ReleaseScope(scopeId)` khi rời màn hình.

### 11.10. Chiến lược kiểm thử

| Loại | File | Mục tiêu |
|---|---|---|
| Unit | `DamageCalculatorTests` | 25 case: giáp, hệ, crit, grade, break, row |
| Unit | `TurnSchedulerTests` | Thứ tự ổn định, haste/slow, preview 8 lượt |
| Unit | `StatusProcessorTests` | 22 status × (apply/tick/expire/stack/resist) |
| Unit | `PoiseSystemTests` | Trừ, break, hồi, kéo dài debuff |
| Unit | `TargetSelectorTests` | 11 TargetMode + taunt override |
| Unit | `EdgeCaseTests` | **24 case ở §4.14 — mỗi case 1 test** |
| Unit | `LootRollerTests` · `GachaPityTests` | Tỉ lệ khớp ±0.05% / 1 triệu roll |
| Unit | `EnhanceSystemTests` · `SaveMigrationTests` | |
| Unit | `NodeMapGeneratorTests` | 10.000 seed → luôn có đường Start→Boss |
| Property | `DeterminismTests` | Cùng seed 2 lần → chuỗi event giống hệt |
| Fuzz | `FuzzBattleTests` | 10.000 trận ngẫu nhiên: 0 exception, ≤200 lượt |
| Golden | `GoldenScenarioTests` | 20 kịch bản cố định, so log |
| Architecture | `AssemblyRuleTests` | `Game.Combat` không ref UI/Random/Time |
| PlayMode | `BootFlowTests` · `BattleFlowTests` · `UIStackTests` | |
| Harness | `BalanceHarnessWindow` | 1000 trận/stage → CSV win-rate/TTK |

**Ngưỡng:** bao phủ `Game.Combat` ≥ 80%, mọi test xanh trước khi merge.

### 11.11. Ngân sách hiệu năng

| Chỉ số | Mobile tầm trung | PC |
|---|---|---|
| FPS | 60 (fallback 30) | 60+ |
| Draw call trong trận | ≤ 120 | ≤ 200 |
| **GC alloc/frame khi chiến đấu** | **0 B** | 0 B |
| RAM | ≤ 700 MB | — |
| Boot → Home | ≤ 6 s | ≤ 4 s |
| Vào trận (nhấn → chơi được) | ≤ 2.5 s | ≤ 1.5 s |
| Build size | ≤ 150 MB (AAB) | — |

---

## 12. ÂM THANH

| Loại | SL | Danh sách |
|---|---|---|
| BGM | 11 | menu, 6 biome, boss_normal, boss_final, victory, defeat |
| SFX chiến đấu | ~60 | hit_{6 element}, crit, perfect, good, miss, break_glass, resist, whoosh, death_{4 type}, heal, shield, buff, debuff, summon, ultimate_{6 class} |
| SFX UI | ~30 | tick, confirm, cancel, tab, error, levelup, reward, legendary, coin, page |
| SFX môi trường | ~15 | wind, water drip, fire crackle,... |
| Voice | 0 (v1) | — |

**Mixer:** Master → {BGM, SFX, UI, Ambient}. Ducking BGM −6 dB khi Ultimate.
**Format:** BGM `.ogg` streaming · SFX `.wav` → Compressed In Memory, force mono.
**Quy ước tên:** `sfx_{nhóm}_{tên}.wav` — VD `sfx_battle_hit_fire.wav`.

---

## 13. BẢN ĐỊA HÓA

- Ngôn ngữ v1: **VI + EN**. Cấu trúc sẵn cho ZH-TW, KO, JA.
- **Quy ước key:** `{màn}.{nhóm}.{tên}` — VD `battle.button.end_turn`, `hero.ember_knight.name`, `skill.blazing_bash.desc`.
- Cấm hard-code chuỗi trong code/prefab — có script `LocalizationScanner` quét và báo lỗi ở CI.
- Font pixel phải đủ dấu tiếng Việt (xác minh tuần 1 — rủi ro R6).
- Text UI chịu được chuỗi dài **1.6×** — test bằng pseudo-locale sinh tự động.

---

## 14. ANALYTICS

| Event | Tham số |
|---|---|
| `session_start` / `session_end` | duration, screen_count |
| `tutorial_step` | step, completed |
| `stage_start` | chapter, node_id, node_type, team_power |
| `stage_complete` | turns, hp_remaining_pct, perfect_count, break_count |
| `stage_fail` | turns, killer_enemy_id |
| `hero_levelup` / `hero_ascend` | hero_id, from, to |
| `summon` | pool_id, count, results[] |
| `currency_change` | type, delta, reason, balance_after |
| `iap_purchase` | sku, price, currency |
| `action_command_result` | grade, skill_id, latency_ms |
| `auto_battle_toggle` | on/off, chapter |
| `screen_view` | screen_id, duration |

**North Star:** *số trận có ≥1 Perfect / DAU* ≥ 3.5.
**Cảnh báo cân bằng:** stage có tỉ lệ thua > 45% hoặc < 5% → cần chỉnh.

---

## 15. RỦI RO

| # | Rủi ro | Mức | Đối sách |
|---|---|---|---|
| R1 | Action Command khó chịu trên mobile (độ trễ) | **Cao** | §4.8.3 hiệu chỉnh độ trễ; nới cửa sổ mobile; cho tắt; đo latency thật tuần 5 |
| R2 | Phạm vi phình (24 hero × 5 skill = 120 skill) | **Cao** | Khoá số lượng ở P0; template skill tái sử dụng; chỉ thêm hero sau khi 6 hero mẫu chơi tốt |
| R3 | Responsive 2 hướng quá tải đội UI | TB | 1 prefab + 2 preset; khoá design system sớm |
| R4 | Cân bằng lệch, phát hiện muộn | TB | Balance harness từ P2, chạy nightly CI |
| R5 | Content pipeline thủ công gây nghẽn | TB | CSV importer + DataValidator ngay P0 |
| R6 | Font pixel thiếu dấu tiếng Việt | Thấp/chặn | Xác minh tuần 1 |
| R7 | Chính sách store về gacha | TB | Công khai tỉ lệ; bản PC bỏ gacha |
| R8 | Deterministic bị phá âm thầm | **Cao** | `AssemblyRuleTests` + `DeterminismTests` chạy mỗi push |
| R9 | Save hỏng / mất dữ liệu người chơi | **Cao** | Ghi atomic + backup + migration test |
| R10 | Quên cập nhật logic khi thêm nội dung | **Cao** | **[object-map.md](object-map.md) §6 — ma trận checklist bắt buộc** |

---

## 16. DEFINITION OF DONE

Một tính năng **xong** khi đủ 10 điều:

1. Logic có unit test (thuộc `Game.Combat` → bao phủ ≥ 80%).
2. Chạy đúng ở **cả Portrait và Landscape**.
3. Không lỗi/cảnh báo trong Console.
4. 0 GC allocation trong vòng lặp thường xuyên (kiểm bằng Profiler).
5. Mọi chuỗi qua localization (VI + EN).
6. Có phản hồi hình + âm thanh (không tương tác nào "câm").
7. Dữ liệu ở ScriptableObject/CSV, không hard-code.
8. **Đã cập nhật [object-map.md](object-map.md) và [structure.md](structure.md).**
9. Đã chạy qua checklist tương ứng ở [object-map.md](object-map.md) §6.
10. Đã chơi thử trên thiết bị Android thật ít nhất 1 lần.

---

## 17. QUYẾT ĐỊNH KỸ THUẬT

| Chủ đề | Chọn | Lý do |
|---|---|---|
| UI framework | **uGUI** cho game · UI Toolkit cho editor tool | uGUI mạnh animation pixel, cộng đồng lớn |
| DI | Constructor injection + 1 `ServiceLocator` | Nhẹ, dễ đọc; thêm VContainer sau nếu cần |
| Asset loading | **Addressables** | Patch nội dung, giảm RAM |
| Async | `Awaitable` của Unity 6 | Có sẵn, không thêm dependency |
| Tilemap | `2d.tilemap.extras` Rule Tile | Đã có trong manifest |
| Animation | **Aseprite Importer** | Import trực tiếp `.aseprite` |
| Source control | Git + **Git LFS** | Project chưa init git — làm ngay |
| CI | GitHub Actions + GameCI | Bắt regression sớm |
| Unity MCP | `com.coplaydev.unity-mcp` (đã cài) | Tự động hoá tạo prefab/scene/script từ agent |

---

## 18. QUY ƯỚC CODE & ĐẶT TÊN

| Loại | Quy ước | Ví dụ |
|---|---|---|
| Namespace | `Game.{Module}.{SubModule}` | `Game.Combat.Systems` |
| Class | `PascalCase` | `DamageCalculator` |
| Interface | `I` + PascalCase | `IPlayerRepository` |
| Private field | `_camelCase` | `_currentActor` |
| Serialized field | `[SerializeField] private` + `_camelCase` | `_hpBar` |
| Const | `SCREAMING_SNAKE` | `ATB_THRESHOLD` |
| Enum | Số ít, PascalCase | `Element.Fire` |
| ScriptableObject asset | `{Loại}_{Id}` | `Hero_EmberKnight`, `Skill_BlazingBash` |
| Prefab | `{Nhóm}_{Tên}` | `UI_SkillSlot`, `Unit_Hero`, `VFX_FireBurst` |
| Scene | PascalCase | `Boot`, `Meta`, `Battle` |
| Data ID (string) | `snake_case` có tiền tố | `hero_ember_knight`, `skill_blazing_bash`, `status_burn` |
| Localization key | `{màn}.{nhóm}.{tên}` | `battle.button.end_turn` |
| Addressable key | `{nhóm}/{tên}` | `heroes/ember_knight_portrait` |
| Test | `{Lớp}Tests.{Method}_{Điều kiện}_{Kỳ vọng}` | `DamageCalculatorTests.Calculate_WithBreak_Multiplies1_5` |
| Event (CombatEvent) | Quá khứ phân từ | `DamageDealt`, `UnitDied` |

**Luật code bổ sung:**
- Cấm `GetComponent`/`Find` trong `Update`.
- Cấm `public` field trên MonoBehaviour (dùng `[SerializeField] private` + property).
- Mọi `MonoBehaviour` phải huỷ đăng ký event trong `OnDisable`/`OnDestroy`.
- Mọi số ma thuật trong combat phải nằm trong `BalanceConstants` hoặc SO.

---

## 19. BẢN QUYỀN

Ảnh trong `_Reference/UI_SAMPLE/` là **tài liệu tham khảo nội bộ** (`game_2.jpg` ghi credit artstation.com/damienh). **Không** dùng lại trong build phát hành. Thư mục này phải nằm **ngoài** `Assets/` để không lọt vào build. Toàn bộ art trong game là tự làm hoặc mua có giấy phép thương mại.

---

## 20. BẢNG THUẬT NGỮ

| Thuật ngữ | Nghĩa trong dự án |
|---|---|
| **ATB** | Active Time Battle — thanh nạp lượt, tick rời rạc, ngưỡng 1000 |
| **Action Command** | Cửa sổ bấm đúng nhịp khi hành động → Perfect/Good/Miss |
| **Poise** | Thanh gan lì; về 0 → **Break** |
| **Break** | Trạng thái mục tiêu mất lượt + nhận ×1.5 damage |
| **Grade** | Kết quả Action Command (`Perfect`/`Good`/`Miss`) |
| **Intent** | Ý định hành động của địch, hiện trước 1 lượt |
| **Node** | 1 điểm trên bản đồ phân nhánh của chương |
| **Run** | 1 lượt chơi xuyên 1 chương từ Start đến Boss |
| **Ascend** | Nâng sao hero bằng mảnh + vật liệu |
| **Awakening** | Passive đặc biệt mở ở ★6 |
| **Synergy** | Bonus khi đội hình thoả điều kiện class/element |
| **Definition (Def)** | ScriptableObject chứa dữ liệu gốc |
| **Instance** | Bản thể runtime của người chơi (hero đã nuôi, trang bị đã roll) |
| **Snapshot** | Ảnh chụp trạng thái để lưu/replay |
| **Scope (asset)** | Nhóm asset load/release cùng nhau |

---

*Cấu trúc project: [structure.md](structure.md) · Bản đồ đối tượng: [object-map.md](object-map.md) · Lộ trình: [roadmap.md](roadmap.md)*

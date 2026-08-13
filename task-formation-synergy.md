# Task: Formation Preset + Team Synergy (plan.md §5.7)

Yêu cầu: xây thật hệ Đội hình (8 preset, mỗi preset bố trí hàng Trước/Sau khác nhau và
cho bonus stat riêng) và Synergy (5 điều kiện bonus khi đội hình thoả điều kiện class/element).
Data model đã tồn tại (`UnlockedFormations`, `CombatUnit.Row`), nhưng không có logic nào đọc/áp dụng.

## §0. Findings

- **`PlayerProfileDto.ProgressDto.UnlockedFormations`** (`List<string>`) — data model sẵn, "formation_balanced"
  được grant khi `LocalPlayerRepository.CreateNew()`, `SaveMigrationRunner` null-guard. Nhưng KHÔNG ai
  đọc list này — không có `FormationSystem`, `FormationCatalog` hay field unlock-gate nào.
- **`CombatUnit.Row`** (`Row.Front` / `Row.Back`) — đã tích cực dùng trong combat:
  - `TargetSelector`: ưu tiên Front trước, Back chỉ khi Front chết hết
  - `DamageCalculator`: Back row nhận −20% Physical nếu không AoE
  - `CombatSimulation` ~line 491/498: auto-promote Back→Front khi Front chết hết
  - **Nhưng** cách gán Row hiện tại là hard-code: `Row = index < 2 ? Row.Front : Row.Back`
    trong `BuildUnitFromDefinition` (BattleSceneInstaller ~line 465/515) — không phụ thuộc formation.
- **`CombatUnit.Class`** (`HeroClass`) và **`CombatUnit.Element`** (`Element`) — đã tồn tại, dùng được cho synergy check.
- **`EquipmentModifiers`** (`List<StatModifier>`) — nơi phù hợp để inject formation/synergy bonus (pure stat,
  áp dụng trước trận, cùng với equipment sub-stat và Set Bonus 2-món).
- **`RunContext.PendingBattle`** — chưa có field `Formation`. `TeamSelectScreen.Open` callback chỉ trả về
  `List<string>` heroIds, không có formation.
- `TeamSelectScreen._onConfirm` là `Action<List<string>>` — sẽ thêm `public string SelectedFormation { get; private set; }` thay vì đổi signature callback (ít breaking hơn).
- **Không có** `FormationSystem.cs`, `SynergySystem.cs` anywhere (grep xác nhận).
- **Siege "4 sau"**: nếu frontCount=0, tất cả hero ở Back ngay từ đầu. `TargetSelector` thấy
  không có Front → sẽ nhắm Back trực tiếp. Auto-promote chỉ xảy ra khi Front chết hết, không phải khi
  Front rỗng từ đầu. Hành vi này là intentional — ghi lại trong task.

## §1. Scope

**Trong phạm vi:**
1. `FormationSystem.cs` (`Game.Meta/Combat/`) — pure static, 8 preset từ plan.md §5.7,
   `GetRow(formationId, slotIndex)`, `GetModifiers(formationId, row)`.
2. `SynergySystem.cs` (`Game.Meta/Combat/`) — pure static, 5 điều kiện từ plan.md §5.7,
   `Apply(List<CombatUnit>)` thêm StatModifier vào EquipmentModifiers của từng unit + MarkStatsDirty.
3. `PendingBattle` thêm field `string Formation` (default "formation_balanced" — không break caller cũ).
4. `RunContext.QueueBattle`/`QueueSpecialBattle` thêm param `formation` (optional, default null → "formation_balanced").
5. `BattleSceneInstaller.SpawnTeamFromDefinitions`: dùng `FormationSystem.GetRow` thay hard-code `index<2`,
   thêm formation modifier vào `unit.EquipmentModifiers`; sau khi spawn hết → gọi `SynergySystem.Apply`.
6. `TeamSelectScreen`: thêm `public string SelectedFormation`, cycle button đơn giản.
7. `MetaSceneInstaller.LaunchBattle`/`LaunchDungeonBattle`/`LaunchTrialBoss`: đọc `_teamSelectScreen.SelectedFormation`.
8. Tests: `FormationSystemTests.cs` + `SynergySystemTests.cs`.

**Ngoài phạm vi (cố ý):**
- Unlock progression: tất cả 8 formation luôn available (không gate sau quest/achievement) — `UnlockedFormations` vẫn có "balanced" từ CreateNew, bổ sung tất cả trong `LocalPlayerRepository.CreateNew` luôn để nhất quán.
- Formation preview UI (hình ảnh vị trí hàng trước/sau).
- Enemy formations (luôn dùng balanced mặc định).
- Element-specific synergy damage bonus — đơn giản hoá thành global `DmgBonusPct` (+8% cho toàn đội khi 2 hero cùng element).

## §2. Design Decisions

**8 Preset (ID → frontCount → modifiers):**
| ID | frontCount | Bonus (áp với ai) |
|---|---|---|
| formation_balanced | 2 | Không có |
| formation_phalanx | 3 | Front: +15% DEF |
| formation_arrowhead | 1 | Back: +12% ATK |
| formation_vanguard_line | 4 | All: +10% ATK, −10% DEF |
| formation_siege | 0 | All: −20% DmgReduct, −15% SPD |
| formation_flanking | 2 | All: +8% CRIT |
| formation_turtle | 3 | All: +20% DEF, −15% ATK |
| formation_blitz | 2 | All: +12% SPD, −8% MaxHP |

**5 Synergy (điều kiện → bonus áp cho ai):**
| Điều kiện | Bonus |
|---|---|
| ≥2 hero cùng class | +10% stat chính của class đó (tất cả hero trong đội) |
| ≥3 hero cùng class | +18% thay vì +10% (override, không cộng thêm) |
| ≥2 hero cùng element | +8% DmgBonusPct toàn đội |
| 4 hero KHÁC class hoàn toàn | +10% AtkPct+DefPct+MaxHpPct+SpdPct toàn đội |
| Có Vanguard + Warden + ≥1 DPS class | +5% MaxHpPct toàn đội |

**Primary stat per class** (cho synergy class-bonus):
- Vanguard → DefPct
- Slayer → AtkPct
- Arcanist → AtkPct
- Trickster → SpdPct
- Warden → MaxHpPct
- Summoner → AtkPct

**DPS class** (cho synergy Tank+Heal+DPS): Slayer, Arcanist, Trickster, Summoner.

**Source string** trong `StatModifier`: "formation" và "synergy" (cho debug trace).

## §3. Implementation Checklist

- [x] Viết `Assets/_Project/Scripts/Meta/Battle/FormationSystem.cs`
- [x] Viết `Assets/_Project/Scripts/Meta/Battle/SynergySystem.cs`
- [x] `RunContext.PendingBattle`: thêm `string Formation`
- [x] `RunContext.QueueBattle`/`QueueSpecialBattle`: thêm param optional `string formation = null`
- [x] `BattleSceneInstaller.SpawnTeamFromDefinitions`: dùng FormationSystem, gọi SynergySystem sau spawn
- [x] `TeamSelectScreen`: thêm `SelectedFormation`, cycle button + label
- [x] `MetaSceneInstaller`: pass `_teamSelectScreen.SelectedFormation` tới cả 3 QueueBattle callers
- [x] `LocalPlayerRepository.CreateNew`: unlock tất cả 8 formation sẵn (hardcoded strings — Game.Services không ref Game.Meta)
- [x] `validate_script` compile sạch (0 error, 0 warning trên 6 file mới/sửa)
- [x] Viết `FormationSystemTests.cs` + `SynergySystemTests.cs`
- [x] `run_tests` **460/460** xanh (423 cũ + 37 test mới)
- [x] Cập nhật `roadmap.md §0.1`, `object-map.md §12.1`

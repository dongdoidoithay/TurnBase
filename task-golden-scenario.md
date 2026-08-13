# Task: GoldenScenarioTests (plan.md §11.10)

Yêu cầu: "20 kịch bản cố định, so log" — regression safety net cho mọi thay đổi logic combat.

## §0. Findings

- **`sim.RunToCompletion()`** — chạy trận đến hết dùng `DefaultAutoIntent()` cho player.
- **`sim.Events.ToSignature()`** — chuỗi `"{type},{src},{tgt},{intVal};"` toàn bộ event sequence.
  Thay đổi bất kỳ ở damage formula / turn scheduler / crit / element → signature thay đổi → test đỏ.
- **Quy trình golden master**: chạy 20 kịch bản một lần qua `execute_code`, hardcode expected
  signature, các lần sau chạy test so sánh.
- **Namespace đúng**: `Game.Combat.CombatSimulation`, `Game.Combat.Model.CombatUnit`,
  `Game.Combat.Model.SkillRuntime`, `Game.Data.*` (enums: Row, TeamSide, Element, HeroClass).
- **Element valid**: Neutral=0, Fire=1, Water=2, Earth=3, Wind=4, Light=5, Dark=6.
- **HeroClass valid**: Vanguard=0, Slayer=1, Arcanist=2, Warden=3, Trickster=4, Summoner=5.
  (Không có "Mage" — dùng Arcanist cho magic user.)

## §1. Scope

**Trong phạm vi:** `GoldenScenarioTests.cs` (~20 tests) trong `Assets/Tests/EditMode/Combat/`

**Ngoài phạm vi:** snapshot to file (JSON/txt), record-mode flag, update workflow.

## §2. Design — 20 kịch bản

| ID | Mô tả | Expected result | Turns |
|---|---|---|---|
| S01 | Duel neutral 1v1 seed 1001 | Defeat | 14 |
| S02 | Duel neutral 1v1 seed 2002 | Victory | 13 |
| S03 | Duel Fire(hero) vs Water(enemy) seed 3003 | Defeat | 18 |
| S04 | Duel Water(hero) vs Fire(enemy) seed 4004 | Victory | 11 |
| S05 | Duel multi-hit 3×0.4 vs basic seed 5005 | Victory | 8 |
| S06 | Duel high power ×2.5 seed 6006 | Victory | 4 |
| S07 | Duel strong enemy str=25 seed 7007 | Defeat | 8 |
| S08 | Duel tanky hero con=30 seed 8008 | Victory | 17 |
| S09 | Duel low poise hero (5) seed 9009 | Defeat | 10 |
| S10 | Duel Earth(hero) vs Wind(enemy) seed 1010 | Defeat | 16 |
| S11 | TeamBattle 4v4 seed 2001 | Victory | 53 |
| S12 | TeamBattle 4v4 seed 3001 | Victory | 52 |
| S13 | TeamBattle 4v4 seed 4001 | Defeat | 53 |
| S14 | Duel Arcanist magic (int=25) seed 1414 | Victory | 7 |
| S15 | AoE 1v2 (hero vs 2 enemies) seed 1515 | Defeat | 12 |
| S16 | Duel neutral seed 1616 | Victory | 15 |
| S17 | Duel Wind(hero) vs Earth(enemy) seed 1717 | Victory | 7 |
| S18 | TeamBattle 4v4 seed 5001 | Victory | 48 |
| S19 | Duel str=12 both seed 1919 | Victory | 13 |
| S20 | TeamBattle 4v4 seed 6001 | Defeat | 55 |

## §3. Implementation Checklist

- [x] Viết `task-golden-scenario.md` (file này)
- [x] Viết `GoldenScenarioTests.cs` với 20 hardcoded signature constants (validate_script 0 lỗi, S01 fix lại sau khi mirror test với execute_code)
- [x] `run_tests` → **514/514 xanh** (494 cũ + 20 mới) ✓
- [x] Cập nhật `roadmap.md §0.1`, `object-map.md §12.1`

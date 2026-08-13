# Task: Analyze Tactic (plan.md §5.6)

Yêu cầu: "Analyze | 5 SP | Hiện toàn bộ stat + intent + kháng của 1 địch (vĩnh viễn trong trận)"

## §0. Findings

**Kiến trúc hiện tại:**
- Tactic row có 4 nút: Guard/ESC/SWAP/FOCUS (task-tactic-row.md). Cột 5 của hàng TACTIC còn trống.
- `ActionIntent` có `IsGuard/IsEscape/IsSwapRow/IsFocus` — Analyze thêm `IsAnalyze` theo cùng khuôn.
- `BattleState` có `DamageByUnit` đọc thẳng bởi `BattleHudScreen.Update()` (Damage Meter). Analyze
  panel dùng cùng pattern: thêm `AnalyzedEnemyIds (HashSet<int>)` vào `BattleState`, HUD poll mỗi frame.
- Không cần `CombatEventType` mới — HUD đọc State trực tiếp, không qua event queue.
- Auto-target: `_targeting.AutoSuggest(_currentActor, TeamSide.Enemy)` — cùng logic AI tự chọn mục tiêu.
  Không có manual target UI ở bất kỳ đâu trong game (pattern đã thiết lập).

**DerivedStats của enemy có thể hiển thị:**
- `u.Stats.AtkPhys`, `u.Stats.Def`, `u.Stats.Spd`, `u.MaxHp`, `u.MaxSp`
- Element resistances: `ElementTable.Multiplier(attacker=e, defender=u.Element)` với e=Fire/Water/Earth/Wind
  (Light/Dark đã đặc biệt: nếu defender = Light → Dark strong ×1.4, ngược lại). Hiển thị tóm gọn.

**"Intent" (AI sẽ làm gì):** quá phức tạp để reveal an toàn — `_ai.Choose()` có side-effect lên RNG state
→ scope out. Không hiển thị "intent" trong v1. Plan.md chỉ nói "stat + intent + kháng", intent để v2.

## §1. Scope

**Trong phạm vi:**
- `BattleState.AnalyzedEnemyIds` (HashSet<int>) — lưu ID địch đã analyze
- `ActionIntent.IsAnalyze` (bool) — báo hiệu Analyze intent
- `CombatSimulation.ExecuteIntent`: xử lý IsAnalyze (cost 5 SP, auto-target, FinishTurn)
- `BattleHudScreen`: nút ANALYZE thứ 5 trong tactic row, panel "ANALYZE INFO" dưới-phải hiện stats
- `BattleSceneInstaller`: `HandleAnalyze` wired vào `OnAnalyzePressed`
- 3 test: không đủ SP → bỏ qua, đủ SP → mark enemy + trừ SP, kiểm tra panel state

**Ngoài phạm vi:**
- AI intent revelation (side-effect RNG)
- Manual target selection cho Analyze
- Analyze trong `DefaultAutoIntent()`
- CombatEventType mới

## §2. Thiết kế panel

**Vị trí:** góc dưới-phải của HUD (hiện chưa có gì — Damage Meter ở dưới-trái). Kích thước ~200×120 px.
Ẩn hoàn toàn khi chưa có địch nào bị analyze; hiện ra khi analyze lần đầu.

**Nội dung (địch đã analyze gần nhất):**
```
ANALYZED: {DisplayNameKey}          [Element icon]
HP {hp}/{maxHp}  SP {sp}/{maxSp}
ATK {AtkPhys:.0}  DEF {Def:.0}  SPD {Spd:.0}
ELEM: F{fire_mult} W{water_mult} E{earth_mult} Wi{wind_mult}
```
Trong đó `fire_mult` = ElementTable.Multiplier(Fire, defender.Element) — hiển thị "×1.3" / "×0.7" / "—".
Cập nhật mỗi frame từ `_sim.State` (HP/SP thay đổi theo trận; AnalyzedEnemyIds cố định).

## §3. Implementation Checklist

- [x] Viết `task-analyze-tactic.md` (file này)
- [x] `BattleState.cs`: thêm `AnalyzedEnemyIds`
- [x] `CombatSimulation.cs`: thêm `IsAnalyze` vào `ActionIntent` + xử lý trong `ExecuteIntent`
- [x] `BattleHudScreen.cs`: nút ANALYZE (tactic row col 4) + panel ANALYZE INFO (dưới-phải) + event
- [x] `BattleSceneInstaller.cs`: `HandleAnalyze` + wire `OnAnalyzePressed`
- [x] `AnalyzeTacticTests.cs`: 3 test engine-level
- [x] `validate_script` → 0 lỗi
- [x] `run_tests` → **518/518 xanh** (515 cũ + 3 mới)
- [x] Cập nhật `roadmap.md §0.1`, `object-map.md §12.1`

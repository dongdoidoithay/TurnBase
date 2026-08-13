# Task: Tactic Row — Hàng 3 BattleHUD (plan.md §5.6)

Yêu cầu: xây hàng 3 trong BattleHudScreen (TACTIC) với 4 nút Guard/Escape/SwapRow/Focus,
nối xuống CombatSimulation. Guard + Escape đã có engine nhưng chưa có nút UI; SwapRow + Focus
chưa có engine lẫn UI.

## §0. Findings

- **`BattleHudScreen`**: `GRID_ROWS = 2` (hàng 0 skill, hàng 1 item). Hàng 2 (TACTIC) chưa tồn
  tại. Kích thước panel tự tính từ `GRID_ROWS × cell + gaps + 16` nên chỉ cần tăng lên 3.
- **`ActionIntent`**: đã có `IsGuard`, `IsEscape`, `IsUseItem`; chưa có `IsSwapRow`, `IsFocus`.
- **`CombatSimulation.ExecuteIntent`**: đã xử lý `IsGuard` (DefUp 1 lượt, +8 SP) và `IsEscape`
  (40% + SPD delta) — không cần sửa engine logic.
- **`StatusId`**: max hiện tại = `Reflect = 37`. Cần thêm `Focus = 38`.
- **`DamageCalculator.Calculate` line 77**: `bool crit = rng.Chance(...)`. Focus cần force
  `crit = true` bằng cách kiểm tra `attacker.HasStatus(StatusId.Focus)` trước câu đó.
- **`CombatUnit`**: chưa có `HasSwappedRowThisTurn`. SwapRow không kết thúc lượt — actor vẫn
  ở `AwaitInput` và có thể chọn skill/item sau đó. Cần bit flag để disable nút lần 2.
- **`BeginTurn()`** CombatSimulation: nơi reset các trạng thái per-turn (paralyze check, SP regen,
  cooldown tick). Cần reset `HasSwappedRowThisTurn` ở đây.
- **SwapRow "không kết thúc lượt"**: trong `ExecuteIntent`, sau khi swap `actor.Row` và set flag,
  KHÔNG gọi `FinishTurn()`. `Phase` quay về `AwaitInput` để actor có thể hành động tiếp. Gọi
  `Events.Emit(CombatEventType.Moved, ...)` để view cập nhật vị trí nếu cần (có thể skip nếu
  không có view-side handler — không break).
- **Focus UI disable**: Focus button bị disable sau khi đã dùng (bắt đầu lượt, khi `HasSwappedRowThisTurn`
  không liên quan) — thực ra Focus *kết thúc lượt* (cùng khuôn Guard), nên không cần flag riêng.
  Chỉ SwapRow cần `HasSwappedRowThisTurn`.
- **Escape disable trong boss fight**: `BattleState.AllowEscape = false` khi boss. SwapRow button
  không bị ảnh hưởng. Focus không bị ảnh hưởng.
- **Auto-battle**: `DefaultAutoIntent()` phải KHÔNG dùng SwapRow/Focus (trả về skill slot 0 như cũ).
  SwapRow "không kết thúc lượt" sẽ tạo vòng vô hạn nếu auto-intent trả về SwapRow mãi.
- **Analyze (plan.md §5.6 mục 5)**: phức tạp (cần UI reveal persistent per enemy) → out of scope.

## §1. Scope

**Trong phạm vi:**
1. `CombatEnums.cs`: thêm `StatusId.Focus = 38`
2. `CombatSimulation.cs`/`ActionIntent`: thêm `IsSwapRow`, `IsFocus`; cập nhật constructor
3. `CombatUnit.cs`: thêm `public bool HasSwappedRowThisTurn;`
4. `CombatSimulation.BeginTurn()`: reset `_currentActor.HasSwappedRowThisTurn = false`
5. `CombatSimulation.ExecuteIntent()`: xử lý `IsSwapRow` (swap Row, set flag, KHÔNG FinishTurn)
   và `IsFocus` (apply StatusId.Focus duration 1, FinishTurn(0))
6. `DamageCalculator.cs` line 77: `bool crit = attacker.HasStatus(StatusId.Focus) || rng.Chance(...)`
7. `BattleHudScreen.cs`: `GRID_ROWS 2→3`, thêm `BuildTacticRow()`, fields `_guardBtn/_escBtn/_swapBtn/_focusBtn`,
   events `OnGuardPressed/OnEscapePressed/OnSwapRowPressed/OnFocusPressed`,
   `RefreshTacticRow()` gọi từ `Update()` để disable tương ứng
8. `BattleSceneInstaller.cs`: `WireHud()` thêm 4 event → 4 handler `HandleGuard/HandleEscape/HandleSwapRow/HandleFocus`
9. Test file `TacticSystemTests.cs` (~12 tests)

**Ngoài phạm vi:**
- Analyze (reveal enemy stats)
- Auto-battle 7-priority policy (task riêng)

## §2. Design

**Focus status**: duration = 1, timing = TurnEnd (tự xóa sau khi turn kết thúc — bao gồm lượt đang
dùng). Stack = true (không quan trọng vì chỉ áp 1 lần). SourceId = actorId.

**SwapRow trong ExecuteIntent**:
```csharp
if (intent.IsSwapRow)
{
    if (!_currentActor.HasSwappedRowThisTurn)
    {
        _currentActor.Row = _currentActor.Row == Row.Front ? Row.Back : Row.Front;
        _currentActor.HasSwappedRowThisTurn = true;
        Events.Emit(CombatEventType.Moved, _currentActor.Id, _currentActor.Id);
    }
    Phase = SimPhase.AwaitInput;  // không kết thúc lượt — actor vẫn chờ input
    return;
}
```

**Tactic buttons** (4 nút, đặt trong GRID_COLS=5 cell, cách đều):
- Guard (G): cyan accent, luôn available khi player turn
- ESC: red accent, disable khi `!_sim.State.AllowEscape`
- SWAP: yellow accent, disable khi `_currentActor.HasSwappedRowThisTurn`
- FOCUS: purple accent, luôn available

**Panel height**: GRID_ROWS 2→3: thêm `cell + gap` chiều cao. Hàng 2 offset = `-(8 + 2*(cell+gap))`.

## §3. Implementation Checklist

- [x] Viết `task-tactic-row.md` (file này)
- [x] `CombatEnums.cs`: thêm `Focus = 38`
- [x] `StatusInstance.cs/StatusTable`: thêm `Focus` entry (Buff, dur=2, Tick=None, Dispel)
- [x] `CombatSimulation.cs`: thêm `IsSwapRow`, `IsFocus` vào `ActionIntent` + constructor
- [x] `CombatUnit.cs`: thêm `HasSwappedRowThisTurn`
- [x] `CombatSimulation.BeginTurn()`: reset `HasSwappedRowThisTurn`
- [x] `CombatSimulation.ExecuteIntent()`: xử lý `IsSwapRow` + `IsFocus`
- [x] `DamageCalculator.cs`: force crit khi attacker.HasStatus(Focus)
- [x] `BattleHudScreen.cs`: GRID_ROWS→3, tactic row, 4 events
- [x] `BattleSceneInstaller.cs`: wire 4 events
- [x] `validate_script` compile sạch (0 error, 0 warning trên 8 file mới/sửa)
- [x] Viết `TacticSystemTests.cs` (13 tests)
- [x] `run_tests` **473/473** xanh (460 cũ + 13 test mới)
- [x] Cập nhật `roadmap.md §0.1`, `object-map.md §12.1`

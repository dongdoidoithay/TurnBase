# Task: Auto-battle Policy 7 Ưu Tiên (plan.md §4.16)

Yêu cầu: `DefaultAutoIntent()` hiện tại chỉ uỷ thác cho `_ai.Choose()` — đúng bộ AI dùng cho
địch — không phải policy 7 ưu tiên cho player. Phải thay thế bằng logic đúng từ plan.md §4.16,
và persist trạng thái auto/speed vào `SettingsDto.AutoBattle`.

## §0. Findings

- **`DefaultAutoIntent()`** (CombatSimulation.cs ~line 597): dùng `_ai.Choose(actor, profile, 0, State.Rng)`.
  `_aiProfiles[actor.Id]` của hero Player = null (chưa được `RegisterAi` cho player), nên
  `_ai.Choose(..., null, 0, ...)` chạy theo profile null — fallback về basic attack thường xuyên.
  Không phải policy 7 ưu tiên.

- **`CombatUnit.CanUseSkill(skill, ultimateReady)`** đã tồn tại (unit.cs:217) — kiểm tra
  `CooldownLeft > 0`, `SpCost > Sp`, `IsSilenced() && SlotIndex != 0`, và gate slot 4 theo
  `ultimateReady`. Tái dùng trực tiếp.

- **`CombatUnit.IsBroken`** (unit.cs:40) — bool property `BrokenTurnsLeft > 0`. Sẵn.

- **`BattleState.IsUltimateReady`** (BattleState.cs:109) — bool property. Sẵn.

- **`BattleState.CountAlive(side)`** (BattleState.cs:70) — đếm đơn vị sống. Sẵn.

- **`ElementTable.IsStrong(skillElem, targetElem)`** (ElementTable.cs:45) — bool, accessible từ
  namespace `Game.Combat.Systems` (đã in `using` của CombatSimulation.cs). Sẵn.

- **`SkillData.Type == SkillType.Heal`** hoặc `HealPower > 0f` → heal skill.
  `SkillData.RevivePercent > 0f` → revive skill.
  `SkillData.DealsDamage` property (đã có) → damage skill.
  `SkillData.PowerMultiplier * HitCount` → ước lượng damage để tìm skill cao nhất.

- **`SkillData.TargetsAllies`** property (đã có) = Target là SingleAlly/AllAllies/LowestHpAlly/Self/DeadAlly.

- **Ultimate slot**: `skill.SlotIndex == 4` (theo comment BattleSceneInstaller.cs:959:
  "slot 4 — confirmed via a BattleSceneInstaller.cs comment"). `CanUseSkill` tự gate nó theo
  `ultimateReady`.

- **`SettingsDto.AutoBattle`** (PlayerProfileDto.cs:238) — bool field đã tồn tại nhưng chưa
  được đọc bởi bất kỳ ai. `_autoPlay` trong `BattleSceneInstaller` là local field, mất khi kết
  thúc scene. plan.md §4.16 yêu cầu ghi nhớ.

- **`ISettingsService.Modify(Action<SettingsDto>)`** — pattern đã dùng trong SettingsScreen,
  tái dùng để persist.

- **`BattleSceneInstaller` init `_autoPlay`**: hiện set `[SerializeField] private bool _autoPlay = false`
  — cần đọc từ `_settings.AutoBattle` thay vì default false, trong `BuildBattle()` hoặc `Start()`.

- **Tốc độ (BattleSpeed)**: plan.md ghi "Ghi nhớ tốc độ vào SettingsDto" — `SettingsDto.BattleSpeed`
  đã có (=1) nhưng chưa dùng. Nằm trong scope task này nhưng tối thiểu: chỉ sync `_autoPlay` ↔
  `SettingsDto.AutoBattle`. Persist speed là nice-to-have nếu thời gian cho phép.

- **`_targeting` accessible trong `CombatSimulation`**: yes (line 63, private field). Nhưng
  `DefaultAutoIntent()` không cần gọi `_targeting.Resolve()` để tìm target — chỉ cần first alive
  enemy ID. `TargetSelector.Resolve()` sẽ tự override khi skill có TargetMode khác SingleEnemy.

## §1. Scope

**Trong phạm vi:**
1. `CombatSimulation.DefaultAutoIntent()`: thay toàn bộ thân hàm bằng 7-priority policy + 7 helper
   private method nhỏ bên dưới
2. `BattleSceneInstaller`: đọc `SettingsDto.AutoBattle` để init `_autoPlay`, persist khi toggle
3. Test file `AutoBattlePolicyTests.cs` (~12 tests)

**Ngoài phạm vi:**
- Persist battle speed (`BattleSpeed`) — không có slider/toggle trong HUD hiện tại, scope riêng
- Analyze tactic — đã flagged ở task-tactic-row.md
- SwapRow/Focus trong auto-battle: auto-battle KHÔNG dùng SwapRow (gây vòng vô hạn vì không kết thúc
  lượt) và KHÔNG dùng Focus (không cần — auto đã chọn skill tốt nhất). Giữ nguyên behavior
  không gọi 2 intent này.

## §2. Design

### Policy 7 ưu tiên

```
1. Đồng minh HP < 35% VÀ actor có heal skill khả dụng → heal
2. Đồng minh gục VÀ actor có revive skill khả dụng → revive
3. Có ít nhất 1 địch đang Break VÀ actor có damage skill khả dụng → dùng damage cao nhất
4. Ultimate đầy VÀ ≥2 địch sống → Ultimate
5. Có skill khắc chế element của mục tiêu ưu tiên → dùng skill đó
6. Damage skill cao nhất còn dùng được (theo PowerMultiplier × HitCount)
7. Đánh thường (slot 0)
```

### Helper methods (private, trong CombatSimulation)

- `FirstAliveEnemyId() → int`: duyệt State.Units, trả về Id của địch sống đầu tiên, hoặc -1.
  (Không trùng với BattleSceneInstaller's `FirstAliveEnemyId()` — class khác, không xung đột.)
- `FindHealSkill(actor, ultimateReady) → SkillRuntime?`: tìm skill có `Type==Heal || HealPower>0f`,
  TargetsAllies==true, CanUseSkill — bỏ qua slot 4 (nếu không phải Warden ultimate heal).
- `FindReviveSkill(actor, ultimateReady) → SkillRuntime?`: `RevivePercent>0f`, CanUseSkill.
- `HasLowHpAlly(actor) → bool`: State.Units có unit cùng phe, alive, Hp < 35% MaxHp.
- `HasDeadAlly(actor) → bool`: State.Units có unit cùng phe, !IsAlive.
- `HasBrokenEnemy(out int targetId) → bool`: State.Units có địch alive && IsBroken.
- `FindHighestDamageSkill(actor, ultimateReady) → SkillRuntime?`: max `DealsDamage &&
  PowerMultiplier*HitCount`, CanUseSkill, bỏ slot 4 (ultimate dành cho priority 4).
- `FindElementCounterSkill(actor, targetId, ultimateReady) → SkillRuntime?`: tìm skill mà
  `ElementTable.IsStrong(skill.Data.Element, target.Element)`, DealsDamage, CanUseSkill, bỏ slot 4.

### Sẵn target cho các skill

Phần lớn `TargetMode` (LowestHpAlly, DeadAlly, AllEnemies) tự resolve trong
`ActionResolver.Execute()` → `TargetSelector.Resolve()` — `TargetId` trong ActionIntent bị bỏ qua
hoặc chỉ là gợi ý. Nên:
- Heal/Revive: pass `FirstAliveEnemyId()` hoặc -1 làm targetId (không quan trọng).
- Damage SingleEnemy: pass `brokenEnemyId` hoặc `firstAliveEnemyId`.

### Persist AutoBattle

Trong `BattleSceneInstaller.WireHud()`:
```csharp
_hud.OnAutoToggled += auto => {
    _autoPlay = auto;
    _settings.Modify(s => s.AutoBattle = auto);
};
```

Trong `BattleSceneInstaller.BuildBattle()` (sau khi `_settings` đã inject):
```csharp
_autoPlay = _profile.Settings.AutoBattle;
```

(Hoặc ở `Start()` tuỳ vào thứ tự lifecycle.)

## §3. Implementation Checklist

- [x] Viết `task-auto-battle.md` (file này)
- [x] `CombatSimulation.cs`: thay thân `DefaultAutoIntent()` + thêm 8 helper method private
- [x] `BattleSceneInstaller.cs`: init `_autoPlay` từ settings, persist khi toggle
- [x] Viết `AutoBattlePolicyTests.cs` (13 tests)
- [x] `run_tests` **486/486** xanh (473 cũ + 13 mới)
- [x] Cập nhật `roadmap.md §0.1`, `object-map.md §12.1`

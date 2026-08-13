# Task: FuzzBattleTests + DeterminismTests (plan.md §11.10)

Yêu cầu: 2 loại property test quan trọng còn thiếu trong plan.md §11.10:
- `FuzzBattleTests` — N trận ngẫu nhiên, assert 0 exception + luôn kết thúc trong ≤200 lượt
- `DeterminismTests` — cùng seed 2 lần → chuỗi event giống hệt (prerequisite cho replay/chống gian lận)

## §0. Findings

- **`CombatSimulation.Advance()`** (public) — bước 1 phase, trả về khi `IsFinished` hoặc gặp
  `AwaitInput`. Phải gọi liên tục trong vòng lặp test.

- **`NeedsPlayerInput`** (public bool) — true khi `Phase==AwaitInput && actor.Side==Player`.
  Khi false nhưng vẫn AwaitInput → lượt địch, gọi `Advance()` thêm. Fuzz loop:
  ```csharp
  while (!sim.IsFinished) {
      if (sim.NeedsPlayerInput) sim.SubmitIntent(sim.DefaultAutoIntent());
      else sim.Advance();
  }
  ```

- **`DefaultAutoIntent()`** (public, CombatSimulation) — policy 7 ưu tiên đã xây (task-auto-battle.md).
  Dùng làm player AI cho fuzz — tránh phải submit intent tuỳ tiện có thể gây loop.

- **`CombatEventQueue.ToSignature()`** (public) — đã có sẵn trong `CombatEvent.cs:130`,
  trả về chuỗi `"{type},{src},{tgt},{intVal};"` cho mọi event. Đây là API cho DeterminismTests.

- **`CombatSimulation.Events`** (public) — `CombatEventQueue`, accessible từ sim sau khi chạy xong.

- **SAFETY_TURN_LIMIT = 200** (`CombatSimulation.cs:53`) — bảo vệ có sẵn, trận nào vượt 200 lượt
  tự kết thúc `BattleResult.Timeout`. Test chỉ cần confirm `IsFinished` sau khi loop kết thúc.

- **TestFactory** (`TestFactory.cs`) — có `Duel()`, `TeamBattle()`, `SimpleAi()`, `BasicAttack()`.
  Fuzz dùng biến thể của `TeamBattle` nhưng seed thay đổi mỗi iteration.

- **Namespace tests**: `Game.Tests.Combat` (cùng với `AutoBattlePolicyTests`, `SimulationTests`...).

- **Assembly**: `Game.Tests.EditMode.asmdef` — đã có `using Game.Combat` và `using Game.Combat.Model`.

## §1. Scope

**Trong phạm vi:**
1. `FuzzBattleTests.cs` (~7 tests): N random battles, assert IsFinished + không exception
2. `DeterminismTests.cs` (~6 tests): same seed → same signature

**Ngoài phạm vi:**
- `GoldenScenarioTests` (20 fixed scenario log) — cần thiết kế 20 kịch bản cụ thể, task riêng
- `AssemblyRuleTests` — task riêng (cần reflection/attribute scan)
- `SaveMigrationTests` — P4 task riêng

## §2. Design

### FuzzBattleTests — 7 test cases

Tất cả dùng vòng lặp chuẩn:
```csharp
static void RunToEnd(CombatSimulation sim) {
    while (!sim.IsFinished) {
        if (sim.NeedsPlayerInput) sim.SubmitIntent(sim.DefaultAutoIntent());
        else sim.Advance();
    }
}
```

| Test | Scenario | Assert |
|---|---|---|
| `Fuzz_Duel_500Seeds_NeverThrows` | 500 seeds 1v1 | IsFinished, no exception |
| `Fuzz_TeamBattle_200Seeds_NeverThrows` | 200 seeds 4v4 | IsFinished |
| `Fuzz_TurnCountNeverExceedsSafetyLimit` | 200 seeds 4v4 | TurnCounter ≤ 200 |
| `Fuzz_ResultIsNeverInProgress` | 100 seeds 2v2 | Result ≠ InProgress |
| `Fuzz_WithItems_100Seeds` | 100 seeds, 1 item slot loaded | IsFinished |
| `Fuzz_WithBreakStatus_100Seeds` | poise thấp → Break | IsFinished |
| `Fuzz_MixedTeamSize_100Seeds` | 1v4, 4v1, 2v3 ngẫu nhiên | IsFinished |

Seeds: `ulong seed = 0xDEAD_BEEF_0000_0000UL + (ulong)i` → reproducible, không trùng SEED mặc định.

### DeterminismTests — 6 test cases

```csharp
static string RunAndSignature(ulong seed) {
    var sim = BuildSim(seed);
    RunToEnd(sim);
    return sim.Events.ToSignature();
}
Assert.AreEqual(RunAndSignature(seed), RunAndSignature(seed)); // same seed → same sig
```

| Test | Scenario |
|---|---|
| `Determinism_Duel_SameSeedSameSignature` | 1v1 basic |
| `Determinism_TeamBattle_SameSeedSameSignature` | 4v4 |
| `Determinism_WithPoisonStatus_SameSeedSameSignature` | có DoT |
| `Determinism_HighSeed_SameSeedSameSignature` | seed lớn (edge overflow) |
| `Determinism_DifferentSeeds_DifferentSignature` | 2 seed khác nhau → sig khác (negative assertion) |
| `Determinism_100SeedsAllMatch` | 100 seeds, mỗi seed chạy 2 lần compare |

## §3. Implementation Checklist

- [x] Viết `task-fuzz-determinism.md` (file này)
- [ ] Viết `FuzzBattleTests.cs`
- [ ] Viết `DeterminismTests.cs`
- [ ] Viết `.meta` cho cả 2 file
- [ ] `run_tests` → **?/? xanh** (không regress 486 test cũ)
- [ ] Cập nhật `roadmap.md §0.1`, `object-map.md §12.1`

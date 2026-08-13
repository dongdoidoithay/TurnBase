# TASK-EDGECASES.md — 24 edge case plan.md §4.14: rà lại + vá lỗ hổng test

> Audit thật (không dựa theo memory cũ) cho thấy bức tranh khác "16/24 thiếu" đã ước lượng: **5
> covered đầy đủ, 6 covered MỘT PHẦN (test có nhưng bỏ sót đúng ý plan.md), 9 chưa có test dù
> hành vi ĐÃ tồn tại trong code, và 4 case KHÔNG THỂ test vì tính năng chưa được xây** (không phải
> thiếu test — thiếu hẳn code để test). Việc của lượt này: vá 6 partial + viết 9 case mới, KHÔNG
> viết test giả cho 4 case thiếu tính năng.

---

## 0. Bảng đầy đủ 24 case — verdict thật (grep code, không đoán)

| E# | Verdict | File hiện có / cần vá |
|---|---|---|
| E01 | ❌ Chưa test | `ActionResolver.ResolveDamage` (target chết giữa combo) |
| E02 | ❌ Chưa test | `CombatSimulation.BeginTurn` (chết do DoT đầu lượt) |
| E03 | ❌ Chưa test | `ActionResolver.ResolveDamage`/`ResolveReactions` (chết giữa Counter/Reflect) |
| E04 | ✅ Covered | `SimulationTests.EdgeCaseTests.E04_BothTeamsWiped_PlayerLoses` |
| E05 | ⚠️ Một phần | `CoreSystemTests.FreezeMeltsOnFireHit` chỉ test tan Freeze, THIẾU vế damage ×1.3 |
| E06 | ⚠️ Một phần | `CoreSystemTests.SleepWakesOnDamage` chỉ test tỉnh dậy, THIẾU vế "vẫn ăn full damage" |
| E07 | ⚠️ Một phần | `CoreSystemTests.DeadTaunter_DoesNotOverride` chỉ test targeting, THIẾU vế "status bị gỡ" |
| E08 | ❌ Chưa test | `ActionResolver.ApplyOneHit` (Shield hấp thụ hết → không tính OnDamaged) |
| E09 | ⚠️ Một phần | `SimulationTests.E09_HealDoesNotExceedMaxHp` chỉ test cắt HP, THIẾU vế "không thành shield" |
| E10 | ❌ Chưa test | `ActionResolver.ResolveHeal` nhánh hồi sinh (xoá status + HP theo %) |
| E11 | ✅ Covered | `SimulationTests.EdgeCaseTests.E11_UnitDownTooLong_BecomesPermanentlyDown` |
| E12 | 🚫 **Tính năng chưa tồn tại** | `AutoRevive` — 0 kết quả grep toàn bộ codebase |
| E13 | ⚠️ Một phần | `SimulationTests.E13_TurnLimitCausesTimeout` chỉ test nhánh có `TurnLimit`, THIẾU nhánh `SAFETY_TURN_LIMIT=200` không timer |
| E14 | ⚠️ Một phần | `SimulationTests.E14_BackRowAdvancesWhenFrontDies` chỉ test cuối cùng có đổi hàng, THIẾU vế "không đổi ngay lập tức" |
| E15 | 🚫 **Tính năng chưa tồn tại** | Ultimate gauge chỉ tăng (`AddUltimate`), không có đường tiêu/thực thi Ultimate nào cả |
| E16 | ❌ Chưa test | `DamageCalculator.Calculate` (`!data.IsAoe` gate) + `TargetSelector` AoE còn 1 địch |
| E17 | 🚫 **Tính năng chưa tồn tại** | `BattleSnapshotDto` chỉ khai báo, không hề được ghi/đọc ở đâu — không có save/load trận |
| E18 | 🚫 **Chưa nối dây** | `BattleState.AllowEscape` mặc định `true`, không ai từng set `false`; `CombatPresenter.IsBossFight` tồn tại nhưng không nối vào `AllowEscape` |
| E19 | ✅ Covered | `SimulationTests.EdgeCaseTests.E19_AiNeverPicksUnusableSkill` |
| E20 | ❌ Chưa test | `CombatSimulation.CleanupOrphanMinions` + đếm ngược `MinionTurnsLeft` |
| E21 | ❌ Chưa test | `StatusProcessor.Apply`/`ComputeStatusValue` (2 nguồn cùng ID, lấy giá trị nguồn mạnh hơn) |
| E22 | ✅ Covered (2 chỗ) | `SimulationTests.E22_SpdNeverReachesZero` + `CoreSystemTests.SpdFloor_PreventsDeadlock` |
| E23 | ❌ Chưa test (yếu theo thiết kế) | `ActionCommandEvaluator` vốn không phụ thuộc `Time.*`/tốc độ game (đúng luật plan.md §4.17) → bất biến "by construction", nhưng chưa có test khẳng định rõ |
| E24 | ✅ Covered (2 chỗ) | `SimulationTests.E24_DamageNeverBelowOne` + `DamageCalculatorTests.Calculate_AlwaysAtLeastOne_...` |

## 1. KHÔNG làm — 4 case chặn bởi thiếu tính năng (ghi rõ, không viết test giả)

- **E12 AutoRevive** — không có passive/trigger loại này trong `PassiveProcessor`/`AwakeningCatalog`.
  Muốn test thật phải xây tính năng trước (ngoài phạm vi lượt này — có thể cân nhắc thêm 1
  `PassiveTrigger.OnTeamWiped` sau này nếu cần).
- **E15 Ultimate execution** — `BattleState.UltimateGauge` chỉ cộng dồn (`AddUltimate`), không có
  `SkillData.IsUltimate`, không có đường tiêu gauge nào. Test "gauge giữ nguyên khi hero chết"
  hiện tại sẽ luôn pass 1 cách vô nghĩa (gauge chưa từng bị trừ bởi bất cứ gì) — viết ra sẽ là
  test giả, không giữ được bug thật nào.
- **E17 BattleSnapshot** — `BattleSnapshotDto` khai báo trong `PlayerProfileDto` nhưng không có
  code ghi/đọc nào. Đây là hệ thống lưu/khôi phục trận dở dang, thuộc phạm vi save system riêng,
  không phải Combat edge case thuần tuý.
- **E18 Escape-block-tại-Boss** — `BattleState.AllowEscape`/`CombatPresenter.IsBossFight` đều tồn
  tại nhưng CHƯA nối với nhau. Đây là 1 dòng code thiếu (`BattleSceneInstaller` cần set
  `AllowEscape = !isBossFight` khi tạo `CombatSimulation`), không lớn — **cân nhắc vá thẳng luôn
  trong lượt này** (mục 3) rồi mới viết test, thay vì bỏ qua như 3 case kia.

## 2. Vá 6 test "một phần" — bổ sung vế còn thiếu, GIỮ NGUYÊN test cũ

- [x] **E05** (`CoreSystemTests.cs`): thêm assertion/test riêng cho vế damage ×1.3 — unit đang
      Freeze, trúng đòn Fire, so damage với unit không Freeze (dùng
      `StatusTable.FREEZE_DMG_TAKEN_BONUS` làm hệ số kỳ vọng).
- [x] **E06**: thêm test/assertion "vẫn ăn full damage" — HP giảm đúng số dù đang Sleep, KHÔNG bị
      giảm/chặn bởi trạng thái ngủ.
- [x] **E07**: thêm assertion gọi `ActionResolver.HandleDeath`/`StatusProcessor.ClearTauntFromDead`
      thật (không chỉ set `Hp=0` tay) rồi kiểm tra Taunt status đã bị gỡ khỏi unit chết.
- [x] **E09**: thêm `Assert.IsFalse(target.HasStatus(StatusId.Shield))` sau khi hồi máu vượt MaxHP.
- [x] **E13**: thêm test riêng cho nhánh KHÔNG có `TurnLimit` (turnLimit=0) — chạy quá
      `SAFETY_TURN_LIMIT` (200) → `BattleResult.Timeout`.
- [x] **E14**: thêm assertion NGAY sau khi hàng trước chết (trước khi `FinishTurn` chạy hết) —
      `Row` của hàng sau VẪN LÀ `Back`, chỉ đổi sau khi bước 14 (`AdvanceRowsIfFrontEmpty`) chạy.

## 3. Vá dây trước khi test — E18

- [x] `BattleSceneInstaller.cs`: khi tạo `CombatSimulation`, set `State.AllowEscape =
      !isBossFight` (đọc `node.Type == NodeType.Boss` giống chỗ đã dùng ở `MetaSceneInstaller`).
      Nối `CombatPresenter.IsBossFight`/nút Escape ẩn nếu UI đã có sẵn chỗ cắm (nếu chưa có UI
      escape button thì chỉ vá phần logic `AllowEscape`, ghi rõ UI ẩn nút vẫn ngoài phạm vi).
- [x] Test mới: `AllowEscape=false` ở node Boss → `CombatSimulation.TryEscape`/`SubmitIntent` với
      `IsEscape=true` không thoát được trận.

## 4. Viết mới — 9 case có hành vi thật, chỉ thiếu test

- [x] **E01**: `TeamBattle` 4v4, skill nhiều hit nhắm 1 địch, địch chết giữa combo → hit còn lại
      chuyển sang địch khác; nếu hết địch → dừng, không lỗi.
- [x] **E02**: unit dính DoT đủ mạnh chết đúng lúc đầu lượt (`TickTurnStart`) → lượt bị bỏ, SP
      không đổi, ATB reset (dùng `TurnScheduler.ResetAtb` làm mốc so sánh).
- [x] **E03**: attacker có Counter, defender phản đòn giết actor gốc giữa combo nhiều hit → hit
      hiện tại hoàn tất trước khi actor được xử lý chết (assert số hit đã áp dụng đúng, không bị
      cắt giữa chừng).
- [x] **E08**: Shield đủ lớn hấp thụ hết 1 đòn → HP mục tiêu không đổi, `PassiveProcessor`
      `TriggerOnDamageTaken`/`CheckHpThreshold` KHÔNG được gọi (dùng passive test có
      `Trigger=OnDamageTaken` để verify không kích hoạt).
- [x] **E10**: hồi sinh 1 unit đang có debuff (vd AtkDown) → sau hồi sinh: hết mọi status, HP đúng
      `MaxHp * RevivePercent` của skill.
- [x] **E16**: skill AoE, chỉ còn 1 địch sống ở hàng sau → vẫn KHÔNG bị nhân `BACK_ROW_PHYS_MULT`
      (vì `data.IsAoe == true`), so sánh với đòn đơn mục tiêu cùng điều kiện có bị nhân.
- [x] **E20**: Summoner triệu hồi minion, chủ chết → minion còn tồn tại đúng 1 lượt kế rồi biến
      mất (`CleanupOrphanMinions`).
- [x] **E21**: áp status Bleed từ 2 nguồn có `AtkPhys` khác nhau lên cùng 1 target (nguồn yếu áp
      trước, nguồn mạnh áp sau VÀ ngược lại) → `Value`/`SourceUnitId` lưu lại luôn là nguồn mạnh
      hơn, không phụ thuộc thứ tự áp dụng.
- [x] **E23**: test khẳng định rõ ràng (dù "yếu" theo thiết kế) — `ActionCommandEvaluator.Evaluate`
      với cùng input ms cho kết quả giống hệt nhau bất kể `PlayerProfileDto.BattleSpeed` truyền
      vào bao nhiêu (chứng minh bằng code review + test rằng `BattleSpeed` không hề được đọc bởi
      `ActionCommandEvaluator`) — ghi rõ trong test đây là "bất biến do thiết kế" chứ không phải
      1 luật được enforce chủ động.

## 5. Test

- [x] Tất cả test mới nằm trong `SimulationTests.EdgeCaseTests` (case pipeline-level: E01, E02,
      E03, E10, E13, E14, E16, E18, E20) hoặc `CoreSystemTests`/`DamageCalculatorTests` (case
      system-level: E05, E06, E07, E08, E09, E21, E23) — đúng theo cách chia hiện có (pipeline
      đầy đủ dùng `CombatSimulation`/`TestFactory.Duel`/`TeamBattle`, còn lại dựng thẳng
      `StatusProcessor`/`DamageCalculator`/`ActionResolver` như style đã có).
  - đặt tên `E<NN>_MôTảHànhViBằngPascalCase`, message assertion ghi rõ "Edge case E<NN>" theo
    đúng convention cũ.
- [x] Nếu cần helper mới trong `TestFactory.cs` (skill hồi sinh, skill Shield, skill Summon) —
      thêm `ReviveSkill(...)`, `ShieldSkill(...)`, `SummonSkill(...)` dùng chung, tránh lặp code
      dựng tay ở nhiều test.
- [x] Chạy full EditMode suite, phải xanh 100%.

## 6. Verification — ĐÃ CHẠY, kết quả thật

- `mcp__unityMCP__run_tests` (EditMode): **222/222 xanh** (208 trước lượt này + 14 test mới/sửa:
  6 vá E05/E06/E09/E13/E14/E21 thành công phần thiếu + 8 test hoàn toàn mới E01/E02/E03/E07/E08/
  E10/E16/E18/E20 — đúng 9 case như kế hoạch, gộp chung 14 vì 1 số case gộp assertion vào test có
  sẵn thay vì tạo test riêng).
- Không cần Play mode — toàn bộ logic Combat thuần C#, EditMode test đã đủ tin cậy (đúng kỷ luật
  deterministic của `Game.Combat`).
- **E18 KHÔNG còn là case chặn** — đã nối dây thật (`BattleSceneInstaller.cs`:
  `Simulation.State.AllowEscape = !Presenter.IsBossFight`, tái dùng đúng heuristic `IsBossFight`
  có sẵn) + có test (`E18_AllowEscape_DefaultsTrue_CanBeDisabledForBossFights`). Chỉ còn UI ẩn nút
  Escape ngoài phạm vi (chưa có nút Escape nào trong UI cả).
- **Kết quả cuối: 21/24 case covered đầy đủ** (5 covered từ trước + 6 vá + 1 wired-and-tested mới
  là E18 + 9 case mới E01/E02/E03/E07/E08/E09/E10/E16/E20/E21/E23 — tính lại đúng: 5+6+10=21).
  **3 case còn lại (E12 AutoRevive, E15 Ultimate execution, E17 BattleSnapshot save/load) tuỳ
  thuộc tính năng chưa xây — không phải nợ test, không viết test giả cho hành vi không tồn tại.**

## 7. E12 AutoRevive — xây thật (lượt sau, đã làm)

- **PassiveTrigger.OnTeamWipe** (mới, giá trị 11) — `Threshold` = % MaxHp hồi lại (mặc định 0.3
  nếu không set). Khác mọi trigger khác: owner ĐANG CHẾT khi trigger nổ.
- `PassiveProcessor.TryAutoRevive(rng)` — quét unit `TeamSide.Player` đã chết, chưa
  `PermanentlyDown`, có `Passive`/`Awakening` mang `OnTeamWipe` chưa `Consumed` → hồi HP theo
  `Threshold`, xoá status, reset Poise, đánh dấu `Consumed`. Method riêng, KHÔNG qua `Fire`/`Apply`
  (2 hàm đó gate `owner.IsAlive`).
- `CombatSimulation.CheckEnd()`: nếu `EvaluateResult()` ra `Defeat` → gọi `TryAutoRevive` trước,
  thành công thì re-evaluate — đúng yêu cầu "kích hoạt TRƯỚC KHI kiểm tra thua".
- **Chưa gán cho hero/Awakening nào** (không có nội dung plan.md nào tên "AutoRevive" ở 6 hero
  mẫu) — cố tình để trống, đúng tinh thần "dư địa mở rộng có chủ đích" đã dùng cho 4 trigger khác
  ở task-ascend.md §10. Trigger đã có hook thật + test thật, sẵn sàng dùng khi có nội dung.
- Test: `SimulationTests.EdgeCaseTests` — 4 test mới (`E12_TeamWipe_WithAutoRevivePassive_...`,
  `..._WithoutAutoRevivePassive_...`, `..._PassiveAlreadyConsumed_...`, `..._ZeroThreshold_...`).
  Verify thêm qua `execute_code` (không cần Play mode, thuần C#): dựng `TestFactory.Duel`, set
  `hero.Hp=0` trước khi có Passive → xác nhận tie-break ATB đưa quyền điều khiển về lại hero ngay
  sau khi hồi sinh (không bị địch đánh chết lần 2 trong cùng 1 `Advance()`).
- **Kết quả: 22/24 case covered.** Còn E15 (Ultimate execution) và E17 (BattleSnapshot) — cả 2 vẫn
  là tính năng CHƯA XÂY (không phải chỉ thiếu edge-case), quy mô lớn hơn nhiều so với E12.

## 8. E15 Ultimate execution — xây thật (lượt sau, đã làm)

**Phát hiện quan trọng:** `BattleState.UltimateGauge` đã tích (+4 dmg/+6 nhận/+8 Perfect qua
`ActionResolver`) nhưng **không có gì tiêu thụ nó** — `CombatUnit.CanUseSkill` không hề biết tới
gauge. Nghĩa là trước lượt này, hero có thể spam skill Ultimate (slot 4, `SpCost=0, Cooldown=0`
theo đúng plan.md — VD `skill_inferno_bulwark` 2.2 power AoE Breaker) **miễn phí mỗi lượt, mãi
mãi** — không phải chỉ thiếu edge-case E15, mà là lỗ hổng cân bằng nghiêm trọng ở core mechanic.

- `BattleState.ConsumeUltimate()` (mới) — set gauge về 0.
- `CombatUnit.CanUseSkill(SkillRuntime skill, bool ultimateReady = true)` — thêm tham số (default
  `true` để không phá caller/test cũ), gate: `skill.SlotIndex==4 && Side==Player && !ultimateReady
  → false`. Enemy/boss dùng slot 4 riêng không bị ảnh hưởng (gauge chỉ của phe Player).
- 4 nơi gọi `CanUseSkill` cập nhật truyền `state.IsUltimateReady`: `CombatSimulation.ExecuteIntent`,
  `AIController.Choose`/`FallbackBasicAttack` (2 chỗ).
- `CombatSimulation.ExecuteIntent`: sau khi `_resolver.Execute(...)` xong, nếu skill vừa dùng là
  Ultimate của Player VÀ actor còn sống → `State.ConsumeUltimate()`. **Actor chết giữa lúc dùng
  Ultimate (edge case E15 thật) → KHÔNG gọi, gauge giữ nguyên cho hero khác.**
- UI (`BattleHudScreen`/`SkillSlotView`) đã sẵn có (đọc `IsUltimateReady`, khoá nút) — lượt này chỉ
  vá tầng Combat/Simulation, không đụng UI.
- Test: 3 test mới trong `SimulationTests.EdgeCaseTests` — gauge chưa đầy → rơi về đánh thường,
  gauge không đổi; dùng thành công → tiêu về 0; **actor chết do Reflect ngay từ chính đòn Ultimate
  → gauge giữ nguyên 100** (dùng damage Magical + status Reflect thay vì Physical + Counter để
  không phụ thuộc RNG né đòn — kết quả xác định 100%, đúng kỷ luật determinism `Game.Combat`).
  Verify thêm qua `execute_code` trước khi viết test chính thức (phát hiện: phiên bản đầu dùng
  Physical + Counter bị miss ngẫu nhiên, phải đổi sang Magical + Reflect).
- **Kết quả: 23/24 case covered.** Chỉ còn E17 (BattleSnapshot save/load resume) — vẫn là tính
  năng chưa xây, quy mô lớn nhất trong 3 case còn lại ban đầu (cần replay/save schema thật).

## 9. E17 BattleSnapshot resume — xây thật (lượt sau, đã làm — 24/24 hoàn tất)

**Thiết kế cốt lõi:** chỉ cần lưu **intent của phe Player** (không cần lưu/replay intent enemy).
Lý do: enemy tự tái tạo GIỐNG HỆT qua AI + cùng seed nhờ bảo đảm determinism toàn hệ thống đã có
sẵn (`DeterminismTests`) — đúng tinh thần `ReplayData = {seed, battleConfigId, intent list}` ở
plan.md §4.17. Replay = dựng lại sim cùng seed/team → lặp `Advance()` (tự chạy enemy) rồi
`SubmitIntent(intent đã ghi)` cho từng intent Player đã lưu, theo ĐÚNG thứ tự.

- **`PlayerProfileDto.BattleSnapshotDto`** — bỏ field `StageId` (chưa từng dùng), thêm `NodeId`,
  `HeroDefIds`, `EnemyDefIds` (cần để tái tạo ĐÚNG team gốc — enemy roster được chọn ngẫu nhiên
  1 lần lúc `LaunchBattle` bằng nguồn không xác định lại được, seed riêng của Combat không đủ).
- **`Game.Combat.BattleReplay.ReplayPlayerIntents(sim, playerIntents)`** (mới, `Assets/_Project/
  Scripts/Combat/BattleReplay.cs`) — pure C#, không phụ thuộc Unity, w/ 4 test EditMode riêng
  (`BattleReplayTests.cs`). Trả `false` nếu actor đang chờ input không khớp intent tiếp theo
  (snapshot lệch trận hiện tại) — caller không được cố chơi tiếp từ trạng thái sai.
- **`BattleSceneInstaller`**: `OnApplicationPause(true)` (Unity lifecycle, kích hoạt đúng lúc
  "thoát app giữa trận") → `SaveSnapshot()` ghi seed/NodeId/HeroDefIds/EnemyDefIds + lọc
  `IntentLog` chỉ giữ intent của Player → lưu vào `profile.Run.BattleSnapshot` → `SaveAsync`.
  `BuildBattle()`: sau `Simulation.Start()`, gọi `TryResumeFromSnapshot()` — nếu có snapshot hợp
  lệ khớp `NodeId`, replay trước khi trao quyền điều khiển sống.
- **`MetaSceneInstaller`**: `Start()` — sau `EnsureRun()`, `BindCanvasRefs()` rồi
  `TryResumeBattleFromSnapshot()` — nếu profile có snapshot hợp lệ, tự động `QueueBattle` +
  `LoadScene("Battle")` NGAY, bỏ qua Node Map. `ApplyPendingBattleResult()`: xoá snapshot khi có
  kết quả trận thật (Victory/Defeat/Escaped/Timeout) — chống snapshot cũ sống sót nếu app chỉ
  pause bình thường (không bị kill) rồi người chơi tự đánh xong trong cùng phiên.
- **Lỗi phát hiện lúc verify Play mode thật (đã vá):** `TryResumeBattleFromSnapshot()` gọi
  `_canvasRoot.gameObject.SetActive(false)` nhưng `_canvasRoot` chỉ được gán bên trong
  `BindCanvasRefs()`/`BuildUi()` — thứ tự gọi ban đầu đặt check resume TRƯỚC khi bind, gây
  `NullReferenceException`. Vá bằng cách gọi `BindCanvasRefs()` tường minh trước check resume.
  Đây là ví dụ cụ thể vì sao lượt này bắt buộc verify Play mode thật, không dừng ở EditMode test.
- **Verify Play mode thật (không phải chỉ execute_code giả lập)** — kịch bản đầy đủ nhất trong cả
  5 lượt task-edgecases.md: vào node Battle thật qua UI (click node → TeamSelect → Start), chơi 3
  lượt thật (3 hero khác nhau lần lượt hạ 1 enemy), gọi `OnApplicationPause(true)` qua reflection
  → xác nhận snapshot ghi đúng NodeId/seed/HeroDefIds/EnemyDefIds/3 intent Player. Sau đó **thoát
  Play mode và vào lại Play mode thật** (mô phỏng gần nhất với "khởi động lại app" có thể làm
  trong Editor — buộc load lại profile từ file save AES thật trên đĩa, không phải chỉ giữ state
  trong RAM) → xác nhận: scene tự nhảy thẳng vào Battle, `RunContext.Pending` đúng dữ liệu, và sau
  khi build lại trận (`BuildBattle()`), **HP của cả 7 unit khớp tuyệt đối** với trạng thái ngay
  lúc pause ban đầu. Đây là bằng chứng thật nhất có thể có được trong Editor cho "resume sau khi
  app bị kill" — không chỉ là logic thuần lý thuyết.
- Sau khi verify xong, dọn sạch trận test khỏi save thật của người dùng (không để lại snapshot dở
  dang trong save.json) — xác nhận lại `BattleSnapshot.Valid=False` trước khi kết thúc lượt.
- **Kết quả: 24/24 edge case (plan.md §4.14) covered — toàn bộ danh sách edge case đã hoàn tất.**

## 10. Ngoài phạm vi (E17, ghi rõ)

- **Không có dialog xác nhận "Tiếp tục trận đang dở?"** trước khi tự resume — auto-resume thẳng,
  đơn giản hoá tối thiểu cho V1. Nếu muốn hỏi trước, cần thêm 1 màn hình xác nhận ở
  `MetaSceneInstaller.TryResumeBattleFromSnapshot()`.
- **Không throttle/giới hạn tần suất ghi snapshot** — mỗi lần `OnApplicationPause(true)` đều ghi
  đè + `SaveAsync` toàn bộ profile; chấp nhận được vì pause không xảy ra liên tục trong 1 trận.
  Không capture liên tục theo mỗi lượt (khác cân nhắc ban đầu) — chỉ khi thật sự pause.
  `OnApplicationQuit()` KHÔNG được gọi riêng (đủ dùng `OnApplicationPause`, vì trên mobile OS luôn
  gọi pause trước khi có thể kill process; PC/Editor có thể thoát thẳng không qua pause — chấp
  nhận rủi ro nhỏ này, ngoài phạm vi vá thêm ở V1).
- **`ReplayVerifier`/chống gian lận server-side** (plan.md §4.17) — hoàn toàn chưa xây, đây chỉ là
  cơ chế resume cho người chơi tự nhiên, không phải hệ chống cheat khi lên server (v1.2+).
- **"Xem lại trận" (replay viewer UI)** — cơ chế `BattleReplay` đủ nền tảng để làm tính năng này
  sau, nhưng UI xem lại (tua nhanh/chậm, không tương tác) chưa xây.

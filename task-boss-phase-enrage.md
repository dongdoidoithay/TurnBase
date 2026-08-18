# Task: Boss Phase Transitions + Enrage + Signature Move

plan.md §4.13.3. Từ audit "hoàn thiện chức năng" — người dùng chọn qua AskUserQuestion cùng đợt
Red Dot, Gacha rate disclosure, Accessibility phần còn lại. Trước lượt này chỉ có khung sườn CHẾT:
`BattleState.EnrageRound=12` không ai đọc, `CombatEventType.PhaseChanged` không bao giờ phát,
`AIConditionType.PhaseIs` không bao giờ kích hoạt được (không có gì gán `phase` khác 0), không có
`SignatureMove` nào — boss đánh y hệt suốt trận bất kể HP.

## §1. Phase — `CombatSimulation.RefreshBossPhases`

`CombatUnit.Phase` mới (mặc định 1, CHỈ TĂNG — không giảm lại nếu được hồi máu, tránh nhảy tới nhảy
lui). Ngưỡng 60%/30% HP đúng plan.md. Gọi ở 2 điểm: ngay sau `_resolver.Execute(...)` trong
`ExecuteIntent` (bắt mọi đòn/counter/reflect — nguồn HP-đổi chính) và sau `_status.TickTurnStart`
trong `BeginTurn` (bắt DoT tự gây lên chính actor). Idempotent (so sánh Phase trước/sau nên gọi
nhiều lần trong 1 lượt không lặp event).

Phase mới → `PoiseSystem.ResetOnPhaseChange(unit)` (hàm CÓ SẴN từ trước, chưa ai gọi tới — phát hiện
lúc đọc code, tái dùng thay vì viết lại) + phát `CombatEventType.PhaseChanged`.

## §2. Enrage — `CombatSimulation.RefreshBossEnrage`

Sau `BattleState.EnrageRound` (mặc định 12), +50% ATK/+30% SPD MỖI 3 lượt tiếp theo, cộng dồn — đúng
số plan.md. Implement bằng cách thêm `StatModifier(AtkPct/SpdPct)` vào `CombatUnit.PassiveModifiers`
(tái dùng pipeline `ComputeStats()` có sẵn qua `Accumulate(PassiveModifiers)`, không cần bước tính
stat riêng) + `MarkStatsDirty()`. Gọi 1 lần mỗi `BeginRound()`. Phát `CombatEventType.Enraged`.

Chỉ áp cho `IsBoss == true` (đúng phạm vi §4.13.3 "Boss", không phải mọi enemy).

## §3. Signature Move — `AIController.Choose`

`AIRule.IsSignatureMove` (bool) mới. Khi rule này thắng điểm VÀ chưa có move nào đang đếm dở:
KHÔNG thực thi ngay — set `CombatUnit.SignatureMoveTurnsLeft = 3` + `PendingSignatureMoveSkillId`,
đặt cooldown rule, rồi tự chấm điểm lại (loại IsSignatureMove) để chọn hành động THẬT cho lượt này
(fallback, không phải lượt trắng). Các lượt kế của CHÍNH unit đó: đếm `SignatureMoveTurnsLeft--`; về
0 thì thực thi signature skill ngay (bỏ qua scoring bình thường lượt đó).

**Counterplay bắt buộc (plan.md: "Break / dispel / burst")**: implement **Break** — nếu unit đang
`IsBroken` khi `Choose()` được gọi, huỷ ngay pending signature move. Dispel/burst KHÔNG implement
(dispel cần cơ chế "gỡ 1 trạng thái nội bộ AI" chưa có khái niệm tương đương status effect nào;
burst — hạ gục trước khi move nổ — đã tự nhiên đúng vì unit chết thì không còn lượt để thực thi,
không cần code riêng). Ghi rõ như giới hạn đã biết, không giả vờ đủ cả 3.

**`peek` (Intent Preview) KHÔNG được mutate** — mọi thay đổi `SignatureMoveTurnsLeft`/
`PendingSignatureMoveSkillId` chỉ chạy khi `!peek`, cùng nguyên tắc `rng.Fork()` đã có sẵn cho peek.

**Wiring vào nội dung thật**: `ai_boss` profile (dùng bởi MỌI boss đơn — `BattleSceneInstaller`,
`Presenter.IsBossFight` = 1 địch) đã có sẵn đúng 1 rule "chiêu đặc trưng" (skill slot 1, weight 70,
cooldown 2) từ trước — đánh dấu `IsSignatureMove = true` ngay rule đó thay vì thêm rule/nội dung
mới. Mọi boss hiện có tự động có Signature Move thật, không cần sửa data 66 enemy.

## §4. Rủi ro & xác nhận an toàn

- Grep toàn bộ `Assets/Tests/` xác nhận **0 test nào set `IsBoss = true`** trước lượt này → thêm
  `PhaseChanged`/`Enraged` event KHÔNG thể phá `GoldenScenarioTests` (hardcode `ToSignature()`,
  nhạy với event mới xen vào) hay bất kỳ test cũ nào.
- 2 `CombatEventType` mới (`Enraged=29`, `SignatureMoveTelegraphed`... — thực ra KHÔNG phát
  `SignatureMoveTelegraphed` ở lượt này, chỉ khai chỗ cho tương lai UI/Presenter cần; logic hiện tại
  chỉ phát `PhaseChanged`/`Enraged`) thêm ở CUỐI enum, không chèn giữa → không đổi giá trị số của
  event cũ.
- Refactor nhỏ trong `AIController.Choose`: đường KHÔNG-signature-move (mọi profile hiện có, vì
  `IsSignatureMove` mặc định `false`) giữ NGUYÊN 100% logic/thứ tự tiêu RNG cũ (vòng lặp chấm điểm +
  vòng lặp đặt cooldown-theo-rule-khớp-skill không đổi) — chỉ chèn 2 nhánh MỚI hoàn toàn cô lập
  (đầu hàm cho countdown, cuối vòng lặp scoring cho bắt đầu telegraph).

## §5. Verify

- `validate_script` 5 file sửa (`CombatUnit.cs`, `CombatEvent.cs`, `AIController.cs`,
  `CombatSimulation.cs`, `BattleSceneInstaller.cs`) 0 lỗi. Compile toàn project 0 lỗi.
- 10 test mới:
  - `BossSignatureMoveTests.cs` (5 test, gọi thẳng `AIController.Choose` không cần dựng cả
    simulation) — bắt đầu đếm/rơi về fallback, đếm tiếp không thực thi sớm, thực thi đúng lúc về 0,
    Break huỷ giữa chừng, peek không mutate, profile không có `IsSignatureMove` không bị ảnh hưởng.
  - `BossPhaseEnrageTests.cs` (5 test, dựng `CombatSimulation` thật qua `TestFactory`, chơi thật
    bằng `Advance()`/`SubmitIntent`) — đòn mạnh nhảy thẳng phase 3 + reset Poise + đúng 1 event, đòn
    nhẹ không đổi phase, unit không phải boss không bao giờ đổi Phase, qua EnrageRound cộng buff
    thật (đo `Stats.AtkPhys` tăng thật qua `ComputeStats()`, không chỉ đọc field), trước EnrageRound
    không có gì áp.
- **657/657 (baseline) + 10 mới xanh** — xem kết quả `run_tests` thật sau khi validate.

## §6. Ngoài phạm vi

- Dispel/burst counterplay cho SignatureMove (chỉ Break) — xem §3.
- UI hiển thị Phase/Enrage/SignatureMove countdown công khai trên đầu boss ("hiển thị công khai"
  theo plan.md) — lượt này chỉ xây ĐÚNG logic + phát event, chưa nối vào `BattleHudScreen`/
  `CombatPresenter`. Event `PhaseChanged`/`Enraged` đã phát sẵn, cắm UI sau không cần đổi lõi.
- Animation chuyển pha ("có animation chuyển pha") — chưa có clip/VFX riêng.
- Enrage/Phase cho MỌI enemy (chỉ Boss, đúng phạm vi §4.13.3 tên chương).

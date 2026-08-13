# TASK-SETBONUS.md — Set Bonus trang bị (8 bộ, plan.md §7.4)

> Mục tiêu: xây `SetBonusResolver` — 2 món kích hoạt bonus nhẹ (stat), 4 món kích hoạt bonus mạnh
> (passive/proc). Liên quan: [plan.md §7.4](plan.md), [roadmap.md §0.1](roadmap.md),
> [object-map.md §12/§12.1](object-map.md). `EquipmentInstanceDto.SetId` đã có field sẵn từ
> task-equipment.md, chưa ai đọc — đúng pattern "data model có sẵn chưa wire" lặp lại lần nữa.

---

## 0. Phát hiện quan trọng trước khi làm

- `StatType` (`MetaEnums.cs`) đã có ĐỦ mọi field % cần cho 7/8 bộ 2-món:
  `AtkPct/DefPct/CritPct/MaxHpPct/PoiseDmgPct/LifestealPct` — và các field derived tương ứng
  (`Stats.DmgBonus/DmgReduct/PoiseDmgBonus/Lifesteal`) **ĐÃ được `DamageCalculator`/
  `ActionResolver` đọc thật** (không phải dead field như lo ban đầu — đã kiểm bằng grep trực
  tiếp, không suy đoán). Nghĩa là bonus 2-món chỉ cần thêm `StatModifier` vào
  `CombatUnit.EquipmentModifiers`, KHÔNG cần sửa gì ở Combat layer.
- **Thiếu 1 field**: không có `StatType.MaxSpPct` (chỉ có `MaxHp Pct`, không có tương đương cho
  SP) — cần cho bộ Sage (+15% MaxSP). Thêm mới, mirror đúng pattern `MaxHpPct` đã có
  (`CombatUnit.ComputeStats`: biến tích luỹ `spPct`, áp ở cuối `d.MaxSp = Round(d.MaxSp * (1+spPct))`).
- `CombatUnit` chỉ có 2 slot passive: `.Passive` (Innate) + `.Awakening` — không phải
  `List<PassiveData>`. Set Bonus 4-món cần slot thứ 3 → thêm field `CombatUnit.SetBonus` (đơn,
  không phải list — 1 hero chỉ có thể đang mặc ĐỦ 4 món của ĐÚNG 1 bộ tại 1 thời điểm, không cần
  nhiều slot). `PassiveProcessor.Fire()`/`CheckHpThreshold()`/`TryAutoRevive()` (3 chỗ) phải thêm
  đúng 1 dòng gọi cho `owner.SetBonus`, y hệt cách `.Passive`/`.Awakening` đã làm — không đổi kiến
  trúc, chỉ thêm 1 nguồn.
- `PassiveTrigger` (`CombatEnums.cs`) đã có `OnPerfectCommand`/`OnHpBelowThreshold`/`OnKill`/
  `OnBreakTriggered`/`OnHitDealt` — đủ cho 5/8 bộ 4-món KHÔNG cần trigger mới. Thiếu
  `OnTurnEnd` (cho Guardian) — thêm mới, mirror đúng cách `OnTeamWipe`/`OnBreakTriggered` đã được
  thêm trước đây (thêm enum value + 1 method `TriggerOnTurnEnd` + 1 call site trong
  `CombatSimulation.FinishTurn()`).
- `StatusApplication.TargetSelf` chỉ hỗ trợ Self hoặc "đối tượng ngữ cảnh" (contextTarget) — bộ
  Breaker cần buff CẢ ĐỘI khi Break 1 địch, chưa hỗ trợ. Thêm field mới
  `StatusApplication.TargetAllAllies` (bool, song song `TargetSelf`), xử lý trong
  `PassiveProcessor.Apply()`.
- `ActionResolver.Execute` đã có `data.SpCost` sẵn trong scope ngay tại điểm gọi
  `TriggerOnPerfectCommand` (dòng ngay sau `actor.AddSp(-data.SpCost)`) — bộ Sage (hoàn 30% SP khi
  Perfect) khả thi rẻ: truyền thêm `spCost` vào `TriggerOnPerfectCommand`, `PassiveData` thêm field
  `SpRefundPercent` (float, mặc định 0).
- **Đã kiểm tra kỹ (quan trọng, đổi hướng implement so với dự đoán ban đầu):**
  `equipment.csv`/`EquipmentDefinitionSO` KHÔNG có cột/field `SetId` nào cả — `SetId` CHỈ tồn tại
  trên `EquipmentInstanceDto` (mặc định `""`) và KHÔNG bao giờ được gán ở đâu, kể cả
  `EquipmentGenerator.RollFrom`. Đúng NGHĨA đây là field ROLL NGẪU NHIÊN Ở TẦNG INSTANCE, giống
  hệt cách `Rarity`/sub-stat đã làm ("Chọn def theo slot, bỏ qua def.Rarity — rồi TỰ ROLL Rarity +
  sub-stat của INSTANCE độc lập với def", xem docstring `EquipmentGenerator`) — KHÔNG PHẢI thuộc
  tính cố định của từng loại trang bị trong catalog. **Kết luận: KHÔNG cần author thêm CSV/data
  equipment nào cả** — chỉ cần `RollFrom` roll thêm 1 `SetId` ngẫu nhiên (đều 1/8 mỗi bộ) mỗi khi
  sinh instance mới, y hệt cách `RollSubStats` đã làm. Xoá bỏ lo ngại ban đầu về việc phải author
  thêm data catalog — không cần.

## 1. Phạm vi V1 — 8/8 bộ 2-món, 7/8 bộ 4-món

Toàn bộ 8 bộ đều có bonus 2-món (rẻ, đồng nhất — chỉ StatModifier). Bonus 4-món: làm 7/8, **hoãn
Tempest** — lý do xem §1.1.

| Bộ | 2 món (StatType) | 4 món | Trigger | Cách làm |
|---|---|---|---|---|
| Ember | `AtkPct` +12 | Đòn Perfect gây `burn` 3 lượt | `OnPerfectCommand` | Có sẵn, dùng `Applies` |
| Bastion | `DefPct` +15 | HP<50% → `shield` 20% MaxHP, 1 lần/trận | `OnHpBelowThreshold` | Có sẵn, dùng `CheckHpThreshold` (y hệt Awakening) |
| Tempest | `Spd` +8 (flat) | Hành động đầu mỗi round +20% dmg | — | **HOÃN — xem §1.1** |
| Assassin | `CritPct` +10 | Crit → thêm 15% dmg đã gây dạng `bleed` | `OnHitDealt` | **Đơn giản hoá**: potency `bleed` CỐ ĐỊNH (không scale theo damage thật — `StatusApplication.Potency` không hỗ trợ %-của-damage-vừa-gây), ghi rõ trong code + task này |
| Sage | `MaxSpPct` +15 (field mới) | Perfect → hoàn 30% SP kỹ năng vừa dùng | `OnPerfectCommand` + `spCost` | Field mới `PassiveData.SpRefundPercent`, xử lý trong `PassiveProcessor.Apply` |
| Guardian | `MaxHpPct` +12 | Hồi 8% MaxHP mỗi khi kết thúc lượt | `OnTurnEnd` (mới) | Trigger mới, mirror `OnBreakTriggered` |
| Breaker | `PoiseDmgPct` +15 | Break mục tiêu → CẢ ĐỘI +15% ATK 2 lượt | `OnBreakTriggered` | Có sẵn trigger, cần `StatusApplication.TargetAllAllies` (mới) |
| Vampire | `LifestealPct` +8 | Giết địch → hồi 15% MaxHP | `OnKill` | Có sẵn, dùng `Applies` (heal qua status hoặc trực tiếp `SetHp`) |

### 1.1. Vì sao hoãn Tempest

"Hành động đầu tiên mỗi round +20% dmg" cần biết TẠI THỜI ĐIỂM TÍNH DAMAGE liệu actor có phải là
người hành động đầu tiên round này không — nhưng `DamageCalculator.Calculate` là method TĨNH,
KHÔNG nhận `BattleState`/`RoundNumber` làm tham số (chỉ nhận attacker/defender/skill/grade/rng).
Muốn biết cần 1 trong 2 hướng đều tốn hơn hẳn 7 bộ còn lại:
(a) thêm tham số `BattleState` vào `Calculate` — đổi chữ ký, sửa MỌI call site + test hiện có
(rủi ro regression cao nhất trong task này), hoặc
(b) track cờ "đã hành động lần đầu round này" ở tầng `CombatUnit`/`BattleState`, set đúng lúc
TRƯỚC khi combat resolve — thêm state mới vào vòng lặp lượt (`BeginRound`/`ExecuteIntent`), phức
tạp hơn hẳn "1 trigger nổ tại 1 điểm có sẵn" của 7 bộ kia.

Không tương xứng để làm chung 1 task với 7 bộ còn lại vốn chỉ cần trigger có sẵn. Ghi lại đây,
KHÔNG xoá `Tempest` khỏi `SetBonusCatalog` — chỉ để field 4-món passive = `null` (giống style
"chưa xây" đã dùng ở nơi khác, VD `DungeonKind.Tower` trước khi được làm), 2-món (`Spd` +8) vẫn
hoạt động đầy đủ ngay từ V1 vì không liên quan tới giới hạn này.

## 1.2. Tempest 4-món — làm sau ("tiếp tục Tempest 4-món")

Đánh giá lại §1.1 sau khi đã xây xong 7 bộ kia: kết luận "cần đổi chữ ký `DamageCalculator.
Calculate`" ở trên là BI QUAN QUÁ MỨC — nhìn kỹ cách `ExtraDamagePercent` (task-extra-damage.md)
đã hoạt động thì thấy `Calculate` **không cần `BattleState`**, nó chỉ cần đọc 1 field TRÊN CHÍNH
`CombatUnit` (`attacker.Passive`/`.Awakening`), y hệt cách nó đã đọc `attacker.Stats.DmgBonus`.
Muốn biết "actor có phải người hành động đầu round này" tại thời điểm `Calculate` chạy, chỉ cần
1 field bool TRÊN `CombatUnit`, được `CombatSimulation` set đúng lúc TRƯỚC KHI gọi
`ActionResolver` — không phải sửa `Calculate`.

**Thiết kế (không đổi chữ ký `Calculate`, không rủi ro regression cho 7 bộ/mọi trận khác):**

- [x] `CombatUnit.cs`: thêm `public bool IsFirstActorThisRound;`.
- [x] `CombatSimulation.cs`:
      - `BeginRound()`: thêm vòng lặp reset `u.IsFirstActorThisRound = false` cho mọi
        `State.Units`, và reset field mới `_roundFirstActorAssigned = false` (private, theo dõi
        "đã phát cờ cho round này chưa").
      - `BeginTurn()`: ngay TRƯỚC `return true` cuối cùng (đúng lúc actor CHẮC CHẮN sẽ hành động
        thật, không phải bị skip do chết/stun/paralyze/minion hết hạn) — nếu
        `!_roundFirstActorAssigned`, set `_currentActor.IsFirstActorThisRound = true;
        _roundFirstActorAssigned = true;`. Actor bị skip KHÔNG được tính là "hành động đầu" —
        đúng nghĩa đen "hành động" (action), không phải "lượt" (turn).
- [x] `Combat/Model/CombatUnit.cs` (`PassiveData`): thêm `public bool RequiresFirstActionOfRound;`
      (mirror `RequiresCrit`/`RequiresPerfectGrade`).
- [x] `Combat/Systems/DamageCalculator.cs`:
      - `ExtraDamageFrom(PassiveData p)` → đổi chữ ký thêm `CombatUnit attacker`, gate thêm
        `(!p.RequiresFirstActionOfRound || attacker.IsFirstActorThisRound)`.
      - **Phát hiện phụ**: dòng gọi `ExtraDamageFrom` ở bước 7 hiện CHỈ cộng `attacker.Passive`/
        `.Awakening`, THIẾU `attacker.SetBonus` — không bộ nào trong 7 bộ đã xây dùng
        `ExtraDamagePercent` nên lỗ hổng này chưa lộ ra qua test nào. Fix cùng lúc: thêm
        `ExtraDamageFrom(attacker.SetBonus, attacker)` vào tổng.
- [x] `Meta/Equipment/SetBonusCatalog.cs`: đổi `"tempest" => null` thành
      `Trigger = OnHitDealt, RequiresFirstActionOfRound = true, ExtraDamagePercent = 20f`.
- [x] Test: `CombatSimulation` chỉ đúng 1 unit/round có `IsFirstActorThisRound=true`, reset đúng
      round sau; actor bị skip (VD Stun) KHÔNG được tính; `DamageCalculator` cộng đúng 20% khi cờ
      bật + có Tempest SetBonus, KHÔNG cộng khi thiếu 1 trong 2 điều kiện; cập nhật
      `SetBonusCatalog_FourPiece_SevenOfEightSets...` → 8/8; xoá `Tempest.FourPiece == null` test
      cũ, thay bằng test dương tính.
- [x] Chạy lại toàn bộ EditMode suite — baseline 346/346, không được regression.

## 1.3. Phát hiện bug thật KHÔNG liên quan trực tiếp Set Bonus — `RoundNumber` không bao giờ tăng

Lúc viết test "IsFirstActorThisRound đổi người khi sang round mới", phát hiện `State.RoundNumber`
**KHÔNG BAO GIỜ tăng quá 1 trong suốt cả trận**. Nguyên nhân: `SimPhase.RoundEnd` (case xử lý sẵn
trong switch của `Advance()`, chuyển `Phase = SimPhase.RoundStart` để gọi lại `BeginRound()`) —
nhưng **không có bất kỳ chỗ nào trong code gán `Phase = SimPhase.RoundEnd` cả**. `FinishTurn()`/
`FinishTurnSkipped()` luôn quay lại thẳng `TurnStart`, không bao giờ qua `RoundEnd`. `BeginRound()`
(tăng `RoundNumber`, tick `AIController.TickRuleCooldowns`, phát event `RoundStarted`) vì vậy chỉ
chạy ĐÚNG 1 LẦN — lúc `Start()` gọi lần đầu.

**Ảnh hưởng thật:** `AIConditionType.RoundAtLeast` (`state.RoundNumber >= Value`) — đã kiểm, KHÔNG
có enemy/boss nào trong data thật hiện dùng điều kiện này (an toàn, fix không đổi hành vi gì đang
chạy), nhưng cơ chế này chưa bao giờ hoạt động đúng nếu có ai dùng. Quan trọng hơn: Tempest 4-món
("hành động đầu MỖI ROUND") sẽ chỉ nổ ĐÚNG 1 LẦN CHO CẢ TRẬN (người đầu tiên hành động khi trận bắt
đầu) thay vì mỗi round — sai hẳn tinh thần thiết kế, phải sửa để Tempest có ý nghĩa thật.

**Sửa (tối thiểu, đúng phạm vi cần cho Tempest hoạt động đúng):**

- [x] `CombatUnit.cs`: thêm `public bool HasActedThisRound;` — khác `IsFirstActorThisRound`
      (Tempest, loại trừ turn bị skip): field này tính CẢ turn bị skip (chết/stun/paralyze) là "đã
      qua lượt round này", để 1 unit bị stun vĩnh viễn không chặn round kết thúc mãi mãi.
- [x] `CombatSimulation.cs`:
      - `BeginRound()`: reset `u.HasActedThisRound = false` cho mọi unit (cùng vòng lặp reset
        `IsFirstActorThisRound`).
      - `BeginTurn()`: set `_currentActor.HasActedThisRound = true` NGAY ĐẦU (sau khi chọn actor,
        TRƯỚC mọi nhánh skip) — khác `IsFirstActorThisRound` chỉ set ở cuối khi chắc chắn không
        skip.
      - Thêm helper `AllAliveUnitsActedThisRound()` — duyệt `State.Units`, còn sống mà chưa
        `HasActedThisRound` thì trả false.
      - `FinishTurn()`/`FinishTurnSkipped()`: đổi `if (!IsFinished) Phase = SimPhase.TurnStart;`
        thành `if (!IsFinished) Phase = AllAliveUnitsActedThisRound() ? SimPhase.RoundEnd :
        SimPhase.TurnStart;`. Unit MỚI thêm giữa round (Tower multi-wave, Summon minion) mặc định
        `HasActedThisRound=false` nên round không kết thúc sớm khi họ chưa được hành động.
- [x] Test: round thật sự tăng qua nhiều round trong 1 trận dài; unit mới thêm giữa round (mô
      phỏng Tower wave) không làm round kết thúc sớm; unit bị skip cả round (Stun dài) không chặn
      round những unit khác vẫn kết thúc đúng.
- [x] Chạy lại EditMode suite — không được regression cho bất kỳ trận/test nào khác (fix này đụng
      vào core loop `CombatSimulation`, rủi ro cao nhất trong cả 2 đợt Set Bonus).

**Kết quả:** 350/350 test EditMode xanh (346 + 4 mới: round thật sự tăng qua nhiều round,
`CombatUnit.SetBonus` được cộng vào `ExtraDamagePercent` — lỗ hổng phụ đã sửa, Tempest cộng đúng
20% khi cờ bật, không cộng khi tắt) — bao gồm cả `FuzzBattleTests` (2000 trận ngẫu nhiên) và cả 3
`MultiWaveTests` (Tháp Vô Tận — unit mới thêm giữa round không làm round kết thúc sớm) đều xanh
sau khi đụng vào core loop, xác nhận fix an toàn.

## 2. Checklist implement

### 2.1. Data model

- [x] `Data/Enums/MetaEnums.cs`: thêm `StatType.MaxSpPct = 40` (nối sau `PoiseDmgPct = 39`).
- [x] `Data/Enums/CombatEnums.cs`: thêm `PassiveTrigger.OnTurnEnd = 12` (nối sau `OnTeamWipe = 11`).
- [x] `Combat/Model/CombatUnit.cs`:
      - `ComputeStats()`: thêm biến `spPct`, case `StatType.MaxSpPct: spPct += m.Value/100f`,
        áp `d.MaxSp = RoundToInt(d.MaxSp * (1f + spPct))` cuối hàm (mirror `hpPct`/`MaxHp`).
      - Thêm field `public PassiveData SetBonus;` (slot thứ 3, song song `Passive`/`Awakening`).
- [x] `Combat/Model/StatusApplication.cs` (hoặc nơi struct này khai báo): thêm
      `public bool TargetAllAllies;` (mặc định false — không đổi hành vi status application hiện
      có nào).
- [x] `Combat/Model/PassiveData.cs` (hoặc file khai báo): thêm `public float SpRefundPercent;`
      (mặc định 0).
- [x] **Phát hiện lúc implement (không có trong bản kế hoạch gốc):** Vampire (hồi 15% MaxHP khi
      giết) và Guardian (hồi 8% MaxHP cuối lượt) đều cần hồi máu NGAY LẬP TỨC — không có field nào
      trên `PassiveData` hỗ trợ (`Applies`/`StatusApplication` chỉ áp STATUS như Regen/Shield, không
      phải heal tức thời; Regen là hiệu ứng tick theo lượt, không tương đương). Thêm
      `public float HealPercentMaxHp;` (mặc định 0) — dùng chung cho cả 2 bộ, xử lý trong
      `PassiveProcessor.Apply()` bằng đúng pattern `ActionResolver.ResolveHeal` đã dùng
      (`SetHp` cắt tại MaxHp, tính `actual` từ hiệu số trước/sau, emit `CombatEventType.HealApplied`
      để Presenter hiện floating heal number — không tự chế cách hiện khác).
- [x] **Phát hiện thứ 2 lúc implement — SỬA LẠI cách làm Bastion (khác dự tính ban đầu ở bảng §1):**
      `StatusProcessor.Apply(source, target, in StatusApplication, rng)` (đường chung mà
      `PassiveProcessor.Apply` gọi qua `Applies[]`) tính `Value` của status qua `ComputeStatusValue`
      — hàm này CHỈ xử lý riêng `Bleed` (= ATK nguồn), mọi status khác kể cả `Shield` đều nhận
      `Value = 0`. Nghĩa là nếu áp `StatusId.Shield` qua `Applies[]` như kế hoạch gốc, shield sẽ có
      lượng hấp thụ = 0 (vô dụng). `StatusProcessor` có sẵn method riêng
      `ApplyShield(source, target, float amount, int duration, rng)` chính xác cho việc này (dùng
      bởi skill có `ShieldPower`) — nhưng cần `amount` tính sẵn (%MaxHP), không nhận qua
      `StatusApplication`. Thêm `public float ShieldPercentMaxHp;` trên `PassiveData`, xử lý trong
      `PassiveProcessor.Apply()`: `_status.ApplyShield(owner, owner, owner.MaxHp *
      ShieldPercentMaxHp/100f, duration: 3, rng)` (duration mirror `StatusTable.Shield.
      DefaultDuration = 3` — không quan trọng lắm vì `Consumed=true` của `OnHpBelowThreshold` đã
      đảm bảo chỉ nổ 1 lần/trận, duration chỉ quyết định shield tồn tại bao lâu nếu không bị hấp
      thụ hết trước đó).
- [x] **Phát hiện thứ 3 lúc implement:** `result.IsCrit` (đã tính sẵn trong `DamageCalculator.
      Calculate`) thật ra CÓ SẴN trong scope ngay tại điểm `ActionResolver` gọi
      `TriggerOnHitDealt` (vài dòng phía trên) — không cần bỏ qua yêu cầu "Crit" của bộ Assassin
      như lo ban đầu. Thêm `PassiveData.RequiresCrit` (bool) + tham số `isCrit` xuyên suốt
      `TriggerOnHitDealt`/`Fire`/`FireOne` (mirror đúng cách `spCost` đã làm cho Sage) — `FireOne`
      gate `if (passive.RequiresCrit && !isCrit) return;`. Mọi passive `OnHitDealt` khác trong
      `AwakeningCatalog` (Frost Sage/Pyromancer/Terra Seer/Void Scholar) không set field này nên
      KHÔNG đổi hành vi.
- [x] **Phát hiện thứ 4 lúc implement — SỬA LẠI cách làm Ember (khác dự tính ban đầu ở bảng §1):**
      `PassiveTrigger.OnPerfectCommand` bắn ở `ActionResolver.Execute` TRƯỚC khi biết mục tiêu cụ
      thể bị đánh trúng (trigger chung cho grade, không phải cho 1 cú đánh có target) — `Fire(actor,
      actor, ...)` nghĩa là contextTarget LUÔN LÀ actor, không phải địch. Nếu áp Burn qua trigger
      này với `TargetSelf=false` như kế hoạch gốc, Burn sẽ rơi NHẦM lên chính actor thay vì địch.
      Sửa: dùng `OnHitDealt` (đã có contextTarget = địch thật) thay vì `OnPerfectCommand`, thêm
      field `PassiveData.RequiresPerfectGrade` (bool) + tham số `isPerfect` xuyên suốt
      `TriggerOnHitDealt`/`Fire`/`FireOne` — mirror Y HỆT cách `RequiresCrit`/`isCrit` vừa làm cho
      Assassin (tái dùng luôn tham số `grade` đã có sẵn trong `ActionResolver.Execute`).

### 2.2. `PassiveProcessor.cs` — nối `SetBonus` vào pipeline có sẵn

- [x] `Fire()`: thêm `FireOne(owner, owner.SetBonus, contextTarget, trigger, rng);`.
- [x] `CheckHpThreshold()`: thêm `CheckThreshold(unit, unit.SetBonus, ratio, rng);`.
- [x] `TryAutoRevive()`: KHÔNG cần đụng — Set Bonus không có bộ nào dùng `OnTeamWipe`, bỏ qua có
      chủ đích (ghi rõ trong code nếu cần, tránh người sau tưởng quên).
- [x] `Apply()`:
      - Applies-loop: nếu `app.TargetAllAllies` → lặp toàn bộ `_state.Units` cùng `owner.Side`,
        còn sống, gọi `_status.Apply(owner, ally, app, rng)` cho từng người (thay vì chỉ
        `target` đơn); giữ nguyên nhánh `TargetSelf`/contextTarget hiện có cho các trường hợp còn
        lại.
      - Thêm xử lý `passive.SpRefundPercent > 0` — cần biết SP cost của skill vừa dùng, xem §2.3.
- [x] `TriggerOnPerfectCommand(actor, rng)` → đổi chữ ký thêm `int spCost = 0`, dùng để tính hoàn
      SP nếu `SetBonus.SpRefundPercent > 0`.
- [x] Thêm `TriggerOnTurnEnd(CombatUnit actor, IRandomSource rng) => Fire(actor, actor,
      PassiveTrigger.OnTurnEnd, rng);`.

### 2.3. `ActionResolver.cs`

- [x] `Execute()`: đổi lời gọi `_passive.TriggerOnPerfectCommand(actor, rng)` →
      `_passive.TriggerOnPerfectCommand(actor, rng, data.SpCost)` (đã có `data.SpCost` sẵn trong
      scope, không cần lấy thêm gì).

### 2.4. `CombatSimulation.cs`

- [x] `FinishTurn()`: thêm `_passive.TriggerOnTurnEnd(actor, rng);` — đặt SAU
      `_status.TickTurnEnd(actor)`/`_poise.TickTurnEnd(actor)` (đúng thứ tự "kết thúc lượt" nhất
      quán với 2 hệ đó), TRƯỚC `_scheduler.ConsumeTurn`.

### 2.4.5. `EquipmentGenerator.cs` — roll `SetId` ở tầng instance

- [x] `RollFrom`: thêm `SetId = SetBonusCatalog.RollRandomSetId(rng)` vào `EquipmentInstanceDto`
      vừa tạo — đều 1/8 mỗi bộ, KHÔNG có khả năng "không thuộc bộ nào" (giữ đơn giản, mọi trang bị
      sinh ra đều thuộc đúng 1 trong 8 bộ). Test hiện có (`EquipmentGeneratorTests.cs`) có thể cần
      cập nhật nếu có assertion cứng về field nào của `EquipmentInstanceDto` (kiểm tra lại, không
      giả định).

### 2.5. `Meta/Equipment/SetBonusCatalog.cs` (file mới)

- [x] Hard-code (KHÔNG ScriptableObject — lý do y hệt `AwakeningCatalog`: `StatModifier`/
      `StatusApplication` có field `readonly` bị serializer Unity bỏ qua âm thầm).
- [x] `readonly struct SetDefinition { string SetId; StatModifier[] TwoPiece; PassiveData
      FourPiece; }` — 8 entry (`ember`/`bastion`/`tempest`/`assassin`/`sage`/`guardian`/`breaker`/
      `vampire`, khớp `EquipmentInstanceDto.SetId` — cần xác nhận giá trị `SetId` thật đang dùng ở
      `EquipmentDefinitionSO`/CSV, KHÔNG tự đặt tên khác).
- [x] `Tempest.FourPiece = null` (xem §1.1).

### 2.6. `Meta/Equipment/SetBonusResolver.cs` (file mới, pure logic)

- [x] `ComputeActivePieces(HeroInstanceDto hero, PlayerProfileDto profile) ->
      Dictionary<string,int>` (setId → số món đang mặc, đếm qua `hero.Equipped[]` → tra
      `profile.Equipment` theo Uid → đọc `EquipmentInstanceDto.SetId`).
- [x] `GetActiveTwoPieceBonuses(...)`/`GetActiveFourPieceBonus(...)` — trả về danh sách
      `StatModifier` (2 món, có thể nhiều bộ cùng lúc) và tối đa 1 `PassiveData` (4 món — hero chỉ
      thật sự có ĐỦ 4 món của 1 bộ tại 1 thời điểm trong đa số trường hợp thực tế 6 slot/hero;
      nếu lý thuyết mặc đủ 4 món của 2 bộ khác nhau cùng lúc — KHÔNG THỂ xảy ra vì chỉ có 6 slot
      trang bị, 2 bộ 4-món cần tối thiểu 8 slot — không cần xử lý case này).

### 2.7. `CombatView/BattleSceneInstaller.cs` — wiring

- [x] `SpawnTeamFromDefinitions` (nhánh Player, chỗ đã gọi
      `EquipmentService.GetEquipmentModifiers`): gọi thêm `SetBonusResolver.
      GetActiveTwoPieceBonuses` → `unit.EquipmentModifiers.AddRange(...)`, và
      `GetActiveFourPieceBonus` → gán `unit.SetBonus = ...` (null nếu không đủ 4 món bộ nào).

### 2.8. Test

- [x] `SetBonusResolverTests.cs` (EditMode, Meta layer) — đếm món đúng theo `SetId`, 2 món kích
      hoạt 2pc không kích hoạt 4pc, 4 món kích hoạt cả 2, không đủ 2 món thì không có gì, nhiều bộ
      2-món cùng lúc cộng dồn đúng.
- [x] Test Combat layer (SimulationTests.cs, dùng `TestFactory`) cho tối thiểu 3 bộ đại diện đủ 3
      dạng trigger khác nhau: Ember (Applies qua OnPerfectCommand), Bastion (OnHpBelowThreshold,
      Consumed 1 lần/trận), Breaker (TargetAllAllies — buff cả đội, không chỉ actor).
- [x] Chạy lại toàn bộ EditMode suite — baseline hiện tại 323/323, không được regression.

## 2.9. Kết quả

Toàn bộ §2 đã xong. **346/346 test EditMode xanh** (323 baseline + 23 mới: 14 `PassiveProcessorTests`
+ 9 `SetBonusResolverTests`). Verify thêm bằng `execute_code` với catalog trang bị THẬT (không chỉ
fixture test): roll 800 item ngẫu nhiên → cả 8 bộ đều xuất hiện, phân bố đều (~100/bộ); dựng 1 hero
mặc đủ 4 món Vampire thật từ catalog → `GetActiveTwoPieceBonuses` trả đúng `LifestealPct +8`,
`GetActiveFourPieceBonus` trả đúng `set_vampire_bloodthirst` (Trigger=OnKill, HealPercentMaxHp=15).

Trong lúc implement phát hiện thêm 4 vấn đề KHÔNG có trong kế hoạch gốc (đều đã sửa, xem đánh dấu
"Phát hiện lúc implement" trong §2.1): Vampire/Guardian cần field heal tức thời riêng
(`HealPercentMaxHp`), Bastion không thể dùng `Applies[]` cho Shield (cần `ShieldPercentMaxHp` +
`ApplyShield` trực tiếp), Assassin cần gate Crit thật (`RequiresCrit`) thay vì đơn giản hoá thành
"mọi đòn trúng", Ember cần đổi từ `OnPerfectCommand` sang `OnHitDealt` + `RequiresPerfectGrade` vì
trigger gốc không có địch làm contextTarget. Tất cả đều tái dùng ĐÚNG 1 pattern (thêm field nhỏ
trên `PassiveData` + thread tham số qua `Fire`/`FireOne`), không phát sinh kiến trúc mới.

## 3. Giới hạn đã biết / ngoài phạm vi

- **Tempest 4-món hoãn** — xem §1.1, để `v1.1` hoặc làm riêng nếu người dùng yêu cầu sau.
- **Assassin 4-món đơn giản hoá** — bleed potency cố định, không scale %-theo-damage-vừa-gây (đúng
  công thức plan.md sẽ cần mở rộng `StatusApplication`/`DamageCalculator` để truyền "damage vừa
  tính" vào status application — ngoài phạm vi task này).
- Không có UI riêng hiển thị "đang kích hoạt bộ X" trong trận (HUD) — plan.md không mô tả rõ cần
  UI này ở v1, chỉ cần hiệu ứng hoạt động đúng. Có thể thêm sau nếu cần.
- `SetId` roll ĐỀU 1/8 (uniform), không theo rarity/tỉ lệ đặc biệt nào — plan.md không quy định tỉ
  lệ, đơn giản hoá có chủ đích giống nhiều quyết định khác trong dự án (VD `TrialBossSystem`/
  `TowerSystem` reward tier numbers).

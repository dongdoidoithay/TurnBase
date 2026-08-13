# Task: Enhance cho sub-stat

Yêu cầu: "tiếp tục Enhance cho sub-stat" — mở rộng hệ thống Enhance trang bị (hiện chỉ scale
main-stat) để cũng tác động tới sub-stat, theo plan.md §7.3.

Theo quy trình chuẩn đã thống nhất trong session này: viết xong file task này (tìm hiểu + quyết
định phạm vi + checklist) rồi mới chạm code.

## §0. Findings

Đọc toàn bộ `EquipmentService.cs` (191 dòng) + `EquipmentGenerator.cs` + phần Enhance UI trong
`TeamSelectScreen.cs` (dòng ~264-293, ~339-359) + grep test coverage hiện có.

- **Enhance hiện tại (`EquipmentService.cs`)**: `MAX_ENHANCE_LEVEL = 9` (không phải +15 như
  plan.md §7.3 mô tả), `EnhanceCost(level) = 80 * (level+1)` Gold-only, luôn 100% thành công
  (`TryEnhance` không có khái niệm fail), `EffectiveStatValue` chỉ scale **main stat** +10%/cấp.
  Sub-stat (`inst.SubStats`) hoàn toàn không bị đụng tới — được roll cố định 1 lần duy nhất tại
  `EquipmentGenerator.RollSubStats` lúc sinh item, Enhance sau đó không đọc/ghi gì vào đó.
- **plan.md §7.3** mô tả 1 hệ +0→+15 đầy đủ: mốc +3/+6/+9/+12 "mở sub stat mới", +12-15 có tỉ lệ
  thất bại (70%→25%), +15 nhân main stat ×1.5, chi phí tăng dần cả Gold lẫn **Đá cường hoá**.
  Hệ hiện tại chỉ có 9/15 cấp và không dùng Đá — đây là gap thực sự, không phải đã làm rồi.
- **`CurrencyType.EnhanceStone` là currency chết**: `DungeonSystem.GrantFloorReward` (case
  `DungeonKind.Stone`) grant thật (`economy.Grant(profile.Wallet, CurrencyType.EnhanceStone,
  floor)`, có test `DungeonSystemTests.GrantFloorReward_Stone_GrantsEnhanceStoneEqualToFloor`),
  nhưng **không có chỗ nào consume nó** — người chơi cày ra Đá nhưng không tiêu được ở đâu cả.
  Đây đúng là chỗ Enhance nên tiêu, khớp thiết kế gốc (Dungeon Đá tồn tại ĐỂ nuôi Enhance).
- **RNG pattern trong Meta layer**: mọi chỗ roll ngẫu nhiên ở tầng Meta (Gacha, LootRoller,
  PlaceholderLootTable, SetBonusCatalog.RollRandomSetId) đều nhận `IRandomSource rng` làm tham số,
  KHÔNG tự new bên trong hàm static — caller (1 MonoBehaviour màn hình) giữ 1
  `IRandomSource _rng` field, khởi tạo `new XorShiftRandom((ulong)DateTime.UtcNow.Ticks)` 1 lần
  (VD `SummonScreen._rng`, `MetaSceneInstaller._lootRng`). `TeamSelectScreen` hiện KHÔNG có field
  rng nào — Enhance từ trước tới giờ hoàn toàn deterministic nên không cần. Việc thêm unlock
  sub-stat (chọn ngẫu nhiên loại + giá trị) sẽ là chỗ dùng RNG đầu tiên trong Enhance, nên
  `TryEnhance` cần thêm tham số `IRandomSource rng`.
- **Pool sub-stat có thể tái dùng**: `EquipmentGenerator.POOL` (8 loại: AtkPct/MaxHpPct/DefPct/
  Spd/CritPct/CritDmgPct/Res/EffAcc) + bảng min/max theo rarity đã có sẵn, dùng để roll sub-stat
  lúc sinh item. Milestone "mở sub-stat mới" của Enhance nên tái dùng CHÍNH bảng này (roll 1 loại
  CHƯA có trong `inst.SubStats`, cùng khoảng giá trị theo rarity của item) — không cần bảng số mới.
  `POOL`/`SUBSTAT_COUNT` hiện là `private` trong `EquipmentGenerator` — cần thêm 1 method public
  mới trong chính file đó (không phải EquipmentService) để giữ pool data ở 1 chỗ.
- **Zero test coverage hiện tại cho Enhance**: grep `TryEnhance|EnhanceCost|MAX_ENHANCE_LEVEL` trên
  toàn bộ `Assets/Tests/` ra 0 kết quả. `EquipmentServiceModifierTests.cs` chỉ test
  `GetEquipmentModifiers` (đọc sub-stat có sẵn vào combat), không test Enhance. Nghĩa là toàn bộ
  hàm `TryEnhance`/`EnhanceCost`/`EffectiveStatValue` đang chạy production mà chưa từng có test —
  việc này cũng nên được lấp khi sửa.
- **UI (`TeamSelectScreen.cs`)**: `EnhanceButton` hiện show `"+1 ({cost}g)"`, gọi thẳng
  `EquipmentService.TryEnhance(_profile, capturedUid)`. `FormatItemText` (dòng 347-359) đã tự động
  in TOÀN BỘ `inst.SubStats` hiện có (kể cả sub-stat mới nếu list được thêm phần tử) — nghĩa là
  UI hiển thị item KHÔNG cần sửa gì để show sub-stat mới, chỉ cần sửa chỗ gọi `TryEnhance` (thêm
  rng) và chỗ hiển thị cost (thêm Đá).

## §1. Scope decision

Plan.md §7.3 đầy đủ (+15, tỉ lệ fail, ×1.5 milestone cuối) là 1 rework lớn của toàn bộ hệ thống
Enhance, không phải riêng "sub-stat" — vượt quá yêu cầu "Enhance cho sub-stat" của user. Chọn
hướng **tối thiểu nhưng thật**, đúng kỷ luật cả session: giữ khung Enhance hiện có (+0..+9, luôn
thành công), chỉ thêm đúng phần sub-stat + đóng gói lại chi phí Đá đi kèm (vì 2 việc này gắn chặt
— milestone mở sub-stat MỚI mà không tốn thêm tài nguyên gì thì vô nghĩa về balance).

**Trong phạm vi:**
1. Milestone mở sub-stat mới tại level 3/6/9 (nén 4 mốc 3/6/9/12 của plan.md xuống 3 mốc, khớp
   trần +9 hiện tại — mốc +12 của plan.md không áp dụng được vì vượt trần).
2. Roll 1 sub-stat loại CHƯA có (loại trừ dedup, cùng nguyên tắc `RollSubStats`), giá trị theo
   đúng range rarity của item — dùng `IRandomSource` mới thêm vào `TryEnhance`.
3. Nếu item đã có đủ 8/8 loại sub-stat (hiếm nhưng có thể xảy ra với Mythic + đã unlock hết) —
   milestone là no-op, không throw, không tốn thêm gì ngoài Gold/Đá của cấp đó.
4. Sửa `EnhanceCost` thành 2 phần: Gold (giữ nguyên công thức cũ, không đổi số cũ để không phá
   balance hiện có) + Đá cường hoá theo tier (1 cho level 0-2, 2 cho level 3-5, 3 cho level 6-8 —
   bám theo đúng 3 tier đầu của bảng plan.md §7.3, cắt bỏ các tier vượt trần +9).
5. `TryEnhance` kiểm tra đủ CẢ HAI currency trước khi trừ bất kỳ currency nào (atomic — không trừ
   Gold rồi mới phát hiện thiếu Đá).
6. UI: thêm field `IRandomSource _rng` vào `TeamSelectScreen` (giống `SummonScreen._rng`), truyền
   vào `TryEnhance`; cost label hiện thêm số Đá cần (VD `"+1 (240g · 1◆)"`).
7. Viết test mới cho toàn bộ `EquipmentService.TryEnhance`/`EnhanceCost` (trước đây chưa có test
   nào) + test riêng cho milestone unlock (mốc đúng level, không trùng loại, no-op khi đầy pool).

**Ngoài phạm vi (out of scope, ghi rõ để không ai tưởng nhầm là đã làm):**
- Mở rộng trần lên +15, cơ chế tỉ lệ thành công/thất bại (+12..+15), milestone "main stat ×1.5" ở
  +15 — đây là rework khung Enhance, không phải "cho sub-stat", để lại cho 1 task riêng nếu cần.
- Reforge / re-roll sub-stat hiện có (plan.md có nhắc nhưng khác hẳn "mở sub-stat mới").

## §2. Implementation checklist

- [x] `EquipmentGenerator.cs`: thêm method public `TryRollAdditionalSubStat(Rarity rarity,
      List<SubStatDto> existing, IRandomSource rng, out SubStatDto result)` — loại trừ các
      `StatType` đã có trong `existing` khỏi `POOL`, trả `false` nếu hết loại để roll.
- [x] `EquipmentService.cs`:
  - [x] Thêm hằng số milestone `SUBSTAT_UNLOCK_LEVELS = { 3, 6, 9 }`.
  - [x] Thêm `EnhanceStoneCost(int currentLevel)` song song `EnhanceCost` (Gold) — switch
        expression theo 3 bracket thay vì mảng tier riêng (đơn giản hơn, cùng kết quả).
  - [x] Sửa `TryEnhance` thêm tham số `IRandomSource rng`: kiểm tra đủ Gold + Đá (atomic, dùng
        `economy.Get` trước khi `TryConsume`), trừ cả 2, tăng `Level`, nếu `Level` mới thuộc
        milestone thì gọi `EquipmentGenerator.TryRollAdditionalSubStat` và append nếu thành công.
  - [x] Cập nhật doc comment class-level (đang nói "Chưa làm sub-stat" — nay đã làm 1 phần).
- [x] `TeamSelectScreen.cs`:
  - [x] Thêm field `IRandomSource _rng`, khởi tạo kiểu `SummonScreen._rng` (trong `Open()`).
  - [x] Sửa cost label thành có cả Gold + Đá (`"+1 ({gold}g · {stone}◆)"`).
  - [x] Sửa lời gọi `TryEnhance` truyền `_rng`.
- [x] Tests (`Assets/Tests/EditMode/Meta/`):
  - [x] `EquipmentServiceEnhanceTests.cs` MỚI (9 test): `TryEnhance` tăng level đúng, trừ đúng
        Gold+Đá, thất bại khi thiếu 1 trong 2 currency (không trừ currency còn lại, verify atomic),
        thất bại khi đã MAX level, milestone level 3/6/9 thêm đúng 1 sub-stat mới không trùng loại
        (30 seed), no-op khi pool đã đầy 8/8, level không phải mốc không thêm gì,
        `EnhanceStoneCost` đúng 3 bracket.
  - [x] `EquipmentGeneratorTests.cs`: thêm 3 case cho `TryRollAdditionalSubStat` (loại trừ đúng,
        trả false khi hết pool, giá trị nằm trong range rarity).
- [x] Chạy full EditMode suite (`run_tests`) — **362/362 xanh** (350 cũ + 12 test mới), không test
      nào cũ bị vỡ. Phát hiện lúc implement: build đầu tiên lỗi CS0308 ("non-generic type List") ở
      `EquipmentGeneratorTests.cs` — file này thiếu `using System.Collections.Generic;` từ đầu
      (chỉ dùng `List<T>` gián tiếp qua các using khác trước đó); fix bằng cách gọi tường minh
      `System.Collections.Generic.List<Game.Data.Dto.SubStatDto>` ở 3 chỗ mới thêm thay vì thêm
      using mới (tránh rủi ro đổi resolution của các chỗ khác trong cùng file).
- [ ] Play-mode smoke check (nếu khả thi): enhance 1 item thật từ +0 lên +3 qua UI, xác nhận sub-
      stat mới xuất hiện trên `ItemLabel` và cost label hiện đúng Đá. (Chưa chạy — xem §3.)
- [x] Cập nhật `roadmap.md` §0.1 (dòng P4: "Enhance cho sub-stat" chuyển từ "còn thiếu" sang xong,
      ghi rõ giới hạn +0..+9/3 mốc, không phải full +15) và `object-map.md` §12/§12.1.
- [ ] Cập nhật memory backlog snapshot nếu cần (không bắt buộc mỗi task, chỉ khi mốc lớn).

## §3. Known limitations

- Play-mode smoke check chưa chạy trong lượt này (chỉ verify qua EditMode test, vốn đã cover đủ
  logic thuần). Nếu cần verify UI thật, mở TeamSelectScreen trong Play mode, Enhance 1 item tới
  level 3 và quan sát `ItemLabel`/`EnhanceLabel`.
- Item khởi tạo qua `EnsureStarterEquipment` (chưa qua `EquipmentGenerator`) dùng `def.Rarity` thô
  từ CSV (giá trị 1/2, quy ước tier khác với enum `Rarity` Common=0..Mythic=4 — đã ghi chú sẵn ở
  `FormatItemText`). Nếu 1 item starter được Enhance lên mốc unlock, sub-stat mới sẽ dùng range
  của `Rarity.Rare`/`Rarity.Epic` (do ép kiểu (Rarity)1/(Rarity)2) thay vì đúng ý nghĩa gốc — không
  crash (vẫn trong khoảng mảng 0-4 hợp lệ), chỉ lệch balance nhẹ cho nhóm item starter hiếm khi
  còn dùng tới cuối game. Đây là gap có từ trước (task-equipment.md), không phải lỗi mới.
- Không mở rộng trần +9 → +15, không thêm tỉ lệ thất bại +12+, không thêm milestone ×1.5 ở +15 —
  cố ý out-of-scope theo §1.

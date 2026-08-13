# Task: Enhance full +0 → +15 system

Yêu cầu: chọn qua `AskUserQuestion` ("Enhance full +15 system (Recommended)") trong số 2 lựa chọn
còn lại (Enhance +15 / Mail badge-expiry-ClaimAll). Việc lớn, đụng cơ chế lõi tiến triển trang bị —
viết xong task file này rồi mới chạm code, theo quy trình chuẩn.

## §0. Findings

Đọc lại toàn bộ `EquipmentService.cs` (191 dòng, đã có Đá + sub-stat milestone từ
task-enhance-substat.md), `EquipmentServiceEnhanceTests.cs` (12 test hiện có), phần Enhance UI
trong `TeamSelectScreen.cs` (~dòng 267-296), và plan.md §7.3 đầy đủ.

- **Trạng thái hiện tại** (`EquipmentService.cs`): `MAX_ENHANCE_LEVEL = 9`, `SUBSTAT_UNLOCK_LEVELS
  = {3,6,9}` (nén từ plan.md {3,6,9,12} vì trần cũ chỉ tới 9), `EnhanceStoneCost` 3 bracket
  (1/2/3), `EnhanceCost(level) = 80*(level+1)` Gold-only liên tục, **không có khái niệm thất bại —
  `TryEnhance` luôn thành công nếu đủ tiền**. `EffectiveStatValue = MainStatValue*(1+Level*0.1)`.
- **plan.md §7.3 (bảng đầy đủ)**:

  | Mốc (level hiện tại → attempt) | Tỉ lệ thành công | Vàng | Đá | Hiệu quả |
  |---|---|---|---|---|
  | 0-2 → +1..+3 | 100% | 200–600 | 1 | Main stat +10%/mốc (liên tục, mọi level) |
  | 3-5 → +4..+6 | 100% | 900–1.800 | 2 | **+3: mở sub-stat mới** |
  | 6-8 → +7..+9 | 100% | 2.400–4.000 | 3 | **+6: mở sub-stat mới** |
  | 9-10 → +10..+11 | 100% | 5.000–7.000 | 5 | **+9: mở sub-stat mới** |
  | 11 → +12 | 70% | 9.000 | 8 | **+12: mở sub-stat mới** |
  | 12 → +13 | 55% | 12.000 | 10 | |
  | 13 → +14 | 40% | 16.000 | 14 | |
  | 14 → +15 | 25% | 22.000 | 20 | **+15: main stat ×1.5** |

  "Thất bại: không mất đồ, không tụt level, chỉ mất tài nguyên" — Gold/Đá vẫn bị trừ dù roll trượt.
- **Gold hiện tại KHÔNG khớp plan.md, và đây là quyết định CŨ đã có chủ đích** (task-enhance-substat
  .md §1: "giữ nguyên công thức Gold cũ, không đổi số cũ để không phá balance hiện có"). Ở level 8,
  công thức cũ ra 720 Gold trong khi plan.md muốn 2.400-4.000 — chênh lệch đã tồn tại từ trước, không
  phải lỗi mới. **Giữ nguyên tinh thần đó cho phần mở rộng**: KHÔNG nhảy 10x sang số plan.md ở
  level 9 (sẽ tạo 1 bậc thang giật cục ngay biên +9/+10), mà tiếp tục nguyên công thức
  `80*(level+1)` cho TOÀN BỘ 0-14 — ở level 14 ra 1.200 Gold. Đối chiếu kinh tế thật của game (Gold
  đã luôn là currency RẺ/dồi dào xuyên suốt dự án — `AscendSystem.COSTS` tốn tới 250.000 Gold cho
  ★5→6, Treasure chỉ ra 100-460 Gold/node theo task-balance-loottable.md) — cái THẬT SỰ khan hiếm và
  tạo cảm giác "cao cấp/rủi ro" ở +12-15 không phải Gold mà là **Đá cường hoá** (nguồn duy nhất:
  Dungeon Đá, giới hạn tuần) + **tỉ lệ thất bại** — 2 thứ này bám sát số plan.md 100%. Gold chỉ là
  phanh nhẹ, không phải nút thắt chính — đúng vai trò nó đã có xuyên suốt session.
- **Đá cường hoá**: dùng ĐÚNG số plan.md cho các bracket mới (5/8/10/14/20) — không có "công thức
  cũ" nào phải giữ ở đây vì Đá vốn dĩ chỉ mới bắt đầu được tiêu từ task-enhance-substat.md, không có
  tiền lệ số liệu để mâu thuẫn.
- **`TryEnhance` hiện trả `bool`** — không đủ diễn tả 3 trạng thái thật cần có khi thêm tỉ lệ thất
  bại: (1) **Rejected** — không đủ tài nguyên/đã max level, KHÔNG trừ gì cả (như hiện tại); (2)
  **Failed** — đủ tài nguyên, ĐÃ TRỪ, roll trượt, level/sub-stat không đổi; (3) **Succeeded** — đủ
  tài nguyên, ĐÃ TRỪ, level+1, có thể mở sub-stat mới. (1) và (2) đều "không lên cấp" nhưng khác hẳn
  nhau về việc CÓ mất tài nguyên hay không — 1 `bool` không phân biệt được, phải đổi kiểu trả về
  sang `enum EnhanceOutcome { Rejected, Failed, Succeeded }`. Đây là thay đổi API breaking nhưng
  phạm vi nhỏ — grep xác nhận `TryEnhance` chỉ có ĐÚNG 1 call site production
  (`TeamSelectScreen.cs`) + test file hiện có (12 test dùng `bool ok`, phải sửa toàn bộ assertion
  sang enum, không phải regression).
- **UI feedback cho outcome mới**: `TeamSelectScreen` hiện chỉ phát SFX confirm/cancel dựa vào
  `bool`, không có Toast/label nào báo "thất bại, đã mất tài nguyên" — nếu giữ nguyên UI hiện có,
  người chơi trả Gold+Đá mà không lên cấp sẽ tưởng là BUG (im lặng hoàn toàn). `TeamSelectScreen`
  KHÔNG có Toast panel riêng (đó là 1 phần chỉ tồn tại trong `MetaSceneInstaller`'s canvas — 2
  script/2 canvas độc lập, không dùng chung). Xây Toast riêng cho `TeamSelectScreen` là việc lớn
  ngoài phạm vi — thay vào đó tái dùng ĐÚNG `EnhanceLabel` đã có sẵn: thêm 2 field transient
  `_lastEnhanceUid`/`_lastEnhanceOutcome` trên script, `RefreshGearPanel` đọc field này để hiện
  "FAILED — try again" (màu đỏ nhạt, dùng lại `DISABLED`/thêm 1 màu warning mới) đúng 1 lần cho
  đúng dòng vừa bấm, rồi bản thân field tự "tiêu" (không hiện lại ở lần rebuild sau trừ khi bấm
  tiếp) — không cần Toast/canvas mới.
- **`EnhanceLabel` có nguy cơ tràn chữ giống lỗi đã biết** (`feedback_unity_mcp_ui_gotchas.md`) —
  đo thật qua `execute_code`: `EnhanceButton` 84×30px, `EnhanceLabel` stretch full nút, fontSize 11,
  `Wrap`+`Truncate`. Format hiện tại `"+1 ({gold}g · {stone}◆)"` CHƯA TỪNG được verify hiển thị thật
  (task-enhance-substat.md §3 tự ghi "Play-mode smoke check chưa chạy"), và định thêm "· 70%" cho
  các mốc rủi ro sẽ dài hơn nữa — rủi ro tràn dòng 2 bị Truncate mất (đúng lỗi đã ghi nhận ở Quest/
  Dungeon/TrialBoss labels trước đây). **Quyết định**: đo bằng `Text.cachedTextGenerator.
  GetPreferredWidth(...)` qua `execute_code` (kỹ thuật KHÔNG cần Play mode/frame thật — tránh hẳn
  vấn đề MCP frame-stall gặp ở task-teamselect-scroll.md) để biết chính xác chuỗi có vừa hay không,
  rồi chọn RÚT NGẮN chuỗi thay vì nới `RectTransform` (đúng bài học đã ghi: rút ngắn rẻ/an toàn hơn
  tính lại anchor) — VD bỏ ký hiệu "g"/"◆" thành số thô, hoặc tách "70%" thành dòng phụ nếu cần.
- **`EffectiveStatValue` × 1.5 ở +15**: đọc plan.md, dòng "Hiệu quả" của mỗi mốc là liệt kê THÊM
  (VD "+3: mở sub-stat mới" là 1 sự kiện CỘNG THÊM vào hiệu ứng nền +10%/mốc liên tục, không thay
  thế nó — đúng cách milestone sub-stat đã hoạt động). Áp dụng cùng logic: "+15: main stat ×1.5" là
  1 hệ số nhân THÊM khi đã đạt level 15, không thay thế công thức tuyến tính hiện có. Công thức mới:
  `MainStatValue * (1 + Level*0.1) * (Level >= 15 ? 1.5f : 1f)`. Đây là 1 quyết định diễn giải (plan
  .md không nói rõ "thay thế" hay "cộng thêm") — ghi rõ để không ai tưởng nhầm là số chắc chắn.

## §1. Scope decision

**Trong phạm vi:**
1. `MAX_ENHANCE_LEVEL`: 9 → 15.
2. `SUBSTAT_UNLOCK_LEVELS`: {3,6,9} → {3,6,9,12} (khớp đủ 4 mốc plan.md, không nén nữa vì trần đã
   đủ chỗ).
3. `EnhanceStoneCost`: mở rộng switch thêm 4 bracket mới (9-10→5, 11→8, 12→10, 13→14, 14→20) —
   dùng ĐÚNG số plan.md, không tự thiết kế.
4. `EnhanceCost` (Gold): KHÔNG đổi công thức, chỉ mở rộng phạm vi áp dụng `80*(level+1)` lên tới
   level 14 (không tự nhảy sang số plan.md) — lý do đã ghi ở §0.
5. Thêm `SuccessChance(int currentLevel)` — 100% cho level 0-10, 70/55/40/25% cho level 11-14 đúng
   plan.md.
6. Đổi `TryEnhance` trả `EnhanceOutcome` (enum mới `Rejected`/`Failed`/`Succeeded`) thay vì `bool` —
   sửa toàn bộ 12 test hiện có + 1 call site UI theo kiểu mới.
7. `EffectiveStatValue`: thêm hệ số ×1.5 khi `Level >= 15` (cộng thêm, không thay thế — xem §0).
8. UI (`TeamSelectScreen.cs`): hiện tỉ lệ thành công trong label khi <100% (rút gọn chuỗi nếu đo
   thật thấy tràn), feedback "FAILED" 1 lần cho đúng dòng vừa bấm khi outcome=Failed (không cần
   Toast/canvas mới — tái dùng `EnhanceLabel`).
9. Test mới: `SuccessChance`/`EnhanceStoneCost` 8 bracket đầy đủ, `TryEnhance` ở level 11-14 với
   RNG cố định (seed ép cả 2 nhánh thành/bại), milestone level 12 mở sub-stat, ×1.5 ở level 15,
   atomic-consume vẫn đúng khi Failed (không chỉ khi Succeeded).

**Ngoài phạm vi (cố ý, ghi rõ):**
- KHÔNG xây Toast/canvas riêng cho `TeamSelectScreen` — dùng lại `EnhanceLabel` transient.
- KHÔNG đổi công thức Gold sang số plan.md — giữ nguyên tinh thần "Gold rẻ, Đá+tỉ lệ mới là nút
  thắt thật" đã thiết lập từ task-enhance-substat.md.
- KHÔNG có "bảo hiểm/tăng tỉ lệ thành công" (item/currency phụ để tăng % — plan.md không nhắc,
  không tự bịa thêm).
- KHÔNG động tới Reforge/re-roll sub-stat hiện có (vẫn ngoài phạm vi như task-enhance-substat.md đã
  ghi).

## §2. Implementation checklist

- [x] `EquipmentService.cs`: `MAX_ENHANCE_LEVEL` → 15, `SUBSTAT_UNLOCK_LEVELS` → {3,6,9,12},
      `EnhanceStoneCost` thêm 4 bracket (5/8/10/14/20, đúng plan.md), thêm `SuccessChance`
      (100% ở 0-10, 70/55/40/25% ở 11-14), thêm `enum EnhanceOutcome{Rejected,Failed,Succeeded}`,
      đổi chữ ký + thân `TryEnhance` (roll `rng.Chance(SuccessChance(...))` SAU khi trừ tài
      nguyên, TRƯỚC khi tăng Level — Failed vẫn giữ nguyên phần trừ tài nguyên phía trên), sửa
      `EffectiveStatValue` thêm ×1.5 khi `Level >= 15` (cộng thêm lên công thức tuyến tính nền).
- [x] `EquipmentServiceEnhanceTests.cs`: sửa 12 test cũ sang `EnhanceOutcome` (không đổi ý nghĩa
      test cũ, chỉ đổi kiểu assertion `bool`→enum + đổi tên 2 test Fails→Rejected cho khớp nghĩa
      mới), thêm 8 test mới: `EnhanceStoneCost` đủ 8 bracket, `SuccessChance` 2 test (100% dải
      thấp + đúng bảng dải cao), 2 test level 11 quét seed 1-200 tìm cả nhánh Failed (verify atomic
      consume) lẫn Succeeded (verify mốc 12 mở sub-stat) — KHÔNG đoán 1 seed cụ thể, quét thật để
      tránh test flaky/sai nếu implementation RNG thay đổi thứ tự gọi sau này, 2 test
      `EffectiveStatValue` (×1.5 ở level 15, KHÔNG áp dụng ở level 14).
- [x] Đo thật `EnhanceLabel` bằng `execute_code` (`TextGenerator.GetPreferredWidth` qua
      `Text.GetGenerationSettings`) — phát hiện quan trọng: format ban đầu dự tính
      `"+1(9000g·8◆·70%)"` (dùng số Vàng LITERAL từ plan.md) tràn hộp 84px (width 93-105px cho cả
      4 mức rủi ro + chuỗi "FAILED — try again" cũng tràn, width 97px). Sau khi xác nhận công thức
      Gold THẬT của code (giữ nguyên `80*(level+1)`, xem §0) chỉ ra số nhỏ hơn nhiều (960-1200,
      không phải 9000-22000), đo lại bằng ĐÚNG con số thật thì vừa khít (70-82px, box 84px) — không
      cần rút gọn thêm gì ngoài bỏ tiền tố "+1" ở format rủi ro. "FAILED! (retry)" (75px) thay cho
      bản dài hơn.
- [x] `TeamSelectScreen.cs`: sửa lời gọi `TryEnhance` theo `EnhanceOutcome`, thêm hiện tỉ lệ khi
      <100% (dùng chuỗi đã đo vừa ở trên), thêm field `_lastEnhanceUid`/`_lastEnhanceOutcome` +
      hiển thị "FAILED! (retry)" đúng 1 lần trong `RefreshGearPanel` cho đúng dòng vừa bấm, màu
      `FAIL_RED` mới.
- [x] `refresh_unity` compile sạch (3 lần qua các đợt sửa).
- [x] Chạy full EditMode suite — **408/408 xanh** (402 cũ + 6 net mới: +8 test mới − đổi tên/gộp
      1 test cũ thành seed-scan robust hơn không tính net thêm/bớt số lượng).
- [x] Verify cấu trúc/label thật: gặp LẠI đúng MCP frame-stall đã ghi nhận ở
      task-teamselect-scroll.md (`Time.frameCount` đứng yên ở giá trị 2 dù thời gian thực trôi qua,
      xác nhận qua `manage_editor play` → chờ 3s → đọc lại `Time.frameCount` không đổi) — không ép
      chạy tiếp UI thật qua Play mode, dừng đúng như bài học đã ghi. Bù lại bằng 2 kỹ thuật KHÔNG
      cần frame thật: (1) `TextGenerator` đo chữ thật (không đoán) — xem trên; (2) EditMode test
      seed-scan thật (không mock RNG) cho toàn bộ logic thành/bại + mốc + hệ số, phủ đủ những gì
      UI click thật sẽ kiểm nếu chạy được. Giới hạn còn lại: KHÔNG xác nhận trực tiếp bằng mắt màu
      `FAIL_RED`/vị trí label trên màn hình thật.
- [x] Cập nhật `roadmap.md §0.1` (P4: Enhance +0..+9 → +0..+15 đầy đủ), `object-map.md §12.1`.

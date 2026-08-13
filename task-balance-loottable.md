# Task: Balance Harness pass — per-chapter loot table

Yêu cầu: chọn qua `AskUserQuestion` ("Balance Harness pass cho loot table (Recommended)") trong số
3 lựa chọn còn lại (Enhance +15 / Mail badge-expiry-ClaimAll / Balance Harness). Việc "vừa", có
nhiều phát hiện/quyết định thật cần ghi lại trước khi sửa code — viết xong task file này rồi mới
đụng code, theo quy trình chuẩn.

## §0. Findings

`Assets/Tools/Balance/BalanceHarness.cs` → `MaterialDropReport()` được viết TRƯỚC khi
task-loottable-chapters.md thêm 10 asset theo chương — đọc kỹ code thật (không giả định) phát hiện
**2 lỗi thật khiến báo cáo hiện tại sai lệch nặng so với gameplay thật**, không phải chỉ là "chưa
tối ưu":

1. **Hardcode `chapter: 1`** (dòng 74-75): `LootRoller.Resolve(chapter: 1, ...)` cho cả Treasure lẫn
   Boss — dù giờ có đủ 5 bảng riêng theo chương (task-loottable-chapters.md), harness vẫn chỉ đo
   MỘT bảng chương 1 lặp lại 1000 lần, hoàn toàn bỏ qua 4 bảng còn lại. Không đo được đúng cái task
   này cần đo.
2. **Bug đếm mảnh hero từ Boss sai với code thật** (dòng 122):
   `totalShards += SIMULATED_OWNED_HEROES;` — cộng CỨNG 6 mảnh/run bất kể gì, với comment "1 mảnh/
   hero ra trận khi thắng Boss". Đối chiếu với `MetaSceneInstaller.GrantBossAscendMaterials`
   (code thật đang chạy): chỉ gọi `LootRoller.Roll(table, rng, ...)` ĐÚNG 1 LẦN, mảnh hero chỉ ra
   nếu roll trúng `HeroShardChance` (xác suất, không phải chắc chắn) và khi trúng chỉ cấp cho
   ĐÚNG 1 hero ngẫu nhiên (không phải cả đội). **Đọc trực tiếp cả 5 file `loottable_boss_ch{1..5}
   .asset` xác nhận `HeroShardChance: 0` cho TẤT CẢ — Boss KHÔNG BAO GIỜ cấp mảnh hero theo thiết
   kế thật** (chủ ý — mảnh hero chỉ đến từ Treasure, xem task-loottable-chapters.md). Harness hiện
   tại báo cáo "~6 mảnh/run từ Boss" khi con số thật là 0 — sai hoàn toàn, không phải sai số nhỏ.
3. **`SIMULATED_OWNED_HEROES = 6`** (không sửa) — đối chiếu `LocalPlayerRepository.CreateNew()`:
   người chơi MỚI thật sự bắt đầu với đúng 6 hero khởi điểm (`starters[]`, 6 defId cố định), dù
   `heroes.csv` giờ có 24. Với early/mid-game (giai đoạn cần Ascend nhất, trước khi cày Gacha ra
   nhiều hero), 6 vẫn là giả định hợp lý — KHÔNG đổi. (Ghi chú ngoài lề: comment tại
   `LocalPlayerRepository.cs:186` "toàn bộ heroes.csv hiện có" giờ sai vì heroes.csv có 24, không
   phải 6 — lỗi tài liệu nhỏ, không liên quan tới BalanceHarness, sẽ báo riêng qua task nền, KHÔNG
   sửa trong task này.)
4. **Không có chapter replay trong code thật** — grep `MetaSceneInstaller` xác nhận map mới luôn
   sinh tại `_profile.Progress.ChapterUnlocked` (chương cao nhất đã mở), không có màn "chọn lại
   chương cũ". Nghĩa là 1 người chơi thật đi qua mỗi chương ĐÚNG 1 LẦN trong cả game (không phải
   "cày lại chương 1 vô hạn" như model `PlaceholderLootTable` cũ ngầm giả định). `RUNS=1000` trong
   harness vẫn đúng về mặt kỹ thuật (Monte Carlo để tính kỳ vọng của ĐÚNG 1 lần chơi qua map ngẫu
   nhiên đó) — chỉ cần diễn giải kết quả đúng: tổng vật liệu "cả game" = TỔNG kỳ vọng của 5 chương
   (mỗi chương 1 lần), không phải nhân RUNS lên.
5. **Nguồn cày vật liệu Ascend lâu dài thật sự KHÔNG phải loot table chương** — sau khi hết 5
   chương, `DungeonSystem` (`DungeonKind.Material`, task-endgame.md) mới là vòng lặp cày lại được
   (3 ngày/tuần, tối đa 10 tầng/ngày: EssenceI cố định 2, EssenceII từ tầng ≥4, EssenceIII từ tầng
   ≥8 — không có Core từ nguồn này). Đưa việc mô phỏng Dungeon vào NGOÀI phạm vi hạng mục này (đó là
   1 hệ thống khác, đã có harness riêng tính "story-only" trước đây `task-ascend.md §8`) — nhưng
   **PHẢI ghi rõ trong output báo cáo** rằng con số "X run để đủ" chỉ tính nguồn chương (1 lần/
   chương), không tính Dungeon, để không tạo kết luận sai "phải chơi lại chương mãi mãi" (chương
   không replay được).

## §1. Scope decision

**Trong phạm vi:**
1. Sửa `MaterialDropReport()`: lặp qua 5 chương thay vì hardcode chương 1, in báo cáo riêng từng
   chương (Treasure trung bình ghé/Gold/material/mảnh, Boss material) + 1 khối TỔNG cả 5 chương
   (mô phỏng đúng 1 lần chơi hết game, không nhân RUNS).
2. Sửa bug đếm mảnh Boss — dùng đúng `roll.ShardHeroIndex >= 0` như phía Treasure đã làm đúng, bỏ
   dòng cộng cứng `SIMULATED_OWNED_HEROES`.
3. `AscendPacingReport` tính lại theo TỔNG vật liệu cả 5 chương (không phải theo 1 chương lặp lại),
   thêm 1 dòng ghi chú rõ "chỉ tính nguồn chương truyện, KHÔNG tính Material Dungeon (task-endgame.md,
   nguồn cày dài hạn thật sự sau khi hết truyện)".
4. Chạy harness đã sửa THẬT qua `execute_code` (gọi thẳng `BalanceHarness.MaterialDropReport()`),
   đọc log thật qua Console, không đoán số.
5. Đối chiếu số liệu thật với `AscendSystem.COSTS` (5 mốc ★, đã có sẵn trong code) — nếu phát hiện
   bậc nào THỰC SỰ bất thường (VD 1 loại material cần nhưng chưa từng xuất hiện ở chương đã mở khoá
   nó, hoặc số run cần lệch quá xa mức hợp lý ~5-20 run/bậc theo đúng tinh thần task-ascend.md §8 đã
   dùng trước đây) thì chỉnh số trong asset `.asset` liên quan — sửa VÁ, không tune lại toàn bộ từ
   đầu (10 asset đã có đường cong hợp lý từ task-loottable-chapters.md, chỉ sửa nếu có bằng chứng
   thật từ harness).
6. Cập nhật `task-loottable-chapters.md` (thêm mục "phát hiện lúc balance pass"), `roadmap.md §0.1`
   (P5 loot table + P7 Balance), `object-map.md §12.1`.

**Ngoài phạm vi (cố ý, ghi rõ):**
- KHÔNG mô phỏng `DungeonSystem`/Material Dungeon vào harness này (hệ khác, xem §0 mục 5).
- KHÔNG sửa comment sai ở `LocalPlayerRepository.cs:186` (không liên quan loot table, báo riêng).
- KHÔNG động tới `GachaPityReport()` (đã đúng, không có phát hiện gì mới ở đó).
- KHÔNG viết test tự động cho `BalanceHarness` — đây là Editor tool thủ công cho dev (đúng bản chất
  đã ghi trong chính doc-comment của file), không phải logic gameplay cần coverage CI.

## §2. Implementation checklist

- [x] Sửa `BalanceHarness.MaterialDropReport()`: bỏ hardcode chương 1, lặp `for (int chapter = 1;
      chapter <= 5; chapter++)` qua `SimulateChapter()` mới tách riêng, in báo cáo riêng từng chương.
- [x] Sửa bug đếm mảnh Boss — dùng `bossRoll.ShardHeroIndex >= 0` thay vì cộng cứng
      `SIMULATED_OWNED_HEROES`.
- [x] Thêm khối TỔNG 5 chương (cộng dồn, không nhân RUNS) + viết lại `AscendPacingReport` theo %
      chi phí mỗi bậc (không còn "X run" — chương không replay được nên "run" không có nghĩa), kèm
      ghi chú rõ giới hạn phạm vi (không tính Dungeon/Shop/Gacha dupe).
- [x] Tách `BuildMaterialDropReport()` (trả `string`) khỏi `MaterialDropReport()` (menu item,
      `Debug.Log` chuỗi đó) — phát hiện phụ lúc verify: Unity Console chỉ hiện DÒNG ĐẦU của
      `Debug.Log` nhiều dòng trong `read_console`, cần gọi thẳng hàm trả chuỗi qua `execute_code`
      để lấy đủ nội dung, không phải bug của báo cáo.
- [x] `refresh_unity` compile sạch (2 lần, sau mỗi đợt sửa).
- [x] Chạy thật qua `execute_code` (gọi `BuildMaterialDropReport()` qua reflection, không qua
      `Debug.Log`/`read_console` vì lý do trên) — số liệu thật (2 lần chạy, sai số ngẫu nhiên nhỏ,
      kết luận không đổi):
      - Chương 1: Treasure ~0.86 node, Gold ~103-119, Mảnh ~0.44-0.46, EssenceI ~7.76-7.79
      - Chương 2: Treasure ~0.82-0.87, Gold ~139-148, Mảnh ~0.40-0.43, EssenceI ~5.99-6.05,
        EssenceII ~2.71-2.78
      - Chương 3: Treasure ~0.81-0.83, Gold ~180-182, Mảnh ~0.43-0.44, EssenceII ~5.81,
        Core ~1.49-1.50, EssenceI ~1.98-2.07
      - Chương 4: Treasure ~0.79-0.85, Gold ~227-247, Mảnh ~0.45-0.50, EssenceII ~5.38-5.60,
        EssenceIII ~2.74-2.75, Core ~2.51
      - Chương 5: Treasure ~0.83-0.88, Gold ~312-332, Mảnh ~0.53-0.56, EssenceIII ~5.76-5.84,
        Core ~4.15-4.19, EssenceII ~1.35-1.43
      - **TỔNG 1 playthrough trọn vẹn**: Mảnh ~2.32, EssenceI ~15.8, EssenceII ~15.3-15.5,
        EssenceIII ~8.5-8.6, Core ~8.16-8.20.
- [x] Đối chiếu số liệu thật với `AscendSystem.COSTS` — **KHÔNG sửa asset nào**, kết luận chi tiết
      ở §3 bên dưới: chênh lệch quan sát được (story-only chỉ đủ 23%/0% các bậc) là ĐÚNG THIẾT KẾ
      (nền kinh tế nhiều lớp: Gacha dupe = nguồn Mảnh chính, Shop mua Gem = nguồn Core/Essence bù,
      Material Dungeon = nguồn cày dài hạn — cả 3 đều đã xây thật, xác nhận qua code, không phải
      giả định), không phải lỗi số liệu 10 asset chương. Đường cong NỘI TẠI của 10 asset (thứ tự mở
      khoá vật liệu theo chương: EssenceI ch1 → EssenceII ch2 → Core ch3 → EssenceIII ch4) khớp
      đúng thứ tự `AscendSystem.COSTS` cần (★1-2 EssenceI, ★3-4 EssenceII, ★4-5 Core, ★5-6
      EssenceIII) — mỗi loại mở khoá SỚM HƠN 1 chương so với mốc ★ cần nó, đúng ý đồ thiết kế ban
      đầu, không có bậc nào "kẹt cứng" (0 nguồn vĩnh viễn) như lỗ hổng Core/EssenceIII đã tìm thấy
      và vá ở task-ascend.md §8 (lượt đó vá bằng hằng số cố định trên `PlaceholderLootTable`,
      LƯỢT NÀY xác nhận per-chapter asset thật đã kế thừa đúng và mở rộng bản vá đó, không bị thụt
      lùi khi task-loottable-chapters.md thay thế wildcard bằng bảng riêng theo chương).
- [x] Chạy full EditMode suite — **402/402 xanh**, không đổi (thay đổi chỉ ở Editor tool
      `BalanceHarness.cs`, không đụng gameplay code/test).
- [x] Cập nhật `task-loottable-chapters.md`, `roadmap.md §0.1`, `object-map.md §12.1`.
- [x] `spawn_task` báo riêng comment sai ở `LocalPlayerRepository.cs:186`.

## §3. Kết luận

**Không sửa số liệu nào trong 10 asset `loottable_*_ch{1..5}.asset`.** Báo cáo ban đầu (trước khi
sửa 2 bug) sẽ khiến người đọc tưởng lầm "Mảnh hero rơi ra quá nhiều từ Boss" (do bug cộng cứng) và
"chỉ chương 1 mới đáng tin" (do hardcode) — sau khi sửa, số liệu thật cho thấy đường cong 10 asset
đã THIẾT KẾ ĐÚNG Ý ĐỒ ban đầu của task-loottable-chapters.md (mở khoá vật liệu sớm hơn 1 chương so
với mốc cần). Việc story-only chỉ đủ 23%/0% chi phí Ascend từng bậc là DỰ KIẾN, không phải thiếu sót
— trò chơi có 3 nguồn khác đã xây thật và verify hoạt động ở các session trước (Gacha dupe → Mảnh,
Shop mua Gem → Core/Essence, Material Dungeon → cày dài hạn), BalanceHarness lượt này chỉ đo được 1
trong 4 nguồn (đúng phạm vi "loot table" được giao), nên % thấp không tự nó là tín hiệu mất cân
bằng. Giá trị chính của lượt này là 2 con bug được sửa (hardcode chương + đếm sai mảnh Boss) khiến
báo cáo từ nay phản ánh đúng thực tế, không phải việc chỉnh số — matching tinh thần "chỉ vá nếu có
bằng chứng thật" đã ghi ở §1.

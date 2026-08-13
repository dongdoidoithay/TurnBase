# Task: Per-chapter loot table tuning

Yêu cầu: hạng mục được chọn qua `AskUserQuestion` ("Per-chapter loot table tuning (Recommended)")
trong số 3 lựa chọn còn lại (Consumable items / loot table / khác). Theo quy trình chuẩn: viết
xong task file này rồi mới chạm code.

## §0. Findings

- **`LootRoller`/`LootTableDefinitionSO` đã có đủ hạ tầng kỹ thuật** (task-loottable.md, phiên
  trước) — `LootRoller.Resolve(chapter, nodeType)` ưu tiên bảng khớp đúng `Chapter`, fallback
  `Chapter=0` (wildcard) nếu không có. Việc còn lại THUẦN LÀ TẠO THÊM ASSET, không cần sửa code.
- **Hiện chỉ có 2 asset, cả 2 đều wildcard (`Chapter=0`)**:
  `loottable_treasure_default.asset` (NodeType=Treasure) và `loottable_boss_default.asset`
  (NodeType=Boss) — dùng CHUNG 1 bộ số cho mọi chương, không phân biệt độ khó/tiến trình.
- **`LootRoller.Resolve` chỉ được gọi cho đúng 2 NodeType này** (grep xác nhận: `ResolveTreasure`
  cho `NodeType.Treasure`, `GrantBossAscendMaterials` cho `NodeType.Boss`) — không có node type
  nào khác cần bảng loot cả, phạm vi tuning chỉ giới hạn ở 2 loại này × 5 chương = **10 asset
  mới**.
- **plan.md không có bảng tỉ lệ cụ thể theo chương** (đã ghi nhận từ trước trong chính doc-comment
  của `LootTableDefinitionSO`) — số liệu là TỰ THIẾT KẾ, dùng
  `AscendSystem.COSTS` (bảng chi phí Essence/Core theo từng mốc ★) làm neo tham chiếu để đường
  cong material hợp lý (không phải random số): mốc ★2 cần EssenceI 5-10, ★3 cần EssenceII 15,
  ★4 cần EssenceII 25 + Core 5, ★5 cần EssenceIII 40 + Core 15. Thiết kế material Boss theo từng
  chương bám sát các mốc này (chương sớm cấp vật liệu bậc thấp, chương cuối cấp vật liệu bậc cao)
  để người chơi lên ★ đúng nhịp tiến trình, KHÔNG claim đây là số liệu đã cân bằng qua playtest
  thật — ghi rõ "chưa qua Balance Harness", giống cách các hệ thống khác trong dự án đã làm.
- **Giữ nguyên 2 asset wildcard cũ** — không xoá, không sửa. Chúng vẫn là fallback an toàn cho
  bất kỳ combo chương/loại node nào lỡ chưa author (dù giờ cả 5 chương đều có bảng riêng, phòng
  hờ chương 6+ trong tương lai nếu roadmap mở rộng). Việc thêm asset là THUẦN CỘNG THÊM, không có
  rủi ro phá hành vi cũ.
- **`LootRollerTests.cs` đã tồn tại (15 test) — 2 test SẼ VỠ khi thêm asset chương cụ thể, phải
  sửa (không phải bug của tôi gây ra, mà là hệ quả TẤT YẾU của việc thêm đúng data task này yêu
  cầu):**
  - `Resolve_PrefersExactChapterMatch_OverWildcard` — tên đúng ý định nhưng THÂN TEST hiện tại
    lại assert NGƯỢC LẠI (`Assert.AreEqual(0, table.Chapter)` — verify nó RƠI VỀ wildcard) vì lúc
    viết test này chưa có asset chương cụ thể nào để verify "ưu tiên" thật. Sau khi thêm
    `loottable_treasure_ch1.asset`, `Resolve(1, Treasure)` sẽ trả đúng bảng chương 1 (`Chapter=1`)
    — phải SỬA assertion để test cuối cùng verify đúng cái tên nó tuyên bố.
  - `RealBossAsset_CoversEveryMaterialType_AscendSystemEverRequires` — hiện chỉ gọi
    `Resolve(chapter: 1, Boss)` MỘT LẦN rồi kiểm tra bảng đó một mình phải cấp đủ MỌI loại vật
    liệu cho MỌI mốc ★ tới `MAX_STAR` (hợp lý khi chỉ có 1 bảng wildcard dùng chung). Thiết kế
    per-chapter mới CỐ Ý chia nhỏ vật liệu theo chương (chương 1 chỉ có EssenceI, chương 3 mới có
    Core, chương 4 mới có EssenceIII...) — đúng tinh thần "tiến trình" (phải đi qua các chương để
    mở khoá vật liệu bậc cao, không cày mãi chương 1). Test cũ dựa trên giả định 1-bảng-lo-hết
    KHÔNG còn đúng nữa. → Sửa test thành: kiểm tra HỘI của cả 5 bảng Boss (chương 1-5) cộng lại
    phải phủ đủ mọi loại vật liệu — đúng ý nghĩa thật của "progression", không phải hack để test
    xanh.

## §1. Scope decision

**Trong phạm vi:**
1. 5 asset `loottable_treasure_ch{N}.asset` (N=1..5), 5 asset `loottable_boss_ch{N}.asset`
   (N=1..5) — tạo bằng cách viết trực tiếp file `.asset` (YAML) theo đúng khuôn 2 file mẫu hiện
   có, không qua Editor UI (nhanh, chính xác, không rủi ro thao tác chuột).
2. Đường cong thiết kế (ghi cụ thể trong checklist bên dưới) — Gold/material/equipment rarity
   tăng dần theo chương, material Boss bám mốc `AscendSystem.COSTS`.
3. Test xác nhận `LootRoller.Resolve` trả đúng bảng theo chương (không lẫn/không rơi về wildcard
   khi đã có bảng riêng).

**Ngoài phạm vi (cố ý, ghi rõ):**
- KHÔNG chạy Balance Harness để tinh chỉnh số liệu — đây là 1 bước cân bằng riêng, cần dữ liệu
  chơi thật, không thuộc phạm vi "tune loot table" ban đầu (tạo bảng theo chương, không phải cân
  bằng kinh tế toàn game).
- KHÔNG thêm NodeType nào khác vào hệ loot table (Event/Rest đã có cơ chế riêng qua
  `NodeChoiceSystem`, không dùng `LootRoller`).
- KHÔNG sửa `LootRoller.cs`/`LootTableDefinitionSO.cs` — hạ tầng đã đủ, chỉ thêm data.

## §2. Implementation checklist

Đường cong Treasure (Gold/EssenceI-III/HeroShard/Equipment rarity tăng dần):

| Chương | Gold | Material | HeroShard | Equip MinRarity |
|---|---|---|---|---|
| 1 | 80-160 | EssenceI 2-4 @50% | 50% | Rare |
| 2 | 120-220 | EssenceI 3-5 @60% + EssenceII 1-2 @20% | 50% | Rare |
| 3 | 160-280 | EssenceI 4-6 @50% + EssenceII 2-3 @40% | 55% | Epic |
| 4 | 220-360 | EssenceII 3-4 @50% + EssenceIII 1-2 @25% | 60% | Epic |
| 5 | 300-460 | EssenceII 3-5 @40% + EssenceIII 2-3 @40% + Core 1 @20% | 65% | Legendary |

Đường cong Boss (Gold=0 giữ nguyên — Gold Boss lấy từ battle reward, không qua bảng này; material
bám mốc `AscendSystem.COSTS`):

| Chương | Material |
|---|---|
| 1 | EssenceI 5-8 @100% |
| 2 | EssenceI 3-5 @100% + EssenceII 2-3 @100% |
| 3 | EssenceII 4-6 @100% + Core 1-2 @100% |
| 4 | EssenceII 3-5 @100% + EssenceIII 2-3 @100% + Core 2-3 @100% |
| 5 | EssenceIII 4-6 @100% + Core 3-5 @100% |

- [x] Tạo 5 asset `loottable_treasure_ch{1..5}.asset`.
- [x] Tạo 5 asset `loottable_boss_ch{1..5}.asset`.
- [x] `refresh_unity` xác nhận Unity import đủ 12 asset (10 mới + 2 wildcard cũ), không lỗi —
      verify qua `execute_code` đọc `Resources.LoadAll` thật, không chỉ tin test.
- [x] Sửa 2 test vỡ (đã ghi chi tiết ở §0): `Resolve_PrefersExactChapterMatch_OverWildcard` giờ
      verify đúng ý nghĩa tên (chương cụ thể thắng wildcard), thêm
      `Resolve_AllFiveChapters_ReturnDedicatedTable_ForTreasureAndBoss`; đổi
      `RealBossAsset_CoversEveryMaterialType_AscendSystemEverRequires` thành
      `RealBossAssets_UnionAcrossAllChapters_...` — verify HỘI 5 bảng chương, không phải 1 bảng
      đơn lẻ.
- [x] Chạy full EditMode suite — **388/388 xanh** (387 cũ + 2 test mới − 1 test cũ đổi tên/sửa
      thân giữ nguyên số lượng net = +1).
- [x] execute_code smoke check thật: `LootRoller.Resolve(N, Treasure/Boss)` cho N=1..5 trên Editor
      đang chạy, cả 10 tổ hợp trả đúng `DefId` mong đợi (`loottable_treasure_ch{N}`/
      `loottable_boss_ch{N}`), không lệch/không rơi wildcard.
- [x] Cập nhật `roadmap.md` §0.1 (P5: "loot table chỉ wildcard chưa theo chương" → xong) và
      `object-map.md` §12/§12.1.

## §3. Phát hiện lúc Balance Harness pass (follow-up, task-balance-loottable.md)

Session sau chạy thật `BalanceHarness.MaterialDropReport()` (đã sửa 2 bug cũ: hardcode chương 1,
đếm sai mảnh hero Boss) đối chiếu 10 asset ở đây với `AscendSystem.COSTS` — **kết luận: KHÔNG cần
sửa số liệu nào**. Đường cong mở khoá vật liệu theo chương (EssenceI ch1 → EssenceII ch2 → Core ch3
→ EssenceIII ch4, mỗi loại mở sớm hơn 1 chương so với mốc ★ cần) khớp đúng ý đồ thiết kế ban đầu ở
§2 phía trên. Số liệu thật: cả 5 chương cộng lại (1 playthrough, không replay được) cho ~2.32 mảnh
hero, ~15.8 EssenceI, ~15.4 EssenceII, ~8.5 EssenceIII, ~8.2 Core — chỉ đủ một phần nhỏ chi phí
Ascend cả game, nhưng đây là DỰ KIẾN vì loot table chương chỉ là 1 trong 4 nguồn (Gacha dupe mới là
nguồn Mảnh chính, Shop mua Gem bù Core/Essence, Material Dungeon cày dài hạn) — xem
task-balance-loottable.md §3 để biết đầy đủ.

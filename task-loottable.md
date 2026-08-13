# TASK-LOOTTABLE.md — `LootTableDefinitionSO` thật, thay `PlaceholderLootTable`

> Thay hằng số cố định (`PlaceholderLootTable`) bằng ScriptableObject thật, theo đúng khuôn
> `HeroDefinitionSO`/`EnemyDefinitionSO` — đúng plan.md §8.1 (Treasure/Boss), object-map.md §6.2/
> §9 (`LootTableDefinition` → `LootRoller`/`RewardResolver`).

---

## 0. Phát hiện quan trọng trước khi làm

- **plan.md KHÔNG có bảng tỉ lệ loot cụ thể theo chương/rarity** (đã tìm kỹ §7.2/§8.1) — §7.2 là
  bảng sub-stat trang bị, §8.1 là tỉ lệ LOẠI NODE (Battle 45%/Elite 15%/Treasure 10%/...), không
  phải tỉ lệ VẬT LIỆU rơi trong Treasure. Nghĩa là schema `LootTableDefinitionSO` phải tự thiết
  kế — không có "đáp án đúng" để chép, chỉ có khuôn dạng (`*DefinitionSO`) và điểm neo (Ascend
  cost table, `NodeType`, `RunStateDto.ChapterId`) để bám theo.
- **KHÁC `AwakeningCatalog`/`AscendSystem.COSTS`** (không dùng ScriptableObject vì
  `StatModifier`/`StatusApplication` có field readonly) — `LootTableDefinitionSO` ở đây chỉ chứa
  field thường (string/int/float/enum), **DÙNG ScriptableObject được, nên PHẢI dùng** — đây là hệ
  loot thật đúng nghĩa, khác các catalog hard-code trước đó.
- plan.md §8.1 ghi Treasure "đảm bảo ≥ 1 trang bị Rare" — **KHÔNG làm phần trang bị lượt này**
  (chưa có `EquipmentGenerator` nào tồn tại trong code — 0 kết quả grep, là 1 hệ hoàn toàn riêng,
  không tương xứng phạm vi "thay Ascend material placeholder"). Ghi rõ ngoài phạm vi ở mục 5.
- `object-map.md` xác nhận người tiêu thụ dự kiến: `LootRoller` (roll runtime) +
  `RewardResolver` (đường dẫn kết quả trận → Economy — cũng chưa tồn tại, không xây lượt này, chỉ
  gọi thẳng `LootRoller` từ `MetaSceneInstaller` như `PlaceholderLootTable` đang làm).
- Test class kỳ vọng theo `object-map.md` §8: `LootRollerTests`, code `T-META-LOOT`.

## 1. `LootTableDefinitionSO`

- [x] File mới `Assets/_Project/Scripts/Meta/Content/LootTableDefinitionSO.cs`,
      `Game.Meta.Content`, đúng khuôn `HeroDefinitionSO`/`EnemyDefinitionSO`
      (`[CreateAssetMenu(menuName = "TurnBase/Content/Loot Table", fileName =
      "loottable_new")]`, `OnValidate` backfill `DefId` từ tên asset).
- [x] Field:
      `public string DefId`, `public int Chapter` (0 = áp dụng mọi chương, ưu tiên số cụ thể nếu
      có nhiều bảng khớp), `public NodeType NodeType` (Treasure/Boss).
      `public int GoldMin/GoldMax`.
      `[Serializable] public struct MaterialDrop { CurrencyType Type; int MinAmount; int
      MaxAmount; float Chance; }` — `public MaterialDrop[] Materials`.
      `public float HeroShardChance; public int HeroShardMin/HeroShardMax;` (mảnh hero ngẫu
      nhiên trong số đang sở hữu, giữ đúng hành vi `PlaceholderLootTable` cũ).
- [x] KHÔNG thêm field trang bị (mục 0).

## 2. `LootRoller.cs`

- [x] File mới `Assets/_Project/Scripts/Meta/Dungeon/LootRoller.cs`, `Game.Meta.Dungeon`, static
      class, dùng `IRandomSource` (đúng kỷ luật `GachaSystem`/`PlaceholderLootTable` cũ).
- [x] `Resolve(int chapter, NodeType nodeType) → LootTableDefinitionSO` — `Resources.LoadAll<
      LootTableDefinitionSO>("Data/LootTables")`, lọc `NodeType` khớp, ưu tiên `Chapter` khớp
      chính xác, fallback `Chapter == 0` (wildcard) nếu không có bảng riêng cho chương đó. Cache
      tĩnh (đúng pattern `EquipmentService.Catalog`) vì `Resources.LoadAll` gọi nhiều lần tốn kém.
- [x] `Roll(LootTableDefinitionSO table, IRandomSource rng, int ownedHeroCount) →
      LootRollResult{ long Gold; List<(CurrencyType,int)> Materials; int ShardHeroIndex }` — mỗi
      `MaterialDrop` roll ĐỘC LẬP theo `Chance` riêng (khác `PlaceholderLootTable` cũ chỉ có
      đúng 1 nhánh 50/50 loại trừ nhau — thiết kế mới cho phép nhiều vật liệu cùng rơi 1 lúc, linh
      hoạt hơn cho content sau này), Gold luôn roll trong `[GoldMin, GoldMax)`.
- [x] Nếu `Resolve` không tìm được bảng nào khớp (chưa author asset) → trả `null`, caller
      (`MetaSceneInstaller`) fallback về **giữ nguyên hành vi `PlaceholderLootTable` cũ** làm lưới
      an toàn — KHÔNG để game vỡ vì thiếu asset (áp dụng đúng tinh thần "không phá cái đang chạy").

## 3. Author asset thật (Unity Editor, không phải chỉ viết code)

- [x] Tạo thư mục `Assets/_Project/Resources/Data/LootTables/` (chưa tồn tại).
- [x] Tạo **2 asset wildcard V1** (Chapter=0, áp dụng mọi chương — chưa tách theo chương, đó là
      việc content-design lượt sau, ghi rõ trong comment):
      `loottable_treasure_default.asset` (NodeType=Treasure) — copy đúng số liệu hành vi cũ:
      Gold 80-160, EssenceI 2-4 @ 50% chance, HeroShard 1 @ 50% chance (2 dòng loại trừ nhau về
      mặt xác suất TỔNG nhưng giờ là 2 `MaterialDrop` độc lập — nếu muốn giữ ĐÚNG 100% hành vi cũ
      loại trừ nhau thì cần 1 field bool `MutuallyExclusiveWithNext` hoặc nhóm — CÂN NHẮC: nếu
      không muốn thêm độ phức tạp, chấp nhận sai khác nhỏ (2 nhánh giờ có thể cùng rơi) và ghi rõ
      đây là **cải thiện có chủ đích**, không phải bug, so với `PlaceholderLootTable`).
      `loottable_boss_default.asset` (NodeType=Boss) — EssenceII 3 @ 100%, EssenceIII 1 @ 100%,
      Core 1 @ 100%, HeroShard xử lý riêng (1/hero ra trận, không qua `HeroShardChance` field vì
      là "mỗi hero" chứ không phải "1 hero ngẫu nhiên" — giữ logic đặc biệt này trong
      `MetaSceneInstaller.GrantBossAscendMaterials`, KHÔNG nhét vào `LootTableDefinitionSO`).
      **Gem KHÔNG còn nằm ở đây** nếu `task-quest.md` đã làm trước (Gem chuyển sang Quest) — nếu
      làm `task-loottable.md` TRƯỚC `task-quest.md` (đúng thứ tự user yêu cầu) thì vẫn giữ
      `BOSS_REWARD_GEM` tạm thời, `task-quest.md` sẽ xoá sau.
- [x] Dùng `unity-mcp-skill` (`manage_asset`/`execute_code` tạo ScriptableObject instance, set
      field, `AssetDatabase.SaveAssets`) — KHÔNG tạo asset bằng tay ngoài Editor.

## 4. Nối vào `MetaSceneInstaller`

- [x] `ResolveTreasure`: thay `PlaceholderLootTable.RollTreasure(...)` bằng
      `LootRoller.Resolve(_profile.Run.ChapterId, NodeType.Treasure)` → nếu có bảng, `Roll(...)`;
      nếu `null`, fallback `PlaceholderLootTable.RollTreasure(...)` (mục 2).
- [x] `GrantBossAscendMaterials`: tương tự với `NodeType.Boss`.
- [x] Giữ nguyên toàn bộ Toast/UI text hiện có, chỉ đổi nguồn số liệu.

## 5. Ngoài phạm vi (ghi rõ)

- Trang bị rơi ở Treasure (plan.md §8.1 "≥ Rare") — cần `EquipmentGenerator` (hệ riêng, chưa
  tồn tại) — KHÔNG làm lượt này.
- Bảng loot riêng theo TỪNG chương (1-5) — V1 chỉ có bảng wildcard `Chapter=0`. Cân bằng thật theo
  chương là việc content-design, dùng `BalanceHarness` đã có để kiểm tra sau khi có số liệu.
- `RewardResolver`/`IEventBus` (object-map.md §9) — kiến trúc event-driven đầy đủ, dự án hiện gọi
  method trực tiếp (`MetaSceneInstaller` gọi thẳng `LootRoller`), giữ nguyên pattern này.

## 6. Test

- [x] `LootRollerTests.cs` (`Assets/Tests/EditMode/Meta/`, đúng tên object-map.md §8 kỳ vọng) —
      theo style `GachaPityTests`/`PlaceholderLootTableTests`:
      `Resolve` ưu tiên bảng khớp Chapter cụ thể trước wildcard; `Resolve` trả `null` khi không có
      bảng nào khớp NodeType; `Roll` mỗi `MaterialDrop` độc lập theo đúng `Chance` (test thống kê
      qua nhiều lần roll, ±3% dung sai như `PlaceholderLootTableTests` cũ); Gold luôn trong
      khoảng `[Min, Max)`.
- [x] Regression quan trọng — copy lại đúng tinh thần
      `BossReward_CoversEveryMaterialType_AscendSystemEverRequires` cũ: mọi `CurrencyType` mà
      `AscendSystem` từng yêu cầu phải có ít nhất 1 `MaterialDrop` trong asset Boss/Treasure với
      `Chance > 0` — nếu không, bậc ★ đó lại bế tắc như đã phát hiện ở task-ascend.md §8.
- [x] Test `MetaSceneInstaller` fallback đúng khi `LootRoller.Resolve` trả `null` (chưa author
      asset) — không crash, dùng `PlaceholderLootTable` cũ.
- [x] Chạy full EditMode suite, phải xanh 100%.

## 7. Verification

- Play mode: ghé node Treasure/thắng Boss thật → vật liệu cộng đúng theo asset mới (không phải
  hằng số cũ), Toast hiển thị đúng.
- Chạy `Tools/Balance Harness/Material Drop Report` lại — cập nhật `BalanceHarness.cs` để đọc số
  liệu qua `LootRoller`/asset thay vì hằng số `PlaceholderLootTable` (nếu asset đã thay thế hoàn
  toàn); đối chiếu số liệu vẫn hợp lý (không xấu đi so với task-ascend.md §8 đã tính).

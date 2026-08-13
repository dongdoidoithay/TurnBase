# TASK-EQUIPMENT.md — `EquipmentGenerator`: trang bị rơi thật ở node Treasure

> Xây hệ sinh trang bị ngẫu nhiên mà `task-loottable.md` §0/§5 đã cố tình né ("0 kết quả grep
> EquipmentGenerator — hệ hoàn toàn riêng, không tương xứng phạm vi lượt đó"). Mục tiêu lượt này:
> node Treasure rơi **đúng 1 trang bị thật ≥ Rare** theo plan.md §8.1, sub-stat theo đúng bảng
> §7.2, và sub-stat đó có **tác dụng thật trong combat** (không chỉ là số hiển thị).
>
> Liên quan: [plan.md §7](plan.md), [plan.md §8.1](plan.md), [task-loottable.md](task-loottable.md),
> [object-map.md §6.2/§8/§9](object-map.md).

---

## 0. Phát hiện quan trọng trước khi làm

- **`CombatUnit.EquipmentModifiers` đã tồn tại và ĐÃ được xử lý đầy đủ** —
  `Assets/_Project/Scripts/Combat/Model/CombatUnit.cs` dòng ~48 khai báo
  `readonly List<StatModifier> EquipmentModifiers`, và `ComputeStats()` (dòng ~87-118) đã
  `Accumulate()` list này cho **toàn bộ** `StatType` cần cho pool sub-stat §7.2 (`MaxHpPct`,
  `AtkPct`, `DefPct`, `SpdPct`, `CritPct`, `CritDmgPct`, `Res`, `EffAcc`, cùng `Spd` flat) — đúng
  thứ tự modifier plan.md §4.5. **Nhưng KHÔNG ai populate list này trong production** — chỉ
  `EquipmentService.GetBonusPrimary` ghi vào `PrimaryStats` (và chỉ đọc `MainStat`, hoàn toàn bỏ
  qua `SubStats`). Mồ côi y hệt kiểu `task-ascend.md` §10 đã phát hiện với `HeroInstanceDto.
  Awakened`. → Nghĩa là generator PHẢI đi kèm bước wiring nhỏ (mục 3) để sub-stat rơi ra có ý
  nghĩa, nếu không chỉ là dữ liệu chết.
- **`EquipmentInstanceDto.SubStats` (`List<SubStatDto>`) và `.SetId` đã có sẵn trong schema**
  (`Data/Dto/PlayerProfileDto.cs` dòng ~97-98), chưa ai ghi — **không cần đổi save schema**,
  giống pattern `task-ascend.md` §0.
- **`EquipmentDefinitionSO.Rarity` là `int` thường** (giá trị hiện tại chỉ 1 hoặc 2 trên cả 14
  asset trong `Resources/Data/Equipment/`), **KHÔNG phải** enum `Rarity` (Common..Mythic) dùng ở
  Hero/Gacha (`CombatEnums.cs`). `EquipmentService.EnsureStarterEquipment` dùng `def.Rarity` để
  gán rarity cho item khởi điểm — đây là nhãn TIER CỐ ĐỊNH của template, không liên quan tới hệ
  roll ngẫu nhiên mới. **Quyết định: KHÔNG convert field này, KHÔNG đụng 14 asset cũ.**
  `EquipmentGenerator` chọn def theo `Slot` (bỏ qua `def.Rarity` khi lọc), rồi tự roll
  **Rarity của INSTANCE** độc lập, lưu vào `EquipmentInstanceDto.Rarity` dạng `(int)Rarity` khớp
  thứ tự enum chung (0=Common..4=Mythic) để nhất quán với Hero/Gacha — khác quy ước 1/2 cũ của
  `def.Rarity`, nhưng 2 field này chưa từng được so sánh trực tiếp với nhau nên không xung đột.
- **Assembly**: `Meta/Equipment`, `Meta/Dungeon`, `Meta/Content` đều nằm chung 1 asmdef
  `Game.Meta` — `LootRoller` gọi thẳng `EquipmentGenerator`/`EquipmentService` không cần đổi
  `.asmdef` nào.
- **Chưa có Inventory/Equipment UI screen nào** (`grep -n "Equip" HeroDetailScreen.cs` → 0 kết
  quả) — trang bị rơi ra sẽ nằm trong `profile.Equipment`, hiển thị qua Toast (như material/shard
  hiện tại ở `ResolveTreasure`) nhưng người chơi chưa có cách tự equip qua UI. Gap có từ trước,
  KHÔNG phải do task này gây ra — ghi rõ ngoài phạm vi (mục 6).

---

## 1. `EquipmentGenerator` (mới)

- [x] File mới `Assets/_Project/Scripts/Meta/Equipment/EquipmentGenerator.cs`, `Game.Meta.
      Equipment`, static class, dùng `IRandomSource` (đúng kỷ luật `GachaSystem`/`LootRoller` —
      test/harness gọi lại được với seed cố định).
- [x] Bảng sub-stat pool + khoảng giá trị đúng **plan.md §7.2** — 8 dòng:

  | Sub stat | `StatType` | Common | Rare | Epic | Legendary | Mythic |
  |---|---|---|---|---|---|---|
  | ATK% | `AtkPct` | 2–4 | 4–7 | 6–10 | 9–14 | 12–18 |
  | HP% | `MaxHpPct` | 3–5 | 5–8 | 7–12 | 11–16 | 14–20 |
  | DEF% | `DefPct` | 3–5 | 5–8 | 7–12 | 11–16 | 14–20 |
  | SPD (flat) | `Spd` | 1–2 | 2–4 | 3–6 | 5–8 | 7–11 |
  | CRIT% | `CritPct` | 1–2 | 2–4 | 3–6 | 5–8 | 7–10 |
  | CRIT_DMG% | `CritDmgPct` | 2–4 | 4–8 | 7–12 | 10–18 | 15–25 |
  | RES | `Res` | 2–4 | 4–7 | 6–10 | 9–14 | 12–18 |
  | EFF_ACC | `EffAcc` | 2–4 | 4–7 | 6–10 | 9–14 | 12–18 |

- [x] Số sub-stat khởi điểm theo rarity đúng plan: Common 1 · Rare 2 · Epic 2 · Legendary 3 ·
      Mythic 4.
- [x] `Roll(EquipSlot? slot, Rarity rarity, IRandomSource rng) → EquipmentInstanceDto`:
  - Lọc `EquipmentService.Catalog` theo `slot` (null = mọi slot); tập rỗng → trả `null` (caller tự
    fallback, không throw — đúng tinh thần `LootRoller.Resolve`).
  - Chọn 1 def ngẫu nhiên trong tập đã lọc (bỏ qua `def.Rarity` — finding mục 0).
  - `Uid` mới không trùng (đủ để không đụng độ trong 1 save — dùng `System.Guid.NewGuid()` là đơn
    giản nhất, khác `EnsureStarterEquipment` dùng counter vì đó là batch 1 lần).
  - `MainStatType`/`MainStatValue` = lấy nguyên từ def (giữ đúng hành vi MVP hiện có — **không
    đổi**).
  - `Rarity` = `(int)rarity`.
  - Roll N sub-stat **không trùng loại nhau** (loại trừ dần khỏi pool sau mỗi lần chọn, không
    phải reject-and-retry vô hạn), mỗi giá trị `rng.NextFloat(min, max)` theo đúng cột rarity ở
    bảng trên.
- [x] Hàm roll số lượng/loại sub-stat tách riêng (`RollSubStats`, public), để test được độc lập
      với việc chọn def. Thêm luôn `RollFrom(catalog, slot, rarity, rng)` — overload nhận catalog
      tuỳ ý, tách khỏi `Roll` (dùng `EquipmentService.Catalog` thật) — để test được case "slot
      không có def nào" mà không phụ thuộc `Resources.LoadAll` (không có trong kế hoạch ban đầu,
      phát sinh lúc viết test).

## 2. Nối vào `LootTableDefinitionSO` + `LootRoller`

- [x] `LootTableDefinitionSO` thêm field (Unity không serialize `Nullable<enum>` tốt trong
      Inspector — dùng cặp bool+enum thay vì `EquipSlot?`):
      `[Range(0f,1f)] public float EquipmentChance;`
      `public Rarity EquipmentMinRarity = Rarity.Rare;`
      `public bool EquipmentAnySlot = true; public EquipSlot EquipmentSlot;`
- [x] `LootRoller.LootRollResult` thêm field `EquipmentInstanceDto Equipment` (null nếu không
      trúng hoặc không có def nào khớp slot).
- [x] `LootRoller.Roll(...)`: nếu `rng.Chance(table.EquipmentChance)` → roll rarity thật từ
      `EquipmentMinRarity` trở lên (plan không cho công thức cụ thể cho việc này — **tự thiết kế**
      giống cách `LootTableDefinitionSO` đã tự thiết kế schema ở `task-loottable.md` §0; phân phối
      mỗi bậc cao hơn = 40% trọng số bậc liền trước, ghi rõ trong comment đây là placeholder chờ
      Balance Harness) → gọi `EquipmentGenerator.Roll(...)`.
- [x] **Không đổi** hành vi `PlaceholderLootTable` fallback (không có trang bị) — đúng tinh thần
      "không phá cái đang chạy" của `task-loottable.md`.

## 3. Wiring vào Combat — bắt buộc để sub-stat có tác dụng thật (finding mục 0)

- [x] `EquipmentService` thêm `GetEquipmentModifiers(PlayerProfileDto profile, HeroInstanceDto
      hero) → List<StatModifier>`: duyệt 6 slot `hero.Equipped`, mỗi item convert `SubStats`
      (`List<SubStatDto>`) thành `StatModifier(stat, value)` — `SubStatDto.StatType` đã cùng enum
      `StatType` nên convert trực tiếp, không cần mapping bảng.
      **Không** convert `MainStat` ở đây — main stat vẫn đi qua `GetBonusPrimary` như cũ, chỉ
      thêm đường mới cho `SubStats` (2 đường song song, không thay thế nhau).
- [x] `BattleSceneInstaller.SpawnTeamFromDefinitions` (khu vực dòng ~234-244, ngay sau
      `unit = BuildUnitFromDefinition(...)` ở nhánh player): thêm
      `if (heroInstance != null) unit.EquipmentModifiers.AddRange(EquipmentService.
      GetEquipmentModifiers(profile, heroInstance));`
- [x] **Không đụng** `PassiveModifiers`/`ComputeStats()` — logic đã đúng sẵn, chỉ cần populate.

## 4. Nối vào `MetaSceneInstaller.ResolveTreasure`

- [x] Sau khi roll, nếu `roll.Equipment != null` → `profile.Equipment.Add(roll.Equipment)`, thêm
      dòng vào `parts` hiển thị Toast hiện có (VD `"+1 Rare Ring (eq_ring_focus)"` — có thể viết
      helper nhỏ format tên, không bắt buộc đẹp).
- [x] **Không** grant equipment ở Boss (`GrantBossAscendMaterials`) lượt này — plan.md §8.1 chỉ
      ghi rõ Treasure, giữ tối giản đúng kỷ luật các task trước (mục 6).

## 5. Author asset thật (Unity Editor, không chỉ viết code)

- [x] Cập nhật asset Treasure hiện có trong `Resources/Data/LootTables/`
      (`loottable_treasure_default.asset`): `EquipmentChance = 1.0`, `EquipmentMinRarity = Rare`,
      `EquipmentAnySlot = true` — đúng "đảm bảo ≥1 trang bị ≥ Rare" plan.md §8.1. Đặt qua
      `manage_scriptable_object` (Unity MCP), verify lại bằng cách đọc thẳng YAML asset.

## 6. Ngoài phạm vi (ghi rõ)

- Inventory/Equipment UI screen (equip/unequip qua UI người chơi tự thao tác) — chưa có màn hình
  nào tồn tại (finding mục 0), không xây lượt này. Verify vẫn làm được qua `execute_code` (mục 8).
- Set Bonus (2/4 món, plan §7.4) — `EquipmentInstanceDto.SetId` vẫn để trống, `EquipmentGenerator`
  không gán set. Cần `SetBonusResolver` riêng (object-map.md §6.2), hệ hoàn toàn khác.
- Enhance/Reforge (+0→+15, plan §7.3) — `EquipmentService.TryEnhance` MVP hiện có (+10%/cấp flat
  trên MainStat) **không** áp dụng cho SubStats mới, không mở rộng lượt này.
- Trang bị rơi ở Boss/Elite — chỉ Treasure theo đúng plan §8.1.
- Bảng loot riêng theo từng chương (Chapter cụ thể thay vì wildcard `Chapter=0`) — như
  `task-loottable.md` đã ghi, việc content-design riêng.
- Convert `EquipmentDefinitionSO.Rarity` sang enum `Rarity` dùng chung — quyết định giữ nguyên
  (finding mục 0), không đụng 14 asset cũ.
- Vật phẩm tiêu hao (plan §7.5) — hệ hoàn toàn khác (item slot trong trận), không liên quan.

## 7. Test

- [x] `EquipmentGeneratorTests.cs` (`Assets/Tests/EditMode/Meta/`, đúng tên `T-META-EQGEN`
      object-map.md §8 kỳ vọng): số sub-stat đúng theo rarity (1/2/2/3/4); giá trị nằm trong đúng
      khoảng theo cột rarity (test thống kê nhiều lần roll); không trùng loại sub-stat trong 1
      item; `RollFrom` với `slot` cụ thể luôn trả đúng slot đó; `RollFrom` với slot không có def
      nào trong catalog giả → `null`, không throw; main stat lấy nguyên từ def, không bị random
      hoá; smoke test `Roll()` (catalog thật) cho cả 6 `EquipSlot`.
- [x] `LootRollerTests.cs` bổ sung: `EquipmentChance = 1` → `roll.Equipment != null` mọi lần;
      `EquipmentChance = 0` → luôn `null`; rarity roll ra luôn `≥ EquipmentMinRarity`; thêm
      `RealTreasureAsset_GuaranteesEquipmentAtOrAboveRare` (regression khoá cứng asset thật,
      cùng tinh thần `RealBossAsset_CoversEveryMaterialType...`).
- [x] Test wiring Meta→Combat: `EquipmentServiceModifierTests.cs` (mới, vì
      `EquipmentService` chưa từng có test EditMode riêng — đúng phát hiện task-ascend.md §6) —
      verify `GetEquipmentModifiers` convert đúng `SubStats` từ mọi slot đã trang bị thành
      `StatModifier`, bỏ qua `MainStat`, trả rỗng khi không trang bị gì / hero null. **Không** lặp
      lại việc test `EquipmentModifiers → Stats` ở tầng `CombatUnit.ComputeStats` — phần đó đã có
      sẵn 3 test trong `DamageCalculatorTests`/`SimulationTests` từ trước (dòng ~104/233/316, dùng
      trực tiếp `unit.EquipmentModifiers.Add(...)`), không phải code mới của task này.
- [x] Regression: test cũ liên quan Treasure (`PlaceholderLootTableTests`, `LootRollerTests` cũ)
      vẫn xanh — đường fallback không đổi.
- [x] Chạy full EditMode suite: **236/236 xanh** (222 trước lượt này + 14 test mới: 7
      `EquipmentGeneratorTests` + 3 `LootRollerTests` bổ sung + 4 `EquipmentServiceModifierTests`).

## 8. Verification

- **Không cần Play mode** — toàn bộ code động chạm (generator, loot roll, modifier conversion) là
  C# thuần dựa trên `IRandomSource`, đã verify trực tiếp qua `execute_code` (tương đương cách
  task-edgecases.md xác nhận Combat thuần không cần Play mode):
  roll 5 lần từ asset Treasure thật (`EquipmentChance=1`) → luôn ra trang bị, rarity ≥ Rare, sub-stat
  đúng số lượng/khoảng theo cột rarity (VD roll ra Mythic: `DefPct=16.8, CritPct=9.7, Res=15.4,
  CritDmgPct=16.1` — đều nằm trong cột Mythic plan §7.2); equip 1 item vừa roll cho hero giả qua
  `EquipmentService.Equip`-style gán trực tiếp `hero.Equipped[...]` rồi gọi `GetEquipmentModifiers`
  → ra đúng số `StatModifier` khớp `SubStats` của item.
- **Chưa verify qua UI thật** (click vào node Treasure trong Play mode xem Toast) — vì chưa cần
  thiết cho logic (đã verify bằng execute_code ở trên) và UI Toast dùng đúng pattern hiển thị đã
  có sẵn cho material/shard, không có logic mới cần Play mode riêng để bắt lỗi. Nếu muốn chắc chắn
  100% Toast hiển thị đúng, cần 1 lượt Play mode thủ công sau đó.
- `BalanceHarness` — không đụng tới lượt này (không nằm trong phạm vi mục 6); nếu cần ước lượng
  tỉ lệ rơi trang bị theo rarity, để lượt sau.

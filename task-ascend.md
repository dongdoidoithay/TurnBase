# TASK-ASCEND.md — Ascend "đúng chuẩn" + skill slot unlock

> Bổ sung cho `AscendSystem`/`SkillUpgradeSystem` đã có (chỉ dùng Gold). Mục tiêu lượt này:
> (1) Ascend tiêu đúng Mảnh hero + Essence/Core như plan.md §5.4, (2) mở khoá skill slot 4/Ultimate
> theo sao, (3) một nguồn thu thập vật liệu tối thiểu để test được — KHÔNG phải gacha/loot table
> đầy đủ (đó là hệ thống riêng, chưa làm).
>
> Liên quan: [plan.md §5.4](plan.md), [object-map.md §6.2](object-map.md), memory
> `project_backlog_2026-08-09.md`.

---

## 0. Phát hiện quan trọng trước khi làm

Kiểm tra lại `PlayerProfileDto.cs` thì **data model đã có sẵn**, chỉ chưa dùng — giống hệt tình
trạng `CombatUnit.Level`/`SkillRuntime.Level` trước đây:

```csharp
public class WalletDto
{
    public long Gold;
    public long Gem;
    ...
    /// <summary>Vật liệu: khoá là CurrencyType dạng số hoặc id vật liệu.</summary>
    public List<CurrencyEntryDto> Materials = new();
    /// <summary>Mảnh hero: khoá là defId của hero.</summary>
    public List<CurrencyEntryDto> HeroShards = new();
}
```

Nên **không cần đổi save schema** — chỉ cần: (a) mở rộng `IEconomyService` để đọc/ghi 2 list này,
(b) một nguồn cấp phát, (c) `AscendSystem` tiêu đúng.

---

## 1. Mở rộng `IEconomyService` — Materials + HeroShards

- [x] `Get(WalletDto, CurrencyType)` mở rộng: ngoài Gold/Gem, tra thêm trong `wallet.Materials`
      bằng khoá `type.ToString()` (VD `"EssenceI"`, `"Core"`).
- [x] `Grant(WalletDto, CurrencyType, long delta)` mở rộng tương tự — tìm hoặc thêm
      `CurrencyEntryDto` trong `Materials`, kẹp tại 0.
- [x] `TryConsume` tự động dùng chung `Get`/`Apply` nên KHÔNG cần sửa thêm nếu bước trên làm đúng.
- [x] Thêm 3 method mới cho Mảnh hero (khoá là `defId` string, không phải `CurrencyType`, nên
      không dùng chung signature được):
      `long GetShards(WalletDto, string heroDefId)`,
      `void GrantShards(WalletDto, string heroDefId, long delta)`,
      `bool TryConsumeShards(WalletDto, string heroDefId, long amount)`.
- [x] Helper private dùng chung để tìm/thêm entry trong 1 `List<CurrencyEntryDto>` theo khoá
      (dùng lại cho cả Materials và HeroShards, tránh lặp code).

## 2. Nguồn thu thập tối thiểu (KHÔNG phải loot table đầy đủ)

Chưa có `LootTableDefinition`/gacha — chỉ cắm tạm 2 điểm cấp phát vào code đã có sẵn, đủ để
Ascend test được thật, ghi rõ đây là placeholder chờ hệ thống loot thật (plan.md §8.1/§7):

- [x] **Node Treasure** (`MetaSceneInstaller.ResolveTreasure`): ngoài Gold hiện có, roll thêm —
      50% được 2-4 Essence I, 50% được 1 mảnh hero ngẫu nhiên (trong 6 hero đang sở hữu).
      Cập nhật `Toast()` báo đúng thứ nhận được.
- [x] **Thắng trận Boss** (`MetaSceneInstaller.ApplyPendingBattleResult`, khi
      `node.Type == NodeType.Boss`): cấp thêm 3 Essence II + 1 mảnh cho MỖI hero trong đội hình
      thắng trận (heroIds đã có trong `RunContext`/kết quả — kiểm tra field nào đang giữ danh
      sách hero ra trận trước khi code).
- [x] Không đụng tới Elite/Battle thường — giữ tối giản, tránh làm khó cân bằng thêm.

## 3. `AscendSystem` — tiêu đúng bảng chi phí plan.md §5.4

- [x] Đổi cấu trúc chi phí từ `long[]` (chỉ Gold) sang bảng đủ 3 thành phần mỗi bậc:

  | Từ→Đến | Mảnh | Vật liệu | Gold |
  |---|---|---|---|
  | ★1→★2 | 10 | 5 EssenceI | 5.000 |
  | ★2→★3 | 20 | 10 EssenceI | 15.000 |
  | ★3→★4 | 40 | 15 EssenceII | 40.000 |
  | ★4→★5 | 70 | 25 EssenceII + 5 Core | 100.000 |
  | ★5→★6 | 120 | 40 EssenceIII + 15 Core | 250.000 |

- [x] `TryAscend` phải là **giao dịch nguyên tử**: kiểm tra ĐỦ cả Gold + Mảnh + mọi Vật liệu
      TRƯỚC, chỉ trừ khi chắc chắn đủ hết (không được trừ Gold rồi mới phát hiện thiếu Mảnh).
- [x] `CostForNextStar` cũ (chỉ trả `long` Gold) phải đổi kiểu trả về hoặc thêm hàm mới trả đủ
      3 thành phần — `HeroDetailScreen` cần hiển thị đủ cả 3, không chỉ Gold.
- [x] Khi `hero.Star` đạt 6 (Ascend thành công lần cuối): set `hero.Awakened = true`. **KHÔNG**
      implement hiệu ứng passive thật (chưa có `PassiveProcessor` trong Combat) — chỉ bật cờ dữ
      liệu, ghi rõ comment đây là placeholder.

## 4. Unlock skill slot theo sao

- [x] `AscendSystem.IsSkillSlotUnlocked(int star, int slotIndex)`: slot 0/1/2 luôn mở; slot 3
      (skill C) cần ★≥4; slot 4 (Ultimate) cần ★≥5.
- [x] `BattleSceneInstaller.BuildUnitFromDefinition`: bỏ qua skill ở slot chưa mở khoá (cần
      truyền thêm `hero.Star` vào, giống cách đã truyền `hero.Level`/`SkillLevels`) — hero ra
      trận sẽ KHÔNG có access vào skill C/Ultimate nếu chưa đủ sao.
- [x] `SkillUpgradeSystem.CanUpgrade`: thêm điều kiện slot phải unlocked (không nâng cấp được
      skill chưa mở khoá dù đủ tiền).

## 5. UI — `HeroDetailScreen` / `UI_HeroDetail.prefab`

- [x] Skill row bị khoá: ẩn nút Upgrade, đổi `LevelLabel` thành `"🔒 Ascend to ★4"` (hoặc ★5 cho
      Ultimate) thay vì "Lv X/8".
- [x] `AscendButton`/label: hiện đủ 3 thành phần chi phí (VD `"★2 · 10 Shard · 5 EssenceI ·
      5000g"`) thay vì chỉ Gold — cần layout lại label cho đủ chỗ (có thể 2 dòng).
- [x] Cân nhắc thêm dòng hiển thị số Mảnh/Essence/Core hiện có của hero đó (để người chơi biết
      thiếu gì) — nếu không đủ chỗ trong panel hiện tại (620×420) thì bỏ qua, đã có label lỗi/disable đủ rõ.

## 6. Test

- [x] `IEconomyService` Materials/HeroShards: Grant/TryConsume/kẹp-tại-0, giống style
      `EconomyServiceTests` (nếu chưa có file test riêng cho EconomyService thì tạo luôn — hiện
      tại EconomyService MỚI CHỈ được test gián tiếp qua Play mode, chưa có EditMode test).
- [x] `AscendSystem`: đủ 3 thành phần mới thiếu 1 loại vẫn phải fail toàn bộ (không trừ phần đã đủ).
- [x] `AscendSystem`: `IsSkillSlotUnlocked` đúng bảng ★.
- [x] Cập nhật lại test cũ nếu cost signature đổi kiểu trả về (`SkillUpgradeAndAscendTests.cs`).
- [x] Chạy full EditMode suite, phải xanh 100%.

## 7. Gacha/Shop, Awakening thật, Balance Harness (lượt 2 — đã làm)

Đã triển khai đủ 4 phần con user chọn. Chi tiết implementation plan:
`~/.claude/plans/logical-pondering-wolf.md`. Vẫn CHƯA đụng: IAP/Battle Pass, Trial/Tháp Vô Tận,
Arena — ngoài phạm vi.

- [x] **PassiveProcessor + Awakening thật** — `Combat/Systems/PassiveProcessor.cs` (10 trigger,
      gọi trực tiếp từ `ActionResolver`/`PoiseSystem`/`CombatSimulation`, không qua
      CombatEventQueue vì hàng đó chỉ là log 1 chiều). `CombatUnit.PassiveModifiers` (list riêng,
      tách khỏi `EquipmentModifiers`). `AwakeningCatalog.cs` — 6 passive thật theo class/element
      từng hero (Molten Bulwark, Night Executioner, Absolute Zero, Radiant Ward, Windwalker's
      Gambit, Grave Pact) — V1, số liệu tạm chờ playtest. Verify Play mode: Ember Knight ★6 vào
      trận → DEF +15%/HP +10% đúng công thức (test qua execute_code, xem transcript).
- [x] **Gacha pity thật** — `Meta/Gacha/GachaSystem.cs`, đúng thuật toán plan.md §9.3 (soft pity
      Legendary từ lần 45, hard pity Legendary lần 60/Epic lần 10), dùng `IRandomSource` để test
      được. Trùng hero → cấp Mảnh (placeholder theo rarity). `GachaPityTests.cs` — 5M roll ±0.05%
      đúng bảng tỉ lệ (organic, chưa cộng pity) + test hard pity riêng. UI: `UI_Summon.prefab` +
      `SummonScreen.cs`, nút SUMMON mới trên TopBar (`Boot.unity`).
- [x] **Shop mua vật liệu bằng Gem** — node `NodeType.Shop` trên map giờ mở `ShopScreen`
      (`UI_Shop.prefab`), catalog 4 dòng cố định (Essence I/II/III, Core), giá TẠM chờ Balance
      Harness tinh chỉnh.
- [x] **Balance Harness** — `Assets/Tools/Balance/BalanceHarness.cs`, menu `Tools/Balance
      Harness/Gacha Pity Report` + `Material Drop Report`. Tách `PlaceholderLootTable.cs` khỏi
      `MetaSceneInstaller` (dùng `IRandomSource`) để harness gọi lại được với seed.

## 8. Cân bằng bằng Balance Harness (lượt 3 — đã chạy, đã vá 1 lỗ hổng)

Chạy `Tools/Balance Harness/Gacha Pity Report` + `Material Drop Report` thật (không phải chỉ viết
code) và dùng số liệu để sửa:

- **Phát hiện quan trọng:** Essence III và Core **không rơi ở đâu cả** trong bản gốc (Treasure chỉ
  ra Essence I, Boss chỉ ra Essence II) → ★4→★5 và ★5→★6 (chính bậc mở Awakening!) **không thể đạt
  được qua chơi thường**, chỉ có đường Shop nhưng Shop cần Gem mà Gem chưa có faucet nào trong
  game hiện tại. Test `PlaceholderLootTableTests.BossReward_CoversEveryMaterialType_...` giờ khoá
  chặt phát hiện này lại (fail nếu ai lỡ xoá nguồn cấp mà không thay thế).
- **Đã vá tối thiểu:** thêm `PlaceholderLootTable.BOSS_REWARD_CORE = 1` và
  `BOSS_REWARD_ESSENCE_III = 1`, cấp kèm mỗi lần thắng Boss (`MetaSceneInstaller.
  GrantBossAscendMaterials`). Sau khi vá, mọi bậc ★ đều hữu hạn:
  ★1→2 ~3.9 run (nút thắt Essence I) · ★2→3 ~7.8 run (Essence I) · ★3→4 ~6.2 run (Mảnh) ·
  ★4→5 ~10.9 run (Mảnh) · ★5→6 ~40 run (Essence III — chậm nhưng chấp nhận được cho bậc tối
  thượng, endgame vốn nên grind lâu).
- **Gacha:** tỉ lệ GỐC (không pity) khớp đúng bảng §9.3 trong ±0.05% (5 triệu roll,
  `GachaPityTests`). Tỉ lệ THỰC TẾ khi pity cộng dồn cao hơn hẳn base rate — Legendary ~2.77%
  (không phải 1.5%), Epic ~15.7% (không phải 12%) — **đây là thiết kế đúng của pity, không phải
  bug**, chỉ ghi lại để không ai nhầm so sánh trực tiếp với bảng gốc.
- Giá Shop, số mảnh gacha khi trùng hero, số liệu 6 Awakening passive (stack/lượt/%): CHƯA đổi —
  không có tín hiệu rõ ràng nào từ harness đòi phải chỉnh.

## 9. Gem faucet tối thiểu (lượt 4 — đã làm)

**Phát hiện:** Gem hoàn toàn không có nguồn nào trong game (plan.md §9.1 liệt "Quest, thành tựu,
chương mới, IAP" nhưng không hệ thống nào tồn tại) → Shop (mục C) và Gacha (mục B) chỉ dùng được
đúng 1 lần với 300 Gem khởi điểm rồi bế tắc vĩnh viễn, dù cả 2 hệ thống đã xây xong và verify hoạt
động đúng.

**Đã vá tối thiểu (LÚC ĐÓ):** `PlaceholderLootTable.BOSS_REWARD_GEM = 100`, cấp mỗi lần thắng Boss.

**⚠️ ĐÃ THAY THẾ (task-quest.md, lượt sau):** `BOSS_REWARD_GEM` đã bị XOÁ hoàn toàn khỏi
`PlaceholderLootTable`/`GrantBossAscendMaterials`. Gem giờ đến từ `QuestSystem` thật (3 Daily
Quest + 3 Achievement, `Assets/_Project/Scripts/Meta/Quest/`) — xem task-quest.md để biết chi
tiết. Đoạn mô tả bên dưới giữ lại làm lịch sử, KHÔNG còn đúng với code hiện tại.

## 10. Audit "code không gắn vào luồng game" (lượt 5 — đã rà, vá 1 chỗ)

Rà toàn bộ code mục 7-9 tìm field/method viết ra nhưng production không bao giờ gọi/đọc (chỉ có
test gọi). Kết quả:

- **`HeroInstanceDto.Awakened`** — chỉ được GHI (`AscendSystem.TryAscend`), chưa từng được ĐỌC ở
  production trước bản vá này (`BattleSceneInstaller` dùng `star >= MAX_STAR` thay vì đọc cờ này
  — tương đương logic nhưng khiến field mồ côi). Hệ quả thật: **người chơi không có cách nào biết
  hero đã Awakening** ngoài cảm nhận qua combat. **Đã vá:** `HeroDetailScreen.RefreshAscendButton`
  giờ đọc `_hero.Awakened` thật, hiện `"★6 · AWAKENED"` thay vì `"MAX STAR"` chung chung khi đã
  awaken. Verify Play mode: ép 1 hero ★6/Awakened=true → label đúng.
- **`CombatUnit.Passive`** (không phải `.Awakening`) — chưa từng được gán ở production. Đây KHÔNG
  phải sót của lượt này — field có sẵn từ trước, dành cho 1 hệ "passive bẩm sinh mỗi hero" khác
  hẳn Awakening, ngoài phạm vi task-ascend.md §7. `PassiveProcessor` đã xử lý field này đúng cách
  (generic cho cả `.Passive` lẫn `.Awakening`) nên khi hệ đó được xây sau này chỉ cần gán, không
  cần sửa gì thêm ở Combat.
- **`PassiveData.ExtraDamagePercent`** — đã biết từ mục 9, không phải phát hiện mới.
- **KHÔNG phải mồ côi (dễ hiểu nhầm):** 4/10 giá trị `PassiveTrigger` (`OnTurnStart`,
  `OnDamageTaken`, `OnHpBelowThreshold`, `OnBreakTriggered`) có hook thật, được gọi thật từ
  `ActionResolver`/`PoiseSystem`/`CombatSimulation` — chỉ là chưa Awakening nào trong 6 cái dùng
  tới. Đây là dư địa mở rộng có chủ đích (thêm hero/passive mới dùng ngay được), không phải bug.

## 11. Vẫn ngoài phạm vi

- `LootTableDefinition` đầy đủ theo plan.md §7.2/§8.1 — Treasure/Boss vẫn dùng
  `PlaceholderLootTable`, không phải bảng loot thật theo rarity/chương. Gem/Core/Essence III hiện
  là hằng số cố định trên Boss, chưa theo rarity/chương như thiết kế gốc.
- `ExtraDamagePercent` trong `PassiveData` khai báo nhưng chưa nối vào `DamageCalculator`.
- Quest/thành tựu thật để thay thế Gem faucet tạm ở mục 9.
- Hệ "passive bẩm sinh mỗi hero" (`CombatUnit.Passive`, khác Awakening) — chưa có nguồn dữ liệu
  nào gán field này, dù `PassiveProcessor` đã sẵn sàng xử lý.
- 16/24 combat edge-case test còn thiếu (plan.md §4.14).

# Task: Consumable items (vật phẩm tiêu hao)

Yêu cầu: hạng mục được chọn qua `AskUserQuestion` ("Consumable items (Recommended)") — đã nhường
2 lần trước (cho Codex, cho loot table), nay là gap lớn nhất còn lại có spec thật (plan.md §7.5).
Đây là task LỚN NHẤT session này (duy nhất chạm `Game.Combat`). Theo quy trình chuẩn: viết xong
task file này rồi mới chạm code. Nghiên cứu đã dùng 1 subagent Explore + đọc trực tiếp nhiều file
combat trước khi viết file này — mọi quyết định dưới đây đã verify bằng cách đọc code thật, không
đoán.

## §0. Findings

### 0.1. Spec (plan.md §7.5)

| Item | Hiệu ứng | Giá |
|---|---|---|
| Potion | Hồi 35% MaxHP 1 hero | 200 vàng |
| Ether | Hồi 40 SP | 300 vàng |
| Antidote | Cleanse mọi DoT 1 hero | 250 vàng |
| Smoke Bomb | Escape 100% | 500 vàng |
| Revive Feather | Hồi sinh 40% HP | 1.500 vàng |
| Elemental Bomb | 2.0× dmg hệ + −20 Poise, AoE | 800 vàng |

Mang tối đa **5 loại × 3 cái/trận**. Toàn bộ giá bằng **Vàng** (khác Shop hiện tại bán vật liệu
Ascend bằng **Gem**).

### 0.2. Data/hạ tầng có sẵn nhưng CHẾT hoàn toàn

- `InventoryDto.Items` (`List<CurrencyEntryDto>`) — 0 call site đọc/ghi ngoài khai báo +
  null-guard trong `SaveMigrationRunner`. Đây đúng là chỗ lưu số lượng item sở hữu.
- Battle HUD Skill Grid (`Assets/_Project/Scripts/UI/Battle/BattleHudScreen.cs`) đã CHỪA SẴN chỗ:
  `GRID_COLS=5`, nhưng `GRID_ROWS=1` (chỉ hàng 0 dùng) — comment tại chỗ:
  *"2 hàng ITEM/TACTIC theo plan.md §5.5 chưa có hệ thống đứng sau"*. Bật `GRID_ROWS=2` cho đúng
  5 slot item — khớp CHÍNH XÁC "5 loại" của §7.5, không cần tính toán kích thước mới.
- `ActionIntent` (`CombatSimulation.cs`) đã có tiền lệ field hành động đặc biệt ngoài skill-slot
  bình thường: `IsGuard`, `IsEscape` — xử lý riêng ở đầu `ExecuteIntent()` trước khi chạm
  `ActionResolver`. Thêm `IsUseItem` (bool) theo đúng khuôn này, **tái dùng field `SkillSlot` có
  sẵn làm "item index"** (không cần thêm field mới) — targetId vẫn dùng field `TargetId` có sẵn
  cho item cần target.
- `CombatSimulation.TryEscape()` đã có cơ chế thoát trận (tỉ lệ theo SPD chênh lệch,
  `State.AllowEscape` gate). Smoke Bomb chỉ cần bypass công thức tỉ lệ, GỌI THẲNG
  `Finish(BattleResult.Escaped)` — không cần sửa `TryEscape` hiện có.
- `IEconomyService` đã có tiền lệ y hệt cần thiết: `GetShards`/`GrantShards`/`TryConsumeShards`
  thao tác trên `wallet.HeroShards` (1 `List<CurrencyEntryDto>` riêng, key=string). Thêm 3 method
  song song `GetItemCount`/`GrantItem`/`TryConsumeItem` thao tác trên `InventoryDto.Items` — tái
  dùng ĐÚNG private helper `GetEntry`/`ApplyEntry` đã có, không viết logic key-value mới.

### 0.3. 3/6 item tái dùng được `ActionResolver.Execute` — 3/6 KHÔNG (verify bằng cách đọc code
    thật `ResolveHeal`/`ResolveSupport`, không giả định)

- **Ether → TÁI DÙNG được**: `ResolveSupport` (`ActionResolver.cs:282-311`) đã có sẵn nhánh
  `data.SpRestore > 0 → target.AddSp(data.SpRestore)` — **flat, không phụ thuộc stat người dùng**.
  Đồng nghĩa "Hồi 40 SP" của item khớp CHÍNH XÁC. Synthesize `SkillData{Type=Support,
  SpRestore=40}`, gọi `_resolver.Execute(...)`.
- **Revive Feather → TÁI DÙNG được**: `ResolveHeal`'s nhánh hồi sinh (`ActionResolver.cs:250-260`,
  `TargetMode.DeadAlly && RevivePercent>0`) tính `target.SetHp(target.MaxHp * data.RevivePercent)`
  — **dựa trên MaxHP CỦA TARGET**, không phải stat người dùng. Khớp "Hồi sinh 40% HP". Target mode
  `DeadAlly` đã tự auto-pick qua `TargetSelector.FirstRevivable` (ưu tiên ai downed lâu nhất) —
  ĐÚNG tinh thần "không có UI chọn target thủ công ở đâu trong game" (xem §0.4). Synthesize
  `SkillData{Type=Heal, Target=DeadAlly, RevivePercent=0.4f}`.
- **Elemental Bomb → TÁI DÙNG được, kể cả phần Poise**: `ApplyOneHit`
  (`ActionResolver.cs:187-188`) đã tự gọi `DamageCalculator.CalculatePoiseDamage` rồi
  `_poise.DamagePoise` cho MỌI hit đi qua `ResolveDamage` — nghĩa là chỉ cần synthesize
  `SkillData{Target=AllEnemies, IsAoe=true, PowerMultiplier=2f, PoiseDamage=20, Element=Neutral}`
  và gọi `_resolver.Execute`, phần "-20 Poise AoE" TỰ ĐỘNG áp dụng, không cần gọi
  `PoiseSystem.DamagePoise` riêng. (Damage vẫn nhân qua `attacker.Stats` như skill thường — chấp
  nhận được, "quả bom" hero mạnh ném ra vẫn mạnh hơn hero yếu, không phải bug, giữ nguyên tắc thiết
  kế "power scales with the wielder" nhất quán toàn game.)
- **Potion → KHÔNG tái dùng được `ActionResolver`**: `DamageCalculator.CalculateHeal`
  (`DamageCalculator.cs:121-128`) = `healer.Stats.AtkMag/AtkPhys * HealPower * levelMult` —
  **phụ thuộc stat NGƯỜI DÙNG skill**, hoàn toàn khác "Hồi 35% MaxHP" (phải dựa trên MaxHP của
  TARGET, độc lập ai dùng). Route qua `SkillType.Heal` thường sẽ cho kết quả sai/thất thường tuỳ
  hero nào cầm item. → Xử lý RIÊNG, không qua `_resolver.Execute`, viết trực tiếp trong
  `ItemResolver` mới (`target.SetHp(target.Hp + FloorToInt(target.MaxHp * 0.35f))`, cùng khuôn
  before/after diff đã dùng khắp `ActionResolver` — không có helper `Heal()` dùng chung sẵn, xem
  §0.2, nên viết inline theo đúng khuôn hiện có thay vì thêm API mới vào `CombatUnit`).
- **Antidote → KHÔNG tái dùng được `StatusProcessor.Cleanse`**: đọc thật `StatusInstance.cs`
  (bảng `StatusTable`, dòng 54-81) — **MỌI status nhóm Dot/Control/Debuff đều gắn
  `DispelType.Cleanse`** (Stun, Silence, AtkDown, Blind, Curse... không chỉ Burn/Poison/Bleed).
  Gọi thẳng `Cleanse(target, count)` sẽ xoá LUÔN CẢ stun/silence/debuff — vượt xa "cleanse mọi
  DoT" của spec, quá mạnh so với giá 250 vàng. → Thêm method MỚI, hẹp hơn:
  `StatusProcessor.CleanseGroup(CombatUnit target, StatusGroup group)` — xoá TOÀN BỘ status thuộc
  đúng 1 `StatusGroup` (dùng `group: StatusGroup.Dot` cho Antidote). Đây là bổ sung THUẦN CỘNG
  THÊM vào `StatusProcessor`, không đổi `Cleanse`/`Dispel` hiện có.
- **Smoke Bomb → KHÔNG qua `ActionResolver`**: gọi thẳng `Finish(BattleResult.Escaped)` trong
  `CombatSimulation`, không cần `SkillData` giả nào — đơn giản nhất trong 6 item.

### 0.4. KHÔNG có UI chọn target thủ công ở BẤT KỲ ĐÂU trong game (xác nhận qua Explore)

`BattleSceneInstaller.HandleSkillChosen` hard-code target = `FirstAliveEnemyId()` — hoàn toàn tự
động, không có target-picking mode/UI nào ở Battle HUD (không unit nào — sống hay chết — có
`Button`/click-target). Quyết định: **item CŨNG auto-target, không xây UI chọn target mới** — nhất
quán 100% với quy ước hiện tại, tránh 1 hạng mục UI lớn (click-to-target) ngoài phạm vi hẳn của
"thêm item". Auto-target cho từng loại: Potion = ally sống %HP thấp nhất; Antidote = ally đầu tiên
đang mang ≥1 status nhóm Dot (nút mờ nếu không ai đủ điều kiện — cùng khuôn `Interactable`-gating
của `SkillSlotView`); Revive Feather = `TargetSelector` tự lo qua `DeadAlly`; Ether/Elemental
Bomb/Smoke Bomb không cần chọn ai (Ether: ally SP thấp nhất; Bomb: AoE toàn bộ enemy; Smoke: cả
đội).

### 0.5. Shop hiện tại (`ShopScreen.cs`) hard-code Gem, không tổng quát

`CatalogItem` struct field tên thẳng `PriceGem` (không phải `CurrencyType PriceCurrency` tổng
quát), `Refresh()`/`Buy()` đọc cứng `CurrencyType.Gem`. `UI_Shop.prefab` có ĐÚNG 4 row khớp
`CATALOG.Length=4`. Quyết định: tổng quát hoá `PriceGem`→`Price` + thêm field
`CurrencyType PriceCurrency`, sửa `Buy`/`Refresh` đọc đúng currency của từng dòng (Gem cho 4 dòng
cũ, Gold cho 6 dòng item mới), thêm 6 row vào `UI_Shop.prefab` (10 tổng) — tái dùng đúng kỹ thuật
`open_prefab_stage` + `manage_gameobject duplicate` đã dùng ổn định 3 lần trong session
(NodeChoice/Mail/Codex).

### 0.6. Không xây UI chọn loadout trước trận

"5 loại × 3 cái/trận" nhưng TOÀN BỘ game chỉ có 6 loại item — "chọn 5 trong 6" gần như không có ý
nghĩa thực tế đủ lớn để đáng 1 màn hình riêng. Quyết định: **tự động mang** min(5, số loại đang sở
hữu) loại, mỗi loại tối đa 3, theo thứ tự cố định của catalog (Potion→Ether→Antidote→Smoke
Bomb→Revive Feather→Elemental Bomb) — không có màn chọn loadout thủ công. Ghi rõ đây là cắt phạm
vi cố ý, không phải thiếu sót.

## §1. Scope decision

**Trong phạm vi (checklist §2 chi tiết hoá từng bước):**
1. `Game.Data.ItemType` enum (6 giá trị) + `ItemCatalog` (Meta, hard-code — cùng khuôn
   `AwakeningCatalog`/`SetBonusCatalog`, không cần ScriptableObject cho 6 mục cố định): tên,
   giá Vàng, mô tả.
2. `IEconomyService`/`EconomyService.cs`: `GetItemCount`/`GrantItem`/`TryConsumeItem` (thao tác
   `InventoryDto.Items`, tái dùng `GetEntry`/`ApplyEntry` private helper có sẵn).
3. `ShopScreen.cs`: tổng quát `CatalogItem.PriceGem`→`Price`+`PriceCurrency`, thêm 6 dòng item.
   `UI_Shop.prefab`: thêm 6 row.
4. `StatusProcessor.cs`: `CleanseGroup(target, StatusGroup group)` — cộng thêm thuần tuý.
5. `Game.Combat.Systems.ItemResolver` MỚI (cùng vai trò `PoiseSystem`/`StatusProcessor` —
   1 resolver riêng cho item, được `CombatSimulation` sở hữu) — chứa 6 nhánh xử lý theo §0.3,
   nhận `BattleState`/`Events`/`StatusProcessor`/`ActionResolver` qua constructor.
6. `CombatSimulation.cs`: `ActionIntent.IsUseItem` (bool mới, tái dùng `SkillSlot`/`TargetId` có
   sẵn làm item-index/target); `BattleState.ItemLoadout` (`Dictionary<ItemType,int>` mới, đặt lúc
   spawn trận); nhánh `IsUseItem` trong `ExecuteIntent` gọi `_items.Use(...)`, giảm
   `ItemLoadout[type]`, `FinishTurn(0)` (item tốn trọn lượt, giống Guard/Escape).
7. `BattleSceneInstaller.cs`: đọc `PendingBattle.ItemLoadout` (field mới, tái dùng khuôn
   `SpecialMode`/`SpecialFloor` hiện có trên `PendingBattle`), set vào `Simulation.State`.
8. `RunContext.cs`: `QueueBattle`/`QueueSpecialBattle` thêm tham số `itemLoadout` (mặc định null
   = rỗng, KHÔNG phá lời gọi cũ nào — optional param cuối).
9. Nơi khởi tạo loadout: `MetaSceneInstaller.StartBattle`/tương đương — đọc
   `profile.Inventory.Items`, cắt theo §0.6, truyền vào `QueueBattle`.
10. Battle HUD: `GRID_ROWS=2` (bật hàng item có sẵn chỗ), `ItemSlotView` MỚI (sibling đơn giản
    hoá của `SkillSlotView` — icon + số lượng, không SP/cooldown/element), wire click →
    `OnItemChosen` → `SubmitPlayerAction` biến thể cho item (bỏ qua Action Command minigame —
    dùng item không qua timing, khớp cách Guard/Escape hiện tại cũng bỏ qua).
11. Sau trận: đồng bộ số item ĐÃ DÙNG THẬT (không phải đã MANG) trừ vĩnh viễn khỏi
    `profile.Inventory.Items` — qua `BattleOutcome`/`RunContext.ReportResult` (tái dùng khuôn
    `TotalPlayerDamage` đã có: 1 field long/dictionary mới trên `BattleOutcome`).
12. Test: `ItemResolverTests.cs` (Combat layer, EditMode) — từng item 1 test riêng (hiệu ứng đúng,
    biên: Antidote không có DoT thì không làm gì, Revive không target được ai còn sống, Elemental
    Bomb đánh trúng toàn bộ enemy còn sống). `EconomyServiceTests.cs` thêm case Item. Play-mode
    smoke check thật cho ít nhất Potion + Smoke Bomb (2 đại diện: có target vs không target).

**Ngoài phạm vi (cố ý, ghi rõ):**
- KHÔNG xây UI chọn loadout trước trận (auto-mang, xem §0.6).
- KHÔNG xây UI chọn target thủ công (auto-target mọi item, xem §0.4) — đây là cắt phạm vi LỚN
  NHẤT, tránh phải xây 1 hệ click-to-target hoàn toàn mới cho cả game (không chỉ riêng item).
- KHÔNG cho phép dùng item ở Meta (ngoài trận) — plan.md chỉ mô tả dùng "trong trận"
  (`ItemSlotBar` là UI Battle HUD), không có yêu cầu dùng item ngoài combat.
- KHÔNG thêm Action Command minigame cho thao tác dùng item — item resolve ngay khi bấm (giống
  Guard/Escape hiện tại, không qua `ActionCommandUI`).
- KHÔNG cho AI địch dùng item — chỉ phe Player mới có `ItemLoadout` (Enemy dùng skill/AI như cũ).

## §2. Implementation checklist

- [x] `Game.Data.ItemType` enum + `Game.Meta.Items.ItemCatalog` (giá/tên/mô tả 6 item, hard-code).
- [x] `IEconomyService`: `GetItemCount`/`GrantItem`/`TryConsumeItem`.
- [x] `StatusProcessor.CleanseGroup(target, StatusGroup)`.
- [x] `Game.Combat.Systems.ItemResolver`: 5 nhánh `UseX(...)` (Smoke Bomb xử lý riêng ở
      `CombatSimulation` vì cần gọi `Finish()`, `ItemResolver` không có quyền đổi `SimPhase`) +
      1 điểm vào `Use(ItemType, actor, rng)` trả `bool`. **Phát hiện quan trọng lúc implement**
      (đọc thật `DamageCalculator.CalculateHeal`/`ResolveSupport`/`StatusTable` trước khi code,
      không đoán): Potion KHÔNG tái dùng được `ActionResolver.Execute` (heal theo % MaxHP của
      TARGET, khác `CalculateHeal` vốn phụ thuộc stat người dùng) — xử lý trực tiếp. Antidote
      KHÔNG dùng được `StatusProcessor.Cleanse` có sẵn (nó xoá MỌI status `DispelType.Cleanse`,
      bao gồm cả Stun/Silence/Debuff — quá rộng so với "chỉ DoT") — thêm `CleanseGroup` mới, hẹp
      đúng `StatusGroup.Dot`. Ether/Revive Feather/Elemental Bomb khớp đúng cơ chế có sẵn
      (`SpRestore` flat, `RevivePercent` theo MaxHP target, `PoiseDamage` tự áp dụng qua
      `ApplyOneHit`) — Revive/Bomb tái dùng `_resolver.Execute` với `SkillData` tổng hợp (cùng mẫu
      `ActionResolver.BasicAttackFor` cho minion); Ether đơn giản tới mức không đáng route qua cả
      pipeline skill, xử lý trực tiếp luôn (đổi nhẹ so với kế hoạch §0.3 ban đầu).
- [x] `CombatSimulation.cs`: `ActionIntent.IsUseItem` (tái dùng field `SkillSlot` có sẵn làm
      item-index, không thêm field mới), `BattleState.ItemLoadout`/`ItemsUsed`, nhánh
      `ExecuteIntent`→`ExecuteUseItem` xử lý item (Smoke Bomb gọi thẳng `Finish` ở đây).
- [x] `RunContext.cs`: `PendingBattle.ItemLoadout`, `BattleOutcome.ItemsUsed`,
      `QueueBattle`/`QueueSpecialBattle`/`ReportResult` thêm tham số optional cuối — không phá lời
      gọi cũ nào (verify bằng full suite xanh ngay sau khi sửa).
- [x] `BattleSceneInstaller.cs`: đọc `_pending.ItemLoadout` → `Simulation.State.ItemLoadout`;
      `HandleContinue` truyền `Simulation.State.ItemsUsed` vào `ReportResult`.
- [x] `MetaSceneInstaller.cs`: `ComputeAutoItemLoadout()` (auto-mang tối đa 5 loại×3, theo thứ tự
      `ItemCatalog.ALL`) gọi ở CẢ 5 điểm `QueueBattle`/`QueueSpecialBattle` (node map + resume-
      snapshot + Dungeon + TrialBoss + Tower — không chỉ node map thường); `SyncConsumedItems()`
      gọi 1 chỗ duy nhất trong `ApplyPendingBattleResult` (áp dụng chung cho cả 2 nhánh
      thường/special vì đặt trước điểm rẽ nhánh).
- [x] `ShopScreen.cs` + `UI_Shop.prefab`: tổng quát `CatalogItem.PriceGem`→`Price`+
      `PriceCurrency`+`ItemToGrant`, thêm 6 dòng bán item bằng Vàng (10 dòng tổng). Prefab: mở
      rộng Panel 420→600 + RowListContainer 170→340 (đủ chỗ 10 row, verify bằng
      `GetWorldCorners()` không đè `CloseButton` — đúng bài học rút ra từ lỗi hand-math ở
      task-codex.md).
- [x] `BattleHudScreen.cs`: `GRID_ROWS=2` (hàng có sẵn chỗ chờ theo comment cũ "chưa có hệ thống
      đứng sau" — nay đã có), tách `BuildSkillGrid()` thành 2 vòng lặp riêng (hàng skill/hàng
      item), `RefreshItemSlots()`, `OnItemChosen` event.
- [x] `ItemSlotView.cs` MỚI (sibling đơn giản hoá `SkillSlotView` — không SP/cooldown/element,
      chỉ tên + số lượng).
- [x] Đồng bộ số item đã dùng thật về `profile.Inventory.Items` sau trận — verify THẬT bằng
      Play-mode (không chỉ tin logic): mang 3 Potion vào trận, dùng 1, sau trận
      `profile.Inventory.Items` giảm đúng 1 (không phải 3).
- [x] `ItemResolverTests.cs` (Combat, 10 test qua API công khai `CombatSimulation.SubmitIntent` —
      không tự dựng `ItemResolver` vì field nội bộ không public, cùng cách `SimulationTests` test
      Guard/Escape) — mỗi item ≥1 case đúng + 1 case biên (Antidote không có DoT, Revive không có
      ai để hồi sinh, Elemental Bomb không còn enemy sống).
- [x] `EconomyServiceTests.cs`: thêm 4 case `GetItemCount`/`GrantItem`/`TryConsumeItem`.
- [x] Chạy full EditMode suite — **402/402 xanh** (388 cũ + 10 ItemResolverTests + 4
      EconomyServiceTests), tất cả PASS ngay lần chạy đầu tiên sau khi thêm `Advance()` trước mỗi
      `SubmitIntent` (thiếu bước này ban đầu khiến intent bị âm thầm bỏ qua do
      `Phase != AwaitInput` — sửa xong trước khi chạy, không phải fix sau khi thấy fail).
- [x] Play-mode smoke check THẬT — toàn bộ chuỗi Meta→Combat→persistence, không chỉ 1 khâu:
      mua 3 Potion + 1 Ether + 1 Antidote + 1 Elemental Bomb qua `ShopScreen.Buy` thật (không
      bypass) → `LaunchBattle` thật → xác nhận `Simulation.State.ItemLoadout` đúng auto-loadout
      (Potion=3, Ether=1, Antidote=1, ElementalBomb=1 — capped đúng 3/loại) → `BattleHudScreen`
      thật hiện đúng 5 slot item, `Interactable` đúng theo sở hữu → dùng Potion qua
      `BattleSceneInstaller.HandleItemChosen` thật (không gọi thẳng `ItemResolver`) → ally máu 1
      hồi lên ĐÚNG 35% MaxHP tính tay → diệt hết enemy → `HandleContinue` thật → về Meta →
      `profile.Inventory.Items[Potion]` giảm đúng 1 (không phải 3 đã mang). **Sự cố lúc test, tự
      chẩn đoán và tránh lặp lại**: lần thử Play-mode ĐẦU TIÊN gọi `BuildBattle()`/`Start()` thủ
      công qua reflection khi Unity's `Start()` đã ÂM THẦM chạy rồi (frame-stall khiến khó biết
      chắc) → double-init (10 item slot thay vì 5, `NullReferenceException` khi `Advance()`) —
      không phải bug sản phẩm. Sửa bằng cách LUÔN kiểm tra field liên quan (`Simulation == null`/
      `_profile == null`) trước khi gọi lifecycle method thủ công, chỉ gọi đúng 1 lần khi chắc
      chắn `Start()` chưa chạy.
- [x] Cập nhật `roadmap.md` §0.1 (P4 §7.5) và `object-map.md` §12/§12.1.

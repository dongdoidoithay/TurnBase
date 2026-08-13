# Task: Event/Rest node redesign

Yêu cầu: hạng mục được chọn qua `AskUserQuestion` ("Event/Rest node redesign") trong số 3 gap có
thật còn lại (Event/Rest / Mail / per-chapter loot table). Theo quy trình chuẩn: viết xong task
file này rồi mới chạm code.

## §0. Findings

- **Hiện trạng thật (`MetaSceneInstaller.cs`)**: `ResolveRest` = auto full-heal (Toast text) + 50
  gold cố định, KHÔNG có lựa chọn nào. `ResolveEvent` = 1 roll `UnityEngine.Random.value < 0.6f` ẩn
  (60% +40..100 gold / 40% −20..−50 gold), người chơi bấm node là NHẬN NGAY kết quả, không hề thấy
  hay chọn gì cả. Cả 2 dùng `UnityEngine.Random` trực tiếp thay vì `_lootRng` (IRandomSource) đã có
  sẵn trong cùng file (dùng cho Treasure) — không đồng nhất, không test được xác định.
- **plan.md §8.1**: `Event` (12% node) = "2-3 lựa chọn có rủi ro". `Rest` (8% node) = "Hồi 30% HP
  **hoặc** +1 skill level" — 1 lựa chọn nhị phân. Không có spec chi tiết hơn cho nội dung từng lựa
  chọn Event — tự thiết kế trong phạm vi "rủi ro thật, có đánh đổi".
- **Phát hiện quan trọng làm lệch thiết kế gốc**: game này **KHÔNG có HP dai dẳng giữa các trận
  trên node map** — `HeroInstanceDto` không có field `CurrentHp`/tương đương nào (grep xác nhận),
  mọi trận battle luôn bắt đầu ở full HP tính từ stat. Ngoại lệ DUY NHẤT là Tháp Vô Tận (Tower) —
  nhưng đó là 1 `CombatSimulation` chạy liên tục xuyên nhiều tầng, khác hẳn cơ chế node map. Nghĩa
  là **"Hồi 30% HP" của Rest không có ý nghĩa thật trong kiến trúc hiện tại** — không có gì để hồi.
  Xây lại persistent-HP-giữa-các-trận-node-map là 1 thay đổi kinh tế/combat lớn, rủi ro cao, ngoài
  phạm vi 1 task "redesign Event/Rest". → Quyết định: thay "hồi HP" bằng phần thưởng Gold (vì HP đã
  ngầm định luôn đầy sẵn, y hệt cách diễn giải cũ), giữ đúng Ý NGHĨA lựa chọn ("an toàn, ít giá trị"
  vs "đầu tư vào sức mạnh lâu dài") mà không giả vờ có 1 cơ chế hồi máu không tồn tại.
- **`RunStateDto.TeamUids`** (dùng cho "đội hình đang active của run") **là field chết** — chỉ được
  gán `??= new List<string>()` trong `SaveMigrationRunner` lúc migrate save cũ, KHÔNG có chỗ nào
  khác đọc/ghi nó. Đội hình ra trận thực ra chọn MỚI mỗi lần qua `TeamSelectScreen.Open` (không có
  khái niệm "đội hình hiện tại của run" thật sự tồn tại). → Rest "Train" dùng toàn bộ
  `profile.Heroes` (mọi hero sở hữu) làm nguồn chọn ngẫu nhiên, không dùng `TeamUids`.
- **`CurrencyReason` enum** (có sẵn `EventNode = 41` — đúng ngay tên cho task này) **cũng là field
  chết** — không có `Grant`/`TryConsume` overload nào nhận `CurrencyReason`, chỉ tồn tại trong enum.
  Wiring 1 hệ thống lý do giao dịch/analytics đầy đủ là việc khác hẳn, ngoài phạm vi — KHÔNG đụng
  vào, chỉ ghi nhận ở đây để không ai tưởng đã có analytics.
- **`SkillUpgradeSystem.TryUpgrade`** đã có sẵn (tốn Gold, dùng cho màn Hero Detail) nhưng luôn tính
  phí — Rest "Train" cần lên cấp MIỄN PHÍ (đây là phần thưởng, không phải mua). Cần 1 method mới
  tách biệt, không đổi `TryUpgrade` hiện có (giữ hành vi cũ nguyên vẹn cho Hero Detail).
- **UI pattern tái dùng được**: `ShopScreen.cs` (+`UI_Shop.prefab`) đúng khuôn 1 modal N-row: `Open
  (profile, onClosed)` → `BuildShell()` 1 lần (Instantiate + Find) → `Refresh()` cập nhật text/
  interactable mỗi lần mở. `UI_Shop.prefab` hierarchy: `Panel > Title, WalletLabel, CloseButton
  (Label), RowListContainer > Row_0..3 (NameLabel, BuyButton > Label)`. Đủ để clone thành
  `UI_NodeChoice.prefab` (3 row tối đa cho Event, ẩn row 3 cho Rest 2-lựa-chọn), tái dùng
  `WalletLabel` làm dòng mô tả/flavor text VÀ dòng kết quả sau khi chọn (2 trạng thái hiển thị:
  đang chọn / đã chọn xong), `CloseButton` đổi ý nghĩa thành nút "Continue" (ẩn cho tới khi đã chọn
  xong 1 option — khác Shop, ở đây không có nút thoát trước khi chọn, đúng tinh thần "đã vào Event
  thì phải chọn 1 trong các rủi ro", không có đường lui).
- **Không có test coverage nào cho `ResolveRest`/`ResolveEvent`/`ResolveMystery` hiện tại** (chúng
  nằm trong `MetaSceneInstaller`, 1 MonoBehaviour lớn, không có unit test riêng — khớp nhận xét cũ
  trong object-map.md §12.1 rằng đây là 1 "God-object"). Logic MỚI (chọn lựa/roll rủi ro) sẽ được
  tách ra 1 class static thuần (`NodeChoiceSystem`, giống `DungeonSystem`/`TrialBossSystem`) để có
  thể unit test đầy đủ — `MetaSceneInstaller`/`NodeChoiceScreen` chỉ còn việc gọi + hiển thị.

## §1. Scope decision

**Trong phạm vi:**
1. `Meta/Dungeon/NodeChoiceSystem.cs` MỚI — pure static logic, tách khỏi UI (giống
   `DungeonSystem`), test được với `IRandomSource` seed cố định:
   - `RestOptions`/`EventOptions`: mảng label + flavor text hiển thị cho UI (không lộ xác suất/kết
     quả thật trước khi chọn — đúng tinh thần "rủi ro").
   - `IsRestTrainAvailable(profile)`: có ít nhất 1 hero sở hữu 1 skill slot unlock-được và chưa MAX
     — dùng để UI làm mờ nút "Train" nếu không hero nào đủ điều kiện (fallback về chỉ còn lựa chọn
     Gold, không throw, không hiện nút chết).
   - `ResolveRest(profile, optionIndex, economy, rng)` → `NodeChoiceResult` (option 0 = "Recover":
     Gold cố định 50 — giữ nguyên số cũ, không đổi balance; option 1 = "Train": +1 skill level MIỄN
     PHÍ cho 1 hero+slot ngẫu nhiên đủ điều kiện trong `profile.Heroes`).
   - `ResolveEvent(profile, optionIndex, economy, rng)` → `NodeChoiceResult` (3 lựa chọn, xem bảng
     dưới) — dùng `rng` truyền vào (từ `_lootRng` có sẵn ở `MetaSceneInstaller`, KHÔNG dùng
     `UnityEngine.Random` nữa, đồng nhất với `ResolveTreasure`).

   | # | Tên | Cơ chế |
   |---|---|---|
   | 0 | Play it safe | 100%: +30 gold |
   | 1 | Take a chance | 50%: +150 gold · 50%: −50 gold (kẹp sàn 0, không âm ví) |
   | 2 | All in | 25%: +1 trang bị Rare+ ngẫu nhiên (`EquipmentGenerator.Roll(null, Rare, rng)`) ·
       75%: −80 gold (kẹp sàn 0) |

2. `Meta/Hero/SkillUpgradeSystem.cs` — thêm `GrantFreeLevel(HeroInstanceDto hero, int skillSlot)`
   (tái dùng `CanUpgrade` để check điều kiện, KHÔNG đụng tới Gold/`TryUpgrade` hiện có).
3. `Meta/NodeChoiceScreen.cs` MỚI (mirror `ShopScreen.cs`) + `UI_NodeChoice.prefab` MỚI (clone từ
   `UI_Shop.prefab`, sửa row count 4→3, đổi ý nghĩa `WalletLabel`/`CloseButton`).
4. `MetaSceneInstaller.cs`: `ResolveRest`/`ResolveEvent` đổi từ auto-resolve sang mở
   `NodeChoiceScreen` (giống `ResolveShop` đã mở `ShopScreen` — chỉ `MarkVisitedAndUnlock`/Save/
   Refresh SAU khi đóng modal, không phải ngay khi bấm node). Xoá `ResolveMystery`'s nhánh gọi
   `ResolveEvent`/`ResolveRest` cũ — vẫn phải hoạt động được khi Mystery ngẫu nhiên trúng Event/Rest
   (mở modal luôn, không tự resolve hộ).
5. Test (`Assets/Tests/EditMode/Meta/`): `NodeChoiceSystemTests.cs` MỚI — cả 2 lựa chọn Rest, cả 3
   lựa chọn Event, biên (không hero đủ điều kiện Train, ví không đủ để trừ khi thua cược không áp
   dụng vì Event chỉ CỘNG/TRỪ chứ không "tốn phí trước" — xem thiết kế trên, không cần check số dư
   trước khi risk).

**Ngoài phạm vi (cố ý, ghi rõ):**
- KHÔNG xây persistent-HP-giữa-các-trận-node-map — lý do kiến trúc ở §0, việc quá lớn so với 1
  task redesign Event/Rest.
- KHÔNG động vào `CurrencyReason`/analytics — field chết không liên quan trực tiếp.
- KHÔNG đổi `RunStateDto.TeamUids` thành field sống — không cần thiết cho task này (dùng
  `profile.Heroes` trực tiếp).
- KHÔNG thêm UI chọn TAY hero/skill nào được +1 level ở Rest "Train" — chọn ngẫu nhiên tự động,
  giữ modal đơn giản (không cần thêm 1 màn chọn hero lồng bên trong modal chọn lựa).

## §2. Implementation checklist

- [x] `NodeChoiceSystem.cs` (`Meta/Dungeon/`): struct `NodeChoiceOption`/`NodeChoiceResult`,
      `RestOptions`/`EventOptions` static arrays, `IsRestTrainAvailable`, `ResolveRest`,
      `ResolveEvent`.
- [x] `SkillUpgradeSystem.GrantFreeLevel(hero, skillSlot)`.
- [x] `UI_NodeChoice.prefab` — clone `UI_Shop.prefab` (copy file + đổi `m_Name`), xoá Row_3 qua
      `manage_prefabs modify_contents delete_child`, giữ nguyên tên GameObject
      WalletLabel/CloseButton/BuyButton (không rename — repurpose bằng comment trong code, tránh
      thao tác rename rủi ro trong prefab YAML).
- [x] `NodeChoiceScreen.cs` (`Meta/`) — 2 trạng thái hiển thị (đang chọn / đã chọn), `Open(profile,
      isRest, economy, rng, onClosed)`.
- [x] `MetaSceneInstaller.cs`: sửa `ResolveRest`/`ResolveEvent` mở `NodeChoiceScreen` thay vì auto-
      resolve; thêm field `_nodeChoiceScreen` giống `_shopScreen`. `ResolveMystery` không cần sửa —
      nó chỉ gọi `ResolveEvent(node)`/`ResolveRest(node)`, đã tự hoạt động đúng qua modal mới.
- [x] `NodeChoiceSystemTests.cs` (11 test): Rest option 0/1 (kể cả no-eligible-hero + all-maxed
      fallback), `IsRestTrainAvailable`, Event option 0/1/2 (cả 2 nhánh xác suất mỗi option qua
      `FixedRandom` cục bộ — cùng mẫu `GachaPityTests.FixedRandom`), kẹp sàn gold không âm + verify
      `ResultText` phản ánh đúng số THẬT bị mất (không phải số danh nghĩa khi bị kẹp).
- [x] Chạy full EditMode suite — **373/373 xanh** (362 cũ + 11 test mới), không test nào vỡ.
- [x] Play-mode smoke check THẬT (không chỉ execute_code giả lập logic — thao tác qua UI thật):
      vào Play mode, ép 1 node Rest thật trên map thành Available, gọi `OnNodeClicked` qua
      reflection (mô phỏng bấm), verify modal mở đúng 2 row (Recover/Train), bấm nút Recover qua
      `button.onClick.Invoke()` → Gold +50 đúng số, mô tả đổi thành kết quả, nút Continue hiện;
      bấm Continue → modal đóng, `node.State` = Visited (2). Lặp lại cho Event: map hiện tại
      (chương 11, seed hiện tại) tình cờ KHÔNG roll ra node Event nào trong 14 node (12% tỉ lệ,
      hợp lý) — dùng 1 `MapNodeDto` tổng hợp (Type=Event, không nằm trong `Run.MapNodes`) để test
      riêng nhánh Event, vẫn qua đúng `OnNodeClicked` thật; verify đủ 3 row hiện (Play it
      safe/Take a chance/All in), bấm "Take a chance" → trúng nhánh thua thật (-50 gold), Continue
      đóng modal đúng. Cả 2 luồng chạy đúng, không lỗi console.
- [x] Cập nhật `roadmap.md` §0.1 (dòng P5: "Event/Rest node còn nông" → xong, ghi rõ giới hạn không
      có persistent HP) và `object-map.md` §12/§12.1.

# Task: Addressables pilot (HeroDefinitionSO single-lookup)

Yêu cầu: người dùng chọn làm cả 3 hạng mục lớn (Addressables/Localization/Animation) — thực hiện
lần lượt, KHÔNG gộp 1 lượt. Addressables trước. Sau khi khảo sát thật (grep 23 file/~30 chỗ gọi
`Resources.Load`/`LoadAll`), xác nhận qua `AskUserQuestion` đây là thay đổi rủi ro cao nhất session
này — người dùng chọn "Pilot nhỏ trước" thay vì quét hết 23 file 1 lượt. Việc lớn, breaking-change
tiềm ẩn — viết xong task file này rồi mới chạm code.

## §0. Findings

- **Addressables CHƯA được cài trong project** — grep `Packages/manifest.json`/`packages-lock.json`
  ra 0 kết quả cho "addressable". Cần cài package `com.unity.addressables` trước tiên (qua
  `manage_packages add_package`, không tự sửa tay `manifest.json` để Package Manager tự resolve
  đúng version/dependency cho Unity 6000.5.2f1).
- **Quy mô thật nếu làm hết 1 lượt**: 23 file thật, ~30 chỗ gọi `Resources.Load`/`LoadAll` — trải
  khắp `Game.Meta` (12+ screen), `Game.CombatView` (`BattleSceneInstaller`, `VfxPlayer`),
  `Game.Services.Audio`. Nhiều loại asset khác nhau: prefab UI, sprite (hero/enemy/skill icon/VFX),
  ScriptableObject catalog (Hero/Enemy/Skill/Equipment/LootTable), AudioClip.
- **Addressables API vốn dĩ BẤT ĐỒNG BỘ** (`Addressables.LoadAssetAsync<T>(key)` trả
  `AsyncOperationHandle<T>`) — toàn bộ 23 file hiện tại gọi ĐỒNG BỘ (`Resources.Load` trả kết quả
  ngay). Codebase này KHÔNG dùng `async`/`await` ở tầng Meta/CombatView (chỉ
  `GameBootstrap.Awake()` có `async void` cho load save) — refactor sang bất đồng bộ thật cho cả 23
  file sẽ lan ra MỌI caller chain của chúng (VD `TeamSelectScreen.Open()` → `RefreshHeroList()` →
  ... đều phải thành async), phạm vi lan rộng không kiểm soát được trong 1 lượt.
- **Quyết định cho pilot**: dùng `Addressables.LoadAssetAsync<T>(key).WaitForCompletion()` — API
  CHÍNH THỨC Unity cung cấp riêng cho tình huống "migrate code đồng bộ sang Addressables mà chưa
  muốn/chưa thể tái cấu trúc bất đồng bộ ngay" (đợi đồng bộ, block caller y hệt `Resources.Load` cũ
  — hành vi bên ngoài KHÔNG đổi với caller, chỉ đổi ĐƯỜNG lấy asset bên trong). Đánh đổi: block
  main thread trong lúc chờ — chấp nhận được vì asset local-only (build cùng app, không qua CDN từ
  xa), cùng chi phí thật như `Resources.Load` vốn cũng block main thread.
- **Phạm vi pilot hẹp hơn nữa cả trong nhóm HeroDefinitionSO**: có 5 chỗ gọi cho riêng
  `HeroDefinitionSO`, chia 2 loại:
  1. **Lookup đơn theo defId** (3 chỗ, CÙNG 1 khuôn `Resources.Load<HeroDefinitionSO>($"Data/
     Heroes/{defId}")`): `TeamSelectScreen.cs:362`, `HeroDetailScreen.cs:141`,
     `BattleSceneInstaller.cs:337` (chỗ NÓNG NHẤT — chạy mỗi khi spawn 1 hero vào trận, mọi trận
     đều qua đây).
  2. **Load toàn bộ catalog** (2 chỗ, `Resources.LoadAll<HeroDefinitionSO>("Data/Heroes")`):
     `CodexSystem.cs:19`, `GachaSystem.cs:147` — cần kỹ thuật KHÁC (gắn Label cho cả 24 asset rồi
     `Addressables.LoadAssetsAsync<T>(label)` trả về LIST, không phải 1 key đơn) — phức tạp hơn hẳn
     nhóm (1), và `GachaSystem` đụng tới hệ gacha (nhạy cảm), `CodexSystem` vừa mới xây trong chính
     session này (item 13) — **CỐ Ý ĐỂ NGOÀI PHẠM VI PILOT**, chỉ làm nhóm (1) — 3 file, cùng 1
     khuôn code, thấp rủi ro nhất, đã đủ để verify trọn vẹn luồng "cài package → đánh dấu asset
     Addressable → gán address → đổi API gọi → asset load ra ĐÚNG y hệt trước" trước khi mở rộng.
- **24 asset `HeroDefinitionSO`** tại `Assets/_Project/Resources/Data/Heroes/*.asset` — sẽ đánh dấu
  Addressable với `address` = ĐÚNG chuỗi path cũ (`Data/Heroes/{defId}`, VD
  `Data/Heroes/hero_ember_knight`) — key lookup trong code KHÔNG đổi, chỉ đổi API gọi, giảm rủi ro
  gõ sai chuỗi.
- **Không cần gọi `Addressables.Release()`** cho các asset catalog dùng suốt đời app (giống
  `Resources.Load` chưa từng "release" gì) — `HeroDefinitionSO` là dữ liệu tĩnh đọc xuyên suốt
  game, giữ loaded vĩnh viễn là đúng ý định gốc, không phải leak.
- **Asset vẫn ở nguyên `Assets/_Project/Resources/Data/Heroes/`** — Addressables cho phép asset
  trong `Resources/` được đánh dấu Addressable (Unity tự cảnh báo nhưng vẫn hoạt động); di dời khỏi
  `Resources/` (khuyến nghị chính thức lâu dài) là việc RIÊNG, ngoài phạm vi pilot này (asset vẫn
  cần load được qua `Resources.Load` bình thường cho 21 file/25 chỗ CÒN LẠI chưa migrate, di dời sẽ
  phá những chỗ đó).

## §1. Scope decision

**Trong phạm vi:**
1. Cài `com.unity.addressables` qua `manage_packages`.
2. Khởi tạo `AddressableAssetSettings` mặc định (tự tạo qua API lúc truy cập lần đầu).
3. Đánh dấu Addressable + gán address cho 24 asset `HeroDefinitionSO` (address = path cũ).
4. Đổi 3 file (nhóm lookup đơn): `TeamSelectScreen.cs`, `HeroDetailScreen.cs`,
   `BattleSceneInstaller.cs` — `Resources.Load<HeroDefinitionSO>(key)` →
   `Addressables.LoadAssetAsync<HeroDefinitionSO>(key).WaitForCompletion()`.
5. Verify: build/compile sạch, EditMode suite xanh, Play-mode thật xác nhận hero data load đúng
   (TeamSelect hiện đúng tên/rarity, HeroDetail mở đúng, VÀ QUAN TRỌNG NHẤT — 1 trận thật spawn
   đúng hero với đúng stat, vì `BattleSceneInstaller` là chỗ nóng nhất).

**Ngoài phạm vi (cố ý, ghi rõ):**
- KHÔNG đụng `CodexSystem.cs`/`GachaSystem.cs` (nhóm LoadAll+Label, kỹ thuật khác, nhạy cảm hơn).
- KHÔNG đụng `EnemyDefinitionSO`/`SkillDefinitionSO`/`EquipmentDefinitionSO`/`LootTableDefinitionSO`
  (dữ liệu khác, dù có thể tái dùng đúng mẫu vừa làm ở pilot này cho lượt sau).
- KHÔNG đụng sprite/prefab/AudioClip (loại asset khác, cách đánh dấu Addressable khác).
- KHÔNG refactor bất đồng bộ thật (giữ `WaitForCompletion()`, lý do đã ghi ở §0).
- KHÔNG di dời asset ra khỏi `Resources/`.

## §2. Implementation checklist

- [x] Cài `com.unity.addressables` qua `manage_packages add_package` — version `4.0.1` resolve tự
      động (khớp Unity 6000.5.2f1). Package tự tạo `Assets/AddressableAssetsData/` + kéo theo domain
      reload (bridge MCP tự resume, không phải lỗi).
- [x] `refresh_unity` xác nhận package resolve xong, không lỗi.
- [x] Khởi tạo `AddressableAssetSettings` qua `AddressableAssetSettingsDefaultObject.GetSettings
      (create: true)` trong `execute_code` — tự tạo settings + Default Local Group.
- [x] Đánh dấu 24 asset `HeroDefinitionSO` Addressable qua `execute_code`
      (`AddressableAssetSettings.CreateOrMoveEntry` cho từng GUID), gán `entry.address` = đúng path
      cũ (`Data/Heroes/{defId}`). Verify load thật ngay trong Editor (không cần Play mode) qua
      `Addressables.LoadAssetAsync<HeroDefinitionSO>(key).WaitForCompletion()` — trả đúng
      `hero_ember_knight`.
- [x] Sửa 3 file: `TeamSelectScreen.cs` (`FindHeroDef`), `HeroDetailScreen.cs`,
      `BattleSceneInstaller.cs` (chỗ nóng nhất — spawn hero vào trận).
- [x] **Phát hiện lúc build**: cần thêm CẢ `Unity.Addressables` LẪN `Unity.ResourceManager` vào
      `references` của `Game.Meta.asmdef`/`Game.CombatView.asmdef` — thiếu `Unity.ResourceManager`
      (chứa `AsyncOperationHandle<T>`, kiểu trả về của `LoadAssetAsync`) gây lỗi CS0012 dù đã có
      `Unity.Addressables`; asmdef không tự kéo theo dependency bắc cầu.
- [x] `refresh_unity` compile sạch (2 lần, sau mỗi đợt sửa asmdef).
- [x] Chạy full EditMode suite — **416/416 xanh** (415 cũ + 1 test MỚI KHÔNG PHẢI CỦA TA —
      `AddressableAssets.DocExampleCode.TestStub.RequiredTest` tự kéo theo từ chính package
      Addressables cài vào, vô hại, không phải regression).
- [x] Verify Play-mode THẬT — gặp lại MCP frame-stall ngay từ đầu, áp dụng "check-before-force"
      (ép tay `MetaSceneInstaller.Start()`): mở `TeamSelectScreen` thật → 24 hero card hiện ĐÚNG tên
      + màu rarity thật ("Ember Knight" viền xanh Rare, "Frost Sage" viền tím Epic...) — xác nhận
      `FindHeroDef` qua Addressables trả đúng dữ liệu. Sau đó `LaunchBattle` thật (node Battle thật,
      4 hero thật) → ép tay `BattleSceneInstaller.Start()` → **7 unit spawn đúng, HP THẬT khớp
      CHÍNH XÁC với số liệu đã ghi lại trước đó ở task-damage-meter.md cùng phiên này** (hero_ember_
      knight=688, hero_shadow_fang=456, hero_frost_sage=324, hero_dawn_cleric=369) — bằng chứng
      trước/sau mạnh nhất có thể: cùng 1 profile, cùng 4 hero, HP giống hệt dù đường load đã đổi từ
      `Resources.Load` sang `Addressables.LoadAssetAsync(...).WaitForCompletion()` — xác nhận
      KHÔNG có sai lệch dữ liệu. `HeroDetailScreen` dùng ĐÚNG 1 dòng code mẫu đã verify ở 2 chỗ kia,
      không lặp lại kiểm tra thừa.
- [x] Cập nhật `roadmap.md §0.1`, `object-map.md §12.1`.

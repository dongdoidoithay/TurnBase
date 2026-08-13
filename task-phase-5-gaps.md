# Task: 5 Đầu mục còn thiếu theo Phase (Validate Object Map · Tutorial · Reforge · Localization · Responsive)

Task gộp 5 đầu mục đã khảo sát, chia theo phase của roadmap.md. Mỗi đầu mục là 1 phần
độc lập, làm tuần tự, mỗi phần có checklist + DoD riêng (đúng quy ước `task-*.md` hiện có).

| # | Đầu mục | Phase | Trạng thái nguồn | Ưu tiên |
|---|---------|-------|------------------|---------|
| A | Tools/Validate Object Map | P0 | **✅ XONG 2026-08-12 — 524/524 xanh** | Cao (P0 nợ) |
| B | Tutorial 5 bước | P2 | **✅ XONG 2026-08-12 — 564/564 xanh** | Cao (QG2) |
| C | Reforge sub-stat | P4 | **✅ XONG 2026-08-12 — 524/524 xanh** | Trung bình |
| D | Localization mở rộng | P5 | **✅ CỐT LÕI XONG 2026-08-12 — 571/571 xanh** (item 3 xong; item 4 "nhãn hard-code khác" vẫn để dành lượt sau, xem §D4) | Trung bình |
| E | LayoutProfileSwitcher + Landscape | P6 | **✅ XONG 2026-08-12 — 547/547 xanh** | Cao (mở P6) |

---

# PHẦN A — Tools/Validate Object Map (P0)

Yêu cầu: Editor tool quét 3 scene (Boot, Meta, Battle) + toàn bộ prefab, đối chiếu với
bảng §3/§4 object-map.md, báo script/prefab chưa đăng ký hoặc đăng ký nhưng không tồn tại.
docs §11: bắt buộc chạy trước mỗi merge, cập nhật object-map.md cùng commit.

## §A0. Findings

- Chưa có tool này. `Assets/Tools/` chỉ có `Balance/BalanceHarness.cs` +
  `DataImport/{CsvSchema,CsvToScriptableObject,CsvReader,DataValidator}.cs`.
- Asmdef `Assets/Tools/Game.Tools.asmdef` đã tồn tại (Editor-only, references
  `Game.Core,Game.Data,Game.Combat,Game.CombatView,Game.Meta,Game.UI,Game.Services,Game.Bootstrap`)
  → KHÔNG tạo asmdef mới, đặt tool trong cùng `Game.Tools`.
- Style chuẩn: `DataValidator.cs` / `BalanceHarness.cs` — `[MenuItem]` static class,
  log Console có đếm `errors`/`warnings`, `Debug.LogError` khi lỗi, không block merge.
- Khảo sát hiện trạng: 13 màn Meta screen, 12 prefab `Resources/Prefabs/UI/Screens/UI_*`,
  3 scene, ~28 script màn hình → đây chính là "sự thật" tool phải đối chiếu với docs §3/§4.
- Nguồn registry chuẩn: đọc trực tiếp `object-map.md` (giải nén bảng §3/§4) thay vì
  hardcode — docs là nguồn sự thật duy nhất, tránh lệch 2 nơi.

## §A1. Scope

**Trong phạm vi:**
1. `Assets/Tools/ObjectMap/ObjectMapValidator.cs` — `[MenuItem("Tools/Validate Object Map")]`:
   parse `object-map.md` bảng §3 (scene: GameObject → script) + §4 (prefab → script)
2. Quét 3 scene qua `EditorSceneManager.OpenScene` (load không dirty) + `AssetDatabase`
   cho prefab (12 prefab UI + mọi prefab khác), thu tập `(path, script type)`
3. Đối chiếu 3 chiều: (a) script trong docs không tồn tại · (b) script thực tế chưa đăng ký
   docs · (c) prefab path trong docs không có asset
4. Output Console + optional `[MenuItem("Tools/Object Map/Generate Report")]` ghi
   `object-map-validation.md` để commit
5. `roadmap.md` P0: tick nợ "Tools/Validate Object Map chưa tồn tại"

**Ngoài phạm vi:**
- Tự sửa docs khi lệch (tool chỉ báo cáo; con người quyết định — đúng triết lý DataValidator)

## §A2. Design

- Parser bảng markdown: cột đầu chứa `GO-*`/`UI-*`/code; dòng giữa là GameObject path hoặc
  prefab path; cột script liệt kê MonoBehaviours. Giới hạn regex theo cấu trúc docs §3/§4
  hiện tại (không tự ý tái cấu trúc docs).
- Quét scene: `AssetDatabase.LoadAssetAtPath<SceneAsset>` → mở read-only bằng
  `EditorSceneManager.OpenScene(path, OpenSceneMode.Single)` rồi đóng ngay, thu
  `FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)`
  → lấy `script.GetClass()` + `AssetDatabase.GetAssetPath(script)`. Đóng scene để không
  dirty trạng thái đang mở của dev.
- So sánh bằng `Script` asset path (chuẩn duy nhất), không so tên class.

## §A3. Implementation Checklist

- [x] Viết file này
- [x] `ObjectMapValidator.cs` — parser object-map.md (§3/§4, tổng quát theo cột "Script"/"Prefab",
      cũng vô tình bắt luôn §6.1-6.3 vì cùng tên cột — vô hại, chỉ mở rộng tập "đã biết")
- [x] `ObjectMapValidator.cs` — scanner scenes (Boot/Meta/Battle) + prefabs (đọc text YAML thô,
      regex GUID `m_Script` → `AssetDatabase.GUIDToAssetPath`, KHÔNG mở scene trong Editor — an
      toàn với phiên Editor sống của người dùng, tránh MCP frame-stall)
- [x] `ObjectMapValidator.cs` — 3 chiều đối chiếu + log Console (style DataValidator) — lọc built-in
      Unity/package (Button/Image/Volume...) khỏi chiều (b) vì docs không có ý định tracking chúng
- [x] Menu `Tools/Object Map/Generate Report` → ghi `object-map-validation.md`
- [x] Chạy menu thật (qua `execute_menu_item` + `execute_code` gọi trực tiếp `Validate()`, không
      exception cả 2 đường), đọc report, đối chiếu kết quả với docs hiện tại
- [x] Cập nhật `roadmap.md §0.1` P0 (tick nợ Validate Object Map) + `object-map.md` §11/§12

## §A-DoD
Chạy 2 menu không crash; báo cáo phân biệt đủ 3 loại; kết quả khớp hiện trạng project
(3 scene / 14 prefab thật quét được); không đổi code gameplay; 0 ảnh hưởng test — **524/524 xanh**.

**Kết quả lần chạy đầu (2026-08-12):** 88 script trong §3–§9 chưa tồn tại file (đúng dự đoán §12.1 —
các bảng đó là kiến trúc ĐÍCH, phần lớn chưa xây, KHÔNG phải lỗi cần sửa ngay), 0 script thật gắn
tĩnh trong scene/prefab chưa đăng ký docs (sau khi lọc built-in Unity/package), 34 tên prefab trong
docs chưa có asset khớp. **Giới hạn đã biết:** script gắn qua `AddComponent<T>()` lúc runtime (đa số
màn Meta code-dựng — `TeamSelectScreen`, `ShopScreen`, `SummonScreen`...) KHÔNG serialize vào scene/
prefab nên tool KHÔNG quét thấy — chỉ bắt bề mặt tĩnh, ghi rõ trong doc-comment code. Đầy đủ trong
`object-map-validation.md` (file report, commit cùng lượt này).

---

# PHẦN B — Tutorial 5 bước (P2, QG2)

Yêu cầu: dạy 5 cơ chế trong trận thật lần đầu chơi — ① chọn skill ② Action Command
③ hệ khắc chế (element) ④ Break ⑤ Ultimate. Mảnh lớn nhất còn thiếu để đạt QG2.

## §B0. Findings

- `PlayerProfileDto.ProgressDto.TutorialCompleted` (bool) ĐÃ có sẵn — hook sẵn, chưa ai dùng.
- `BattleHudScreen` đã có đủ event: `OnSkillChosen, OnItemChosen, OnEndTurnPressed,
  OnAutoToggled, OnSpeedChanged, OnGuardPressed, OnEscapePressed, OnSwapRowPressed,
  OnFocusPressed` (+ `OnAnalyzePressed`). `BattleSceneInstaller.cs:244-260` đã bind hết
  → tutorial chỉ cần thêm 1 controller nữa cùng khuôn, không sửa engine.
- `ActionCommandUI.Open(type, beats, Action<CommandGrade> onResult)` — sẵn callback
  `CommandGrade` (Perfect/Good/Miss) → hook bước ②.
- `CombatPresenter` có `Setup(queue, floating, vfx, cam)`, `PlayPending()`, `IsPlaying` —
  không có event riêng; tutorial nên đọc `CombatEventQueue`/State giống presenter, hoặc
  mượn event BattleHud + `OnCommandResolved` mới thêm ở BattleSceneInstaller.
- Ngưỡng Break/poise và element đều nằm trong `Game.Combat` (đã có test) — tutorial chỉ
  cần *phát hiện sự kiện*, không tính lại.
- Meta→Battle: `RunContext.PendingBattle` — có thể thêm flag `IsTutorial` theo sau
  `Formation` (cùng pattern Meta→Battle bridge đã có).

## §B1. Scope

**Trong phạm vi:**
1. `CombatView/Tutorial/TutorialController.cs` — state machine 5 bước, nhận hook từ
   `BattleSceneInstaller`
2. `CombatView/Tutorial/TutorialOverlay.cs` — panel chỉ dẫn + highlight/mũi tên vùng,
   nút Skip
3. `BattleSceneInstaller` — tạo/điều khiển controller khi `PendingBattle.IsTutorial`;
   hook: skill select xong → bước 2; `CommandGrade` về → bước 3 (khắc chế); sự kiện
   Break enemy → bước 4; Ultimate gauge đầy + dùng → bước 5 & hoàn tất
4. Bridge: `RunContext.PendingBattle.IsTutorial` + Meta set khi node battle đầu &
   `!TutorialCompleted`
5. Hoàn tất/Skip → `ProgressDto.TutorialCompleted = true` + save qua đường
   `SaveProfile`/`BattleResultProcessor` có sẵn
6. Test EditMode `TutorialControllerTests` (state transitions, không cần scene)

**Ngoài phạm vi:**
- UI chỉ dẫn kiểu mũi tên bám theo vị trí động từng frame (làm bản đơn giản: highlight
  vùng cố định); asset art tutorial riêng

## §B2. Design

- **Trạng thái:** `Step { Idle, ChooseSkill, ActionCommand, Counter, Break, Ultimate, Done }`
- **Trigger từng bước** (chỉ nhận khi đang đúng step):
  1. ChooseSkill: `OnSkillChosen` lần đầu → highlight skill grid, không advance
  2. ActionCommand: `ActionCommandUI.Open` → màn chỉ dẫn grade; advance khi `onResult` gọi
  3. Counter: sau grade, khi phát hiện đòn khắc chế element (đọc `ElementTable.Multiplier`)
  4. Break: sự kiện enemy poise → 0 (đọc `CombatEventQueue` kiểu Break)
  5. Ultimate: gauge đầy + `OnSkillChosen` vào ô Ultimate → hoàn tất
- **Overlay:** code-dựng (style BattleHudScreen, KHÔNG prefab+Text kiểu Meta) —
  panel đen bán trong suốt + label TMP + nút "Bỏ qua"; giữ layout không đè hàng TACTIC.
- **Điều kiện chạy:** `RunContext.PendingBattle.IsTutorial` (node battle đầu tiên mỗi
  profile mới, `!TutorialCompleted`). Bỏ qua/Tutorial sau này không chạy lại.

## §B3. Implementation Checklist

- [x] Viết file này
- [x] `RunContext.PendingBattle.IsTutorial` (bool, không cần `BattleLaunchInfo` riêng — thêm thẳng
      field vào `PendingBattle` có sẵn, đúng pattern `Formation`/`ItemLoadout`)
- [x] Meta: `MetaSceneInstaller.LaunchBattle` set `isTutorial = !_profile.Progress.TutorialCompleted`
      không phân biệt loại node (xem §B4)
- [x] `TutorialController.cs` — state machine 5 bước, thuần C# không MonoBehaviour
- [x] `TutorialOverlay.cs` — overlay + Skip, code-dựng đúng style `BattleHudScreen`
- [x] `BattleSceneInstaller` — hook 5 trigger + hoàn tất/save flag (`WireTutorial`/
      `HandleTutorialStepChanged`/`CompleteTutorial` + event `OnCommandResolved` mới)
- [x] `validate_script` compile sạch (6 file mới/sửa)
- [x] `TutorialControllerTests.cs` (EditMode, 17 test)
- [x] `run_tests` xanh — **564/564** (547 cũ + 17 mới)
- [x] Verify thật qua `execute_code` (Play-mode không khả thi vì Editor đang ở Play Mode của chính
      người dùng lúc làm task này — không ép dừng, đúng
      [[feedback_shared_editor_session]]/tiền lệ item 41; đợi người dùng tự thoát Play Mode rồi làm
      tiếp bước gắn scene): dựng `BattleSceneInstaller`+`TutorialOverlay` thật qua reflection, gán
      `_pending.IsTutorial=true`, gọi `WireTutorial()` PRIVATE THẬT (không mock) — xác nhận
      `_tutorial` được tạo, overlay hiện đúng lúc bước ChooseSkill, `NotifySkillChosen()` chuyển
      đúng sang ActionCommand, `Skip()` → Done → overlay ẩn + `CompleteTutorial()` chạy thật (dùng
      `LocalPlayerRepository` trỏ thư mục scratch tạm — KHÔNG đụng save thật — xác nhận file
      `save.json` được ghi thật, `profile.Progress.TutorialCompleted` lật đúng `false→true`), dọn
      `ServiceLocator.Clear()` + xoá thư mục scratch ngay sau khi verify xong (tránh để lại
      registration giả ảnh hưởng session Play Mode thật sau này — `ServiceLocator.Register` throw
      nếu đăng ký trùng, phát hiện quan trọng lúc verify). `RunContext.QueueBattle(...,
      isTutorial:true)` xác nhận threading đúng qua `RunContext.Pending.IsTutorial`.
- [x] Cập nhật `roadmap.md §0.1` P2 (tick "chưa Tutorial") + `object-map.md`

## §B4. Phát hiện lúc làm (KHÁC/CHI TIẾT HOÁ bản thiết kế §B1/§B2 ở trên)

- **Cơ chế phát hiện bước 3 (Counter)/4 (Break) không cần event mới**: `CombatEvent.FloatValue`
  (payload có sẵn) ĐÃ mang đúng `ElementMultiplier` (gán ở `ActionResolver.cs:149`
  `floatValue: result.ElementMultiplier`) — bước 3 chỉ cần đọc `DamageDealt` có `FloatValue>1f` từ
  unit phe Player, không cần tính lại `ElementTable.Multiplier`. Bước 4 đọc thẳng
  `CombatEventType.PoiseBroken` có sẵn (source/target đã đúng ngữ nghĩa "ai phá poise của ai").
- **Đọc `CombatEventQueue` KHÔNG dùng `TryDequeue`** (sẽ tranh index đọc với
  `CombatPresenter` — presenter là consumer chính thức duy nhất, object-map.md §3.3) — đọc
  `.All` (IReadOnlyList) + tự giữ high-water-mark riêng (`_scannedEventCount`), đúng pattern
  Damage Meter/Analyze panel đã dùng trước đó trong `BattleHudScreen`.
- **Bước 5 (Ultimate) không cần event `UltimateCharged`** dù đã tồn tại trong enum — phát hiện
  "vừa DÙNG Ultimate" bằng cạnh xuống của chính `BattleState.UltimateGauge` (đầy → 0 giữa 2 lần
  `Tick`), đơn giản hơn hẳn và không phụ thuộc trigger UI nào khác (hoạt động dù Ultimate được
  dùng qua Auto-battle chẳng hạn). `UltimateGauge` DÙNG CHUNG cả đội Player (đã ghi rõ trong
  code có sẵn) nên không cần lọc theo unit.
- **`OnCommandResolved` (event mới trên `BattleSceneInstaller`, đúng gợi ý §B0)** — điểm duy nhất
  cần sửa vào pipeline hiện có, vì grade chỉ tồn tại trong closure private của
  `HandleSkillChosen`, không nơi nào khác quan sát được.
- **`TutorialController` đặt ở `Game.CombatView.Tutorial`** (đúng vị trí §B1 gốc — khác Phần E,
  lần này path gốc ĐÚNG vì không có xung đột asmdef: `BattleSceneInstaller` vốn đã ở
  `Game.CombatView`). Nhưng **test đầu tiên chạm `Game.CombatView`** → phải thêm
  `"Game.CombatView"` vào `references` của `Game.Tests.EditMode.asmdef` (trước đó thiếu, chưa
  test nào cần) — an toàn 1 chiều, không asmdef sản phẩm nào tham chiếu ngược asmdef test.
- **Toạ độ banner overlay tính tay từ code thật** (không đoán): `HeroPanel` x:[12,260] y-từ-đỉnh
  [12,170]; `EnemyPanel` x:[722,948] cùng dải y; `TurnOrderBar` y-từ-đỉnh [12,42]; `SkillGrid`
  y-từ-đáy [58,240]. Banner 400×90 tại anchor-center + offset (0,+35) → x:[280,680] (dư ≥20px 2
  bên khe hở [260,722]), y-từ-đáy:[260,350] (dư 10-20px giữa SkillGrid-đỉnh=240 và
  HeroPanel-đáy=370). Dim nền phủ toàn màn nhưng `raycastTarget=false` — không chặn thao tác
  skill/Action Command thật bên dưới, tutorial "dạy trong trận thật" đúng nghĩa (người chơi vẫn
  thao tác được ngay cả khi overlay đang hiện).
- **Node battle đầu tiên không phân biệt loại node** — node map luôn đặt Boss ở cuối
  (`PickBoss`/`BOSS_BY_CHAPTER` chỉ dùng cho node `Boss`), nên trận ĐẦU TIÊN 1 profile mới bấm
  luôn là `Battle` thường tự nhiên, không cần thêm điều kiện `node.Type` riêng.
- **Play Mode của người dùng chặn `manage_components`/`run_tests` giữa chừng** — đúng lỗi đã biết
  (item 41 memory), không ép dừng; tạm chuyển sang phần việc không cần Editor sống (sửa
  `BattleSceneInstaller.cs`/`MetaSceneInstaller.cs`) trong lúc chờ, hoàn tất bước còn lại
  (gắn `TutorialOverlay` tĩnh vào `Battle.unity`) ngay khi Play Mode tự kết thúc.

## §B-DoD
5 bước hiện đúng thứ tự trong trận thật; Skip & hoàn tất set flag + save; test xanh;
chỉ chạy 1 lần cho profile mới. **Đạt đủ 2026-08-12 — 564/564 xanh.**

---

# PHẦN C — Reforge sub-stat (P4)

Yêu cầu: reroll sub-stat trang bị bằng vàng. Enum `MetaEnums.Reforge = 14` đã có, chưa có
logic lẫn UI. Lấp nốt lỗ hổng P4 (~72%).

## §C0. Findings

- `EquipmentService.TryEnhance(profile, equipUid, IRandomSource rng)` + enum
  `EnhanceOutcome{Rejected, Failed, Succeeded}` — pattern chuẩn để bắt chước
  (dùng `IRandomSource` → deterministic-test được).
- UI Enhance nằm INLINE trong `TeamSelectScreen.cs` (không phải màn riêng):
  - :348 `row.transform.Find("EnhanceButton")`, :350 `EnhanceLabel`, :351-354 `CanEnhance`
    + màu `ENHANCE_BROWN`/`DISABLED`
  - :371 `EquipmentService.EnhanceCost(inst.Level)`, :383 `TryEnhance(_profile, capturedUid, _rng)`
  - :66-69 `_lastEnhanceUid`/`_lastEnhanceOutcome` — hiện "FAILED" đúng 1 lần cho dòng vừa bấm
- `EnhanceCost = 80*(level+1)` (giữ xuyên suốt +0..+14 theo task-enhance-plus15.md);
  Reforge nên dùng cost riêng (theo level + rarity), đặt tên `ReforgeCost`.
- Rarity → số sub-stat đã có sẵn khi sinh trang bị (task-equipment.md) — Reforge giữ
  nguyên số lượng sub-stat theo rarity, chỉ reroll giá trị (và có thể type).

## §C1. Scope

**Trong phạm vi:**
1. `EquipmentService.TryReforge(profile, equipUid, IRandomSource rng)` → enum
   `ReforgeOutcome { Rejected, Succeeded }`
2. `EquipmentService.ReforgeCost(int level, Rarity rarity)` + `CanReforge(instance)`
   (điều kiện tối thiểu: có ≥1 sub-stat)
3. Reroll toàn bộ sub-stat giữ nguyên count theo rarity; dùng chung hàm roll sub-stat
   đã có trong `EquipmentService`/`EquipmentInstance`
4. UI: nút `ReforgeButton` + label cạnh `EnhanceButton` trong `TeamSelectScreen` row
   (style Enhance: màu riêng, disable khi không đủ vàng/không có sub-stat);
   hiện sub-stat mới sau reroll; báo "không đủ vàng" kiểu toast/label hiện có
5. Test EditMode `EquipmentServiceTests` — thêm cases Reforge

**Ngoài phạm vi:**
- Reforge bằng currency khác ngoài vàng; màn hình riêng; xác nhận (confirm) 2 bước

## §C2. Design

- **`ReforgeCost`:** `gold = (80 * (level+1)) * (2 + (int)rarity)` — tự thiết kế, bám
  mốc Enhance hiện có; ghi chú "số liệu tự thiết kế, plan.md không cho bảng" (đúng kiểu
  task-loottable-chapters.md).
- **`TryReforge`:** check `CanReforge` + đủ vàng → `economy.SpendGold` → reroll sub-stats
  bằng `rng` truyền vào → trả `Succeeded`; thiếu vàng/không đủ sub-stat → `Rejected`
  (KHÔNG trừ tiền khi Rejected).
- **UI:** nút thứ 2 trong row, cùng vị trí style EnhanceButton; sau `Succeeded` cập nhật
  text sub-stat ngay (refresh row) + refresh TopBar Gold qua event có sẵn (:79 đã nghe).

## §C3. Implementation Checklist

- [x] Viết file này
- [x] `EquipmentService.ReforgeCost` + `CanReforge` + `TryReforge` + `ReforgeOutcome`
- [x] `TeamSelectScreen`: `ReforgeButton` + label + handler + refresh
- [x] `validate_script` compile sạch + force recompile 0 lỗi console
- [x] `EquipmentServiceReforgeTests.cs` — 6 test (cost scale theo level+rarity, CanReforge,
      Succeeded giữ count + trừ Gold đúng, đổi được sub-stat thật qua scan 30 seed, Rejected
      khi không sub-stat, Rejected khi thiếu Gold — không đụng gì khi Rejected)
- [x] `run_tests` → **524/524 xanh** (518 cũ + 6 mới)
- [x] Verify hình học sống qua `execute_code` (không Play-mode do MCP frame-stall đã biết):
      gọi `BuildShell()`/`RefreshGearPanel()` thật qua reflection, đọc `RectTransform` — label
      hiện đúng `"REFORGE 1280g"` cho item Epic +3 (khớp công thức `80*4*4`), nút disable đúng
      khi slot trống.
- [x] **Phát hiện phụ lúc verify hình học**: dòng cuối (slot 5) của `GearSlotsContainer` với
      `rowH=66` cũ đã chồng lên band `FormationRow` (content-bottom `[46,82]`) — TIỀN ĐÃ TỒN TẠI
      từ `task-formation-synergy.md`, không phải do `ReforgeButton` gây ra (đo lại: đáy dòng
      cuối = content-bottom 62 dù CÓ hay KHÔNG có `ReforgeButton`, y hệt `ItemLabel` cũ). Vì
      `FormationRow` vẽ SAU (đè lên) + có `Image` chặn raycast — cùng LOẠI lỗi với
      task-teamselect-start-button-fix.md, chỉ chưa ai bấm trúng để báo. Tiện sửa luôn (cùng
      hàm đang đụng): giảm `rowH` 66→60 trong `RefreshGearPanel` — vẫn giữ 6px hở giữa các dòng,
      đẩy đáy dòng cuối lên content-bottom=92 (cách `FormationRow` 10px, xác nhận qua
      `execute_code`). Không đổi bố cục nội bộ 1 dòng.
- [x] Cập nhật `roadmap.md §0.1` P4 + `object-map.md`

## §C-DoD
Test xanh (524/524); hình học `RectTransform` xác nhận đúng qua `execute_code`; `Rejected`
không trừ vàng; đúng style Enhance hiện có; TIỆN SỬA thêm 1 lỗi chồng-nút thật phát hiện lúc làm.

---

# PHẦN D — Localization mở rộng (P5)

Yêu cầu: từ 10 key pilot → dịch tên 24 hero / 65 enemy / 65 skill + migrate ~28 file
hard-code chuỗi còn lại. Tiếp nối task-localization-pilot.md.

## §D0. Findings

- `strings.csv` (Resources/Localization) — 10 key `key,vi,en`, parser tự viết trong
  `LocalizationService` (KHÔNG dùng `Game.Tools.CsvReader` — asmdef đó Editor-only).
- `HeroDefinitionSO.NameKey`, `EnemyDefinitionSO.NameKey`, `SkillDefinitionSO.Data.NameKey`
  ĐÃ tồn tại + `DataValidator` check "thiếu NameKey" → chuẩn key duy nhất cho cả 3 loại.
- `HeroDisplayUtil.FormatId()` đang sinh tên title-case — dùng làm fallback khi thiếu key.
- `LocalizationService` đã wire `Settings.Language` → `OnLanguageChanged` → `RefreshLabels`
  (pilot đã verify Play-mode đổi VI↔EN thật).
- ~28 file hard-code còn lại — chủ yếu màn Meta + Battle HUD (tên nhân vật, nút, nhãn).

## §D1. Scope

**Trong phạm vi:**
1. Gen key: `Assets/Tools/Localization/LocalizationKeyGenerator.cs` — quét toàn bộ
   Hero/Enemy/Skill SO → ghi `hero.{id}.name` / `enemy.{id}.name` / `skill.{id}.name`
   vào `strings.csv` (tránh gõ tay ~154 key)
2. `LocalizationService.Get(key)` — fallback: thiếu key → `HeroDisplayUtil.FormatId(id)`
   (giữ hành vi cũ khi chưa dịch)
3. Migrate tên nhân vật qua `LocalizationService`: nơi hiện tên hero/enemy/skill trong
   Meta + Battle (TeamSelectScreen, HeroDetailScreen, CodexScreen, ShopScreen, SummonScreen,
   QuestScreen, MailScreen, DungeonScreen, TowerScreen, TrialBossScreen, NodeChoiceScreen,
   InventoryScreen, BattleHudScreen, CombatPresenter)
4. Migrate nhãn/nút hard-code còn lại (màn Meta ưu tiên trước, Battle sau)
5. Test EditMode: đủ key cho mọi hero/enemy/skill; fallback hoạt động

**Ngoài phạm vi:**
- `LocalizationScanner` CI (plan.md §7) — để task riêng P5 khi có CI; font tiếng Việt

## §D2. Design

- Key chuẩn: `hero.{DefId}.name` / `enemy.{DefId}.name` / `skill.{Id}.name` — khớp
  `NameKey` có sẵn (chỉ viết hoa chuẩn hoá, không đổi data).
- Generator là Editor tool chạy tay (không tự chạy khi import — tránh ghi đè file dịch
  của dịch giả). Đọc thư mục SO thật qua `AssetDatabase`.
- `Get(key)`: tra dictionary → thiếu thì fallback tên cũ (không log spam lỗi hàng frame;
  chỉ log 1 lần trong Editor build).
- Mọi chuỗi UI hiện tên nhân vật gọi qua 1 helper `LocalizationService.GetName(Id, kind)`
  để gọn + fallback đồng nhất.

## §D3. Implementation Checklist

- [x] Viết file này
- [x] `LocalizationKeyGenerator.cs` (Editor tool, gen key vào strings.csv) — chạy thật qua
      `Tools/Localization/Generate Name Keys`, thêm đúng 155 key (24 hero + 66 enemy + 65 skill),
      idempotent (chạy lại lần 2 không thêm trùng)
- [x] `LocalizationService.GetName(id, kind)` + fallback title-case (KHÔNG sửa `Get(key)` chung —
      xem §D4 lý do)
- [x] Migrate tên hero/enemy/skill — **5/5 file thật sự dùng `HeroDisplayUtil`** (9 call site):
      `SummonScreen`, `MetaSceneInstaller`, `TeamSelectScreen`, `HeroDetailScreen`, `CodexScreen`.
      **`BattleHudScreen` CỐ Ý KHÔNG migrate lượt này** — kiểm tra thật thì nó KHÔNG dùng
      `HeroDisplayUtil` (khác giả định §D0), mà tự hiện `DefId` THÔ (`actor.DefId.ToUpperInvariant()`,
      `Short(u.DefId)`) — 1 quy ước khác hẳn, riêng biệt, chưa từng qua title-case chứ đừng nói tới
      dịch. Gộp vào phạm vi item 4 (nhãn khác) để dành lượt sau thay vì mở rộng phạm vi lượt này —
      xem §D4.
- [ ] Migrate nhãn hard-code còn lại (màn Meta) — **CHƯA làm, để dành lượt sau** (xem §D4, phạm vi
      quá lớn cho 1 lượt, đã tách riêng khỏi phần "tên nhân vật" là lõi thật sự của gap P5)
- [x] `validate_script` compile sạch (8 file mới/sửa)
- [x] Test: đủ key 3 loại (đọc thẳng strings.csv thật), fallback không lộ key thô, VI/EN parity
      (proper noun không bịa dịch — 7 test mới)
- [x] `run_tests` xanh — **571/571** (564 cũ + 7 mới)
- [x] Verify thật qua `execute_code` (không phải Play-mode do MCP frame-stall đã biết): gọi
      `CodexScreen.Open()` PUBLIC thật (không mock) với profile sở hữu hero/enemy thật, đọc lại
      `_nameLabels[i].text` qua reflection — xác nhận hiện đúng "Beast Tamer"/"Boss Alpha Wolf"...
      (không phải "???"/key thô), đổi `SetLanguage("en")` + mở lại (đúng thiết kế rebuild-on-open,
      không live-refresh) vẫn đúng.
- [x] Cập nhật `roadmap.md §0.1` P5 + `object-map.md`

## §D4. Phát hiện lúc làm (KHÁC/CHI TIẾT HOÁ bản thiết kế §D1/§D2 ở trên)

- **`NameKey` đã gán sẵn 100%** trên cả 24 hero/66 enemy/65 skill, ĐÚNG pattern
  `{kind}.{id không tiền tố}.name` — khác giả định "gen key" của §D1 (ngỡ phải tự gán key), việc
  thật chỉ là ĐIỀN `strings.csv` cho các key đã tồn tại sẵn trên data. `LocalizationKeyGenerator`
  vẫn giữ nhánh tự tính key khi `NameKey` trống (phòng nội dung mới sau này thiếu field), không xoá
  logic đó dù không dùng tới với data hiện có.
- **Không dịch tên riêng (proper noun)** — VI và EN dùng CHUNG 1 giá trị title-case từ id (ví dụ
  "Beast Tamer" cả 2 cột), đúng cách 10 key pilot đã làm với "AETHER LEGION". KHÔNG bịa dịch tiếng
  Việt cho tên fantasy — quyết định có chủ đích, không phải thiếu sót.
- **Kiến trúc lặp lại đúng bài học Phần E**: `LocalizationService` (`Game.Services`) KHÔNG được
  tham chiếu `HeroDisplayUtil` (`Game.Meta`) — `Game.Services` không ref `Game.Meta` (1 chiều,
  `Game.Meta` mới ref `Game.Services`). Giải pháp: viết lại logic title-case NHỎ (8 dòng) trực tiếp
  trong `LocalizationService`, trùng lặp có chủ đích với `HeroDisplayUtil.FormatId` thay vì cố
  import chéo — cùng loại quyết định đã gặp ở Phần E (`Game.Core.UI` thay vì `Game.UI`), khác ở chỗ
  lần này giải pháp là duplicate code nhỏ thay vì đổi namespace, vì không có sẵn 1 "lớp trung gian"
  nào để đặt cả 2 bên cùng dùng.
- **Phát hiện SAI LẦN ĐẦU rồi tự sửa lại trước khi chốt docs** — lúc đầu viết nhầm "Battle HUD
  không hiện tên hero/enemy nào" (đoán thay vì grep). Grep thật ra `_heroName.text = $"{actor.DefId.
  ToUpperInvariant()}..."` (dòng ~550) và `Short(u.DefId)` cho enemy row (dòng ~589/629) —
  `BattleHudScreen` CÓ hiện tên, chỉ là KHÔNG qua `HeroDisplayUtil` (tự làm ToUpperInvariant/viết
  tắt riêng, một quy ước khác hẳn). Sửa lại nhận định + để nó vào phạm vi item 4 (nhãn khác) thay vì
  tuyên bố sai "đã ổn, không cần làm". Bài học nhắc lại: LUÔN grep xác nhận trước khi khẳng định
  "X không cần sửa" trong docs, kể cả khi có vẻ hợp lý.
  `Game.UI` (asmdef của `BattleHudScreen`) THẬT RA CÓ THỂ tham chiếu `Game.Services` (đã có sẵn
  trong references) nên về mặt kiến trúc việc này khả thi, không vướng loại lỗi như Phần E/mục
  trên — chỉ là chưa làm trong lượt này vì phạm vi.
  `EquipmentDefinitionSO.NameKey` cũng tồn tại (không nằm trong §D0 gốc, phát hiện phụ khi khảo
  sát) nhưng KHÔNG migrate trong lượt này — tên trang bị hiện dùng logic khác
  (`EquipmentService.DefOf(...).NameKey` đọc trực tiếp không qua Get(), xem `MetaSceneInstaller.cs`
  dòng loot text), ngoài phạm vi "tên hero/enemy/skill" đã chốt.
- **Item 4 (nhãn hard-code khác) CỐ Ý để dành lượt sau** — phạm vi quá lớn (14 màn hình theo liệt
  kê gốc §D1, mỗi màn nhiều nút/tiêu đề) để làm trọn trong 1 lượt cùng với item 1-3; tách bạch rõ
  "tên nhân vật" (lõi thật sự của gap P5, đã xong) khỏi "nhãn UI chung chung" (công việc lặp lại
  máy móc trên diện rộng, không có rủi ro kỹ thuật mới, phù hợp làm dần theo từng màn khi có nhu
  cầu thay vì 1 lượt lớn). **Chưa migrate:** nút/tiêu đề trong `ShopScreen`, `QuestScreen`,
  `MailScreen`, `DungeonScreen`, `TowerScreen`, `TrialBossScreen`, `NodeChoiceScreen`,
  `InventoryScreen`, `CodexScreen`(nhãn ngoài tên)... — tất cả vẫn hard-code tiếng Anh/Việt như
  trước, KHÔNG đổi hành vi cũ, không regress. **`BattleHudScreen` cũng thuộc danh sách này** —
  tên hero/enemy trong trận vẫn hiện `DefId` thô (`ToUpperInvariant`/viết tắt), CHƯA qua
  `LocalizationService.GetName` (xem phát hiện ở trên).

## §D-DoD
Mọi màn chính hiển thị đúng VI mặc định; đổi EN qua Settings đổi tên nhân vật + nhãn;
fallback không trả key thô; test xanh. **Đạt phần LÕI (tên hero/enemy/skill) 2026-08-12 —
571/571 xanh; nhãn UI khác vẫn hard-code, cố ý để dành lượt sau (§D4).**

---

# PHẦN E — LayoutProfileSwitcher + Landscape (P6)

Yêu cầu: bộ 3 thành phần UI-responsive (Portrait/Landscape), nền móng để làm ~23 màn
hình P6 theo 2 hướng. Hiện chưa có class nào — chỉ còn comment nhắc tới
`LayoutProfileSwitcher` (BattleHudScreen.cs:19).

## §E0. Findings

- `SafeAreaFitter`, `LayoutProfileSwitcher`, `ScreenOrientationService` — grep toàn project
  KHÔNG có class nào (rỗng) → tạo mới cả 3.
- `CanvasScaler` đang dùng `ScaleWithScreenSize` tại `ActionCommandUI.cs`,
  `BattleSceneInstaller.cs:778`, `CameraFx.cs`, `SettingsScreen.cs` → giữ nguyên, chỉ
  thêm lớp preset layout, KHÔNG đổi scaler.
- P6 roadmap: "xoay ngang/dọc bất kỳ màn nào không vỡ layout; 5 tỉ lệ test" (DoD Phase 6).
- `Game.UI` asmdef đã tồn tại — các class UI mới đặt trong `Assets/_Project/Scripts/UI/Core/`.

## §E1. Scope

**Trong phạm vi:**
1. `UI/Core/LayoutProfile.cs` — `[Serializable]`: anchorMin/Max, pivot, pos, sizeDelta,
   scale, tên profile
2. `UI/Core/LayoutProfileSwitcher.cs` — MonoBehaviour: `portraitProfile`/`landscapeProfile`,
   quét `Screen.width > Screen.height` (hoặc `Screen.orientation`) mỗi Update, áp preset
   cho RectTransform đích; `[ExecuteAlways]` để preview Editor; logic chọn tách hàm thuần
   `LayoutProfile PickProfile(int w, int h, LayoutProfile portrait, LayoutProfile landscape)`
3. `UI/Core/SafeAreaFitter.cs` — MonoBehaviour: set RectTransform theo `Screen.safeArea`;
   hàm thuần `Rect GetSafeAreaRect(Rect screen, Vector2 safeArea, Vector2 screenSize)`
   để test 5 tỉ lệ + notch
4. Gắn pilot: `SettingsScreen`, `TitleCanvas` (Boot), `BattleHudScreen`
5. Test EditMode: `PickProfile` (5 tỉ lệ: 16:9, 4:3, 19.5:9, 1:1, 21:9) + `GetSafeAreaRect`

**Ngoài phạm vi:**
- Apply cho toàn bộ 23 màn (làm khi thêm màn P6, không retrofit hết ngay)

## §E2. Design

- **LayoutProfileSwitcher:** giữ reference `RectTransform _target`; `Update()` đọc
  `Screen.width/height` (portable hơn `Screen.orientation`, không cần lock); chỉ áp khi
  thay đổi profile (tránh chấp layout mỗi frame). `[ExecuteAlways]` để thấy kết quả khi
  xoay trong Editor/GameView.
- **SafeAreaFitter:** `OnRectTransformDimensionsChange` + `Update` đọc lại `Screen.safeArea`
  (iPad notch/Android cutout); áp qua `RectTransformUtility` — chuyển từ toạ độ screen sang
  local canvas. Logic toán tách hàm thuần để test 5 tỉ lệ.
- **Pilot:** `SettingsScreen` + `TitleCanvas` + `BattleHudScreen` mỗi cái 1 switcher
  (portrait hiện tại giữ nguyên số liệu; landscape: thu hẹp/giãn panel đã thiết kế).

## §E3. Implementation Checklist

- [x] Viết file này
- [x] `LayoutProfile.cs` (Serializable preset) — **ĐẶT Ở `Game.Core.UI`, KHÔNG `Game.UI`** (xem
      §E4 "phát hiện lúc làm" bên dưới — sai lệch có chủ đích so với bản nháp gốc ở trên)
- [x] `LayoutProfileSwitcher.cs` + hàm thuần `PickProfile`/`IsLandscape`
- [x] `SafeAreaFitter.cs` + hàm thuần `GetSafeAreaRect`
- [x] Gắn pilot: SettingsScreen + TitleCanvas + BattleHudScreen (2 profile thật)
- [x] `validate_script` compile sạch (3 file mới + `refresh_unity` xác nhận `Game.Meta`/`Game.UI`
      load OK)
- [x] `ResponsiveLayoutTests.cs` (EditMode: 6 tỉ lệ `IsLandscape`/`PickProfile` + `GetSafeAreaRect`
      không-notch/notch-trên/cutout-2-bên/identity-5-tỉ-lệ/chia-0 — 23 test)
- [x] `run_tests` xanh — **547/547** (524 cũ + 23 mới)
- [x] Verify thật qua `execute_code` (Play-mode xoay GameView không khả thi do MCP frame-stall đã
      biết — dùng kỹ thuật check-before-force quen thuộc): gọi `Build()`/`BuildLayout()` THẬT qua
      reflection trên GameObject tạm cho SettingsScreen/BattleHudScreen, đọc lại field
      `_portrait`/`_landscape` của chính `LayoutProfileSwitcher` đã gắn, áp `PickProfile` với 2 cặp
      kích thước giả lập (1080×1920 / 1920×1080) xác nhận `RectTransform` đổi đúng số thật; TitleCanvas
      xác nhận qua `grep` trực tiếp `Boot.unity` sau khi lưu scene, thấy đúng
      `Game.Core.UI.SafeAreaFitter`/`LayoutProfileSwitcher` trong YAML.
- [x] Cập nhật `roadmap.md §0.1` P6 + `object-map.md §12`/`§12.1` (đăng ký 3 class mới, ghi rõ
      `ScreenOrientationService`/`E-ORIENTATION_CHANGED` ở §3/§5 VẪN chưa xây — không tự sửa các
      bảng kiến trúc ĐÍCH đó). `plan.md §11.6/§11.7` KHÔNG có 2 mục con này trong plan.md thật (đã
      kiểm — plan.md không đánh số §11.6/§11.7 riêng cho responsive, mục responsive nằm trong
      §10.3/§11.9 chung) nên bỏ qua bước này, không sửa `plan.md`.

## §E4. Phát hiện lúc làm (KHÁC bản thiết kế §E1/§E2 ở trên)

- **Namespace/vị trí file sai trong bản nháp gốc**: §E1 ghi "đặt trong
  `Assets/_Project/Scripts/UI/Core/`" (nghĩa là assembly `Game.UI`). Kiểm tra đồ thị asmdef thật
  (`Game.Meta.asmdef` references không có `Game.UI`, trong khi `Game.UI.asmdef` CÓ reference
  `Game.Meta` — 1 chiều, đúng luật structure.md §6/`AssemblyRuleTests`) phát hiện `SettingsScreen`
  (1 trong 3 pilot bắt buộc) nằm ở `Game.Meta` nên KHÔNG THỂ dùng type từ `Game.UI` — sẽ tạo cycle,
  không compile được. Sửa: đặt cả 3 class ở `Assets/_Project/Scripts/Core/UI/`
  (namespace `Game.Core.UI`) — đúng precedent `IUiRootHost.cs` đã có sẵn trong chính thư mục đó,
  dựng cho lý do y hệt (cross-cutting UI infra cần Game.Meta reach được). Mọi assembly (Meta/UI/
  CombatView/Bootstrap) đều gián tiếp phụ thuộc `Game.Core` nên cả 3 pilot dùng được.
- **`GetSafeAreaRect` đổi chữ ký** so với gợi ý thô trong §E1 (`Rect screen, Vector2 safeArea,
  Vector2 screenSize` — thiếu rõ ràng, `screen` và `screenSize` trùng thông tin). Đổi thành
  `GetSafeAreaRect(Rect screen, Rect safeArea)` — khớp đúng kiểu `Screen.safeArea` thật (Rect, gốc
  dưới-trái theo pixel), trả về Rect ĐÃ CHUẨN HOÁ 0..1 (`position`=anchorMin, `position+size`=
  anchorMax) — thuật toán chuẩn cho pattern safe-area quen thuộc, dễ test/dễ áp thẳng vào
  `RectTransform.anchorMin/anchorMax`.
- **KHÔNG xây `ScreenOrientationService`** dù object-map.md §3.2/§3.3/§5.2 (bảng kiến trúc ĐÍCH) có
  nhắc — `LayoutProfileSwitcher`/`SafeAreaFitter` tự đọc `Screen.width/height`/`Screen.safeArea`
  trực tiếp mỗi `Update()` (`[ExecuteAlways]`), theo đúng lựa chọn ở §E2 gốc ("portable hơn
  `Screen.orientation`, không cần lock"). Không phát sinh `E-ORIENTATION_CHANGED` event nào.
- **Điểm gắn pilot THẬT khác giả định của bảng §3 gốc**: docs giả định 1 GameObject
  `SafeArea` con trực tiếp của `MetaCanvas`/`BattleCanvas` mang cả 2 component. Thực tế
  `BattleHudScreen`/`SettingsScreen` không có wrapper "SafeArea" nào (mọi panel neo thẳng vào 4 góc
  Canvas — tái cấu trúc để có wrapper là việc LỚN hơn hẳn phạm vi pilot, rủi ro regress toàn bộ
  HUD đã tune kỹ qua nhiều task trước). Chọn attach trực tiếp lên panel cụ thể ít rủi ro nhất mỗi
  màn: `SettingsScreen` → panel dialog chính; `BattleHudScreen` → `HeroPanel` (neo góc riêng, không
  cascade sang panel khác); `TitleCanvas` → gắn `SafeAreaFitter` lên chính root rect (đây MỚI đúng
  là "SafeArea-như-docs-mô-tả" vì `TitleCanvas` vốn đã là content-root full-stretch duy nhất) +
  `LayoutProfileSwitcher` lên `TitleLabel`.

## §E-DoD
3 class mới + 2 hàm thuần có test; 3 màn pilot không vỡ ở landscape; test xanh;
docs đăng ký đủ. **Đạt đủ 2026-08-12 — 547/547 xanh.**

---

## Ghi chú chung

- Thứ tự đề xuất: **C (Reforge, nhanh nhất) → A (Object Map) → E (Responsive) → B (Tutorial)
  → D (Localization)** — mỗi phần commit + cập nhật object-map.md riêng.
  **C ✅ + A ✅ + E ✅ + B ✅ + D ✅ (phần lõi) xong (2026-08-12) — cả 5 phần task file này đã hoàn
  tất phần lõi. Còn lại: D item 4 (nhãn hard-code khác + tên nhân vật trong Battle HUD) để dành
  lượt sau, xem §D4.**
- Mọi phần KHÔNG chạm `Game.Combat` logic lõi (trừ Tutorial chỉ *đọc* sự kiện) →
  test base 518/518 giữ nguyên, chỉ cộng thêm test mới từng phần.
- Verify Play-mode: MCP frame-stall là giới hạn đã biết — dùng kỹ thuật
  check-before-force + `GameObject.Find`/`onClick.Invoke()` + `execute_code` đọc state thật.

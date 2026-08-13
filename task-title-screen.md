# Task: Title/Home screen

Yêu cầu: chọn qua `AskUserQuestion` ("Title/Home screen (Recommended)") trong số 3 lựa chọn
(Title/Home / Inventory / Damage Meter) — roadmap.md P6 liệt Title/Home là 1 trong số màn hình
`Game.UI` chưa xây. Việc mới, có quyết định kiến trúc thật (đặt ở đâu, gate boot flow ra sao) —
viết xong task file này rồi mới chạm code.

## §0. Findings

Đọc `GameBootstrap.cs` (85 dòng, entry point Boot scene), hierarchy thật `Boot.unity` qua
`execute_code`, `ServiceInstaller.cs`.

- **Game hiện auto-advance thẳng vào Meta, không dừng lại màn nào** — `GameBootstrap.Awake()`:
  Install services → `LoadProfileAsync()` → `if (_autoAdvanceToMeta) EnterMeta();`. Field
  `[SerializeField] private bool _autoAdvanceToMeta = true;` — TÊN FIELD đã tự gợi ý sẵn có 1 gate
  dự tính từ trước (đặt tên "auto ADVANCE" ngụ ý có thể KHÔNG auto), nhưng chưa có UI nào implement
  nhánh `false`. Đây là bằng chứng thật, không phải suy đoán — field này tồn tại nhưng luôn `true`.
- **`UIRoot` (Boot.unity, `GameBootstrap/__UI__/UIRoot`, DontDestroyOnLoad) hiện chỉ có ĐÚNG 1
  con: `MetaCanvas`** — đọc thật qua `execute_code`, xác nhận chưa có canvas Title/Home nào tồn
  tại dù dạng ẩn. `MetaCanvas` mặc định `activeSelf=true` ngay trong save scene, `sortingOrder=100`,
  `ScreenSpaceOverlay`.
- **plan.md liệt Title/Home là 1 trong 23 màn hình thiết kế gốc** nhưng không cho spec cụ thể nào
  (không có bảng nội dung như Event/Rest hay Mail đã từng thiếu) — toàn bộ nội dung tự thiết kế tối
  thiểu, đúng kỷ luật đã dùng xuyên suốt dự án cho mọi màn thiếu spec (Mail, Codex, Event/Rest).
- **`GameBootstrap` đã có sẵn đúng dữ liệu cần cho 1 dòng tóm tắt** (dùng ngay trong log hiện có):
  `Profile.PlayerId`, `Profile.Wallet.Gold`, `Profile.Heroes.Count` — tái dùng thay vì tính lại.
- **Tên hiển thị**: `plan.md`/`roadmap.md` đều gọi dự án là **"Aether Legion"** (codename) khác
  `productName` Unity ("TurnBase", tên build nội bộ) — dùng "AETHER LEGION" làm tiêu đề hiển thị
  cho người chơi, đúng ý nghĩa "tên trong-game" của codename này.
- **Đặt Title/Home ở ĐÂU**: cân nhắc 2 hướng — (a) scene riêng thứ 4 (kiến trúc gốc plan.md từng
  nhắc 4 scene, hiện chỉ 3, thiếu `Sandbox` "chưa từng cần" theo object-map.md §12), hay (b) 1
  Canvas tĩnh ngay trong Boot.unity, cùng `UIRoot`/`GameBootstrap` đã điều phối Boot→Meta transition
  sẵn. Chọn **(b)** — mọi "màn hình" khác trong dự án (Quest/Mail/Shop/Codex...) đều là Canvas
  overlay trong 1 scene có sẵn, không phải scene riêng; thêm scene thứ 4 chỉ cho 1 màn hình đơn giản
  là lệch hẳn quy ước đã thiết lập, và `GameBootstrap` đã là đúng nơi orchestrate transition này —
  không cần tách scene mới, không cần load/unload thêm gì.
- **Không cần lớp `TitleScreen` riêng** — khác các Screen khác (Mail/Codex/Quest đều là component
  riêng vì chúng có logic thật: claim, phân trang, filter...), Title chỉ có ĐÚNG 1 việc ("hiện
  canvas, chờ bấm START, ẩn canvas, gọi `EnterMeta()`") — gộp thẳng vào `GameBootstrap` (đã là
  MonoBehaviour orchestrator duy nhất của Boot scene) tương xứng hơn là bịa thêm 1 class cho trách
  nhiệm nhỏ như vậy.
- **Settings không cần nút riêng ở Title** — `SettingsScreen` là 1 `Game.Meta` component gắn trên
  GameObject của `MetaSceneInstaller` (scene Meta), không truy cập được từ Boot scene (2 scene khác
  file, giống lý do TopBar buttons ở Mail phải bind qua `Find()` chứ không qua Inspector) — Settings
  đã có sẵn nút trong TopBar ngay khi vào Meta, không cần trùng lặp ở Title.

## §1. Scope decision

**Trong phạm vi:**
1. `TitleCanvas` mới — Canvas tĩnh trong `Boot.unity`, con của `UIRoot` (sibling `MetaCanvas`),
   `sortingOrder` cao hơn `MetaCanvas` (VD 150) để luôn vẽ đè lên nếu cả 2 cùng active.
2. Nội dung: Dim/Fill nền (đúng khuôn mọi modal khác), `TitleLabel` ("AETHER LEGION"),
   `SubtitleLabel` (tóm tắt profile — "{N} Heroes · {Gold} Gold", tái dùng dữ liệu có sẵn),
   `StartButton` (Label "START").
3. `GameBootstrap.cs`: đổi mặc định `_autoAdvanceToMeta` → `false`. Nhánh `else` mới: ẩn
   `MetaCanvas` (nếu đang active), hiện `TitleCanvas`, wire `StartButton.onClick` → ẩn
   `TitleCanvas` → `EnterMeta()`. Giữ nguyên nhánh `true` cũ (fast-path debug/skip, không xoá field
   để không phá khả năng bật lại nếu cần test nhanh).
4. Test: KHÔNG viết EditMode test cho phần này — đây thuần là wiring UI/boot-flow (giống
   `MetaSceneInstaller.BindCanvasRefs`/`BuildUi` cũng không có test trực tiếp, chỉ verify qua
   Play-mode thật), không có logic thuần (`pure function`) nào đáng test riêng.
5. Verify Play-mode thật: vào Play mode từ đầu (không resume dở), xác nhận Title hiện trước Meta,
   bấm START chuyển đúng sang Meta, TopBar hoạt động bình thường sau đó.

**Ngoài phạm vi (cố ý, ghi rõ):**
- KHÔNG thêm scene thứ 4 — lý do đã ghi ở §0.
- KHÔNG thêm nút Settings/Logo art/animation intro — tối thiểu nhưng thật, đúng kỷ luật session.
- KHÔNG đổi `productName` Unity hay bất kỳ config build nào — chỉ đổi TEXT hiển thị trong UI.
- KHÔNG xây lại toàn bộ luồng loading/splash (progress bar, async loading state) — `LoadProfileAsync`
  đã nhanh (đọc file JSON local), không cần UI loading riêng.

## §2. Implementation

- [x] `TitleCanvas` mới trong `Boot.unity` (`GameBootstrap/__UI__/UIRoot/TitleCanvas`, sibling
      `MetaCanvas`) — dựng qua `execute_code` (`GameObject`/`RectTransform`/`Canvas` trực tiếp,
      không qua prefab vì đây là scene-only content, không tái dùng ở đâu khác):
      `sortingOrder=150` (> `MetaCanvas` 100), `CanvasScaler` khớp `MetaCanvas` (ScaleWithScreenSize,
      960×540, matchWidthOrHeight=0.5). Con: `Background` (Fill tối 0.114/0.078/0.129, full-screen),
      `TitleLabel` ("AETHER LEGION", 48pt bold, màu ACCENT 0.957/0.635/0.349), `SubtitleLabel`
      (16pt, rỗng lúc dựng — điền lúc `ShowTitleScreen()`), `StartButton` (220×56, màu ACCENT,
      Label "START" 22pt bold). Mặc định `SetActive(false)` — chỉ `ShowTitleScreen()` bật khi cần.
- [x] `GameBootstrap.cs`: `_autoAdvanceToMeta` đổi default `true`→`false` (cả field default lẫn
      giá trị serialize thật trong `Boot.unity`, sửa qua `SerializedObject` vì đổi default C# không
      tự cập nhật giá trị đã lưu trong scene). Thêm `ShowTitleScreen()`: ẩn `MetaCanvas`, điền
      `SubtitleLabel` từ `Profile.Heroes.Count`/`Profile.Wallet.Gold`, wire `StartButton.onClick` →
      ẩn Title + hiện lại Meta + `EnterMeta()`, rồi bật `TitleCanvas`.
- [x] `refresh_unity` compile sạch. Xác nhận edit sống sót qua domain reload (scene chưa lưu vẫn
      giữ nguyên state trong Editor — khác hẳn reset state khi RELOAD lúc đang Play mode).
- [x] `manage_scene action=save` lưu `Boot.unity`.
- [x] Full EditMode suite — **413/413 xanh, không đổi** (thay đổi thuần Boot-flow/UI, không đụng
      logic nào có test).
- [x] Verify Play-mode THẬT (không giả lập) — session này gặp lại đúng "MCP frame-stall"
      (`Time.frameCount` đứng ở 2 dù real time trôi) NHƯNG lần này không cản được verify vì mọi thao
      tác dùng trực tiếp `GameObject.Find`/`onClick.Invoke()` qua `execute_code`, không cần frame
      thật tick. **Phát hiện phụ lúc verify**: `SceneManager.GetSceneByName("Boot").
      GetRootGameObjects()` KHÔNG còn thấy `GameBootstrap` sau khi `Awake()` chạy — vì
      `DontDestroyOnLoad(gameObject)` đã CHUYỂN nó sang scene giả `DontDestroyOnLoad`, không còn
      thuộc "Boot" nữa (không phải bug, đúng hành vi Unity chuẩn, chỉ là cách tìm sai — sửa bằng
      `GameObject.Find("GameBootstrap")` thay vì duyệt qua `Scene.GetRootGameObjects()`). Xác nhận
      thật: `TitleCanvas.active=True`, `MetaCanvas.active=False`, `SubtitleLabel.text="24 Heroes ·
      935210 Gold"` (đúng số liệu save thật) NGAY SAU boot. Bấm `StartButton.onClick.Invoke()` thật
      → `SceneManager.GetActiveScene().name` đổi thành `"Meta"`, `TitleCanvas.active=False`,
      `MetaCanvas.active=True` — luồng chuyển cảnh đúng hoàn toàn. Không verify được
      `MetaSceneInstaller`'s field khác (`_mailButton`/`_walletLabel` vẫn null lúc kiểm) vì
      `Start()` của nó CHƯA CHẠY do frame-stall (đúng pattern đã biết ở
      `feedback_unity_mcp_ui_gotchas.md` — "Start() never fires on newly-loaded-scene objects" —
      không phải lỗi của thay đổi Title screen, việc load scene "Meta" tự nó đã thành công).
- [x] Cập nhật `roadmap.md §0.1`, `object-map.md §12.1`.

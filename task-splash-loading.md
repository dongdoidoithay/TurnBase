# Task: Splash + Loading (plan.md §10.1) — hạng mục thứ 3/4 màn còn thiếu theo roadmap

Tiếp nối `task-defeat-screen.md`/`task-hero-list.md`. plan.md §10.1:
- Splash | `Splash` | "Logo, 2 giây"
- Loading | `Loading` | "Overlay, có mẹo chơi"

## §1. Phát hiện nghiêm trọng TRƯỚC khi bắt tay vào — `TitleCanvas` không còn tồn tại

Đọc `Boot.unity` (đang load live) để lấy số liệu thật trước khi thêm `SplashCanvas` mới — phát hiện
`UIRoot` chỉ còn `MetaCanvas`, **`TitleCanvas` đã biến mất hoàn toàn** (dù `GameBootstrap.
ShowTitleScreen()` vẫn gọi `_uiRoot.Find("TitleCanvas").gameObject` — sẽ NRE crash thật nếu chạy
nhánh này). Kiểm tra `_autoAdvanceToMeta` trên component thật trong scene: đang = `true` — đúng
nhánh fast-path debug (bỏ qua Title) nên bug đang "ngủ", nhưng đây là **bug crash thật, ảnh hưởng
đúng luồng người chơi thật** (comment code tự ghi rõ `true` không phải hành vi mặc định dự kiến).
Nhiều khả năng mất trong 1 trong các sự cố ghi ở `task-ui-vfx-polish.md §3.4/§3.5` (reset hàng loạt/
`git checkout` lùi quá xa) — không có bằng chứng cụ thể hơn, không đoán thêm.

**Không tự ý đổi `_autoAdvanceToMeta`** (quyết định thuộc người dùng cho luồng test riêng) — chỉ sửa
`TitleCanvas` để nhánh `false` (luồng thật) không còn crash nếu/khi được bật lại.

## §2. Splash — dựng `SplashCanvas` + rebuild `TitleCanvas`, nối vào `GameBootstrap.Awake()`

- **`TitleCanvas`** dựng lại đúng cấu trúc `ShowTitleScreen()` đã cần sẵn (không đoán mù — đọc thẳng
  code để lấy path bắt buộc): `TitleLabel`/`SubtitleLabel`/`StartButton`+`StartButton/Label`. Dùng
  bộ màu đã chốt xuyên suốt phiên (cam `PANEL_BORDER` cho tên game, kem `TEXT` cho phụ đề, nền tối
  `PANEL_BG` qua `pixel_metal_panel`), `sortingOrder=150` (> MetaCanvas=100).
- **`SplashCanvas`** mới — "AETHER LEGION" (chữ, không có asset logo hình ảnh nào trong dự án) +
  caption "Loading...", `sortingOrder=160` (trên cả Title, hiện đầu tiên).
- **`GameBootstrap.Awake()`**: hiện Splash NGAY, chạy `LoadProfileAsync()` SONG SONG với
  `Task.Delay(2000)` (`Task.WhenAll`) — đảm bảo tối thiểu 2 giây (đúng "Logo, 2 giây") nhưng KHÔNG
  cộng dồn với thời gian load thật (nếu load lâu hơn 2s thì Splash tự nhiên hiện đúng bằng thời gian
  đó, không chờ thêm). **CHỦ Ý bỏ qua hẳn Splash (không chỉ Title)** khi `_autoAdvanceToMeta=true` —
  giữ đúng tinh thần cờ debug "vào thẳng Meta", ép chờ 2s mỗi lần Play khi đang lặp nhanh sẽ phản tác
  dụng chính cờ này.

## §3. Loading — overlay + mẹo chơi cho MỌI lần đổi scene (8 điểm gọi)

- Đếm đủ: `GameBootstrap.EnterMeta` (1) + `MetaSceneInstaller` (5, tất cả `LaunchBattle`/node map/
  Dungeon/Tower/TrialBoss) + `BattleSceneInstaller` (2: `HandleContinue`, `HandleRetry` mới ở
  task-defeat-screen.md).
- **`LoadingCanvas`** mới — nền tối + "LOADING..." + `TipLabel` (6 mẹo chơi thật, phản ánh đúng cơ
  chế đã có: Action Command Perfect, Poise/Break, bảng nguyên tố, Swap Row, Ascend, Analyze — không
  bịa tính năng không tồn tại). `sortingOrder=170` (trên cùng, luôn che khi chuyển scene).
- **KHÔNG chuyển `SceneManager.LoadScene` sang `LoadSceneAsync`** — quyết định có chủ đích: scene
  trong game này nhỏ (2D, ít asset), load gần như tức thì, chuyển hẳn async là việc LỚN HƠN NHIỀU
  (đụng progress bar thật, animation callback...) cho giá trị thực tế thấp ở quy mô game này. Thay
  vào đó: hiện overlay → `yield return null` (đợi ĐÚNG 1 frame để overlay kịp render — LoadScene
  đồng bộ chặn main thread ngay khi gọi, không frame nào xen giữa nếu gọi liền tay) → đợi thêm
  `WaitForSecondsRealtime(0.6s)` (đủ đọc 1 dòng mẹo) → `LoadScene` đồng bộ như cũ → ẩn overlay.

### §3.1. Lỗi kiến trúc tự gây ra + tự sửa — tham chiếu NGƯỢC gây circular assembly reference

Lần viết đầu: `GameBootstrap.LoadSceneWithOverlay` là `public static`, gọi trực tiếp từ
`MetaSceneInstaller`/`BattleSceneInstaller` qua `Game.Bootstrap.GameBootstrap.LoadSceneWithOverlay(...)`.
Compile TOÀN PROJECT (không phải `validate_script` từng file — công cụ đó không bắt lỗi này) báo
`CS0234: 'Bootstrap' does not exist in namespace 'Game'` ở `MetaSceneInstaller.cs`. Kiểm tra asmdef
thật: `Game.Bootstrap` đã tham chiếu XUỐNG cả `Game.Meta` VÀ `Game.CombatView` (đúng vai trò entry
point, phụ thuộc TOÀN BỘ layer dưới) — `Game.Meta`/`Game.CombatView` gọi ngược lên `Game.Bootstrap`
tạo **vòng lặp assembly thật** (chặn compile, không phải chỉ sai quy ước kiến trúc).

**Sửa đúng bằng cách tái dùng CHÍNH XÁC mẫu đã có sẵn cho đúng vấn đề này** —
`Game.Core.UI.IUiRootHost` (interface ở `Game.Core`, impl đăng ký qua `ServiceLocator` ở
`ServiceInstaller`, mọi layer gọi qua `ServiceLocator.Get<IUiRootHost>()` không cần biết ai impl):
tạo `Game.Core.Scenes.ISceneTransitionService` (interface thuần, 1 method), `GameBootstrap` implement
+ `ServiceLocator.Register<ISceneTransitionService>(this)` trong `Awake()`. Đổi 6 điểm gọi còn lại
sang `ServiceLocator.Get<ISceneTransitionService>().LoadSceneWithOverlay(...)`. Compile lại sạch
hoàn toàn — xác nhận bằng compile TOÀN PROJECT thật (không chỉ per-file), đúng bài học "per-file
validate không bắt được lỗi cấp assembly-graph".

## §4. Verify

- Đọc trực tiếp `Boot.unity` sau khi lưu (đúng kỷ luật không tin "saved=True" suông): GUID
  `icon_heroes`... à nhầm, ở đây là đếm `m_Name: TitleCanvas`/`SplashCanvas`/`LoadingCanvas`/
  `StartButton`/`LogoLabel`/`TipLabel` — đều xuất hiện đúng 1 lần trên đĩa.
- Verify tĩnh mọi path `ShowTitleScreen()` cần (`TitleLabel`/`SubtitleLabel`/`StartButton`+`Label`)
  + `SplashCanvas`/`LoadingCanvas`+`TipLabel` tồn tại VÀ đúng loại component — 100% khớp.
- `validate_script` 0 lỗi cả 4 file mới/sửa (`GameBootstrap.cs`, `ISceneTransitionService.cs`,
  `MetaSceneInstaller.cs`, `BattleSceneInstaller.cs`), compile TOÀN PROJECT 0 lỗi console (sau khi
  sửa xong lỗi circular reference), **637/637 test xanh** (không đổi khỏi baseline).
- Grep xác nhận không còn `SceneManager.LoadScene` trực tiếp nào ngoài ĐÚNG 1 chỗ bên trong
  `GameBootstrap.LoadSceneRoutine` — mọi nơi khác đã chuyển qua service.
- **KHÔNG test bằng cách thật sự trigger đổi scene** trong phiên này (Editor dùng chung, Boot.unity
  + Battle.unity đang load — gọi Play/đổi scene thật có thể làm mất trạng thái đang mở của người
  dùng). Chỉ verify tĩnh (path + compile + test suite). Người dùng nên tự bấm Play 1 lần để xác nhận
  bằng mắt luồng Splash→Title→START→Loading→Meta.

## §5. Chưa làm / để dành

- `_autoAdvanceToMeta` vẫn giữ nguyên `true` trong scene — người dùng tự quyết định khi nào bật lại
  `false` để thấy Splash/Title thật (không tự ý đổi giá trị này).
- Loading overlay chỉ có 1 mẹo/lần hiện ngẫu nhiên, không xoay vòng nếu load lâu hơn dự kiến — không
  cần thiết ở quy mô load gần-tức-thì hiện tại.
- Landscape cho 3 canvas mới (Splash/Title/Loading) chưa wire — cả 3 dùng chung 1 CanvasScaler tĩnh
  960×540 giống MetaCanvas gốc, chưa có `LayoutProfileSwitcher` riêng (khác các Panel modal đã làm ở
  task-ui-vfx-polish.md §5/§7).

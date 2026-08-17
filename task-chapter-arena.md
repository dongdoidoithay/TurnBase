# Task: Arena (placeholder) + ChapterSelect (ChapterProgressScreen) — hạng mục cuối/roadmap còn thiếu

Tiếp nối `task-hero-list.md`/`task-splash-loading.md`. plan.md §10.1 liệt Arena (v1.1, "Coming Soon"
ở v1.0) và Chapter Select (5 chương) là 2 màn còn thiếu cuối cùng theo roadmap trước khi quay lại
polish/optimize.

## §1. Quyết định thiết kế (chốt qua AskUserQuestion trước khi code)

- **Arena**: thêm icon thứ 11 trên TopBar, thu nhỏ 10 icon cũ để có chỗ (thay vì gộp vào menu phụ
  hay bỏ hẳn khỏi v1.0). Người dùng chọn phương án này (Recommended) qua AskUserQuestion.
- **ChapterSelect**: chỉ hiển thị tiến trình (đọc, không đổi gameplay) — không cho chọn lại chương để
  chơi, vì lối chơi hiện tại là tuyến tính (`MetaSceneInstaller.EnsureRun` luôn sinh NodeMap theo
  đúng `Progress.ChapterUnlocked`, không nhận chapter tuỳ ý — cho phép "chọn chương" thật sẽ cần sửa
  cả hệ thống NodeMap generation, ngoài phạm vi 1 task).

## §2. Chặn giữa chừng bởi Play mode — kỷ luật an toàn

Khi bắt đầu sửa Boot.unity cho Arena, `Application.isPlaying == true` (người dùng đang tự Play test
Splash/Loading vừa xong ở task trước) — dừng ngay theo đúng kỷ luật đã có, KHÔNG ép sửa scene khi
đang Play. Chuyển sang làm phần không đụng scene trước (script `ChapterProgressScreen.cs` + prefab
`UI_ChapterProgress.prefab` qua `PrefabUtility.LoadPrefabContents`/`SaveAsPrefabAsset` — an toàn ở
asset-level dù đang Play mode, khác hẳn sửa GameObject trong scene đang mở). Khi Play mode kết thúc
(`isPlaying=False`, tự kiểm tra lại trước khi đụng scene), mới quay lại làm Boot.unity.

## §3. Arena — resize 8 icon cũ + thêm ArenaButton thứ 9

Đo thật TopBar trước khi sửa: 8 icon cũ (`TowerButton`...`HeroListButton`) đều `w=44 h=40`, bước
50px (44+gap6), trải từ x=220 đến x=570 (center), chiếm 198→592 (394px). Công thức mới: `w=38`,
gap giữ 6 → bước 44px; 9 icon từ center-đầu 217 → center-cuối 569, chiếm 198→588 (390px, VẪN nằm
trong biên cũ 592 — không tràn qua `LeaderPortrait`/`WalletLabel`).

- Resize 8 `Button` cũ tại chỗ (`sizeDelta.x`, `anchoredPosition.x`) theo công thức trên.
- `ArenaButton` = `Instantiate(HeroListButton, topBar)` (giữ nguyên cấu trúc: root `Image`
  (`pixel_blue_panel`) + `Button`, child `Icon` → `Image`) — đổi tên, đổi `anchoredPosition.x=569`,
  gán `Icon.sprite` = `icon_hourglass` (mới, xem §5).
- `MetaSceneInstaller.cs`: field `_arenaButton`, bind trong `BindCanvasRefs()`, click handler
  `Toast("Arena — Coming Soon...")` — không có scene/logic thật nào khác (v1.0 placeholder, đúng
  plan.md).

## §4. ChapterSelect — `ChapterProgressScreen` (thuần đọc)

- **Entry point**: bấm chính `TitleLabel` ("CHAPTER N | X heroes") trên TopBar — KHÔNG thêm nút
  TopBar thứ 10 (đã đủ 9 sau Arena, tránh chật thêm). Thêm `Button` component lên đúng GameObject
  `TitleLabel` sẵn có (`targetGraphic` = chính `Text` đã có, `raycastTarget=true`).
- **`ChapterProgressScreen.cs`** (mới, `Game.Meta`) — 5 dòng cố định (Chapter 1-5), mỗi dòng:
  tên chương (tái dùng `MetaSceneInstaller.CHAPTER_NAMES`, mới thêm — Meadow/Swamp/Crypt/Volcano/
  Void, khớp doc-comment `ChapterAccent` đã có sẵn từ trước), trạng thái CLEARED/IN PROGRESS/LOCKED
  so với `profile.Progress.ChapterUnlocked`, tint nền theo `MetaSceneInstaller.ChapterAccent` (đổi
  `private`→`internal static` để dùng chung, không tính toán lại màu). Chỉ có `CloseButton` —
  `ClaimButton` mỗi dòng bị ẩn hẳn (`SetActive(false)`, khác Quest/Mail có Claim thật).
- **`UI_ChapterProgress.prefab`** (mới) — nhân bản từ `UI_Quest.prefab` (đủ sẵn cấu trúc `Panel/
  RowListContainer/Row_0..5` + `CloseButton`), xoá `Row_5` còn đúng 5 dòng.
- `MetaSceneInstaller.cs`: field `_chapterProgressScreen`, `AddComponent` cùng chỗ với các screen
  khác, field `_titleButton` (Button trên `TitleLabel`, song song `_titleLabel` Text đã có), click
  handler mở `_chapterProgressScreen.Open(_profile, null)`.

## §5. Icon mới — `icon_hourglass` (Arena "Coming Soon")

`Tools/pixel-art-pipeline/scripts/nav_icons.py` — thêm `icon_hourglass()` (đồng hồ cát 2 nét, 24×24
trắng-trong-suốt, cùng style 7 icon cũ). Sinh qua script, xem trước xác nhận hình sạch, import qua
`TextureImporter` (Point filter, Sprite/Single, PPU 100, Compressed — đúng 3 field TopBar icon khác,
KHÔNG dùng `Resources.Load` vì TopBar là scene tĩnh, gán qua `AssetDatabase.LoadAssetAtPath` như 10
icon kia — bài học đã rút ở `task-hero-list.md`).

## §6. Verify

- Đọc trực tiếp `Boot.unity` từ đĩa sau save (không tin "saved=True" suông): parse YAML thật, xác
  nhận cả 9 icon (`TowerButton`...`ArenaButton`) đúng `anchoredPosition.x`/`sizeDelta.x` tính toán ở
  §3; `ArenaButton` có đúng 4 component (`RectTransform`/`CanvasRenderer`/`Image`/`Button`, khớp cấu
  trúc `TowerButton`/`HeroListButton`); `TitleLabel` xuất hiện 2 lần trong scene (1 ở `TitleCanvas`
  — không đổi, 3 component; 1 ở `TopBar` — vừa thêm `Button`, 4 component).
- `PrefabUtility.LoadPrefabContents` verify `UI_ChapterProgress.prefab`: `RowListContainer.
  childCount=5`, mỗi `Row_0..4` có `Image` (root) + `NameLabel`/`ProgressLabel` (Text) + `ClaimButton`
  con, không còn `Row_5`, có `CloseButton` (Button) — khớp 100% những gì `ChapterProgressScreen.cs`
  cần.
- `validate_script` 0 lỗi cả 2 file (`ChapterProgressScreen.cs`, `MetaSceneInstaller.cs`).
- `AssetDatabase.Refresh()` + đọc console: không có lỗi compile nào (loại trừ lại đúng kiểu circular
  reference đã gặp ở task trước — lần này không tham chiếu ngược layer nào, chỉ dùng lại pattern
  `internal static` cùng assembly `Game.Meta`, không cần service mới).
- **637/637 test EditMode xanh** — không đổi khỏi baseline (không thêm test mới, cả 2 màn đều
  UI-orchestration thuần không có logic domain mới cần unit test).
- **KHÔNG test bằng Play mode thật** trong phiên này (giống lý do ở `task-splash-loading.md` §4 —
  Editor dùng chung với người dùng). Người dùng nên tự bấm: (1) icon Arena → Toast "Coming Soon";
  (2) bấm `TitleLabel` → ChapterProgressScreen mở, đúng 5 dòng, đóng lại bằng CloseButton.

## §7. Chưa làm / để dành

- Arena thật (matchmaking/PvP) — để nguyên placeholder, đúng plan.md ghi rõ v1.1.
- ChapterProgressScreen chưa có Landscape wiring riêng (`LayoutProfileSwitcher.
  ApplyStretchPanelLandscape` đã gọi đúng pattern nhưng chưa đo thật Portrait/Landscape như các màn
  ở `task-ui-vfx-polish.md` §5/§7 — nên làm nếu có đợt Landscape rollout tiếp theo).
- Chưa cho chọn chương để chơi lại (out of scope, xem §1).

# Task: HeroList — màn hình roster lọc/sắp xếp (plan.md §10.1)

Tiếp nối `task-defeat-screen.md` — hạng mục thứ 2 trong danh sách 23 màn hình còn thiếu, được người
dùng chỉ định làm tiếp. plan.md §10.1 chỉ ghi 1 dòng "Hero List | `HeroList` | Lọc/sắp xếp" — không
có đặc tả chi tiết nào khác, toàn bộ thiết kế bên dưới tự quyết định.

## §1. Phân biệt với 2 màn "gần giống" đã có

- **CodexScreen** (`Collection`) — liệt kê CẢ hero CHƯA sở hữu (hiện "???"), không lọc/sắp xếp,
  không bấm vào xem chi tiết được. Vai trò: bách khoa/khám phá.
- **TeamSelectScreen** (`PreBattle`) — chỉ hero ĐÃ sở hữu, nhưng mục đích là CHỌN 4 hero vào đội
  hình (kèm cả bảng gear), không lọc/sắp xếp.
- **HeroListScreen (mới)** — chỉ hero ĐÃ sở hữu, có lọc (theo Class) + sắp xếp (Level/Rarity/Name),
  bấm 1 dòng mở thẳng `HeroDetailScreen` (tái dùng màn có sẵn, không tự vẽ chi tiết). Vai trò: quản
  lý/duyệt roster nhanh.

## §2. Thiết kế (tự quyết định, không có số liệu từ plan.md)

- **Sắp xếp** — cycle qua 3 chế độ: Level (giảm dần) → Rarity (giảm dần, phụ theo Level) → Name
  (A-Z). Cùng mẫu "nút cycle" đã dùng cho Formation (TeamSelectScreen) và SwitchTab (CodexScreen).
- **Lọc** — cycle qua 7 trạng thái: ALL + 6 `HeroClass` (Vanguard/Slayer/Arcanist/Warden/Trickster/
  Summoner). Không lọc theo Element/Rarity (Class là trục lọc liên quan nhất tới xếp đội hình).
- **Phân trang** — 6/trang, giống hệt `CodexScreen` (PAGE_SIZE=6).
- **Icon mới** `icon_heroes` (bust đầu+vai đơn giản) — thêm vào `nav_icons.py` (đã có 9 icon, giờ
  10), phân biệt rõ với `icon_codex` (sách — bách khoa) dù cùng chủ đề "hero".

## §3. Implementation

- **`UI_HeroList.prefab`** — nhân bản `UI_Codex.prefab` (đã có Icon/pagination sẵn, gần giống nhất)
  rồi chỉnh: bỏ `SwitchTabButton` (Codex dùng đổi Hero/Enemy, không cần ở đây) thay bằng 2 nút
  `SortButton`/`FilterButton`; mỗi `Row_i` bỏ `ClaimButton` (không có hành động theo dòng) và thêm
  `Button` lên CHÍNH `Row_i` (bấm cả dòng mở chi tiết, khác Codex thuần đọc); `ProgressLabel` nới
  130→200px (tận dụng chỗ trống do bỏ `ClaimButton`, đủ cho chuỗi "Class · Element · Lv X ★Y").
  **Bug tự gây ra + tự sửa ngay khi verify**: tính vị trí `SortButton`/`FilterButton` lúc đầu dùng
  NHẦM số liệu `PrevButton`/`NextButton` (nhớ sai từ màn khác) → chồng lấn 50px cả 2 bên — phát hiện
  qua đo tay `anchoredPosition`/`sizeDelta` THẬT (không tin số nhớ), tính lại đúng theo ngân sách
  220px thật giữa Prev(right=-110)/Next(left=110), sửa còn cách nhau 8-12px dương.
- **`HeroListScreen.cs`** (`Game.Meta.HeroList`, thư mục riêng theo đúng quy ước Mail/Quest/Codex).
  Tái dùng `TeamSelectScreen.RarityColor` (đã `internal` từ task-ui-vfx-polish.md §4.6) thay vì bịa
  bảng màu rarity thứ 2. `EnsureDetailScreen()` mirror chính xác cách `TeamSelectScreen` mở
  `HeroDetailScreen` (add component lên chính GameObject, nghe `OnProfileChanged` để refresh lại
  list — sort Level/Rarity có thể đổi thứ tự sau khi Ascend/lên cấp).
- **TopBar +1 nút** (`HeroListButton`, thứ 10) — đo số liệu THẬT của TopBar trực tiếp trong
  `Boot.unity` đang load live (không đoán từ trí nhớ, TopBar đã bị sửa nhiều lần trong lịch sử dự
  án) trước khi thêm: 7 nút icon hiện tại kết thúc ở Quest[520,564], còn dư khoảng trống thật tới
  Wallet ở canvas tham chiếu 960 — đặt `HeroListButton` tại x=570 (gap 6px, đúng khuôn 6 nút kia).
  **Lỗi tự gây ra + tự sửa**: gán icon qua `Resources.Load` (đúng kỹ thuật Battle HUD vừa dùng ở
  task-ui-vfx-polish.md §6) nhưng TopBar là SCENE TĨNH — icon phải gán qua
  `AssetDatabase.LoadAssetAtPath` (serialize trực tiếp vào scene, giống 9 nút kia), không phải
  `Resources.Load` (chỉ cần cho màn CODE-DỰNG runtime như Battle HUD) — `Resources.Load` trả về
  null vì `Art/UI/Icons/Nav/` không nằm trong thư mục `Resources/` nào. Phát hiện ngay (`iconLoaded=
  False` khi verify), sửa lại đúng cách, verify lại bằng cách đọc GUID thật trong `Boot.unity` trên
  đĩa (đúng bài học "không tin log 'saved=True' suông").

## §4. Verify (đo thật, không chỉ tin compile)

- Dựng `HeroListScreen` thật qua reflection với `LocalPlayerRepository.CreateNew()` (profile thật,
  6 hero khởi đầu — không phải 24, đó là kích thước CATALOG chứ không phải roster ban đầu).
- Sort mặc định (Level) → đổi qua Name (gọi `CycleSort()` 2 lần) → đúng thứ tự A-Z thật
  (Bone Caller, Dawn Cleric, Ember Knight, Frost Sage, Gale Thief, Shadow Fang).
- Filter → Vanguard: đúng còn 1/6 dòng active (Ember Knight), status text đúng "1 heroes · Page
  1/1", 5 dòng còn lại `active=False`.
- Bấm dòng đầu (`OpenDetail(0)`): `HeroDetailScreen` được add + mở đúng, `title='EMBER KNIGHT'`.
- TopBar 10 nút: đo lại toàn bộ span thật sau khi thêm — gap giữa mọi cặp nút liền kề đều 6px
  dương, cách `WalletLabel` 30px — không chồng lấn.
- `validate_script` 0 lỗi cả 2 file (`HeroListScreen.cs`/`MetaSceneInstaller.cs`), `refresh_unity`
  force+compile 0 lỗi console, **637/637 test xanh** (không đổi khỏi baseline — màn mới hoàn toàn
  không có test EditMode riêng, đúng mẫu mọi UI Meta khác trong dự án — chỉ verify qua reflection).

## §5. Chưa làm (để dành)

- Landscape đã wire qua `ApplyStretchPanelLandscape` (đúng khuôn 10 màn khác) nhưng CHƯA verify
  world-space riêng cho màn này (theo chuẩn §8/§9 của task-ui-vfx-polish.md) — nên làm nếu có lượt
  "verify landscape cho màn còn lại" tiếp theo.
- Sort/Filter hiện chỉ lọc theo Class — chưa có lọc theo Element/Rarity nếu người dùng cần sau này.

# Task: Codex/Collection screen

Yêu cầu: hạng mục được chọn qua `AskUserQuestion` ("Codex/Collection screen (Recommended)") trong
số 3 gap thật đã khảo sát (Consumable items / Codex / per-chapter loot table). Theo quy trình
chuẩn: viết xong task file này rồi mới chạm code.

## §0. Findings

- **plan.md chỉ có 1 dòng cho Collection**: `| Collection | \`Collection\` | Codex hero/enemy/item |`
  — không có spec chi tiết, giống hệt tình huống Mail/Event gặp trước đó trong session. Tự thiết
  kế tối thiểu trong phạm vi task này.
- **"item" trong "Codex hero/enemy/item" KHÔNG làm được** — hệ thống vật phẩm tiêu hao
  (`InventoryDto.Items`) hoàn toàn chưa xây (không mua được, không mang được, không dùng được
  trong trận — đây chính là lựa chọn KHÔNG được chọn trong `AskUserQuestion` lần này). Codex cho
  1 hệ thống không tồn tại là vô nghĩa — chỉ làm hero + enemy, ghi rõ item ngoài phạm vi cho tới
  khi hệ vật phẩm được xây (task riêng).
- **Không có tracking "đã gặp" (encountered) cho enemy** — grep không ra field nào kiểu
  `SeenEnemies`/`EncounteredIds`. Thêm tracking mới là việc riêng (phải hook vào mọi nơi spawn
  enemy). Dùng lại đúng dữ liệu tiến trình đã có: **enemy unlock nếu `enemyDef.Chapter <=
  profile.Progress.ChapterUnlocked`** (đã tới chương đó nghĩa là đã/sắp gặp) — không bịa field
  theo dõi mới, tái dùng field thật đã có.
- **Hero unlock = có trong `profile.Heroes`** (sở hữu thật qua gacha/starter) — không cần field
  mới.
- **Catalog load**: `Resources.LoadAll<HeroDefinitionSO>("Data/Heroes")` đã có tiền lệ
  (`GachaSystem.HeroPool`). `Resources.LoadAll<EnemyDefinitionSO>("Data/Enemies")` CHƯA từng dùng
  ở đâu (chỉ có `Resources.Load` theo từng defId lẻ trong `BattleSceneInstaller`) — lần đầu load
  cả catalog enemy, cần verify không lỗi (66 file, đã xác nhận đủ qua object-map.md §12).
- **`HeroDisplayUtil.FormatName`/`FormatSkillName`** đã có sẵn (dùng chung `FormatId` private với
  prefix khác nhau) — chỉ cần thêm 1 hàm `FormatEnemyName` cùng khuôn (prefix "enemy_") thay vì
  viết lại logic định dạng tên.
- **Phát hiện quan trọng về UI pattern hiện có — KHÔNG có scroll/pagination ở bất kỳ đâu trong dự
  án** (`grep ScrollRect` ra 0 kết quả toàn bộ codebase). `TeamSelectScreen`'s `HeroListContainer`
  (danh sách hero) tự lay-out từng row theo `anchoredPosition.y = -i * rowH` KHÔNG có Mask/
  ScrollRect nào che — với 24 hero thật (roster hiện tại), danh sách này **tràn khỏi khung nhìn
  hoàn toàn không kiểm soát**, xác nhận bằng grep `RectMask2D|UI.Mask` ra 0 trên
  `UI_TeamSelect.prefab`. Đây là 1 bug UI CÓ THẬT, tồn tại từ trước, **KHÔNG thuộc phạm vi task
  này** (sửa nó cần thêm hạ tầng ScrollRect/Mask cho 1 màn khác, việc riêng) — ghi nhận rồi báo
  người dùng qua `spawn_task`, không tự tiện sửa luôn (tránh phình phạm vi).
  → Hệ quả cho CHÍNH task Codex: **66 enemy không thể liệt kê hết trong 1 khung cố định như mọi
  màn khác đang làm** (Quest/Mail dùng đúng 6 row cố định, không paginate vì luôn đúng 6 mục).
  Codex phải có **phân trang (Prev/Next)** — khác mọi screen trước đó trong session, lần đầu cần
  thêm cơ chế điều hướng nhiều trang. Chọn PAGE_SIZE=6 (khớp `ROW_COUNT` sẵn có của khuôn Quest,
  không cần thêm/bớt row nào) → 24 hero = 4 trang, 66 enemy = 11 trang.
- **Hình học `UI_Quest.prefab` (Panel 620×420, đo thật qua execute_code)** — xác định được các
  vùng trống an toàn để thêm control mới: dải trống ngang y≈[127,153] bên phải `WalletLabel`
  (WalletLabel dừng ở x=-60, panel rộng tới x=310) đủ chỗ cho 1 nút chuyển tab; dải trống
  y∈[-176,-65] ngay dưới `RowListContainer` (trước `CloseButton`) đủ chỗ cho 2 nút Prev/Next.
  Không cần đụng tới Title/RowListContainer/CloseButton hiện có.

## §1. Scope decision

**Trong phạm vi:**
1. `Meta/Codex/CodexSystem.cs` MỚI (pure static, test được) — cache catalog Hero/Enemy
   (`Resources.LoadAll`, load 1 lần), `IsHeroUnlocked(profile, def)`, `IsEnemyUnlocked(profile,
   def)`.
2. `HeroDisplayUtil.cs`: thêm `FormatEnemyName(defId)`.
3. `UI_Codex.prefab` MỚI (clone `UI_Quest.prefab`) — thêm 3 nút mới (`SwitchTabButton` chuyển
   Hero/Enemy, `PrevButton`/`NextButton` phân trang) vào 2 vùng trống đã đo ở §0. Repurpose
   `WalletLabel` → nhãn trạng thái ("HEROES · Page 1/4"), `ProgressLabel` mỗi row → tóm tắt
   class/element (hero) hoặc archetype/element/chương (enemy) hoặc "LOCKED". Ẩn `ClaimButton` mỗi
   row bằng code (Codex không có hành động claim nào) — không xoá khỏi prefab, chỉ
   `SetActive(false)` lúc `BuildShell()`.
4. `Meta/Codex/CodexScreen.cs` MỚI (mirror `QuestScreen.cs` + thêm state tab/trang).
5. `MetaSceneInstaller.cs`: `_codexScreen` field + nút mở trên TopBar (đặt trong khoảng trống
   210px giữa `QuestButton`/`DungeonButton` — CÙNG khoảng trống `MailButton` đã dùng ở
   task-mail.md, vẫn còn dư chỗ: `QuestButton` trái=-325, `MailButton` chiếm [-475,-385],
   `DungeonButton` phải=-535 → còn khoảng trống [-535,-475] = 60px và [-385,-325]=60px hai bên
   MailButton, đủ cho 1 nút 50-55px rộng ở 1 trong 2 khe. Đo lại thật bằng execute_code trước khi
   đặt, không giả định).
6. Test (`Assets/Tests/EditMode/Meta/`): `CodexSystemTests.cs` — `IsHeroUnlocked`/
   `IsEnemyUnlocked` đúng theo profile thật.

**Ngoài phạm vi (cố ý, ghi rõ):**
- KHÔNG làm Codex cho "item" (vật phẩm tiêu hao) — hệ thống nguồn chưa tồn tại, xem §0.
- KHÔNG thêm tracking "đã gặp" enemy thật (dùng proxy `Chapter <= ChapterUnlocked` thay thế).
- KHÔNG sửa lỗi tràn danh sách của `TeamSelectScreen.HeroListContainer` — bug có thật, phát hiện
  ngoài ý muốn, báo riêng qua `spawn_task` thay vì tự sửa trong task này.
- KHÔNG hiện chi tiết đầy đủ (full stat sheet/skill list) khi bấm vào 1 entry — chỉ liệt kê tóm
  tắt trong danh sách phân trang, giống mức độ chi tiết `QuestScreen` hiện có, không mở thêm 1
  màn con "Codex Detail" (việc đó lớn hơn, để lần sau nếu cần).

## §2. Implementation checklist

- [x] `Meta/Codex/CodexSystem.cs`: cache catalog (`Resources.LoadAll`, lazy static giống
      `EquipmentService.Catalog`), `IsHeroUnlocked`, `IsEnemyUnlocked`.
- [x] `HeroDisplayUtil.cs`: `FormatEnemyName` — lưu ý cosmetic nhỏ: 6/66 enemy dùng prefix
      `boss_` thay vì `enemy_` (VD `boss_alpha_wolf`), `FormatEnemyName` không strip được prefix
      này nên hiện "Boss Alpha Wolf" thay vì "Alpha Wolf" — chấp nhận được (thực ra còn có ích,
      tự gắn nhãn "Boss"), không đáng thêm logic dual-prefix cho 6/66 trường hợp.
- [x] Đo thật vị trí trống trên TopBar qua `execute_code` (đọc `RectTransform` mọi nút) — tìm ra
      2 khe 60px hai bên `MailButton` (giữa `DungeonButton`/`MailButton` và giữa `MailButton`/
      `QuestButton`), đặt `CodexButton` (rộng 50, hẹp hơn mọi nút khác) vào khe trái
      `[-530,-480]`, còn dư khe phải `[-385,-325]` cho nút tương lai nếu cần.
- [x] `UI_Codex.prefab`: clone `UI_Quest.prefab`, thêm `SwitchTabButton`/`PrevButton`/
      `NextButton` (clone `CloseButton` × 3 qua `open_prefab_stage`+`manage_gameobject duplicate`,
      KHÔNG dùng `create_child` từ đầu — nhanh và an toàn hơn vì thừa hưởng đúng Image+Button+
      Label component có sẵn). **Phát hiện lúc implement**: hand-math tính vùng trống ban đầu SAI
      (nhầm giữa "panel-center-relative" và "bottom-edge-relative", đặt 3 nút mới đè lên
      `RowListContainer` — phát hiện được NHỜ verify bằng `RectTransform.GetWorldCorners()` thay
      vì tin vào tính tay) — sửa bằng cách đổi anchor 3 nút sang cùng kiểu top-left (0,1) như
      `WalletLabel`/`RowListContainer` rồi dò `pos.y` bằng thực nghiệm (đọc world corner sau mỗi
      lần chỉnh) thay vì tính tay tiếp — bài học: LUÔN verify layout bằng world corner thật, không
      tin phép tính tay dù đã cẩn thận.
- [x] `CodexScreen.cs`: mirror `QuestScreen.cs`, thêm state tab (Hero/Enemy) + trang hiện tại,
      `Refresh()` đọc `CodexSystem` + phân trang PAGE_SIZE=6. Ẩn `ClaimButton` mỗi row lúc
      `BuildShell()` (Codex không có hành động nào theo dòng).
- [x] `MetaSceneInstaller.cs`: field + bind + wire `CodexButton`/`CodexScreen` — `onClosed=null`
      vì Codex thuần đọc, không đổi profile, không cần Save/refresh sau khi đóng (khác mọi
      screen khác).
- [x] `CodexSystemTests.cs` (7 test) — dùng catalog THẬT (không mock được `Resources.LoadAll`,
      cùng cách `EquipmentGeneratorTests` đã làm).
- [x] Chạy full EditMode suite — **387/387 xanh** (380 cũ + 7 test mới).
- [x] Play-mode smoke check THẬT: bấm `CodexButton` thật, xác nhận tab HERO hiện "Page 1/4"
      (24 hero / 6 mỗi trang), bấm Next → Page 2/4 đổi nội dung đúng; chuyển `SwitchTabButton` →
      ENEMIES "Page 1/11" (66 enemy / 6); bấm Next liên tục 10 lần tới "Page 11/11", xác nhận
      `NextButton` hết interactable đúng biên cuối, `PrevButton` bật lại đúng; bấm Prev lùi về
      Page 10/11 đúng; đóng modal đúng. Không lỗi console suốt quá trình. Save dev hiện tại sở hữu
      hầu hết/toàn bộ hero + tiến trình xa (ChapterUnlocked cao) nên không quan sát được dòng
      "LOCKED" thật trong lượt test này — logic `IsHeroUnlocked`/`IsEnemyUnlocked` đã có test
      EditMode riêng phủ đủ 2 nhánh unlocked/locked, không cần ép thêm qua Play mode.
- [x] `spawn_task` báo riêng bug tràn `HeroListContainer` phát hiện ở §0 (không tự sửa) — task
      "Fix TeamSelectScreen hero list overflow with 24 heroes" (task_26720454).
- [x] Cập nhật `roadmap.md` §0.1 (P6) và `object-map.md` §12/§12.1.

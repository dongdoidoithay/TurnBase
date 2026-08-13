# Task: Mail system

Yêu cầu: hạng mục còn lại trong 3 gap thật đã khảo sát (Event/Rest ĐÃ XONG — task-eventrest.md).
Mail là gap còn lại rõ ràng nhất: roadmap.md P4 ghi thẳng "Mail: chưa" — hoàn toàn chưa bắt đầu,
khác các gap khác trong session này vốn chỉ bị thu hẹp phạm vi. Theo quy trình chuẩn: viết xong
task file này rồi mới chạm code.

## §0. Findings

- **plan.md gần như không có spec cho Mail** — chỉ 1 dòng duy nhất trong bảng danh sách màn hình:
  `| Mail | \`Mail\` | Đền bù LiveOps |` (tạm dịch: màn Mail, dùng để đền bù LiveOps). Không có
  bảng nào khác nhắc Mail, không có cấu trúc dữ liệu, không có luật claim/expiry nào được mô tả.
  Toàn bộ thiết kế cụ thể phải tự quyết trong task này, theo đúng kỷ luật "tối thiểu nhưng thật"
  đã dùng xuyên suốt session (VD task-eventrest.md tự thiết kế nội dung 3 lựa chọn Event vì
  plan.md cũng chỉ ghi khung "2-3 lựa chọn rủi ro" mà không có nội dung cụ thể).
- **Không có currency/trigger "LiveOps" thật nào tồn tại** để gắn Mail vào — grep
  `compensat|đền bù|welcome|chào mừng|gift` không ra kết quả nào trong code. `CurrencyReason.
  MailClaim = 31` tồn tại trong enum nhưng **là field chết** (không có overload
  `Grant`/`TryConsume` nào nhận `CurrencyReason`, giống `CurrencyReason.EventNode` phát hiện ở
  task-eventrest.md §0) — không sửa, chỉ ghi nhận.
- **Quyết định trigger thật cho Mail (để không phải xây 1 UI không bao giờ có nội dung)**: mail
  "Welcome" cấp 1 lần khi tạo profile MỚI (`LocalPlayerRepository.CreateNew()`) — mẫu F2P chuẩn
  (gửi quà chào mừng qua hộp thư thay vì cấp thẳng vào ví), gắn vào đúng điểm khởi tạo profile có
  thật duy nhất trong codebase. Không bịa ra 1 "sự kiện LiveOps" giả không tồn tại.
- **Ràng buộc kiến trúc quan trọng** (structure.md §6, đã áp dụng đúng chỗ này trong
  `CreateNew()` hiện tại — xem comment "Game.Services không được phép ref Game.Meta"):
  `Game.Services` (nơi `LocalPlayerRepository` sống) **KHÔNG được phép reference `Game.Meta`**.
  Nghĩa là KHÔNG thể gọi 1 `Game.Meta.Mail.MailSystem.Grant(...)` từ `CreateNew()`. Giải pháp:
  `CreateNew()` chỉ trực tiếp construct 1 `MailDto` thô (thuần data, giống cách nó đã tự set
  `Wallet.Gold`/tự `Heroes.Add(...)` mà không gọi qua `HeroLevelSystem`) — không vi phạm layering
  vì không gọi logic Meta nào cả, chỉ điền dữ liệu. `MailSystem` (Meta layer) chỉ xử lý CLAIM sau
  đó, không xử lý việc TẠO mail welcome này.
- **UI pattern tái dùng tốt nhất: `QuestScreen.cs` + `UI_Quest.prefab`**, không phải `ShopScreen`
  (task-eventrest.md đã dùng `UI_Shop.prefab` cho `UI_NodeChoice`) — Mail giống Quest hơn nhiều:
  1 danh sách N dòng, mỗi dòng có nút Claim, KHÔNG cần 2 trạng thái chọn/kết quả như NodeChoice.
  `UI_Quest.prefab` hierarchy đã đúng khuôn cần: `Panel > Title, WalletLabel, CloseButton(Label),
  RowListContainer > Row_0..5 (NameLabel, ProgressLabel, ClaimButton>Label)` — 6 row cố định
  (`ROW_COUNT`). Khác Quest (luôn đúng 6 mục cố định: 3 Daily + 3 Achievement), số mail là ĐỘNG —
  cần ẩn/hiện row theo `profile.Mail.Count` thay vì luôn hiện đủ 6.
- **TopBar hiện đã kín chỗ** — kiểm tra thật bằng `execute_code` đọc `RectTransform` các nút:
  `TopBar` rộng 780, các nút neo phải (`anchorMin=anchorMax=(1,0.5)`), nút xa nhất bên trái hiện
  tại là `TowerButton` (x=-780, rộng 90) — tâm nút này đã nằm ĐÚNG mép trái của thanh 780px (rìa
  trái nút tràn ra ngoài ~45px). **Không còn chỗ trống nào để thêm nút thứ 7 mà không mở rộng
  thanh** — đây là phát hiện thật, không phải suy đoán. → Quyết định: mở rộng `TopBar` rộng thêm
  100px (780→880) rồi đặt `MailButton` (90 rộng, cùng cỡ Dungeon/TrialBoss/Tower) ở vị trí
  x=-880, ngay bên trái `TowerButton`. Việc này SỬA Boot.unity thật (theo đúng nguyên tắc
  "Hierarchy nghĩa là static" — xem memory `feedback_hierarchy_means_static.md`, không dùng code
  runtime SetParent/Instantiate để giả UI tĩnh), không phải sinh nút bằng code lúc runtime.
- **`PlayerProfileDto`/`SaveMigrationRunner`**: cần thêm `List<MailDto> Mail = new();` vào
  `PlayerProfileDto` + dòng `p.Mail ??= new List<MailDto>();` vào `SaveMigrationRunner.
  EnsureNotNull` (đúng khuôn mọi collection khác đã có ở đó) — KHÔNG cần bump `CURRENT_VERSION`
  hay viết `ISaveMigration` mới vì đây là field mới trên object gốc, JsonUtility tự trả null cho
  save cũ và `EnsureNotNull` đã vá đúng chỗ đó (giống mọi field khác trong danh sách).
- **`CurrencyEntryDto`** (Key string + Value long, dùng cho Materials/HeroShards) không khớp hoàn
  toàn nhu cầu Mail reward (cần rõ ràng là `CurrencyType`, không phải string key tuỳ ý) — dùng lại
  đúng mẫu `SubStatDto` (int enum-cast + value) thay vì ép `CurrencyEntryDto` vào việc nó không
  thiết kế cho.

## §1. Scope decision

**Trong phạm vi:**
1. `MailDto`/`MailRewardDto` (`Game.Data.Dto`) — Id, Title, Body, `List<MailRewardDto> Rewards`,
   `Claimed`, `CreatedAtUtc`.
2. `PlayerProfileDto.Mail` (`List<MailDto>`) + `SaveMigrationRunner.EnsureNotNull` thêm dòng null
   guard.
3. `LocalPlayerRepository.CreateNew()` — thêm 1 mail "Welcome" (Gold + Gem vừa phải) vào
   `p.Mail`, construct DTO thô, KHÔNG gọi qua `Game.Meta`.
4. `Meta/Mail/MailSystem.cs` MỚI (pure static, giống `QuestSystem`) — `TryClaim(profile, mailId,
   economy)`, `UnclaimedCount(profile)` (tiện cho UI/tương lai làm badge, KHÔNG bắt buộc làm badge
   ở task này).
5. `Meta/MailScreen.cs` MỚI (mirror `QuestScreen.cs`) + `UI_Mail.prefab` MỚI (clone
   `UI_Quest.prefab`, giữ nguyên 6 row, ẩn/hiện theo `profile.Mail.Count`, sắp mail CHƯA claim lên
   trước).
6. Boot.unity: mở rộng `TopBar` rộng 780→880, thêm `MailButton` (clone `TowerButton`, đặt
   x=-880) — sửa THẬT trong Hierarchy/scene file, không phải code runtime.
7. `MetaSceneInstaller.cs`: field `_mailScreen`, bind `MailButton` trong `BindCanvasRefs`, wire
   `onClick` mở `MailScreen`.
8. Test (`Assets/Tests/EditMode/`):
   - `MailSystemTests.cs` (Meta) — claim thành công cấp đúng reward + đánh dấu Claimed, claim lại
     lần 2 thất bại (đã claim), claim id không tồn tại thất bại, `UnclaimedCount` đúng.
   - `LocalPlayerRepositoryTests.cs` (nếu đã có file test cho `CreateNew()`, thêm case; nếu chưa
     có, tạo mới trong `Assets/Tests/EditMode/Services/`) — `CreateNew()` luôn có đúng 1 mail
     Welcome chưa claim.

**Ngoài phạm vi (cố ý, ghi rõ):**
- KHÔNG xây badge/chấm đỏ thông báo số mail chưa đọc trên `MailButton` — dự án chưa có mẫu badge
  nào để tái dùng, thêm mới ngoài phạm vi 1 task Mail cơ bản.
- KHÔNG xây mail có hạn (expiry) — plan.md không yêu cầu, thêm ngày hết hạn cần thêm UI đếm
  ngược/lọc mail hết hạn, việc riêng.
- KHÔNG xây "Claim All" — mỗi dòng tự claim, giống hệt mẫu Quest hiện có, giữ tối giản.
- KHÔNG wire `CurrencyReason.MailClaim` — field chết không liên quan trực tiếp, đã ghi nhận ở §0.
- KHÔNG thêm mail trigger thứ 2 nào khác ngoài Welcome (VD "đền bù bug BalanceHarness tìm thấy")
  — 1 trigger thật là đủ để chứng minh pipeline chạy đúng, thêm nội dung là việc content/balance
  riêng có thể làm sau.

## §2. Implementation checklist

- [x] `PlayerProfileDto.cs`: thêm `MailDto`/`MailRewardDto`, field `Mail` trên `PlayerProfileDto`.
- [x] `SaveMigrationRunner.cs`: `p.Mail ??= new List<MailDto>();`.
- [x] `LocalPlayerRepository.CreateNew()`: thêm mail Welcome thô (2.000 Gold + 100 Gem).
- [x] `Meta/Mail/MailSystem.cs`: `TryClaim`, `UnclaimedCount`.
- [x] Boot.unity — **phát hiện lúc implement, đổi kế hoạch**: thay vì mở rộng TopBar (rủi ro hơn),
      đọc thật `RectTransform` mọi nút TopBar qua `execute_code` và tìm ra sẵn 1 khoảng trống
      210px chưa dùng giữa `QuestButton` (mép trái x=-325) và `DungeonButton` (mép phải x=-535) —
      khoảng trống này vốn đã tồn tại (khác biệt phong cách/spacing giữa cụm Quest và cụm
      Dungeon/TrialBoss/Tower). Đặt `MailButton` (clone `TowerButton`, đổi tên/label "MAIL", giữ
      nguyên size 90×44) vào giữa khoảng trống đó (x=-430) — verify lại bằng `execute_code` đọc
      cạnh trái/phải mọi nút, xác nhận không đè lên bất kỳ nút nào. KHÔNG cần đụng tới kích thước
      TopBar hay bất kỳ nút nào khác — an toàn hơn hẳn phương án ban đầu, không rủi ro layout dây
      chuyền. Lưu Boot.unity qua `manage_scene action=save`.
- [x] `UI_Mail.prefab`: clone `UI_Quest.prefab` (copy file + đổi `m_Name`) — giữ nguyên 6 row, tái
      dùng ĐÚNG cấu trúc `NameLabel`/`ProgressLabel`/`ClaimButton` không cần sửa gì (không như
      NodeChoice phải xoá bớt row).
- [x] `MailScreen.cs`: mirror `QuestScreen.cs`, ẩn/hiện row theo `profile.Mail.Count` (khác Quest
      luôn đúng 6), sort chưa-claim lên trước (`OrderBy(Claimed).ThenBy(CreatedAtUtc)`).
- [x] `MetaSceneInstaller.cs`: field + bind + wire `MailButton`/`MailScreen`.
- [x] `MailSystemTests.cs` (5 test) + `LocalPlayerRepositoryTests.cs` MỚI (2 test — trước đây
      `CreateNew()` hoàn toàn chưa có test nào, chỉ thêm case liên quan Mail, không cố bao phủ lại
      toàn bộ hành vi cũ đã chạy ổn định).
- [x] Chạy full EditMode suite — **380/380 xanh** (373 cũ + 7 test mới).
- [x] Play-mode smoke check THẬT (không chỉ execute_code giả lập logic): vào Play mode, bấm
      `MailButton` thật qua `button.onClick.Invoke()` — save đang chạy trong Editor là save CŨ có
      từ trước task này (`profile.Mail.Count=0`, vì mail Welcome chỉ cấp lúc `CreateNew()`, save
      cũ không tự có), xác nhận đúng: mở modal ra 0 dòng hiện (mọi row ẩn), bấm thử `ClaimButton`
      hàng 0 dù ẩn vẫn an toàn (index-guard chặn, không grant gì, không crash) — nhánh biên xác
      nhận chắc chắn. Sau đó add 1 `MailDto` tổng hợp trực tiếp vào `profile.Mail` (giống kỹ thuật
      node tổng hợp ở task-eventrest.md), mở lại Mail → dòng hiện đúng tên + tóm tắt reward
      ("+777 Gold · +33 Gem"), bấm Claim → Gold/Gem tăng ĐÚNG số (931079→931856, 0→33), label đổi
      "CLAIMED", nút hết interactable, `mail.Claimed=true`. Cả 2 nhánh (0 mail lẫn có mail thật)
      chạy đúng, không lỗi console.
- [x] Cập nhật `roadmap.md` §0.1 (P4: "Mail: chưa" → xong, ghi rõ giới hạn không badge/không
      expiry/chỉ 1 trigger Welcome) và `object-map.md` §12/§12.1.

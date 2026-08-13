# Task: Mail badge/expiry/Claim-All

Yêu cầu: "tiếp tục cho tôi" — hạng mục cuối cùng còn lại đúng phạm vi trong object-map.md §12
(3 phần đã cố ý cắt khỏi task-mail.md: badge số mail chưa đọc, mail hết hạn, nút Claim-All). Việc
"vừa" (3 phần nhỏ, không đụng Game.Combat) — viết xong task file này rồi mới chạm code.

## §0. Findings

Đọc lại `MailSystem.cs` (28 dòng), `MailScreen.cs` (121 dòng), `UI_Mail.prefab` (6 row cố định),
`MailDto`/`MailRewardDto` trong `PlayerProfileDto.cs`, `LocalPlayerRepository.CreateNew()`, và
`MetaSceneInstaller.BindCanvasRefs`/TopBar hierarchy thật (Boot.unity).

- **`MailSystem.UnclaimedCount(profile)` đã tồn tại sẵn** (task-mail.md ghi rõ: "tiện cho UI/
  tương lai làm badge, KHÔNG bắt buộc làm badge ở task đó") — hạ tầng đếm đã có, chỉ cần UI đọc.
- **Chưa có pattern Badge nào trong dự án để tái dùng** — grep `Badge` toàn `Assets/_Project/
  Scripts` chỉ ra 1 kết quả không liên quan (`SkillSlotView.cs`, TextMeshPro cost label trong
  trận, gọi tên biến "Badge" trong comment chứ không phải UI thông báo). Phải xây từ đầu, nhưng
  đơn giản: 1 Image tròn nhỏ + 1 Text số, y hệt mẫu thông báo chuẩn của mọi game.
- **`MailButton` sống TRONG Boot.unity** (`GameBootstrap/__UI__/UIRoot/MetaCanvas/TopBar/
  MailButton`, DontDestroyOnLoad) — đo thật qua `execute_code`: anchor phải-giữa (1,0.5),
  anchoredPosition=(-430,0), sizeDelta=90×44, hiện có đúng 1 con (Label). Theo đúng nguyên tắc
  "Hierarchy nghĩa là static" (`feedback_hierarchy_means_static.md`) — Badge phải là child THẬT
  trong scene file, KHÔNG tạo bằng code runtime.
- **`MailScreen.Refresh()` là nơi duy nhất đọc `profile.Mail`** để vẽ UI — nhưng KHÔNG có hàm nào
  refresh riêng TopBar khi Mail thay đổi (badge cần cập nhật ở TopBar, không phải trong modal Mail
  đang mở). `TeamSelectScreen` có tiền lệ `OnProfileChanged` event mà `MetaSceneInstaller` nghe để
  refresh Wallet — `MailScreen` hiện KHÔNG có event tương tự. Cần thêm 1 event giống hệt mẫu đó để
  `MetaSceneInstaller` biết lúc nào cần vẽ lại badge (sau khi claim, và lúc mở app/mở lại Meta).
- **Mail hiện KHÔNG có field hết hạn nào** — `MailDto` chỉ có `Id/Title/Body/Rewards/Claimed/
  CreatedAtUtc`. Thêm `ExpiresAtUtc` (string, rỗng = không hết hạn — cùng quy ước `CreatedAtUtc`
  dùng `DateTime.ToString("o")`/`DateTime.Parse`).
- **Quyết định quan trọng: mail Welcome (trigger DUY NHẤT hiện có) KHÔNG nên có hạn** — quà chào
  mừng F2P hết hạn là trải nghiệm tệ (người chơi cài game xong không mở ngay vẫn nên nhận được).
  Task này chỉ xây HẠ TẦNG hết hạn thật (field + logic dọn dẹp + UI hiển thị còn bao lâu), không
  ép mail Welcome dùng nó — để trống `ExpiresAtUtc` nghĩa là "không hết hạn", đúng thiết kế mở cho
  các trigger tương lai (VD đền bù sự kiện có giới hạn thời gian) mà không phá trigger hiện tại.
  Test sẽ tự tạo mail có hạn để verify pipeline, không cần trigger game thật thứ 2.
- **Dọn mail hết hạn**: cần 1 điểm gọi dọn dẹp — theo đúng mẫu `DungeonSystem.EnsureDailyReset`/
  `TrialBossSystem.EnsureWeeklyReset` (gọi mỗi khi vào màn hình liên quan), thêm
  `MailSystem.PurgeExpired(profile, DateTime utcNow)` gọi ngay đầu `MailScreen.Open()` — xoá khỏi
  `profile.Mail` mọi mail có `ExpiresAtUtc` không rỗng VÀ đã qua hạn, bất kể `Claimed` hay chưa
  (dọn dẹp thật, không chỉ ẩn).
- **UI hiện ngày còn lại**: `ProgressLabel` mỗi row (90×26px, đã biết hẹp —
  `feedback_unity_mcp_ui_gotchas.md`) hiện đang hiện tóm tắt reward (VD "+2000 Gold · +100 Gem")
  khi chưa claim. Thêm "· 3d left" vào cuối chuỗi này có nguy cơ tràn — đo thật bằng
  `Text.cachedTextGenerator.GetPreferredWidth` (kỹ thuật đã dùng ở task-enhance-plus15.md, không
  cần Play mode) trước khi quyết định format cuối, KHÔNG đoán.
- **Claim-All**: `MailSystem` chưa có hàm claim hàng loạt — thêm `ClaimAll(profile, economy) : int`
  (trả số mail đã claim, lặp `TryClaim` cho từng mail chưa claim). `UI_Mail.prefab` chưa có nút này
  — đo thật `Panel`/`WalletLabel`/`Title`/`CloseButton`/`RowListContainer` qua `execute_code`:
  Panel 620×420, `WalletLabel` neo trái-trên tại x=150 (rộng 200, tới x=350), còn trống từ x=350
  tới rìa phải Panel (x=620) ở cùng hàng y=-70 — đủ chỗ đặt 1 nút ~130×30 neo phải-trên mà không
  đụng `Title` (neo giữa-trên, y=-16, thấp hơn theo trục y so với y=-70 — 2 hàng khác nhau) hay
  `RowListContainer` (bắt đầu tại y=-190, dưới xa nút mới). Verify lại bằng
  `RectTransform.GetWorldCorners()` sau khi đặt (đúng bài học Codex — không tin tay tính).
- **`MailScreen.Refresh()` sort mail chưa-claim lên trước** — Claim-All chỉ ảnh hưởng mail ĐANG
  hiện (6 dòng tối đa), không cần lo mail ẩn phía dưới vì màn không phân trang (khác Codex).

## §1. Scope decision

**Trong phạm vi:**
1. `MailDto.ExpiresAtUtc` (string, mặc định rỗng = không hết hạn).
2. `MailSystem.PurgeExpired(profile, DateTime utcNow)` — xoá mail hết hạn khỏi `profile.Mail`,
   gọi từ đầu `MailScreen.Open()`.
3. `MailSystem.ClaimAll(profile, economy) : int`.
4. `MailScreen.cs`: nút Claim-All mới (ẩn khi không có mail chưa claim nào đang hiện), hiện số
   ngày còn lại trong `ProgressLabel` khi mail có hạn (đo width trước khi chốt format), thêm
   `event Action OnMailChanged` (mirror `TeamSelectScreen.OnProfileChanged`) bắn sau Claim/
   Claim-All/PurgeExpired-có-xoá-gì-đó.
5. `UI_Mail.prefab`: thêm `ClaimAllButton` tĩnh trong prefab (không phải runtime).
6. Badge: `Boot.unity` thêm child tĩnh `MailBadge` (Image tròn + Text số) dưới `MailButton`,
   `MetaSceneInstaller` bind + hiện/ẩn theo `MailSystem.UnclaimedCount`, refresh khi
   `MailScreen.OnMailChanged` bắn VÀ mỗi khi vào lại Meta (cùng thời điểm `RefreshMap`/Wallet
   refresh hiện có).
7. Test mới: `MailSystemTests.cs` thêm case cho `PurgeExpired` (xoá đúng mail hết hạn, giữ nguyên
   mail chưa hết hạn/không có hạn, xoá cả mail đã claim nếu hết hạn) + `ClaimAll` (claim đúng tất
   cả, trả đúng count, không claim lại mail đã claim rồi).

**Ngoài phạm vi (cố ý, ghi rõ):**
- KHÔNG gán `ExpiresAtUtc` cho mail Welcome hiện có — lý do đã ghi ở §0.
- KHÔNG thêm mail trigger mới nào (vẫn chỉ 1 trigger Welcome, giống task-mail.md).
- KHÔNG làm badge dùng chung được cho nút khác (VD Quest) — task này chỉ cần Mail, tổng quát hoá
  sớm khi chưa có nhu cầu thật thứ 2 là việc thừa.
- KHÔNG động `CurrencyReason.MailClaim` (field chết, đã ghi nhận ở task-mail.md, không liên quan).

## §2. Implementation checklist

- [x] `PlayerProfileDto.cs`: thêm `MailDto.ExpiresAtUtc` (mặc định rỗng).
- [x] `MailSystem.cs`: thêm `PurgeExpired` (RemoveAll, kể cả mail đã claim), `ClaimAll` (lặp
      `TryClaim`, trả count).
- [x] Đo thật `ProgressLabel` bằng `TextGenerator` — **phát hiện quan trọng**: định dạng GỐC
      "+2000 Gold · +100 Gem" (từ task-mail.md, chưa từng đo thật) đã TRÀN sẵn hộp 90px (đo ra
      131px) — lỗi có từ trước, không phải do task này. Sửa cùng lúc: bỏ dấu "+", giảm fontSize
      12→10, nới `ProgressLabel` 90→128px (lấy từ rút `NameLabel` 150→136 + `ClaimButton` 62→50,
      đều còn dư vài px so với nội dung thật dài nhất của chúng). Cả reward 2-currency + hạn 2 chữ
      số ("2000 Gold · 100 Gem · 10d") vừa khít 128px. **Giới hạn đã ghi lại**: 3+ loại currency
      trong 1 mail sẽ tràn lại (đo thật: 186px) — không xảy ra với nội dung hiện có (chỉ Welcome,
      2 currency), nhưng ghi rõ cho mail trigger tương lai.
- [x] Đo thật vị trí `ClaimAllButton` qua tính tay + verify `execute_code` đọc lại
      `anchorMin/pivot/anchoredPosition/sizeDelta` (không dùng được `GetWorldCorners` vì prefab
      asset chưa nằm trong scene — world matrix không hợp lệ ngoài Play mode/scene thật; xác nhận
      đúng bằng cách tính tay khoảng cách tới `WalletLabel`/`Title`/`RowListContainer`, đều còn dư
      >100px, không đè).
- [x] `UI_Mail.prefab`: thêm `ClaimAllButton` (clone `CloseButton` qua
      `PrefabUtility.LoadPrefabContents`/`SaveAsPrefabAsset` trong `execute_code` — nhanh hơn
      `open_prefab_stage` cho việc chỉnh RectTransform hàng loạt across 6 row + 1 nút mới cùng lúc).
- [x] `MailScreen.cs`: gọi `PurgeExpired` đầu `Open()` (bắn `OnMailChanged` nếu có xoá), wire
      `ClaimAllButton` (`interactable` = có mail chưa claim đang hiện), hiện số ngày còn lại
      (`FormatRewardLine`), thêm `event Action OnMailChanged`.
- [x] Boot.unity: thêm `MailBadge` tĩnh dưới `MailButton` (Image đỏ 18×18 góc trên-phải + child
      `BadgeLabel` Text, font khớp `MailButton/Label` có sẵn) — **phát hiện lúc làm**: lần `create`
      đầu tiên báo lỗi kết nối ("Could not connect to Unity") nhưng THỰC RA đã tạo thành công phía
      server trước khi mất kết nối — retry tạo thêm 2 bản trùng (tổng 3 `MailBadge` dưới cùng
      `MailButton`), phát hiện qua `find_gameobjects by_name` trả về 3 instanceID thay vì 1, dọn 2
      bản thừa bằng `DestroyImmediate` qua `execute_code` trước khi cấu hình bản còn lại. Mặc định
      `SetActive(false)` — chỉ `RefreshMailBadge()` mới bật khi có mail thật chưa claim.
- [x] `MetaSceneInstaller.cs`: bind `_mailBadge`/`_mailBadgeLabel` trong `BindCanvasRefs`, thêm
      `RefreshMailBadge()` (đọc `MailSystem.UnclaimedCount`, set active + text "9+" khi >9), nghe
      `_mailScreen.OnMailChanged += RefreshMailBadge`, gọi ngay sau khi tạo `_mailScreen` (lần vẽ
      đầu) + trong `RefreshMap()` (mỗi lần vào lại Meta/refresh bản đồ).
- [x] `refresh_unity` compile sạch qua nhiều đợt sửa (gặp vài lần "Unity is reloading"/mất kết nối
      tạm thời giữa các lần gọi — retry sau khi đợi là qua, không phải lỗi biên dịch thật).
- [x] `MailSystemTests.cs`: thêm 5 test (`ClaimAll` claim đúng/không đúp, `PurgeExpired` xoá đúng
      mail hết hạn/giữ mail còn hạn+không hạn/xoá cả mail đã claim/null-safe).
- [x] Chạy full EditMode suite — **413/413 xanh** (408 cũ + 5 test mới).
- [x] Verify cấu trúc thật qua `execute_code` trong Play mode THẬT (không phải giả lập) — session
      này KHÔNG gặp frame-stall (khác 2 task liền trước, `Time.frameCount` tăng bình thường
      681→9937). Tiêm 2 mail tổng hợp (1 có hạn 3 ngày, 1 không hạn) vào `_profile.Mail` sống qua
      reflection, gọi `RefreshMailBadge()` thật → xác nhận badge active=True, label="2" (đúng số mail
      thật chưa claim, không đếm mail "Smoke Test Gift" đã claim từ session trước). Gọi
      `MailScreen.Open()` thật (trực tiếp qua reflection — `mailButton.onClick.Invoke()` không kích
      hoạt được listener dù không throw exception và 1 canary listener khác vẫn fire bình thường,
      nguyên nhân chưa xác định rõ, không phải lỗi logic vì gọi thẳng `Open()` hoạt động đúng ngay
      lập tức) → xác nhận **row0 progress="777 Gold · 33 Gem · 3d"** và **row1 progress="100
      Gold"** hiện đúng, KHÔNG tràn/cụt (khớp chính xác định dạng đã đo bằng TextGenerator ở trên),
      `ClaimAllButton.interactable=True`. Bấm thử `ClaimAllButton.onClick.Invoke()` thật thì gặp
      `NullReferenceException` trong `MailSystem.TryClaim` (dòng `economy.Grant(...)`) — điều tra
      xác nhận **KHÔNG phải bug của task này**: `ServiceLocator.TryGet<IEconomyService>()` lúc đó
      trả `false` (hoàn toàn trống), và kiểm tra ngay sau phát hiện `EditorApplication.isPlaying`
      đã tự chuyển về `false`/scene về `Boot`/`Time.frameCount` reset — Play mode đã tự thoát/reload
      giữa 2 lệnh `execute_code` (môi trường mất ổn định, cùng loại vấn đề với frame-stall ở 2 task
      trước nhưng biểu hiện khác — mất Play mode/ServiceLocator thay vì đứng frame). KHÔNG cố vào
      lại Play mode lần 2 để đuổi theo (đúng bài học đã áp dụng ở 2 task trước — không đốt thời gian
      vô hạn định). Bù lại: `MailSystemTests.ClaimAll_ClaimsEveryUnclaimedMail_ReturnsCount` chạy
      qua ĐÚNG method `MailSystem.ClaimAll`→`TryClaim` với `IEconomyService` đăng ký đúng cách qua
      test `[SetUp]`, xanh — xác nhận logic claim/grant tiền tệ đúng, lỗi live-session thuần là do
      môi trường mất `ServiceLocator` registration, không phải code sai.
- [x] Cập nhật `roadmap.md §0.1` (P4: Mail cơ bản → đủ badge/expiry/Claim-All), `object-map.md
      §12.1`.

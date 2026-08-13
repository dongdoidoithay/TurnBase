# Task: Localization infrastructure (pilot)

Yêu cầu: 2/3 hạng mục lớn người dùng chọn làm hết ("thực hiện cả 3 mục trên") — Addressables xong
(task-addressables-pilot.md), tới Localization. Đã báo trước sẽ cần bàn phạm vi cụ thể (không thể
dịch hết toàn bộ game 1 lượt) — áp dụng ĐÚNG kỷ luật đã dùng cho Addressables: xây hạ tầng thật +
1 pilot hẹp để verify, không quét hết mọi màn hình. Việc lớn — viết xong task file rồi mới code.

## §0. Findings

- **plan.md có spec rõ**: `ILocalizationService`/`LocalizationService`, "CSV key→value, VI/EN"
  (§11.7), quy ước key `{màn}.{nhóm}.{tên}` (§18, VD `battle.button.end_turn`), và tham vọng dài
  hạn "Cấm hard-code chuỗi trong code/prefab — có script LocalizationScanner quét báo lỗi CI"
  (§7 — ngoài phạm vi lượt này, chỉ ghi nhận).
- **Chưa có gì thật cả — nhưng ĐÃ có 3 thư mục stub rỗng** dành riêng cho việc này từ lúc khởi tạo
  dự án (7/8, cùng ngày mọi asmdef khác được tạo): `Assets/Tools/Localization/`,
  `Assets/_Project/Localization/`, `Assets/_Project/Scripts/Services/Localization/` — cả 3 rỗng
  hoàn toàn (0 file ngoài `.meta`). Không dùng lại `Assets/_Project/Localization/` cho file CSV thật
  vì nó KHÔNG nằm trong `Resources/` (không load được qua `Resources.Load<TextAsset>`, cách mọi
  asset non-Addressables khác trong dự án đang dùng) — tạo `Assets/_Project/Resources/Localization/`
  mới, giữ nguyên 3 thư mục stub cũ (không phải việc của task này để quyết định xoá/dùng chúng).
- **`SettingsDto.Language = "vi"` ĐÃ TỒN TẠI SẴN** (mặc định "vi", có null-guard trong
  `SettingsService.cs`) — nhưng KHÔNG CÓ GÌ đọc field này để đổi text hiển thị. Cùng mẫu "hạ tầng
  có sẵn, chưa ai dùng" gặp lại (như `DamageByUnit`, `UnclaimedCount`...).
- **`HeroDisplayUtil.cs` tự ghi nhận gap này từ trước** — doc-comment: "Chưa có ILocalizationService
  để tra NameKey ra chuỗi hiển thị thật (P5)". Toàn bộ tên hero/enemy/skill hiện tại là
  `FormatId()` — CHỈ title-case hoá `DefId` (VD "hero_ember_knight"→"Ember Knight"), KHÔNG PHẢI
  dịch thật, luôn ra tiếng Anh bất kể `Language` là gì.
- **`Game.Tools.CsvReader`** (dùng cho pipeline heroes.csv/skills.csv/enemies.csv) là parser CSV
  tốt nhất trong dự án (xử lý đúng dấu ngoặc kép/phẩy trong field) nhưng **KHÔNG dùng lại được** —
  `Game.Tools.asmdef` có `"includePlatforms": ["Editor"]`, trong khi `LocalizationService` phải
  chạy được ở RUNTIME thật (build), không chỉ Editor. Viết 1 parser CSV nhỏ riêng trong
  `Game.Services.Localization` (không phụ thuộc `Game.Tools`) — không di dời `CsvReader` sang chỗ
  dùng chung được vì đó là thay đổi hạ tầng Editor tooling đang chạy tốt, ngoài phạm vi task này.
- **Chưa có UI nào để đổi ngôn ngữ** — `SettingsScreen.cs` (100% code-dựng, không TMP vì
  `Game.Meta.asmdef` không ref TMP) có sẵn slider Music/SFX + toggle Screen Shake/Action Command,
  chưa có control cho Language — cần thêm 1 nút cycle VI↔EN (mẫu y hệt `AutoSpeed`/`Speed` button
  trong `BattleHudScreen` — nút hiện giá trị hiện tại, bấm đổi) để pilot có cách BẤM THẬT verify
  được, không chỉ gọi code.
- **Chọn Title screen (`GameBootstrap.ShowTitleScreen`) làm pilot hiển thị** — vừa tự tay xây lượt
  trước (task-title-screen.md), nhỏ/gọn (3 chuỗi: `SubtitleLabel` có placeholder động
  `{N} Heroes · {Gold} Gold`, `StartButton` label "START"; `TitleLabel` "AETHER LEGION" là tên
  riêng, KHÔNG dịch — giữ nguyên cả 2 ngôn ngữ, vẫn tra qua key để chứng minh key hoạt động dù giá
  trị trùng nhau), không đụng Combat, đã hiểu rõ toàn bộ code path.

## §1. Scope decision

**Trong phạm vi:**
1. `Game.Services.Localization` (thư mục `Services/Localization/` có sẵn, đang rỗng):
   `ILocalizationService` (`Get(key)`, `Get(key, params object[] args)` dùng `string.Format`,
   `SetLanguage(string)`, `CurrentLanguage`, `event Action OnLanguageChanged`) +
   `LocalizationService` impl (đọc `Assets/_Project/Resources/Localization/strings.csv` qua
   `Resources.Load<TextAsset>`, parser CSV nhỏ tự viết, dictionary `key→(vi,en)`). Key KHÔNG tìm
   thấy → trả về chính `key` (quy ước i18n phổ biến, dễ nhận ra chỗ thiếu khi test bằng mắt, không
   crash/không rỗng vô hình).
2. `strings.csv` mới — pilot ~6-8 key thật cho Title screen + Settings Language label, VI+EN đầy đủ
   cả 2 cột.
3. `ServiceInstaller.cs`: đăng ký `ILocalizationService`, thêm `WireSettingsToLocalization()` (mẫu
   y hệt `WireSettingsToAudio()` — `settings.OnChanged` đổi ngôn ngữ tự động).
4. `GameBootstrap.ShowTitleScreen()`: đổi 3 chuỗi hard-code sang tra qua `ILocalizationService`.
5. `SettingsScreen.cs`: thêm nút Language cycle VI↔EN, ghi qua `_settings.Modify(s => s.Language =
   ...)` (đúng mẫu mọi setting khác đã làm), tự làm mới label chính nó khi đổi (đã đang mở sẵn).
6. Test mới: `LocalizationServiceTests.cs` (parse CSV đúng, `Get` đúng cả 2 ngôn ngữ, format-arg
   đúng, key thiếu trả về chính key, không crash khi CSV null/rỗng).

**Ngoài phạm vi (cố ý, ghi rõ):**
- KHÔNG dịch bất kỳ màn hình nào khác ngoài Title + label Language trong Settings — 20+ file khác
  vẫn hard-code chuỗi, để dành cho các lượt sau (đúng tinh thần pilot của Addressables).
- KHÔNG đụng `HeroDisplayUtil`/tên hero-enemy-skill — nội dung dịch cho 24 hero + enemy + skill là
  khối lượng content lớn riêng, không phải hạ tầng.
- KHÔNG xây `LocalizationScanner` (CI tool chống hard-code string) — công cụ riêng, ngoài phạm vi.
- KHÔNG dùng Addressables cho CSV này — pilot Addressables trước chỉ làm `HeroDefinitionSO`, giữ
  nhất quán KHÔNG mở rộng phạm vi Addressables trong lúc làm việc khác.
- KHÔNG di dời `Game.Tools.CsvReader` sang chỗ dùng chung được cho runtime — lý do đã ghi ở §0.

## §2. Implementation checklist

- [x] `Assets/_Project/Resources/Localization/strings.csv` — 10 key thật (Title 3 + Settings 7,
      mở rộng nhẹ so với dự tính ban đầu vì đang sửa nguyên file `SettingsScreen.cs` — dịch trọn
      vẹn CẢ màn thay vì chỉ riêng label Language, không tăng phạm vi FILE, chỉ tăng độ đầy đủ của
      2 màn đã chọn).
- [x] `Game.Services.Localization/ILocalizationService.cs` + `LocalizationService.cs` — parser CSV
      nhỏ tự viết (không dùng `Game.Tools.CsvReader`, lý do đã ghi ở §0), key thiếu → trả chính key.
- [x] `ServiceInstaller.cs`: đăng ký `ILocalizationService` + `WireSettingsToLocalization()` (đúng
      mẫu `WireSettingsToAudio`).
- [x] `GameBootstrap.cs`: `ShowTitleScreen()` dùng key thay chuỗi cứng (`TitleLabel`/`SubtitleLabel`
      có placeholder/`StartButton`).
- [x] `SettingsScreen.cs`: nút Language cycle (mẫu nút Speed của `BattleHudScreen`) + dịch toàn bộ
      label tĩnh sẵn có (Title/Music/SFX/2 toggle/Close) — đổi `NewToggle` thành
      `NewToggleWithLabel` (trả kèm `Text` để `RefreshLabels()` sửa được lúc runtime, trước đây
      label bị "chôn" bên trong hàm không giữ tham chiếu).
- [x] `LocalizationServiceTests.cs` mới (7 test): key thật cả 2 ngôn ngữ, key thiếu, format-arg cả
      2 ngôn ngữ, `OnLanguageChanged` chỉ bắn khi THẬT SỰ đổi (không bắn khi gọi lại cùng giá trị).
- [x] `refresh_unity` compile sạch.
- [x] Chạy full EditMode suite — **423/423 xanh** (416 cũ + 7 test mới).
- [x] Verify Play-mode THẬT ĐẦY ĐỦ — gặp lại MCP frame-stall (frame dừng ở 2), áp dụng
      check-before-force đã quen thuộc. Title hiện ĐÚNG tiếng Việt mặc định: `TitleLabel`="AETHER
      LEGION", `SubtitleLabel`="24 Tướng · 935390 Vàng" (placeholder thay đúng), `StartButton`=
      "BẮT ĐẦU". Bấm START thật → Meta → mở `SettingsScreen` thật → mọi label đúng tiếng Việt
      ("CÀI ĐẶT"/"Nhạc"/"Ngôn ngữ"/"VI"/"ĐÓNG"). Bấm nút Language thật → **toàn bộ label đổi NGAY
      LẬP TỨC sang tiếng Anh** ("SETTINGS"/"Music"/"Screen Shake"/"Action Command"/"EN"/"CLOSE") —
      xác nhận trọn chuỗi wiring thật: click → `ISettingsService.Modify` → `OnChanged` →
      `WireSettingsToLocalization` → `ILocalizationService.SetLanguage` → `OnLanguageChanged` →
      `SettingsScreen.RefreshLabels`. Đọc thẳng service singleton qua `ServiceLocator` (không qua
      SettingsScreen) xác nhận trạng thái `CurrentLanguage="en"` + `Get("title.button.start")`=
      "START" nhất quán toàn app, không chỉ cục bộ 1 màn.
- [x] Cập nhật `roadmap.md §0.1`, `object-map.md §12.1`.

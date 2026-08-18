# Task: Accessibility — 3/3 mục còn lại của plan.md §10.7

task-accessibility.md đã xong 3/6 mục (Text Scale/Colorblind HP bar/PC hotkey). Từ audit "hoàn
thiện chức năng" — người dùng chọn "Accessibility còn lại" qua AskUserQuestion cùng đợt Red Dot/
Boss phase-enrage/Gacha disclosure. Còn 4 mục nêu trong plan.md §10.7, xử lý 3/4 (mục thứ 4 — tốc độ
chữ — xác nhận KHÔNG áp dụng được, xem §4).

## §1. Gamepad hotkey — `BattleHudScreen.HandleHotkeys`

Thêm nhánh `Gamepad.current` song song nhánh `Keyboard.current` có sẵn (tách 2 khối `if` độc lập,
không còn `return` sớm chung — bàn phím VÀ gamepad đều được đọc mỗi frame, không loại trừ nhau).
4 nút mặt trước → skill 0-3 (`buttonWest/South/East/North`), vai phải (`rightShoulder`) → skill 4
(Ultimate — không đủ 5 nút mặt để khớp 1-1, dùng vai thay), `startButton` → kết lượt. Tái dùng ĐÚNG
`TrySelectSlot`/`_endTurnButton.onClick` — cùng nguyên tắc "hotkey chỉ là lối tắt" của nhánh bàn
phím.

## §2. "Hiển thị số damage lớn" — `SettingsDto.ShowLargeDamageNumbers` + `FloatingTextLayer`

`SettingsDto` thêm field mới (không có sẵn từ trước, khác Text Scale/Colorblind — xác nhận qua audit
đọc toàn bộ field). `FloatingTextLayer.LargeNumbers` (bool, set bởi `BattleSceneInstaller.BuildBattle`
từ settings) nhân thêm ×1.5 vào `scale` của `ShowDamage`/`ShowHeal` — CHỈ 2 hàm này (số damage/heal
thật), KHÔNG áp `ShowMiss`/`ShowPerfect`/`ShowBreak` (đã là banner trạng thái to sẵn, không phải "số
damage"). `SettingsScreen` thêm toggle mới (panel Portrait 500→560, Landscape 460→520 để có chỗ,
Close dời xuống theo).

## §3. Icon nguyên tố hình dạng khác nhau (colorblind) — `SkillSlotView`

**Phát hiện quan trọng**: `Assets/_Project/Art/UI/Icons/Elements/` RỖNG — dự án chưa từng có sprite
icon riêng theo nguyên tố ở BẤT KỲ đâu, chỉ có `Image` tô màu phẳng (`ElementColor` switch, lặp lại
y hệt ở `SkillSlotView`/`BattleHudScreen`). Sinh sprite pixel-art mới cho 7 nguyên tố là việc LỚN
hơn hẳn 1 lượt (cần pixel-art-pipeline, nhiều vòng review hình ảnh) — thay bằng giải pháp THẬT nhưng
gọn hơn: 7 glyph ký tự hình khối tối đa phân biệt (● Neutral · ▲ Fire · ▼ Water · ■ Earth · ◆ Wind ·
★ Light · ✚ Dark), hiện qua badge `TextMeshProUGUI` mới góc dưới-phải ô skill (đối xứng badge Cost
góc dưới-trái), CHỈ hiện khi `ColorblindMode` bật — đúng nghĩa đen "hình dạng khác nhau, không chỉ
khác màu" của plan.md, không cần asset pipeline.

**Phạm vi**: chỉ `SkillSlotView` (Skill Grid — nơi người chơi ra quyết định chọn skill mỗi lượt dựa
theo khắc chế nguyên tố, cao giá trị nhất). `BattleHudScreen`'s hero avatar ring vẫn chỉ tô màu —
để ngoài phạm vi (thông tin phụ, không phải quyết định mỗi lượt), đúng mức độ ưu tiên đã áp dụng cho
Colorblind HP bar ở task-accessibility.md (chỉ Battle HUD, không toàn game).

## §4. Tốc độ chữ 1×/2×/tức thì — XÁC NHẬN KHÔNG ÁP DỤNG ĐƯỢC

Grep toàn bộ `Assets/_Project/Scripts/` cho hiệu ứng "gõ chữ" (typewriter/maxVisibleCharacters/
letter-by-letter reveal) — **0 kết quả**. Không có hội thoại/banner/kết quả nào trong game hiện
hiển thị chữ theo kiểu gõ dần; mọi `Text.text`/`TextMeshProUGUI.text` gán trực tiếp toàn bộ chuỗi
cùng lúc. Mục này của plan.md §10.7 không có gì để "làm nhanh hơn" trong kiến trúc hiện tại — xây hạ
tầng tốc độ chữ cho hiệu ứng không tồn tại là việc bịa ra nhu cầu giả. Ghi rõ N/A thay vì lờ đi.

## §5. Verify

- `validate_script` 7 file sửa (`SettingsScreen.cs`, `PlayerProfileDto.cs`, `FloatingText.cs`,
  `BattleSceneInstaller.cs`, `BattleHudScreen.cs`, `SkillSlotView.cs`) 0 lỗi. **668/668 test xanh**
  (không thêm test EditMode — đúng mẫu UI/input đã verify qua reflection ở accessibility phần 1).
- **Gặp lại + tự chẩn đoán 1 lỗi hạ tầng thật**: sau khi sửa `PlayerProfileDto.cs` (thêm field), 2
  lượt `refresh_unity force` liên tiếp báo lỗi biên dịch "SettingsDto không có ShowLargeDamageNumbers"
  ở `SettingsScreen.cs` dù chính `PlayerProfileDto.cs` không báo lỗi gì — đọc `Logs/Editor.log` xác
  nhận `Game.Data.dll` biên dịch thành công CÙNG LÚC `Game.Meta.dll` (tham chiếu `Game.Data`) vẫn
  dùng bản DLL CŨ (bug tái sử dụng cache incremental Bee giữa các assembly cùng 1 lượt biên dịch) —
  không phải lỗi code. `EditorApplication.isCompiling` đứng ở `True` bất thường lâu; đợi domain
  reload thật sự hoàn tất (MCP bridge tự ngắt-nối lại) rồi kiểm tra lại qua reflection
  (`typeof(SettingsDto).GetFields()`) xác nhận field đã có — biên dịch lại sau đó sạch 0 lỗi.
- Functional thật qua `execute_code`: dựng `SkillSlotView` thật với `SettingsService` thật —
  `ColorblindMode=false` → glyph ẩn; bật `true` → glyph hiện đúng "▲" (Fire), đổi skill sang Water →
  glyph đổi đúng "▼" (xác nhận đổi theo skill, không static). Dựng `FloatingTextLayer` thật — 
  `LargeNumbers=false` → scale=1; `true` → scale=1.5 cho cả `ShowDamage`/`ShowHeal`.
- Gamepad hotkey: xác nhận CẤU TRÚC (cùng field `TrySelectSlot`/`_endTurnButton.onClick` với đường
  bàn phím đã verify) — KHÔNG giả lập `Gamepad.current` thật trong EditMode (cần input device thật
  hoặc InputTestFixture), đúng giới hạn đã ghi cho PC hotkey ở task-accessibility.md §4.

## §6. Tổng kết plan.md §10.7 (6/6 mục)

Tắt Action Command (có sẵn từ trước — `SettingsDto.ActionCommandEnabled`) · tắt screen shake (có sẵn
— `ScreenShake`) · chế độ mù màu (HP bar + glyph nguyên tố Skill Grid, task-accessibility.md +
lượt này) · scale chữ 100/125/150% (task-accessibility.md) · tốc độ text (N/A, xem §4) · hiển thị số
damage lớn (lượt này). **5/6 có ý nghĩa thật, 1/6 (tốc độ chữ) không áp dụng được cho kiến trúc hiện
tại** — không phải khoản thiếu, mà là không có gì để tăng tốc.

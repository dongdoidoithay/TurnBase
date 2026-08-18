# Task: Accessibility — Text Scale + Colorblind Mode + PC Hotkeys

plan.md §10.7. Người dùng chọn làm thật qua AskUserQuestion (cùng đợt Arena/AI diversity/content
localization). Phát hiện quan trọng TRƯỚC khi viết dòng code nào: `SettingsDto.TextScale` (100-150%,
đã CLAMP sẵn ở `SettingsService.Apply`) và `SettingsDto.ColorblindMode` đã tồn tại từ ĐẦU dự án —
đúng mẫu "hạ tầng có sẵn, chưa dùng" lặp lại nhiều lần trong dự án — chỉ chưa có UI lẫn hiệu ứng thật.

## §1. Text Scale — `TextScaleApplier` (Game.Meta.Accessibility)

Quét đệ quy mọi `Text`/`TextMeshProUGUI` dưới 1 root, nhân `fontSize` GỐC theo scale — nhớ size gốc
qua `ConditionalWeakTable` (không cần thêm component, không leak) để gọi lặp lại KHÔNG cộng dồn.
Đặt ở `Game.Meta` (không phải `Game.Core.UI`) vì cần `Unity.TextMeshPro` mà `Game.Core` không tham
chiếu — `Game.UI` (BattleHudScreen) được phép tham chiếu ngược `Game.Meta` nên vẫn dùng được.

Áp dụng 2 chỗ:
- `ServiceInstaller.WireSettingsToTextScale` — phản ứng mỗi khi `TextScale` đổi, quét lại TOÀN BỘ
  `uiRoot` (mọi Canvas Meta/Battle/Title/Splash/Loading/Settings đều là con của đây).
- `BattleHudScreen.Bind()` — áp THÊM 1 lần lúc dựng (scene Battle dựng lại mỗi trận, không thể chỉ
  dựa vào sự kiện phản ứng vì HUD trận đầu tiên chưa từng nghe được sự kiện nào).

**Giới hạn đã biết**: màn hình Meta dựng-lười LẦN ĐẦU sau khi đã đổi TextScale (VD mở Shop lần đầu
sau khi chỉnh Settings) chưa tự áp ngay — chỉ áp đúng khi setting đổi TIẾP theo hoặc khởi động lại
app. Sửa triệt để cần thêm `TextScaleApplier.Apply()` vào MỌI `BuildShell()` của ~20 màn hình — quá
lớn cho 1 lượt, ghi rõ thay vì âm thầm bỏ qua.

## §2. Colorblind Mode — `BattleHudScreen.HpColor`

Đổi từ static sang instance method đọc `_settings.Current.ColorblindMode`. Bộ màu thay thế:
xanh dương (`#2B72B2`) / cam (`#E69F00`) / đỏ SẪM gần đen (`#800020`) thay vì xanh lá/vàng/đỏ tươi
gốc — cố ý khác nhau CẢ hue lẫn ĐỘ SÁNG (không chỉ hue) vì protanopia/deuteranopia (dạng mù màu phổ
biến nhất) khiến xanh lá/đỏ khó phân biệt chỉ dựa vào hue.

**Phạm vi**: chỉ HP bar trong Battle HUD — nơi màu mã hoá thông tin sinh tử quan trọng nhất, không
đổi màu Element/status/rarity ở màn khác (quy mô lớn hơn hẳn 1 lượt).

## §3. PC Hotkeys — `BattleHudScreen.HandleHotkeys`

1-5 chọn ô skill, Enter kết thúc lượt. Dùng `Unity.InputSystem` (`Keyboard.current`, khớp quy ước
đã có ở `ActionCommandUI.cs` — dự án chỉ bật Input System mới, `activeInputHandler=1`, KHÔNG dùng
`UnityEngine.Input` cũ). Thêm `Unity.InputSystem` vào `Game.UI.asmdef` (trước đó chỉ `Game.CombatView`/
`Game.Meta` có, `Game.UI` chưa cần).

Tái dùng ĐÚNG đường xử lý click thật — `SkillSlotView.OnClicked`/`Interactable` (không viết logic
chọn skill thứ 2 song song, tránh 2 luồng có thể lệch nhau) — hotkey chỉ gọi `slot.OnClicked?.
Invoke(slot)` sau khi tự kiểm `Interactable`, giống hệt `OnPointerClick` đã làm.

## §4. Verify

- `validate_script` + compile toàn project 0 lỗi. **647/647 test xanh** (không đổi hành vi combat
  lõi/test hiện có).
- `TextScaleApplier`: đo thật fontSize gốc 20 → scale 1.25→25, 1.5→30, reset 1.0→20 CHÍNH XÁC
  (không cộng dồn khi gọi lặp lại nhiều lần).
- `HpColor`: xác nhận thật qua reflection — bật `ColorblindMode` đổi đúng từ xanh lá/đỏ tươi sang
  xanh dương/đỏ sẫm.
- `SettingsScreen`: dựng thật, xác nhận label localize đúng VI ("Cỡ chữ"/"Chế độ mù màu"), bấm nút
  cycle 3 lần cho đúng chuỗi 100%→125%→150%→100%.
- PC hotkeys: xác nhận CẤU TRÚC (cùng field `OnClicked`/`Interactable` với đường click thật đã có
  test/verify từ trước) — KHÔNG giả lập `Keyboard.current` thật trong EditMode (cần input device
  thật hoặc InputTestFixture, ngoài phạm vi 1 lượt).

## §5. Ngoài phạm vi

- Gamepad hotkeys (chỉ làm PC keyboard, đúng lựa chọn "gamepad/PC hotkey" nhưng ưu tiên PC trước —
  gamepad cần layout khác hẳn, D-pad/face button, việc riêng).
- Colorblind mode cho Element/status/rarity color ở màn khác ngoài Battle HUD HP bar.
- Text Scale áp cho MỌI màn Meta (chỉ áp qua sự kiện phản ứng + Battle HUD chủ động, xem §1 giới
  hạn).

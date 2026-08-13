# Task: Fix "không chọn Auto mà vẫn chơi Auto" (Battle HUD)

Báo cáo người dùng: màn Battle, không bấm nút AUTO nhưng trận vẫn tự chạy như đang bật Auto.

## §0. Root cause (xác nhận qua đọc code + execute_code)

`BattleSceneInstaller.BuildBattle()` (dòng ~88) khôi phục `_autoPlay` (field điều khiển HÀNH VI
thật — dùng ở `ExecuteAiOrAuto`: `if (_autoPlay) Simulation.SubmitIntent(Simulation.
DefaultAutoIntent());`) từ `SettingsDto.AutoBattle` đã lưu từ trận TRƯỚC (task-auto-battle.md —
tính năng persist Auto qua trận vốn có chủ đích). Nhưng `BattleHudScreen._auto` (field điều khiển
HIỂN THỊ — nhãn "AUTO ON"/"AUTO OFF") luôn khởi tạo `false`/"AUTO OFF" mặc định, KHÔNG đọc lại
`_autoPlay` khi HUD được `Bind()`.

Kết quả: nếu người chơi từng bật Auto ở 1 trận trước đó (được lưu vào save), MỌI trận sau đó đều
tự chạy Auto NGẦM (đúng theo thiết kế persist), nhưng nút HUD luôn hiện "AUTO OFF" (sai, không
đồng bộ) — người chơi thấy nút OFF nhưng trận cư xử như ON, đúng triệu chứng báo cáo.

Xác nhận bằng `execute_code`: dựng `BattleHudScreen` thật, gọi `BuildLayout()` qua reflection —
label mặc định "AUTO OFF"/`_auto=false` bất kể trạng thái đã lưu.

## §1. Fix

- `BattleHudScreen`: thêm `public void SetAutoState(bool auto)` — set `_auto` + cập nhật label/màu
  đúng y hệt logic trong nút bấm, nhưng KHÔNG phát `OnAutoToggled` (chỉ đồng bộ hiển thị, không
  phải hành động của người chơi — tránh double-persist/side-effect thừa).
- `BattleSceneInstaller.WireHud()`: gọi `_hud.SetAutoState(_autoPlay)` NGAY SAU `_hud.Bind(Simulation)`
  — đồng bộ hiển thị đúng với trạng thái `_autoPlay` đã khôi phục ở `BuildBattle()`.

## §2. Scope

**Trong phạm vi:** đúng 2 thay đổi trên. Không đổi hành vi `_autoPlay`/persist logic cũ
(task-auto-battle.md) — chỉ vá chỗ HIỂN THỊ bị lệch.

**Ngoài phạm vi:** không có test EditMode nào phủ `BattleHudScreen`/`BattleSceneInstaller` (UI
dựng-bằng-code, xác nhận qua grep) — verify bằng `execute_code` gọi `SetAutoState` trực tiếp.

## §3. Checklist

- [x] Viết file này (root cause xác nhận qua đọc code)
- [x] `BattleHudScreen.SetAutoState(bool)` — set `_auto` + label + màu, không phát event
- [x] `BattleSceneInstaller.WireHud()` gọi `_hud.SetAutoState(_autoPlay)` ngay sau `Bind()`
- [x] `validate_script` + force recompile → 0 lỗi
- [x] Verify qua `execute_code`: `SetAutoState(true)` đổi đúng `_auto`/label "AUTO OFF"→"AUTO ON"
- [x] `run_tests` → **524/524 xanh** (không đổi khỏi baseline — đúng dự đoán, không có test nào
      phủ khu vực này)
- [x] Cập nhật `roadmap.md`, `object-map.md`

## §-DoD
Label HUD luôn khớp `_autoPlay` thật ngay khi vào trận; test xanh; không đổi persist logic cũ.

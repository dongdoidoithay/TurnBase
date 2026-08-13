# Task: Fix Start Battle button bị đè bởi FormationRow (TeamSelectScreen)

Báo cáo người dùng: màn Meta, nút "Start Battle" bị đè dưới "thanh info" — không bấm được.

## §0. Root cause (xác nhận qua execute_code đọc prefab thật)

`UI_TeamSelect.prefab` → `Panel/Content/Footer` cao **cố định 40px** (anchor bottom, `sizeDelta=(0,40)`),
chứa 3 con `SelectedLabel`/`BackButton`/`StartButton` đều neo **giữa chiều cao Footer**
(`anchorMin/Max=(_, 0.5)`).

`task-formation-synergy.md` (session trước) thêm `BuildFormationButton()` — tạo `FormationRow` (full-width,
nền mờ `alpha=0.90`, có `Button` riêng) **parent vào chính `footer`**, neo **TOP của Footer**
(`anchorMin/Max=(0,1)-(1,1)`, `anchoredPos=(0,-4)`, cao **36px**). Vì Footer chỉ cao 40px, dải 36px này
gần như phủ **TOÀN BỘ** vùng Footer — đè lên `StartButton`/`BackButton` nằm giữa Footer. Vì tạo SAU
(sibling cuối) nên vẽ ĐÈ LÊN TRÊN + `Image` của nó chặn luôn raycast xuống `StartButton` bên dưới.

Đây là lỗi thật — không phải browser/MCP artifact. Xác nhận bằng `execute_code` đọc `RectTransform`
thật của prefab (không suy đoán):
```
Footer: sizeDelta=(0,40) anchoredPos=(0,0) anchorMin=(0,0) anchorMax=(1,0)
  SelectedLabel: anchor(0,0.5)  BackButton: anchor(1,0.5) pos(-6,0)  StartButton: anchor(1,0.5) pos(-122,0)
FormationRow (runtime): parent=Footer, anchor(0,1)-(1,1), pos(0,-4), size(0,36)  ← đè hết Footer 40px
```

**Khoảng trống thật có sẵn phía trên Footer** (đo từ `Content`, cao 480px):
- `HeroListViewport` (top-anchored) kết thúc ở content-bottom = 480−380 = 100px
- `GearPanelContainer` (top-anchored) kết thúc ở content-bottom = 480−360 = 120px
- `Footer` chiếm 0–40px (content-bottom)
- → khoảng trống thật **40px–100px** (60px) chưa ai dùng, đủ chỗ cho FormationRow 36px + gap.

## §1. Fix

Đổi `BuildFormationButton` trong `TeamSelectScreen.cs`:
- Parent `FormationRow` vào **`content`** (không phải `footer`) — không đụng prefab.
- Neo bottom của `content` (`anchorMin/Max=(0,0)-(1,0)`, `pivot=(0.5,0)`), `anchoredPosition.y =
  footerHeight + gap` (đọc `footer` RectTransform.sizeDelta.y thật thay vì hardcode 40, chịu được nếu
  ai đổi prefab sau này), `sizeDelta=(0,36)` giữ nguyên.
- Kết quả: dải nằm NGAY TRÊN Footer, cách 6px, KHÔNG chồng lên `StartButton`/`BackButton`/`SelectedLabel`,
  còn dư ~18-40px trước khi chạm `HeroListViewport`/`GearPanelContainer`.

## §2. Scope

**Trong phạm vi:** sửa đúng `BuildFormationButton()` + gọi nó với `content` thay vì `footer` trong
`BuildShell()`. Không đổi prefab, không đổi logic formation cycle.

**Ngoài phạm vi:** không có test EditMode nào phủ UI dựng-bằng-code của màn Meta (xác nhận grep —
0 file test tham chiếu `TeamSelectScreen`/`FormationRow`) → verify bằng đọc `RectTransform` thật qua
`execute_code` sau khi Instantiate prefab (không cần Play-mode, tránh rủi ro MCP frame-stall đã biết).

## §3. Checklist

- [x] Viết task file này (§0 root cause xác nhận qua execute_code)
- [x] Sửa `TeamSelectScreen.cs`: `BuildFormationButton` neo vào `content`, phía trên `Footer`
- [x] `validate_script` → 0 lỗi + force recompile → 0 compile error trong console
- [x] Verify: gọi `BuildShell()` thật qua reflection trên instance mới (không cần `PlayerProfileDto`),
      đọc `RectTransform` sống sau khi chạy — `FormationRow` band content-bottom `[46,82]`,
      `Footer` band `[0,40]` (chứa `StartButton` neo giữa) → **không chồng nhau, cách 6px**, còn dư
      18px trước khi chạm `HeroListViewport`(bottom=100)/`GearPanelContainer`(bottom=120).
- [x] `run_tests` → Editor tạm thời ở Play Mode (phiên chia sẻ, có thể người dùng đang tự test) nên
      lần đầu báo lỗi "Cannot start a test run while... Play Mode" — không ép Stop Play Mode của
      người dùng, đợi tự thoát rồi chạy lại. **518/518 xanh** (không đổi khỏi baseline, đúng dự
      đoán — thay đổi chỉ ở `Game.Meta`, không đụng `Game.Combat`/logic có test).
- [x] Cập nhật `roadmap.md` (P6), `object-map.md` §12.1

# Task: Thay hệ frame-timer C# bằng Unity Mecanim Animator thật

Yêu cầu người dùng (nguyên văn): "không dùng frame code C# sửa lại rule cho tôi: Hãy thiết lập hệ
thống chuyển động (Animation) cho GameObject `[Tên_Nhân_Vật]` bằng Unity Animator Controller. TUYỆT
ĐỐI KHÔNG viết script C# để thay đổi sprite theo frame, hãy sử dụng các công cụ MCP (như
`manage_animation`, `manage_components`) để setup trực tiếp trong Editor." — kèm spec cụ thể:
Animator Controller riêng mỗi nhân vật, parameter `isRunning`(bool)/`Attack`(trigger)/`isDead`(bool),
3 state `Idle`/`Run`/`Attack`, 3 transition (`Idle→Run` khi `isRunning`, `Run→Idle` khi tắt
`isRunning`, `AnyState→Attack` khi trigger `Attack`). Sau đó xác nhận qua AskUserQuestion: (1) dùng
`execute_code` 1 lần để build AnimationClip (vì `manage_animation` không hỗ trợ Sprite object-reference
curve), (2) sửa `UnitView.cs` để dùng Animator thật, (3) làm hết cả 91 nhân vật ngay (không làm hero
trước).

## §0. Bối cảnh — hệ CŨ đã ở đâu trước khi thay

`UnitView.cs` trước đó dùng hệ "sprite-timer" tự viết (xem `task-animation-pilot.md` — đã HOÀN TẤT
từ trước, KHÔNG phải việc của task này): `AnimState` enum + `Sprite[]` mảng frame nạp qua
`Resources.Load` + `Update()` tự đổi `_sprite.sprite` theo FPS. Hệ đó đã phủ **90/90 nhân vật, ĐỦ 5
trạng thái mỗi nhân vật** (idle/attack/move/damage/die) — sinh bằng `character_rig.py`/`enemy_rig.py`
(vẽ thủ tục bằng Pillow, không phải AI). Yêu cầu lần này KHÔNG phải "thêm animation" — mà là thay
đúng CƠ CHẾ CHẠY animation, từ code tay sang Mecanim thật, theo đúng yêu cầu tường minh của người
dùng.

## §1. Việc đã làm

- [x] **Capability gap phát hiện:** `manage_animation` (MCP tool) chỉ hỗ trợ tạo `AnimationClip` với
      curve SỐ (`AnimationUtility.SetEditorCurve`) — KHÔNG có API tạo curve kiểu Sprite
      (`ObjectReferenceKeyframe`/`SetObjectReferenceCurve`), bắt buộc cho animation đổi frame 2D. Xác
      nhận qua AskUserQuestion: dùng đúng 1 lần `execute_code` (không phải runtime C#, chỉ là script
      Editor chạy 1 lần) để build asset — tách bạch rõ với yêu cầu "không viết C# đổi sprite theo
      frame" (điều đó nói về code RUNTIME, không phải build-time asset tool).
- [x] **270 AnimationClip mới** (`Assets/_Project/Resources/Animations/Clips/{defId}_{idle|run|
      attack}.anim`, 90 nhân vật × 3 clip) — build bằng 1 lệnh `execute_code` duy nhất, đọc frame PNG
      đã import sẵn từ hệ cũ (idle/attack dùng nguyên, "run" lấy nguồn từ frame "move" cũ vì game
      không có state "chạy" riêng). idle 8fps loop, run 12fps loop, attack 14fps không loop.
- [x] **90 AnimatorController** (`Assets/_Project/Resources/Animations/Controllers/{defId}.controller`)
      — build HOÀN TOÀN bằng `manage_animation` (không `execute_code`), đúng chuỗi 11 lệnh/nhân vật:
      `controller_create` → 3× `controller_add_parameter` (isRunning/Attack/isDead) → 3×
      `controller_add_state` (Idle mặc định/Run/Attack) → 4× `controller_add_transition`. **Có 1
      transition CỘNG THÊM ngoài spec gốc** (đã báo người dùng lúc làm): `Attack→Idle`
      (hasExitTime=true, exitTime=1.0) — thiếu cái này thì Attack sẽ đứng khựng vĩnh viễn ở frame
      cuối, không có đường thoát.
      Phủ đủ **24 hero + 6 boss + 60 enemy = 90/90**, verify bằng `ls`+`comm` chéo với danh sách
      defId thật từ `Resources/Art/Characters/{Heroes,Enemies}/`.
- [x] **`UnitView.cs` viết lại hoàn toàn phần animation** — bỏ `AnimState`/`Sprite[]`/`LoadFrames`/
      `FramesFor`/`FpsFor`/`AdvanceAnimFrame` (~100 dòng code frame-timer tay); thêm `Animator` field,
      `EnsureRefs()` tự add `Animator` component vào `_visualRoot` (CÙNG GameObject với
      `SpriteRenderer` — bắt buộc vì clip bind `path=""`), `Bind()` nạp
      `Resources.Load<RuntimeAnimatorController>($"Animations/Controllers/{defId}")` rồi
      `Rebind()`+`Update(0f)` để hiện đúng frame Idle ngay khung hình đầu. `PlayAttackLunge()` gọi
      `_animator.SetTrigger("Attack")`. `PlayDeath()`/`PlayRevive()` gọi `_animator.SetBool("isDead",
      true/false)` (param có sẵn theo đúng spec, dù CHƯA có transition nào dùng tới — xem §2 gap).
      Fallback giữ nguyên: nhân vật không có Controller (không nằm trong 90 defId) → tắt Animator,
      dùng sprite tĩnh như hệ cũ.
- [x] Compile sạch (`validate_script` + `refresh_unity force`, 0 lỗi/warning thật).
- [x] Full EditMode suite: **668/668 xanh** — không đụng logic Combat lõi, chỉ view layer.
- [x] Verify runtime thật bằng `execute_code` (ngoài Play mode, tránh lỗi frame-stall MCP đã biết
      nhiều lần trong dự án): gọi `Animator.Update(dt)` thủ công nhiều bước liên tiếp trên
      `hero_ember_knight`, log từng bước — xác nhận ĐÚNG chuỗi: Idle lặp đổi sprite theo 8fps →
      `SetTrigger("Attack")` chuyển sang Attack đúng lúc → chạy hết 4 frame attack ở đúng tốc độ →
      tự động quay về Idle (nhờ transition cộng thêm) → `SetBool("isRunning", true)` sau đó chuyển
      đúng sang Run. Bằng chứng pixel/state thật, không suy đoán.

## §2. Gap thật — CHƯA làm, cần quyết định tiếp

Spec gốc người dùng chỉ liệt kê 3 state (Idle/Run/Attack). Hệ CŨ (đã HOÀN TẤT ở
`task-animation-pilot.md`) có **5 state cho cả 90 nhân vật**: idle/attack/move/**damage**/**die**.
Sau khi thay sang Animator theo đúng spec mới, **2 state Damage và Die KHÔNG còn được dùng** —
`PlayHit()` không còn đổi sprite theo frame "damage" (chỉ còn chớp trắng + rung, hiệu ứng code cũ vẫn
giữ), `PlayDeath()` không còn giữ frame "die" cuối cùng (chỉ còn fade alpha + trôi lên, hiệu ứng code
cũ vẫn giữ) — **mất phần chuyển động sprite thật ở 2 khoảnh khắc đó, dù file PNG damage/die vẫn còn
nguyên trên đĩa** (không xoá gì, chỉ không có Clip/State/Transition trỏ tới).

**Đã hỏi lại — người dùng chọn "Có, thêm luôn cho cả 90 nhân vật".** Đã làm xong hoàn chỉnh:

- [x] 180 `AnimationClip` mới (`{defId}_damage.anim` + `{defId}_die.anim` × 90) — cùng kỹ thuật
      `execute_code`/`SetObjectReferenceCurve` như đợt idle/run/attack đầu. damage 16fps loop=false,
      die 10fps loop=false.
- [x] Thêm parameter `Hit` (trigger, MỚI — không có trong spec gốc, cần để trigger Damage riêng biệt
      với Attack) + 2 state (Damage/Die) + 4 transition/nhân vật vào cả 90 Controller, thuần bằng
      `manage_animation` (630 lệnh): `AnyState→Damage` (trigger `Hit`) → `Damage→Idle` (hasExitTime)
      giống hệt mẫu Attack; `AnyState→Die` (bool `isDead=true`) → `Die→Idle` (bool `isDead=false`,
      transition thoát MỚI thêm ngoài ý định ban đầu "giữ nguyên vĩnh viễn" — cần thiết vì
      `UnitRevived` là sự kiện gameplay thật trong trận, xem `CombatPresenter.cs:194-202`).
- [x] `UnitView.PlayHit()` gọi `_animator.SetTrigger("Hit")`.
- [x] **Bug thật phát hiện qua verify, đã sửa:** `AnyState→Die` dùng bool `isDead` (không phải
      trigger) — Unity re-đánh giá transition NÀY MỖI FRAME trong khi `isDead` vẫn `true`, khiến Die
      liên tục RESTART (chỉ thấy lặp đi lặp lại frame 00/01, không bao giờ chạy hết 6 frame). Xác
      nhận bằng `execute_code` mô phỏng nhiều `Animator.Update()` liên tiếp — thấy rõ pattern lặp sai.
      **Nguyên nhân:** thuộc tính `AnimatorStateTransition.canTransitionToSelf` (chỉ áp dụng cho
      transition xuất phát từ Any State) mặc định `true`. Sửa bằng 1 `execute_code` khác, set
      `canTransitionToSelf = false` trên đúng transition `AnyState→Die` của cả 90 Controller (dùng
      `UnityEditor.Animations.AnimatorController` API trực tiếp, không phải `manage_animation` vì tool
      không có tham số này). Verify lại: Die chạy đủ 6 frame (00→05) rồi giữ nguyên frame cuối vĩnh
      viễn — đúng hành vi mong muốn. (`AnyState→Damage` dùng TRIGGER nên không bị lỗi này — trigger tự
      reset sau khi tiêu thụ, khác bool.)
- [x] `refresh_unity` compile sạch, **668/668 test xanh** (2 lần, trước và sau khi sửa bug
      `canTransitionToSelf`).
- [x] Verify runtime thật lần cuối bằng `execute_code`: Hit→Damage (3 frame, tự về Idle) đúng; isDead
      →Die (6 frame, giữ frame cuối) đúng SAU KHI sửa bug; isDead=false→Idle (hồi sinh giữa trận) đúng.

**Trạng thái cuối: hệ Animator ĐẦY ĐỦ 5 trạng thái (Idle/Run/Attack/Damage/Die) cho cả 90/90 nhân
vật, ngang bằng hệ cũ, chạy hoàn toàn trên Mecanim thật, không còn C# đổi sprite theo frame ở runtime.**

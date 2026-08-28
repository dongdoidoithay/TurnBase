# Task: Hoàn thiện enemy_goblin_v2 vào game

Yêu cầu người dùng: "hoàn thiện enemy_goblin_v2 vào game" — pilot art (`task-combat-dungeon-redesign.md`
Phase A, đã commit trước đó ở `cbbb209`) chỉ có 8 frame idle/attack, KHÔNG có static portrait,
KHÔNG có AnimatorController, KHÔNG có dòng nào trong `enemies.csv` — không thể spawn được trong
game thật. Task này hoàn thiện đủ để enemy này thực sự chơi được.

## Việc đã làm

- [x] `Tools/pixel-art-pipeline/scripts/monster_draw.py` — thêm 2 state còn thiếu: `damage` (2 frame,
      bật ngửa lúc trúng đòn) và `die` (4 frame, lean+head_bob tăng dần mô phỏng đổ gục về trước).
      "Run" KHÔNG cần vẽ riêng — trận đấu trong game không có bước "đi bộ" thật (khớp
      `task-animator-migration.md` §0), tái dùng clip Idle.
- [x] **Bug thật phát hiện ở 8 frame pilot GỐC (idle/attack), tự sửa trước khi làm tiếp:** `.meta`
      của chúng khai sprite rect `32×32` trong khi ảnh THẬT là `48×48` (canvas monster_draw.py dùng,
      to hơn hẳn quy ước nhân vật khác) — nghĩa là sprite hiển thị chỉ 1 góc ảnh bị CẮT, VÀ
      `spritePixelsToUnits: 32` khiến kích thước thế giới = 48/32 = 1.5 lần các nhân vật khác (quái
      to bất thường so với hero/goblin thường). Ghi đè lại TOÀN BỘ 14 file `.meta` (8 cũ + 6 mới)
      bằng template sạch: rect đúng 48×48, `spritePixelsToUnits: 48` (giữ world-size = 1 unit, khớp
      quy ước "PPU = kích thước canvas" mọi nhân vật khác trong dự án).
- [x] Portrait tĩnh `enemy_goblin_v2_v1_00.png` — tái dùng đúng pose `idle_00` (không vẽ thêm 1 pose
      mới riêng), khớp `LoadEnemyPortrait()` đã xây ở Enemies panel (task-ui-chrome-popups.md §3.12).
- [x] `enemies.csv` — thêm dòng `enemy_goblin_v2` (nameKey `enemy.goblin_v2.name`, stats/archetype/
      skillIds/aiProfileId **giống hệt** `enemy_goblin` gốc — đây là bản reskin phong cách, không
      phải quái mới cân bằng riêng, chapter=1 để tự động vào chung hồ chứa địch chương 1 như
      goblin thường).
- [x] `strings.csv` — thêm `enemy.goblin_v2.name` = "Yêu Tinh Rừng" / "Forest Goblin" (tên riêng
      tránh trùng "Goblin" với bản gốc, dù stats giống hệt).
- [x] Chạy menu **Tools/Import Game Data** (`Assets/Tools/DataImport/CsvToScriptableObject.cs`) —
      sinh `Assets/_Project/Resources/Data/Enemies/enemy_goblin_v2.asset`. Log xác nhận
      "67 enemy" (tăng từ 66) và `DataValidator` 0 lỗi/0 cảnh báo.
- [x] 5 `AnimationClip` (idle/run/attack/damage/die) qua `execute_code` +
      `AnimationUtility.SetObjectReferenceCurve` (cách duy nhất tạo curve kiểu Sprite — giống hệt
      kỹ thuật `task-animator-migration.md` đã dùng cho 90 nhân vật kia; `manage_animation` MCP tool
      không hỗ trợ).
- [x] `AnimatorController` qua `manage_animation` (tạo controller/4 tham số/5 state) +
      `execute_code` (8 transition, dùng trực tiếp `UnityEditor.Animations` API vì
      `controller_add_transition` mặc định sai `hasExitTime`/`duration` so với mẫu 90 controller kia
      — đọc NGƯỢC 1 controller có sẵn (`enemy_goblin.controller`) qua reflection để lấy đúng số liệu
      tham chiếu rồi build khớp 100%, bao gồm `canTransitionToSelf=false` trên `AnyState→Die` (bug
      đã tìm ra và sửa ở `task-animator-migration.md` cho 90 nhân vật đầu, áp dụng lại đúng ở đây).
- [x] Verify Play mode thật: `QueueBattle` với `enemyIds=["enemy_goblin_v2","enemy_goblin_v2"]` —
      spawn thành công, sprite hiện đúng kích thước (so với hero, không còn to bất thường), style
      chunky/viền dày rõ rệt khác goblin thường, trận tự chạy hết (goblin bị đánh bại bình thường,
      không lỗi runtime nào trong Console). `portrait`/`controller` load qua `Resources.Load` xác
      nhận đúng tên. 668/668 EditMode test vẫn xanh (thêm content data không phá logic nào).

## Chưa làm (ngoài phạm vi "hoàn thiện vào game" tối thiểu)

- Chưa thêm `enemy_goblin_v2` vào bất kỳ danh sách "quái đặc biệt"/Tower/TrialBoss nào — chỉ tự
  nhiên xuất hiện trong hồ chứa chương 1 chung (do `chapter=1` trong CSV), giống mọi enemy thường
  khác cùng chương.
- Chưa cân bằng lại stats riêng cho bản "v2" — cố tình giữ giống hệt `enemy_goblin` gốc (xem lý do
  ở trên) — nếu muốn quái này mạnh/yếu hơn cần người dùng quyết định số liệu cụ thể.

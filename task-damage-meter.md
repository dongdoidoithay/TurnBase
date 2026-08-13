# Task: Damage Meter UI

Yêu cầu: chọn qua `AskUserQuestion` ("Làm Damage Meter UI") sau khi xác nhận lại (hạng mục cuối
cùng còn lại đúng tầm, đụng `Game.CombatView` — khu vực rủi ro cao hơn các task UI Meta gần đây).
Việc mới, đụng vùng nhạy cảm hơn — đọc kỹ trước khi sửa, viết xong task file này rồi mới chạm code.

## §0. Findings

Đọc `Combat/Events/CombatEvent.cs`, `Combat/Model/BattleState.cs`, `Combat/Systems/ActionResolver.cs`
(2 chỗ gọi `RecordDamage`), `Combat/Systems/StatusProcessor.cs` (DoT tick), `CombatView/
CombatPresenter.cs`, `UI/Battle/BattleHudScreen.cs` (807 dòng, toàn bộ HUD dựng bằng CODE runtime,
không phải prefab tĩnh).

- **Phát hiện quan trọng nhất — hạ tầng ĐÃ CÓ SẴN, chỉ thiếu UI**: `BattleState.DamageByUnit`
  (`Dictionary<int, long>`) đã tồn tại với chính doc-comment: **"Thống kê để tính thưởng và Damage
  Meter"**. `RecordDamage(unitId, amount)` được gọi ĐÚNG ở mọi đường sát thương thật:
  `ActionResolver` (đòn đánh trực tiếp — dòng 144, ghi cho `actor.Id`), `StatusProcessor` (DoT tick
  — Burn/Poison/Bleed, ghi cho người gây status gốc `s.SourceUnitId`, không phải người bị tick),
  và cả Counter/Reflect (`ActionResolver.ResolveReactions` — ghi ĐÚNG cho bên phản đòn thật sự gây
  sát thương, không phải actor gốc). Nghĩa là `DamageByUnit` đã là tổng chính xác, đầy đủ mọi nguồn
  sát thương thật trong trận — **không cần quét `CombatEventQueue` tự làm lại việc này** (rủi ro
  hơn: `CombatEventQueue.TryDequeue` bị `CombatPresenter` tiêu thụ, tự dequeue thêm sẽ ăn tranh
  event của Presenter — `DamageByUnit` tránh hẳn vấn đề này vì là dictionary cộng dồn, không phải
  hàng đợi tiêu một lần).
- **`BattleHudScreen` KHÔNG dùng prefab** (khác mọi Meta screen session này đã làm) — toàn bộ dựng
  runtime bằng `TextMeshProUGUI`/`Image` qua helper `Panel()`/`Label()`/`Bar()`/`BuildAvatar()` có
  sẵn trong chính file, với sprite bo góc runtime tự vẽ (`RoundedSprite()`/`CircleSprite()`). Đọc
  `BattleState` mỗi frame trong `Update()` (rẻ, không cấp phát) — HeroPanel/EnemyPanel/TurnOrderBar
  đều theo mẫu này. **Damage Meter phải theo ĐÚNG mẫu này** (thêm panel + refresh trong `Update()`),
  KHÔNG dùng UnityEngine.UI.Text/prefab tĩnh như các task Meta gần đây — 2 hệ UI khác nhau trong
  cùng dự án, đã xác nhận qua ảnh tham khảo `_Reference/UI_SAMPLE/UI_01.jpg` người dùng gửi (quyết
  định: chỉ tham khảo cho sau này, giữ nguyên phong cách hiện có của TỪNG khu vực — Battle HUD dùng
  style code-dựng+TMP, Meta dùng style prefab+Text, không trộn).
- **Quy tắc kiến trúc HUD** (doc-comment đầu file): "HUD chỉ ĐỌC BattleState và phát sự kiện lên
  trên. Không bao giờ gọi thẳng vào CombatSimulation" — Damage Meter chỉ ĐỌC
  `_sim.State.DamageByUnit`/`_sim.State.GetUnit(id)`, không gọi method nào làm thay đổi simulation.
  Khớp đúng nguyên tắc, không vi phạm.
- **Layout còn trống**: HeroPanel (trái-trên), EnemyPanel (phải-trên), TurnOrderBar (giữa-trên),
  SkillGrid (giữa-dưới, ~292px rộng), EndTurn (giữa-dưới), AutoSpeed (phải-dưới) — **góc trái-dưới
  hoàn toàn trống**, đủ chỗ cho 1 panel gọn ~150×120.
- **Tên hiển thị**: dùng `Short(defId)` — helper CÓ SẴN trong chính file (cắt "hero_"/"enemy_"/
  "boss_" + giới hạn 14 ký tự), ĐÚNG cách `EnemyPanel` đã hiện tên — không cần tự viết lại/không
  cần `HeroDisplayUtil` (dù `Game.CombatView.asmdef` có ref `Game.Meta` nên kỹ thuật vẫn gọi được,
  nhưng dùng `Short()` nhất quán với cách EnemyPanel/TurnOrderBar đã hiện tên trong CHÍNH file này).
- **Màu theo phe**: `HERO_ACCENT`/`ENEMY_ACCENT` đã là hằng số có sẵn trong file — tái dùng, không
  cần bảng màu mới.
- **Không cần reset thủ công giữa các trận nhiều đợt (Tháp Vô Tận)** — `DamageByUnit` không bao giờ
  bị `Clear()` trong code thật (grep xác nhận), cộng dồn suốt battle instance (kể cả nhiều đợt Tháp
  Vô Tận trong 1 `CombatSimulation` liên tục) — đúng ý nghĩa "damage cả trận", không cần xử lý gì
  thêm, mỗi `BattleHudScreen`/`CombatSimulation` mới là 1 instance mới nên tự động sạch.

## §1. Scope decision

**Trong phạm vi:**
1. Panel "DamageMeter" mới trong `BattleHudScreen.cs`, góc trái-dưới, dùng đúng helper `Panel()`/
   `Label()` có sẵn — top 5 unit theo `DamageByUnit` giảm dần, tên qua `Short(u.DefId)`, màu theo
   `Side` (`HERO_ACCENT`/`ENEMY_ACCENT`), ẩn hàng nếu chưa đủ 5 unit từng gây sát thương (0 sát
   thương thì chưa hiện, tránh liệt kê rác "0 dmg" ngay từ đầu trận).
2. Refresh trong `Update()` hiện có — thêm 1 lệnh gọi `RefreshDamageMeter()`, đúng mẫu
   `RefreshHeroPanel`/`RefreshEnemyPanel`/`RefreshTurnOrder` đã có.
3. KHÔNG cần test EditMode mới — `DamageByUnit`/`RecordDamage` đã có test coverage gián tiếp qua
   `ActionCommandTests`/`SimulationTests` hiện có (grep xác nhận trước khi kết luận, xem checklist);
   phần mới thêm THUẦN là UI đọc dữ liệu có sẵn, không có logic nào đáng test riêng (giống mọi
   Refresh* khác trong `BattleHudScreen` không có test riêng).

**Ngoài phạm vi (cố ý, ghi rõ):**
- KHÔNG dùng `CombatEventQueue` — lý do đã ghi ở §0 (rủi ro tranh event với Presenter, và
  `DamageByUnit` đã đủ chính xác).
- KHÔNG thêm toggle ẩn/hiện hay popup riêng — panel luôn hiện, đúng mẫu HeroPanel/EnemyPanel/
  TurnOrderBar khác trong HUD (không có gì trong HUD hiện tại là modal/toggle).
- KHÔNG áp style ảnh tham khảo `UI_SAMPLE/UI_01.jpg` — theo quyết định người dùng, giữ nguyên
  style code-dựng hiện có.
- KHÔNG sửa `ActionResolver`/`StatusProcessor`/`DamageCalculator` — dữ liệu đã đúng sẵn, không cần
  đụng lõi combat.

## §2. Implementation checklist

- [x] Grep xác nhận `DamageByUnit`/`RecordDamage` **KHÔNG có test trực tiếp nào** (0 kết quả trong
      `Assets/Tests`, chỉ coverage gián tiếp qua HP sau trận) — đổi quyết định ban đầu, thêm
      `BattleStateTests.cs` mới (2 test: cộng dồn đúng theo từng unit, bỏ qua `amount <= 0`) trước
      khi UI bắt đầu hiển thị dữ liệu này cho người chơi.
- [x] `BattleHudScreen.cs`: thêm `MAX_METER_ROWS`, `MeterRow` struct, `_meterRows`/`_meterBuffer`,
      `BuildDamageMeterPanel()` (gọi trong `BuildLayout()`, panel góc trái-dưới trống sẵn, dùng
      đúng helper `Panel()`/`Label()` có sẵn), `RefreshDamageMeter()` (gọi trong `Update()` — đọc
      thẳng `_sim.State.DamageByUnit`, sort giảm dần bằng buffer tái sử dụng, top 5, tên qua
      `Short(u.DefId)`, màu theo `Side`).
- [x] `refresh_unity` compile sạch.
- [x] Chạy full EditMode suite — **415/415 xanh** (413 cũ + 2 `BattleStateTests` mới).
- [x] Verify Play-mode THẬT ĐẦY ĐỦ — session này gặp lại MCP frame-stall ngay từ đầu (trước cả khi
      chạm bất kỳ UI nào, `Time.frameCount` đứng ở 1 xuyên suốt) nên áp dụng kỹ thuật
      "check-before-force" đã ghi ở `feedback_unity_mcp_ui_gotchas.md`: gọi tay qua reflection
      `MetaSceneInstaller.Start()` → `LaunchBattle(node, heroIds)` (node Battle thật từ map, 4 hero
      thật từ profile) → `BattleSceneInstaller.Start()` → vào trận thật với `CombatSimulation` thật.
      Sau đó `Advance()`/`SubmitIntent()` thật (không mock) qua NHIỀU lượt, để cả 4 hero LẪN AI địch
      đều gây sát thương thật (bao gồm cả phản đòn/counter từ enemy), rồi gọi tay
      `BattleHudScreen.Update()` (thay cho vòng lặp Update() tự nhiên bị chặn bởi frame-stall) và
      đọc trực tiếp `_meterRows` — xác nhận ĐÚNG: sau lượt đầu (1 đòn) → row0 "hero_ember_kni"="96"
      màu xanh (HERO_ACCENT) đúng số damage thật `DamageByUnit` vừa ghi; sau 6 lượt (nhiều unit cả
      2 phe) → 5 hàng đúng thứ tự giảm dần theo giá trị thật (215/158/135/82/77), tên rút gọn đúng
      `Short()`, màu ĐÚNG theo phe (hero=xanh HERO_ACCENT, enemy `void_horror`/`abyss_stalker`=tím
      ENEMY_ACCENT), hàng thứ 6 (dawn_cleric=40) ĐÚNG bị cắt (chỉ top 5). Đây là lượt verify Play-
      mode ĐẦY ĐỦ VÀ THẬT NHẤT trong toàn bộ session — mô phỏng chiến đấu thật, không chỉ đọc field
      tĩnh, xác nhận cả logic sort/rank lẫn UI binding đều đúng.
- [x] Cập nhật `roadmap.md §0.1`, `object-map.md §12.1`.

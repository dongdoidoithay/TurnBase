# TASK-ENDGAME.md — Dungeon hằng ngày + Trial Boss hằng tuần + Tháp Vô Tận

> Mục tiêu: xây trọn plan.md §8.3 — Dungeon (4 loại, farm tài nguyên hằng ngày), Trial Boss (đo
> damage hằng tuần, xếp hạng cục bộ), và Tháp Vô Tận (100 tầng, HP không hồi giữa tầng, xếp hạng
> hằng tuần theo tầng cao nhất). Liên quan: [plan.md §8.3](plan.md), [roadmap.md §0.1](roadmap.md),
> [object-map.md §12/§12.1](object-map.md).
>
> **§0-§5 dưới đây viết lúc Dungeon+Trial Boss xong (Tower CHƯA làm) — giữ nguyên làm lịch sử.
> Tower được thêm sau, xem §6.**

---

## 0. Phát hiện quan trọng trước khi làm

- `DungeonKind` enum đã tồn tại sẵn trong `MetaEnums.cs`:
  `{ Gold, Exp, Material, Stone, Tower, TrialBoss }` — nhưng KHÔNG ai dùng ở đâu cả.
- `QuestConditionType.DungeonCleared` và `CurrencyReason.DungeonReward` cũng đã có sẵn (dự phòng
  cho tương lai, không dùng trong V1 này — Quest hệ V1 (task-quest.md) không có Daily Quest điều
  kiện Dungeon, ngoài phạm vi).
- `BattleState.DamageByUnit` đã cộng dồn damage mỗi actor sẵn (dùng bởi `ActionResolver.
  RecordDamage`) — Trial Boss Damage Meter tái dùng thẳng field này, không cần track mới.
- `RunContext`/`MetaSceneInstaller.ApplyPendingBattleResult` đã có pattern node-map dùng
  `MapNodeDto.Id` để khớp kết quả — Dungeon/Trial Boss KHÔNG có node thật nên cần đường vòng
  riêng (xem §2).

## 1. Phạm vi V1

- **Dungeon**: 4 loại (Gold/Exp/Material/Stone) — KHÔNG làm Tower (Tháp Vô Tận, `DungeonKind.
  Tower` dùng chung enum nhưng chưa xử lý ở đâu, để dành đợt sau). Mỗi loại chỉ mở vài ngày/tuần
  cố định (bảng hard-code, không ScriptableObject — đúng tinh thần `AwakeningCatalog`), 10 tầng/
  loại, reset UTC hằng ngày (đúng kỷ luật `QuestSystem.EnsureDailyReset`).
- **Trial Boss**: 1 boss HP cực cao (`boss_trial_champion`, mượn NGUYÊN skill kit + sprite
  `boss_void_king` — không author art/skill riêng), đo tổng damage phe Player trong 1 trận có
  turn-limit ngắn (`TRIAL_BOSS_TURN_LIMIT = 30`, riêng cho Trial Boss — trận thường không giới
  hạn lượt, boss HP quá cao để hạ gục thật nên Timeout là kết quả BÌNH THƯỜNG, không phải thua).
  Xếp hạng CỤC BỘ theo damage tốt nhất trong tuần — KHÔNG có leaderboard nhiều người chơi thật
  (offline v1, đúng khung "server-ready" plan.md §0). 3 bậc thưởng nhận CỘNG DỒN tự động ngay
  sau mỗi trận (không có nút Claim riêng — tránh thêm 1 bước UI không cần thiết cho V1).

## 2. Vấn đề "trận không gắn node map" — giải pháp sentinel `NodeId = -1`

`MetaSceneInstaller.ApplyPendingBattleResult` tra `run.MapNodes.Find(n => n.Id == result.NodeId)`
và no-op nếu không tìm thấy. `RunContext.QueueSpecialBattle` tận dụng đúng hành vi này: luôn gán
`NodeId = -1` (không node thật nào có id âm) để nhánh xử lý node-map cũ tự bỏ qua an toàn, rồi
route riêng qua `ApplySpecialBattleResult` (dựa vào `BattleOutcome.SpecialMode != null`, kiểm tra
TRƯỚC bước tra node). Không phải sửa lại logic node-map cũ.

`PendingBattle`/`BattleOutcome` (`RunContext.cs`) mở rộng thêm `DungeonKind? SpecialMode`,
`int SpecialFloor`, và `BattleOutcome` thêm `long TotalPlayerDamage` (chỉ có ý nghĩa khi
`SpecialMode == TrialBoss`, luôn 0 cho mọi trận khác).

## 3. `DungeonSystem.cs` / `TrialBossSystem.cs` (`Meta/Endgame/`)

- [x] Pure static class, giống `QuestSystem`/`AscendSystem` — không qua DI/ServiceLocator (trừ
      `IEconomyService` truyền vào làm tham số, đúng pattern các system khác).
- [x] `DungeonSystem`: `IsAvailableToday`/`EnsureDailyReset`/`FloorCleared`/`NextFloor`/
      `CanEnter`/`EnemyCountForFloor`/`IsTougherFloor`/`MarkFloorCleared`/`GrantFloorReward`.
      Reward mỗi loại: Gold = `200 * floor`; Stone = `floor` EnhanceStone; Material = 2 EssenceI
      luôn + EssenceII từ tầng ≥4 + EssenceIII từ tầng ≥8 (bậc thang, cùng tinh thần task-ascend.md
      §8); Exp = `150 * floor` cộng thẳng cho TOÀN BỘ hero sở hữu (không chỉ đội hình ra trận —
      khớp cách `MetaSceneInstaller.ApplyPendingBattleResult` đã làm cho thưởng trận thường).
- [x] `TrialBossSystem`: `WeekKey`/`EnsureWeeklyReset`/`RecordAttempt`/`TryClaimRewards`.
      `WeekKey` dùng số nguyên `(utcNow - epoch).Days / 7` thay vì ISO week API — tránh phụ thuộc
      API .NET có thể thiếu trên runtime Unity. `TryClaimRewards` nhận CỘNG DỒN mọi bậc mới đủ
      điều kiện (không chỉ bậc cao nhất) — nhảy thẳng lên bậc 3 vẫn phải nhận đủ cả bậc 1+2.
- [x] 25 test EditMode (`DungeonSystemTests.cs` 15 + `TrialBossSystemTests.cs` 10) — reset theo
      ngày/tuần, tầng chỉ tăng không giảm, thưởng đúng công thức từng loại, claim cộng dồn không
      double-grant, `WeekKey` qua mốc 7 ngày.

## 4. `DungeonScreen.cs` / `TrialBossScreen.cs` (`Meta/Endgame/`)

- [x] Nhân bản `UI_Quest.prefab` → `UI_Dungeon.prefab` (4 row: Gold/Exp/Material/Stone) và
      `UI_TrialBoss.prefab` (3 row bậc thưởng + 1 row nút ATTACK) — đúng khuôn Row NameLabel/
      ProgressLabel/ClaimButton đã có, không dựng UI mới từ đầu.
- [x] TopBar (Boot.unity) thêm 2 nút `DungeonButton`/`TrialBossButton` — static authoring (nhân
      bản `QuestButton`, đúng quy ước Hierarchy-authored của dự án), KHÔNG code-generate runtime.
- [x] `MetaSceneInstaller.LaunchDungeonFloor(kind)`/`LaunchTrialBoss()` mở `TeamSelectScreen`
      (truyền `node = null` — đã kiểm tra `TeamSelectScreen` chỉ LƯU tham chiếu node, không đọc
      field nào của nó, nên an toàn) rồi gọi `RunContext.QueueSpecialBattle`.

### Bug phụ phát hiện + đã vá trong lúc làm task này

- **TopBar có Label thừa**: `SummonButton` (2 Label con: "SET" thừa + "SUMMON" đúng) và
  `QuestButton` (2 Label con: "QUEST" đúng + "SUMMON" thừa) — rớt lại từ lần nhân bản nút trước
  đó (mỗi lần nhân bản 1 nút mới quên xoá Label cũ trước khi đổi text), đè chữ chồng lên nhau tại
  cùng vị trí `(0,0)`. Đã dọn sạch (giữ đúng 1 Label/nút) trước khi dùng `QuestButton` làm mẫu cho
  2 nút mới — nếu không sẽ nhân bản luôn cả lỗi.
- **Text bị wrap-rồi-mất ở Row hẹp**: `NameLabel` (150×26)/`ProgressLabel` (90×26)/`WalletLabel`
  (200×26) kế thừa từ `UI_Quest.prefab` đều `Horizontal Overflow = Wrap` + `Vertical Overflow =
  Truncate`. Đủ cho text ngắn của `QuestScreen` nhưng câu dài hơn của Dungeon/Trial Boss ("Gold
  Dungeon — Floor 1/10", "Tier 3 — 10,000 dmg · 500 Gem + 10 Shards") bị wrap xuống dòng 2 rồi
  MẤT vì Truncate — dữ liệu/logic đúng 100%, chỉ là UI cắt mất. Vá bằng cách nới rộng
  `RectTransform.sizeDelta` các label này trong code (`DungeonScreen`/`TrialBossScreen.BuildShell`)
  thay vì sửa lại `UI_Quest.prefab` gốc (tránh ảnh hưởng `QuestScreen` đang chạy). **Chưa kiểm tra
  lại `QuestScreen` gốc có dính lỗi tương tự với tên Quest/Achievement dài hay không** — ngoài
  phạm vi task này, để ý nếu sau này thêm Quest có tên dài.
- **Trial Boss cần turn-limit riêng**: `BattleSceneInstaller._turnLimit` (Inspector) mặc định 0
  (không giới hạn, chỉ có `SAFETY_TURN_LIMIT=200` làm trần cuối). Boss HP quá cao để hạ gục thật
  trong khung đó → thêm `TRIAL_BOSS_TURN_LIMIT = 30` áp riêng khi `SpecialMode ==
  DungeonKind.TrialBoss`, để trận LUÔN kết thúc bằng Timeout ở quy mô hợp lý cho UX 1 lượt thử.
- **Overlay "+0 Gold +0 EXP" gây hiểu nhầm**: trận Dungeon/Trial Boss cố tình bỏ qua tính Gold/EXP
  ở Battle scene (tính thật ở Meta, tránh trùng logic 2 nơi) — nhưng `BuildResultOverlay` ban đầu
  vẫn hiện nguyên câu "+0 Gold +0 EXP" như trận thường, trông như bug. Sửa: Dungeon hiện "Floor
  cleared! Rewards granted."/"Floor failed — try again.", Trial Boss hiện "Damage dealt: N".

## 5. Giới hạn đã biết / ngoài phạm vi

- `DungeonKind.Tower` (Tháp Vô Tận) chưa xử lý — chỉ định nghĩa enum, không có system/UI.
- Trial Boss không đổi boss/stat theo tuần (nội dung tĩnh v1, không phải giới hạn kỹ thuật — chỉ
  đơn giản hoá có chủ đích, plan.md không có cơ chế luân chuyển nội dung theo mùa).
- Không có leaderboard nhiều người chơi thật (offline v1 — đúng khung "Meta/Online: Offline v1,
  kiến trúc server-ready" plan.md §0).
- **Verify môi trường Play mode**: launch flow (DungeonScreen → TeamSelectScreen →
  LaunchDungeonBattle → Battle scene → Victory → return Meta → reward) đã verify TỪNG BƯỚC qua
  gọi method trực tiếp (`execute_code`, bypass UI click — phiên MCP-driven Play mode này bị "kẹt
  frame" không tick Update() bình thường, ServiceLocator có lúc mất đăng ký giữa chừng, khớp
  pattern đã ghi nhận ở memory `feedback_shared_editor_session.md`). Round-trip Meta→Battle→Meta
  đầy đủ qua LoadScene thật CHƯA verify end-to-end trong 1 lần chạy liền mạch do giới hạn môi
  trường này — logic từng mảnh (DungeonSystem/TrialBossSystem, UI render, RunContext data) đã xác
  nhận đúng riêng lẻ, và pattern round-trip này giống hệt luồng trận thường đã chạy thật nhiều lần
  trước đó (không có gì mới về mặt cơ chế).

---

## 6. Tháp Vô Tận (thêm sau — "tiếp tục Tháp Vô Tận cho tôi")

`DungeonKind.Tower` giờ đã xử lý thật — dùng CHUNG mọi hạ tầng ở §0-§5 ở trên (sentinel
`NodeId=-1`, `PendingBattle`/`BattleOutcome.SpecialMode`/`SpecialFloor`, `TeamSelectScreen(node:
null)`, pattern nhân bản `UI_Quest.prefab`) — chỉ khác ở CƠ CHẾ CHIẾN ĐẤU (§6.2, hoàn toàn mới,
không có ở Dungeon/Trial Boss) và Meta-side scope (weekly rank như Trial Boss, nhưng đo bằng
"tầng cao nhất" thay vì "damage").

### 6.1. `TowerSystem.cs` (`Meta/Endgame/`)

Pure static class, cùng khuôn `TrialBossSystem` — dùng lại thẳng `TrialBossSystem.WeekKey` (không
viết lại công thức tính tuần). 2 mốc riêng biệt, cả 2 đều được ghi mỗi lần leo
(`RecordClimb`):
- `TowerProgressDto.BestFloorThisWeek` — reset hằng tuần (`EnsureWeeklyReset`), dùng tính bậc
  thưởng, giống hệt `TrialBossProgressDto.BestDamageThisWeek`.
- `ProgressDto.TowerFloor` — **field đã có sẵn từ trước** (dead field, chưa ai dùng, đúng pattern
  "data model có sẵn chưa wire" đã gặp nhiều lần ở các task khác) — mốc CAO NHẤT MỌI THỜI ĐẠI,
  KHÔNG BAO GIỜ reset, kể cả qua tuần mới. Tách biệt 2 mốc này để tuần sau vẫn giữ được kỷ lục cũ
  dù `BestFloorThisWeek` đã về 0.

5 bậc thưởng (placeholder, chưa qua Balance Harness — cùng tinh thần `TrialBossSystem.TIERS`):
floor 10/25/50/75/100 → Gem tăng dần + Core (từ bậc 2) + **1 trang bị Mythic thật** ở bậc cuối
(`EquipmentGenerator.Roll(null, Rarity.Mythic, rng)` — hàm này ĐÃ hỗ trợ mọi rarity kể cả Mythic từ
trước, không cần sửa gì ở `EquipmentGenerator`). `TryClaimRewards` nhận CỘNG DỒN mọi bậc mới đủ
điều kiện, giống hệt `TrialBossSystem`.

`EnemyCountForFloor`/`IsTougherFloor`: 3 địch (tầng ≤20) → 4 (≤60) → 5 (≤100), tougher từ tầng 41
— quy mô lớn hơn Dungeon (10 tầng) vì Tower có 100 tầng, breakpoint giãn ra tương ứng.

12 test EditMode (`TowerSystemTests.cs`) — reset tuần giữ nguyên mốc mọi-thời-đại, `RecordClimb`
không hạ mốc khi leo yếu hơn, claim cộng dồn tới cả Mythic equipment, `EnemyCountForFloor`/
`IsTougherFloor` đúng breakpoint.

### 6.2. Cơ chế chiến đấu mới: nhiều đợt địch trong 1 `CombatSimulation`

**Vấn đề:** "100 tầng, HP không hồi giữa tầng" (plan.md §8.3) đòi hỏi HP phe Player CARRY OVER
giữa các tầng — nhưng game KHÔNG có field nào lưu "HP hiện tại" của hero ngoài trận (`HeroInstanceDto`
chỉ có Level/Exp/Star/SkillLevels/Equipped, mọi trận luôn bắt đầu full HP tính lại từ stat gốc).
Cân nhắc 2 hướng:
- (A) Quay lại Meta giữa mỗi tầng (giống Dungeon), tự lưu "HP snapshot" tạm thời để truyền tiếp
  sang trận sau — cần schema/state mới, phức tạp, và không tận dụng được gì có sẵn.
- (B) **1 lượt leo = 1 `CombatSimulation` LIÊN TỤC** — không bao giờ rời Battle scene giữa các
  tầng, chỉ đổi đợt địch. HP hero là field runtime của `CombatUnit` (đã sống trong bộ nhớ suốt
  trận) — không chạm gì vào nó thì nó TỰ NHIÊN không đổi, không cần lưu/khôi phục gì cả.

Chọn (B) — tận dụng đúng tinh thần "tối thiểu nhưng thật": không có cơ chế mới nào phải xây để
lưu HP, chỉ cần KHÔNG reset nó.

**Tiền lệ đã có:** `ActionResolver.ResolveSummon` (skill triệu hồi) đã gọi `_state.AddUnit(minion)`
để thêm unit MỚI giữa trận đang chạy — chứng minh việc thêm unit mid-battle là pattern đã được
engine hỗ trợ, không phải hướng đi chưa kiểm chứng.

**Implement:**
- `CombatSimulation.OnEnemyWaveCleared` (`public Func<CombatSimulation, bool>`, mặc định `null`) —
  field mới, KHÔNG đổi hành vi bất kỳ trận nào khác (đã xác nhận qua 308→323 test vẫn xanh tuyệt
  đối, không có regression).
- `CheckEnd()` sửa: khi `EvaluateResult()` trả `Victory` (phe Enemy bị wipe), hỏi
  `OnEnemyWaveCleared` TRƯỚC khi gọi `Finish()` — hook trả `true` (đã spawn đợt mới) thì
  `CheckEnd()` return sớm, KHÔNG `Finish()`, trận tiếp tục ở `Phase.TurnStart` như chưa có gì xảy
  ra. Hook trả `false` (hoặc null) → `Finish(Victory)` y hệt trước đây.
- `BattleSceneInstaller.TryAdvanceTowerWave` implement hook: tăng `_towerFloorsCleared`/
  `_towerFloor`; nếu vượt `TowerSystem.MAX_FLOOR` → trả `false` (leo hết 100 tầng, kết thúc thật
  bằng Victory). Ngược lại: **destroy `UnitView` của đợt địch cũ** (đã chết — `PlayDeath()` chỉ
  đổi pose chứ KHÔNG tự destroy GameObject, để nguyên sẽ đè hình lên đợt mới cùng slot vì
  `ENEMY_SLOTS` chỉ có 5 vị trí cố định dùng lại cho mọi tầng), rồi `SpawnTeamFromDefinitions`
  y hệt code path spawn ban đầu (tự động `Simulation.AddUnit` + tạo `UnitView` mới), và
  `Presenter.RegisterView` cho từng view mới (không tự động — phải gọi tay, khác lúc `BuildBattle()`
  gọi 1 lần cho cả đội hình ban đầu).
- `PickTowerEnemies(floor)`: chọn địch qua **`Simulation.State.Rng`**, KHÔNG phải
  `UnityEngine.Random` (khác `MetaSceneInstaller.PickEnemies` dùng cho tầng 1 — đó là lựa chọn MỘT
  LẦN ở Meta, trước khi seed combat tồn tại, nên không cần xác định). Từ tầng 2 trở đi, việc chọn
  địch xảy ra TRONG Battle scene khi seed đã chạy — phải xác định theo seed để `BattleReplay` (edge
  case E17) resume đúng nếu app thoát giữa 1 lượt leo Tháp. Không giới hạn theo chương (nội dung
  endgame, dùng chung mọi enemy — chapterId=0 truyền cho `PickEnemies` ở Meta để rơi vào nhánh
  fallback "mọi enemy" có sẵn, không cần sửa `PickEnemies`).
- `ShowResultOverlay`/`BuildResultOverlay`: thêm nhánh `isTower` — MỌI kết quả (Victory-hết-100-
  tầng/Defeat/Escaped/Timeout) đều hiện `displayAsSuccess=true` và "Floor {N} reached!" (không có
  khái niệm "thua" ở Tower, chỉ là dừng lại ở đâu — giống triết lý Trial Boss nhưng áp dụng cho cả
  3 kết quả còn lại thay vì chỉ Timeout). `HandleContinue` báo `SpecialFloor` về Meta là
  `_towerFloorsCleared` (tầng ĐÃ LEO ĐƯỢC trong lượt này), KHÁC Dungeon nơi `SpecialFloor` là tầng
  cố định của cả trận — 2 field trùng tên nhưng khác ngữ nghĩa giữa `PendingBattle`/`BattleOutcome`.

### 6.3. `TowerScreen.cs` + `TowerButton`

Nhân bản `UI_Quest.prefab` GIỮ NGUYÊN đủ 6 row gốc (khác Dungeon/TrialBoss phải trim bớt) — 5 row
bậc thưởng + 1 row nút CLIMB, đúng khuôn `TrialBossScreen`. TopBar thêm nút thứ 3 (`TowerButton`),
tiếp tục nhân bản `QuestButton` — tổng 3 nút Endgame trên TopBar giờ là DUNGEON/TRIAL/TOWER.

**Text length lại là vấn đề (y hệt §4 ở trên, tái diễn dù đã biết trước):** "Best this week: Floor
N · All-time best: Floor M" (56 ký tự) và "Climb the Tower — HP does not heal between floors."
(52 ký tự) đều bị wrap-rồi-Truncate ở box hẹp kế thừa. Lần này SỬA THEO CÁCH KHÁC — không nới rộng
box nữa (đã thử ở Dungeon/TrialBoss, tốn công tính lại vị trí neo mỗi lần), mà **RÚT NGẮN CÂU CHỮ**
cho chắc chắn nằm gọn 1 dòng: "This week: 32 · All-time: 55" (29 ký tự), "Ready to climb?" (16 ký
tự). Bài học: với box Text kế thừa cố định trong dự án này, rút ngắn text luôn rẻ và chắc chắn hơn
tính lại kích thước box.

### 6.4. Verify

- **EditMode (đáng tin cậy 100%)**: 323/323 test xanh (308 cũ + 12 `TowerSystemTests` + 3
  `MultiWaveTests`). `MultiWaveTests` (Combat layer, KHÔNG phụ thuộc Unity Play mode) chứng minh
  trực tiếp: HP hero giữ nguyên tuyệt đối khi `OnEnemyWaveCleared` spawn đợt mới; trả `false` thì
  trận kết thúc bình thường bằng Victory; để `null` thì hành vi y hệt trước khi có tính năng này
  (regression an toàn cho mọi trận khác).
- **Play mode thật**: launch Tower → Battle scene → hạ hết địch tầng 1 (verify: tầng 2 spawn đúng
  3 địch mới, cả 4 hero HP giữ nguyên TUYỆT ĐỐI so với trước khi hạ địch, tổng unit=10 đúng 4 hero
  + 3 xác tầng 1 + 3 địch tầng 2 mới) → leo tiếp rồi thua ở tầng 2 (verify: `floorsCleared` báo về
  đúng = 1, không phải 2 — chỉ tính tầng ĐÃ QUA, không tính tầng đang đánh dở). Cả 2 bước verify
  PHẢI chạy trong 1 lời gọi `execute_code` liền mạch — tách ra nhiều lời gọi riêng từng bị dính
  race condition của scene-load bất đồng bộ (kết quả đọc được stale/reset), không phải bug thật
  (xem memory `feedback_unity_mcp_ui_gotchas.md`).
- **Chưa verify**: round-trip Meta→Battle→Meta đầy đủ (reward crediting thật) — cùng giới hạn môi
  trường ServiceLocator đã ghi ở §5, không phải lỗi riêng của Tower.

# Task: Arena PvP (plan.md v1.1, TopBar placeholder → thật)

Tiếp nối `task-chapter-arena.md` (đã dựng nút TopBar + Toast "Coming Soon" placeholder cho v1.0).
Người dùng chọn làm thật cho phiên này qua AskUserQuestion (cùng 3 mục khác: AI diversity/
Accessibility/content localization).

## §1. Ràng buộc thật — không có backend

plan.md: `Arena (v1.1) | Mùa 14 ngày | PvP async với snapshot đội hình do AI điều khiển | Honor`.
Dự án này **hoàn toàn local-save** (`LocalPlayerRepository`, không server) — "PvP với người chơi
thật" theo nghĩa đen là bất khả thi. Diễn giải trung thực nhất khớp với chính plan.md ("snapshot đội
hình DO AI ĐIỀU KHIỂN", không nói "người chơi thật khác"): sinh **đối thủ ảo** (team hero thật, chỉ
số/cấp/sao được RNG chọn, KHÔNG giả vờ là dữ liệu người chơi khác) — đúng bản chất "ghost battle"
nhiều game F2P dùng khi chưa có server, không lừa người chơi rằng có multiplayer thật.

## §2. Kiến trúc — tái dùng tối đa, KHÔNG đụng code nhạy cảm sẵn có

- **`DungeonKind.Arena = 6`** thêm vào enum đã dùng chung cho "special battle mode" (Tower=4/
  TrialBoss=5 đã theo đúng mẫu này) — `RunContext.QueueSpecialBattle(DungeonKind.Arena, ...)`.
- **`ArenaProgressDto`** mới trong `PlayerProfileDto.cs` — mirror `TrialBossProgressDto` (
  `LastSeasonKey` số nguyên ngày/14 thay vì tuần, KHÔNG parse chuỗi ISO), `List<ArenaOpponentDto>`
  (snapshot đối thủ hiện tại, ổn định tới khi hết mùa), `Rating` (số hiển thị kiểu ELO, tăng khi
  thắng, KHÔNG giảm khi thua — đúng tinh thần "không phạt nặng" đã thấy ở NodeChoice/Enhance).
- **`ArenaOpponentDto`**: `string[] HeroDefIds` (3 hero), `Level`, `Star`, `HonorReward`, `Claimed`
  (đã thắng lượt này chưa — không cho nhận Honor lặp lại tới khi hết mùa, giống Tower/TrialBoss
  claimed-tier).
- **`ArenaSystem`** (pure static, `Game.Meta.Endgame`, mirror `TrialBossSystem`): `SeasonKey`,
  `EnsureSeasonReset` (KHÔNG tự sinh đối thủ ở đây — chỉ reset state, đúng tách bạch pure/impure đã
  có: `TrialBossSystem`/`TowerSystem`/`DungeonSystem` không hề random-pick nội dung, việc đó luôn ở
  `MetaSceneInstaller` — xem `PickEnemies`/`PickBoss` dùng thẳng `UnityEngine.Random`), `TryClaim`
  (cấp Honor + đánh dấu Claimed, không cấp lại).
- **`MetaSceneInstaller.PickArenaOpponents()`** (impure, mirror `PickEnemies`) — SINH MỚI khi
  `EnsureSeasonReset` phát hiện đổi mùa: lấy toàn bộ `HeroDefinitionSO` catalog, chọn ngẫu nhiên 3
  hero/đối thủ × 5 bậc, Level/Star tăng dần theo bậc SCALE THEO CẤP TRUNG BÌNH ĐỘI HÌNH NGƯỜI CHƠI
  HIỆN TẠI (không hardcode số tuyệt đối — tự thích ứng độ khó thật theo tiến độ, giống Dungeon/
  Tower/TrialBoss scale theo chương/tuần).
- **`BattleSceneInstaller.SpawnArenaOpponentTeam`** — method MỚI, KHÔNG sửa
  `SpawnTeamFromDefinitions` hiện có (tránh rủi ro hồi quy đường spawn nóng nhất của game) — copy
  đúng nhánh "player" của hàm đó (load Addressables `Data/Heroes/{defId}`, `HeroLevelSystem.
  EffectivePrimary`) nhưng gán `TeamSide.Enemy` + AI (`ai_special`, cùng hồ sơ AI enemy thường —
  "AI điều khiển" đúng nghĩa plan.md, không cố mô phỏng quyết định người chơi thật) thay vì đọc
  `HeroInstanceDto`/trang bị thật của người chơi (đối thủ ảo không có Equipment/Ascend thật — chỉ
  Level/Star snapshot đã sinh).
- **UI**: `ArenaScreen`/`UI_Arena.prefab` — clone `UI_Quest.prefab` (5 dòng = 5 bậc đối thủ,
  NameLabel hiện tên 3 hero rút gọn, ProgressLabel hiện trạng thái CLAIMED/CHALLENGE), nút CHALLENGE
  launch trận qua `RunContext.QueueSpecialBattle`.
- **`ApplySpecialBattleResult`** (dispatcher có sẵn cho Tower/TrialBoss) thêm nhánh `DungeonKind.
  Arena`: thắng → `ArenaSystem.TryClaim` (Honor + Rating +) — thua → KHÔNG mất gì (đúng %2 "không
  phạt nặng").

## §3. Cố ý ngoài phạm vi

- Season 14 ngày chỉ tính bằng `LastSeasonKey`, KHÔNG có countdown UI thật (đủ hạ tầng, chưa cần
  hiển thị đếm ngược — v1 tối thiểu).
- KHÔNG có leaderboard nhiều người chơi thật (đúng tinh thần TrialBossSystem đã ghi rõ "offline v1,
  không leaderboard nhiều người chơi").
- Shop Honor (đổi Honor lấy phần thưởng) — plan.md có nhắc nhưng KHÔNG có trong scope Arena này,
  Honor tạm thời chỉ tích luỹ (giống nhiều currency khác trong game đã tồn tại trước khi có nơi
  tiêu, xem `InventoryScreen` hiện Honor nếu cần) — để dành lượt sau nếu cần.

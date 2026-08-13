# TASK-QUEST.md — Quest hằng ngày + Achievement tối thiểu (thay Gem faucet tạm)

> Mục tiêu: thay `PlaceholderLootTable.BOSS_REWARD_GEM` (100 Gem/Boss, đặt tạm ở task-ascend.md §9)
> bằng nguồn Gem thật hơn — Quest hằng ngày + vài Achievement một-lần. Liên quan:
> [plan.md §9.1](plan.md), [roadmap.md Tuần 19](roadmap.md), [object-map.md §4.3/§5](object-map.md).

---

## 0. Phát hiện quan trọng trước khi làm

Đúng pattern đã gặp nhiều lần (Ascend/Gacha) — **data model đã có sẵn, chỉ chưa dùng**:

- `MetaEnums.cs` đã có `QuestKind {Daily, Weekly, Chain, Event}`, `QuestConditionType
  {BattlesWon, StagesCleared, PerfectHits, BreaksTriggered, SummonsPerformed, HeroLevelUps,
  EquipEnhanced, DungeonCleared, GoldSpent, LoginDays}`, `CurrencyReason` (có sẵn
  `QuestReward=2`/`AchievementReward=3`) — **cả 3 enum chưa ai dùng ở đâu cả**.
- `PlayerProfileDto.Quests: QuestProgressDto` đã có `Daily/Weekly/Chain: List<CurrencyEntryDto>`,
  `ClaimedQuestIds: List<string>`, `UnlockedAchievements: List<string>`, `LastDailyResetUtc:
  string`, `BattlePassLevel/Premium` — chỉ được null-guard trong `SaveMigrationRunner`, chưa hề
  đọc/ghi ở gameplay.
- `PlayerProfileDto.Stats: LifetimeStatsDto` đã có `BattlesWon/BattlesLost/PerfectHits/
  BreaksTriggered/TotalDamageDealt/HeroesCollected` — **KHÔNG field nào từng được tăng ở đâu cả**,
  khớp gần như 1-1 với `QuestConditionType`.
- `GachaStateDto.TotalPulls` (đã có, task-ascend.md §7) có thể dùng thẳng làm điều kiện
  `SummonsPerformed` — không cần field mới.

**Không cần đổi save schema.** Chỉ cần: (a) tăng đúng counter tại đúng chỗ, (b) 1 bảng quest/
achievement cố định (hard-code, không ScriptableObject — lý do y hệt `AwakeningCatalog`), (c) màn
hình claim, (d) reset hằng ngày dùng `LastDailyResetUtc` có sẵn.

## 1. Phạm vi V1 — CHỈ Daily Quest + Achievement một-lần

KHÔNG làm Weekly/Chain/Event quest, KHÔNG làm Battle Pass (dù field đã có) — giữ tối giản đúng
tinh thần dự án. Ghi rõ trong code đây là V1.

3 điều kiện Daily Quest chọn vì **rẻ, không cần plumbing mới**:
- `BattlesWon` — tăng thẳng trong `MetaSceneInstaller.ApplyPendingBattleResult` khi
  `result.Victory` (chỗ đã có sẵn, chỉ thêm 1 dòng).
- `HeroLevelUps` — đã có biến `totalLevelUps` tính sẵn trong cùng hàm, chỉ cộng dồn vào Stats.
- `SummonsPerformed` — đọc thẳng `profile.Gacha.TotalPulls`, không cần counter riêng.

KHÔNG làm `PerfectHits`/`BreaksTriggered` (cần plumbing mới xuyên Battle→Meta qua
`RunContext.BattleOutcome`, việc riêng nếu cần sau) và KHÔNG làm `GoldSpent`/`DungeonCleared`/
`LoginDays`/`StagesCleared`/`EquipEnhanced` (cần hệ khác chưa có hoặc phải sửa nhiều điểm tiêu
tiền cùng lúc — không tương xứng phạm vi V1).

3 Achievement một-lần (dùng `UnlockedAchievements`, điều kiện kiểm tra trực tiếp trên `profile`
mỗi lần mở màn hình, không cần trigger phức tạp):
- Sở hữu đủ 6 hero hiện có (`profile.Heroes.Count >= 6`).
- Ascend 1 hero lên ★6 (`profile.Heroes.Any(h => h.Star >= 6)`).
- Qua chương 3 (`profile.Progress.ChapterUnlocked >= 3`).

## 2. `QuestSystem.cs`

- [x] File mới `Assets/_Project/Scripts/Meta/Quest/QuestSystem.cs`, `Game.Meta.Quest`, static
      class.
- [x] Bảng Daily Quest cố định (hard-code, giống `AscendSystem.COSTS`):
      `readonly struct DailyQuestDef { string Id; QuestConditionType Condition; int Target; long
      GemReward; }` — 3 dòng khớp mục 1 (vd `BattlesWon 3 → 50 Gem`, `HeroLevelUps 5 → 50 Gem`,
      `SummonsPerformed 1 → 100 Gem`). Số liệu placeholder, ghi rõ chờ Balance Harness/playtest.
- [x] Bảng Achievement cố định tương tự: `readonly struct AchievementDef { string Id; string
      NameKey; Func<PlayerProfileDto, bool> IsUnlocked; long GemReward; }` — 3 dòng khớp mục 1.
- [x] `EnsureDailyReset(PlayerProfileDto profile, DateTime utcNow)` — so `LastDailyResetUtc` (ISO
      string) với ngày UTC hiện tại; khác ngày thì: xoá `Daily` progress list, xoá các
      `ClaimedQuestIds` thuộc Daily (giữ nguyên Chain/Achievement id nếu sau này có), set lại
      `LastDailyResetUtc`. Gọi từ `MetaSceneInstaller.Start()` mỗi lần vào Meta (đúng chỗ
      `ApplyPendingBattleResult`/`EnsureRun` đang gọi).
- [x] `GetDailyProgress(PlayerProfileDto profile, string questId) → int` — đọc từ
      `profile.Quests.Daily` (list `CurrencyEntryDto`, key = questId, value = progress).
- [x] `TryClaimDaily(PlayerProfileDto profile, string questId) → bool` — kiểm tra đủ điều kiện +
      chưa claim (`ClaimedQuestIds`), cấp Gem qua `IEconomyService.Grant`, thêm vào
      `ClaimedQuestIds`. Atomic đúng style `AscendSystem.TryAscend` (dù ở đây chỉ có 1 điều kiện,
      không phức tạp bằng, nhưng vẫn kiểm tra trước khi ghi).
- [x] `TryClaimAchievement(PlayerProfileDto profile, string achievementId) → bool` — tương tự,
      dùng `UnlockedAchievements`.
- [x] Helper đọc điều kiện hiện tại theo `QuestConditionType` từ `profile.Stats`/`profile.Gacha` —
      switch nhỏ, chỉ 3 case đang dùng (mục 1), case khác throw/log rõ "chưa hỗ trợ V1" thay vì
      âm thầm trả 0 (tránh bug im lặng nếu sau này thêm điều kiện mà quên code phần đọc).

## 3. Tăng counter đúng chỗ

- [x] `MetaSceneInstaller.ApplyPendingBattleResult`: trong nhánh `result.Victory`, thêm
      `_profile.Stats.BattlesWon++;` (nhánh thua thêm `_profile.Stats.BattlesLost++;` ở else,
      tiện thể vì field đã có sẵn dù chưa quest nào cần).
      `_profile.Stats.HeroLevelUps` — **field này KHÔNG có trong `LifetimeStatsDto`** (chỉ có
      `HeroesCollected`) → cần thêm field mới `public int HeroLevelUps;` vào `LifetimeStatsDto`
      (đây LÀ đổi save schema, nhưng chỉ thêm field mới — an toàn, JsonUtility bỏ qua field lạ
      khi đọc save cũ, field mới mặc định 0). Cộng dồn `totalLevelUps` đã tính sẵn vào đó.
- [x] `SummonsPerformed` không cần counter mới — đọc thẳng `profile.Gacha.TotalPulls` lúc kiểm
      tra điều kiện (mục 2).

## 4. UI — `QuestScreen` + nút trên TopBar

- [x] Theo đúng khuôn `ShopScreen`/`SummonScreen` (task-ascend.md §7 mục B.3/C.2): duplicate
      `UI_HeroDetail.prefab` → `UI_Quest.prefab`, sửa nội dung qua Unity Editor MCP (không dựng
      UI bằng code) — Title "QUEST", 3 dòng Daily Quest (tên điều kiện, tiến độ "x/y", nút Claim),
      3 dòng Achievement (tên, nút Claim), CloseButton.
- [x] Script mới `Assets/_Project/Scripts/Meta/Quest/QuestScreen.cs` — `AddComponent` trong
      `MetaSceneInstaller.BuildUi()`, `Open(profile, onClosed)` cùng pattern `ShopScreen.Open`.
- [x] Thêm `QuestButton` thật vào `Boot.unity` (`MetaCanvas/TopBar`) bằng Editor MCP — duplicate
      `SummonButton` làm mẫu (đã có sẵn cách làm từ task-ascend.md §7 mục B.3), đổi label "QUEST",
      đặt cạnh SummonButton. Bind trong `BindCanvasRefs()`/`BuildUi()` giống `_summonButton`.
      Cân nhắc thêm chấm đỏ nhỏ khi có quest claim được (RedDotService thật ngoài phạm vi — nếu
      làm thì chỉ 1 `Image` bật/tắt thủ công dựa trên
      `HasClaimableQuest(profile)`, không xây hệ RedDot đầy đủ).

## 5. Thay thế Gem faucet tạm

- [x] `MetaSceneInstaller.GrantBossAscendMaterials` — **XOÁ** dòng
      `_economy.Grant(_profile.Wallet, CurrencyType.Gem, PlaceholderLootTable.BOSS_REWARD_GEM);`
      (Gem giờ đến từ Quest/Achievement, không phải mỗi lần thắng Boss nữa).
- [x] `PlaceholderLootTable.BOSS_REWARD_GEM` — xoá hằng số, cập nhật comment giải thích đã thay
      bằng QuestSystem. Cập nhật `BalanceHarness`/test liên quan (xem mục 6) cho khớp.
- [x] Cập nhật `task-ascend.md` §9 — ghi rõ Gem faucet tạm đã được thay bằng Quest thật, không còn
      hiệu lực.

## 6. Test

- [x] `QuestSystemTests.cs` (`Assets/Tests/EditMode/Meta/`) — theo style `SkillUpgradeAndAscendTests.cs`:
      `EnsureDailyReset` xoá đúng progress khi qua ngày mới (mock `DateTime` truyền vào, KHÔNG
      dùng `DateTime.UtcNow` thật trong test); `TryClaimDaily` atomic (chưa đủ điều kiện → fail,
      không cấp Gem); claim 2 lần → lần 2 fail (đã claim); `TryClaimAchievement` tương tự cho cả
      3 điều kiện (đủ hero, đủ sao, đủ chương).
- [x] Cập nhật `PlaceholderLootTableTests.cs`/`BalanceHarness.cs` — xoá phần liên quan
      `BOSS_REWARD_GEM` (test `BossReward_GrantsGem_...` cần xoá hoặc viết lại vì Gem không còn
      đến từ Boss nữa).
- [x] Chạy full EditMode suite, phải xanh 100%.

## 7. Verification

- Play mode: qua 1 ngày giả lập (chỉnh `LastDailyResetUtc` lùi 1 ngày qua `execute_code`) → mở
  QuestScreen → quest reset đúng. Thắng vài trận → tiến độ `BattlesWon` tăng đúng → claim được →
  Gem cộng đúng. Mở lại QuestScreen → nút Claim disable (đã claim). Đủ điều kiện Achievement (ví
  dụ đã có 6 hero từ trước) → claim được ngay.
- Đối chiếu `BalanceHarness` — Gem giờ đến từ Quest, không còn tuyến tính theo số trận Boss như
  trước; nếu cần, thêm ước lượng Gem/ngày trung bình vào `BalanceHarness` (không bắt buộc, có thể
  để lượt sau).

# TASK-INNATE-PASSIVE.md — Hệ passive bẩm sinh mỗi hero (`CombatUnit.Passive`)

> `CombatUnit.Passive` (khác `.Awakening`) khai báo sẵn từ trước, `PassiveProcessor` đã xử lý đúng
> cả 2 slot độc lập (task-ascend.md §10 xác nhận), chỉ thiếu NGUỒN DỮ LIỆU gán vào. Khác
> `.Awakening` (chỉ có ở ★6), `.Passive` là bẩm sinh — có ngay từ ★1, không điều kiện sao.

---

## 0. Thiết kế — tận dụng 4 trigger chưa hero nào dùng

Audit ở task-ascend.md §10 phát hiện 4/10 `PassiveTrigger` có hook thật (`ActionResolver`/
`PoiseSystem`/`CombatSimulation` đã gọi) nhưng chưa Awakening nào dùng: `OnTurnStart`,
`OnDamageTaken`, `OnHpBelowThreshold`, `OnBreakTriggered`. Ưu tiên dùng 4 trigger này cho passive
bẩm sinh để có test thật (không chỉ test tay), đồng thời làm passive bẩm sinh CẢM GIÁC KHÁC
Awakening (không trùng cơ chế), bám class/element từng hero như `AwakeningCatalog` đã làm.

| Hero (Class/Element) | Tên | Trigger | Hiệu ứng | Vì sao |
|---|---|---|---|---|
| `hero_ember_knight` (Vanguard/Fire) | Iron Resolve | `OnDamageTaken` | Tự buff DefUp, 1 lượt, 30% cơ hội | Tank phản ứng khi bị đánh — khác Awakening (buff cố định đầu trận) |
| `hero_shadow_fang` (Slayer/Dark) | Quick Reflexes | `OnTurnStart` | Tự buff SpdUp 1 stack, 1 lượt | Sát thủ nhanh nhẹn mỗi lượt — khác Awakening (chỉ nổ khi giết) |
| `hero_frost_sage` (Arcanist/Water) | Frost Ward | `OnHpBelowThreshold` (0.3) | Tự Shield/DefUp 2 lượt | Pháp sư mỏng máu cần lá chắn khẩn cấp — dùng đúng trigger còn bỏ trống |
| `hero_dawn_cleric` (Warden/Light) | Unbreakable Faith | `OnBreakTriggered` | Tự hồi 1 stack Regen | Healer kiên cường kể cả khi bị Break — dùng đúng trigger còn bỏ trống |
| `hero_gale_thief` (Trickster/Wind) | Windborn | `OnBattleStart` | +8% Spd vĩnh viễn (nhẹ hơn nhiều so với Awakening) | Bẩm sinh nhanh nhẹn — trùng loại trigger với Awakening (được phép, 2 slot độc lập) nhưng biên độ nhỏ hơn hẳn |
| `hero_bone_caller` (Summoner/Dark) | Grave Whisper | `OnKill` | Tự buff AtkUp 1 stack, 2 lượt (yếu hơn Awakening: 1 stack so với 2) | Cộng dồn với Awakening khi ★6 — phần thưởng hợp lý cho việc lên tối đa |

Số liệu (stack/lượt/%) là **placeholder V1** như 6 Awakening — chờ playtest, ghi rõ trong code.

## 1. `InnatePassiveCatalog.cs`

- [x] File mới `Assets/_Project/Scripts/Meta/Hero/InnatePassiveCatalog.cs`, `Game.Meta.Hero`,
      static class — CÙNG PATTERN `AwakeningCatalog.cs` (hard-code, không ScriptableObject, lý do
      readonly-field giống hệt — copy nguyên văn đoạn comment giải thích).
- [x] `Get(string heroDefId) → PassiveData` — factory, instance MỚI mỗi lần gọi (không share,
      đúng lý do `Consumed` đã áp dụng cho `AwakeningCatalog`).
- [x] 6 entry theo bảng mục 0.

## 2. Gán vào `CombatUnit` khi ra trận

- [x] `BattleSceneInstaller.BuildUnitFromDefinition` — sau dòng gán `unit.Awakening = ...`, thêm
      `unit.Passive = Game.Meta.Hero.InnatePassiveCatalog.Get(defId);` — **KHÔNG điều kiện star**
      (bẩm sinh, luôn có, khác Awakening).
- [x] Enemy (`BuildUnit`/nhánh enemy trong `BuildUnitFromDefinition`) — KHÔNG gán Passive (chỉ
      hero người chơi có passive bẩm sinh theo thiết kế plan.md, enemy dùng AI profile riêng).

## 3. Test

- [x] `InnatePassiveCatalogTests.cs` hoặc gộp vào `PassiveProcessorTests.cs` (tuỳ độ dài) —
      giống `AwakeningCatalog_AllSixHeroes_HaveAPassive`/`AwakeningCatalog_Get_ReturnsIndependentInstances`:
      6 hero đều có passive bẩm sinh thật, mỗi lần `Get()` trả instance độc lập.
- [x] Test `BuildUnitFromDefinition` (qua `BattleSceneInstaller` hoặc gián tiếp) — hero ★1 (chưa
      Ascend) vẫn có `unit.Passive != null` (khác `.Awakening` chỉ có ở ★6).
- [x] Test tương tác 2 slot cho `hero_gale_thief`/`hero_bone_caller` ở ★6: cả `.Passive` lẫn
      `.Awakening` cùng kích hoạt đúng lúc (dùng lại
      `Passive_And_Awakening_AreIndependentSlots_BothFire` làm mẫu).
- [x] Chạy full EditMode suite, phải xanh 100%.

## 4. Verification

- Play mode: dựng 1 hero ★1 (chưa ascend) ra trận, kiểm tra `unit.Passive` gán đúng qua
  `execute_code` reflection (theo đúng cách đã verify Awakening ở task-ascend.md).
- Với `hero_shadow_fang`/`hero_ember_knight` (trigger `OnTurnStart`/`OnDamageTaken`): verify
  effect thật xảy ra trong 1 trận mô phỏng ngắn (không cần UI, dùng `CombatSimulation` trực tiếp
  qua `execute_code` như đã làm với Ember Knight Awakening).

## 5. Lệch so với thiết kế ban đầu (phát hiện lúc code, ghi lại)

- **Iron Resolve (ember_knight) bỏ "30% cơ hội"** — `StatusApplication.Chance` CHỈ gate debuff,
  buff lên bản thân LUÔN thành công 100% (đúng luật `StatusProcessor.Apply`, xem plan.md §4.11).
  `PassiveData` không có field "% cơ hội trigger" riêng, nên giữ 30% sẽ cần thêm field mới —
  không tương xứng phạm vi V1. Iron Resolve giờ kích hoạt CHẮC CHẮN mỗi lần bị đánh trúng.

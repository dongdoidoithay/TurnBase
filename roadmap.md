# ROADMAP.md — Lộ trình phát triển

> **Dự án:** TurnBase (*Aether Legion*) · Unity 6000.5.2f1 · URP 2D
> **Bộ tài liệu:** [plan.md](plan.md) — thiết kế & kiến trúc · [structure.md](structure.md) — cây cấu trúc project · [object-map.md](object-map.md) — bản đồ đối tượng & checklist chống sót logic
> **Ngày bắt đầu tính mốc:** 2026-08-06
> **Tổng thời lượng v1.0:** **30 tuần** (~7 tháng) với đội 4 người · **56–64 tuần** nếu làm một mình (xem §12)

---

## 0. Tổng quan lộ trình

| Phase | Tên | Tuần | Mốc bàn giao |
|---|---|---|---|
| **P0** | Nền móng & Tiền sản xuất | 1–2 | Project setup, git, CI, design system |
| **P1** | Prototype chiến đấu (Playable) | 3–6 | 1 trận 2v2 chơi được, có Action Command |
| **P2** | **VERTICAL SLICE** ⭐ | 7–11 | 1 chương hoàn chỉnh, đẹp, chơi được đầu-đến-cuối |
| **P3** | Combat đầy đủ | 12–15 | Toàn bộ skill/status/AI/boss/formation |
| **P4** | Meta & Progression | 16–19 | Hero, trang bị, gacha, save, inventory |
| **P5** | Nội dung & Node Map | 20–23 | 5 chương, dungeon, tháp, 24 hero, 60 enemy |
| **P6** | UI/UX Responsive & Polish | 24–26 | 23 màn hình hoàn chỉnh 2 hướng, juice đầy đủ |
| **P7** | Cân bằng, Tối ưu, Kiểm thử | 27–28 | Đạt ngân sách hiệu năng, cân bằng qua harness |
| **P8** | Beta → Soft Launch → Phát hành | 29–30 | Build store, LiveOps sẵn sàng |
| **P9** | Hậu ra mắt (v1.1+) | 31+ | Arena, server, nội dung mùa |

```
Tuần:  1───2───3───4───5───6───7───8───9──10──11──12──13──14──15
       │P0     │P1: Combat Prototype    │P2: VERTICAL SLICE ⭐  │P3
Tuần: 16──17──18──19──20──21──22──23──24──25──26──27──28──29──30
       │P4: Meta       │P5: Content     │P6: UI/Polish│P7 │P8  │
```

**3 mốc "cổng chất lượng" (Quality Gate) — không đạt thì không đi tiếp:**
- **QG1 (cuối tuần 6):** trận đấu *vui* khi chơi tay không cần đồ họa đẹp. Nếu không vui → quay lại thiết kế, đừng xây tiếp.
- **QG2 (cuối tuần 11):** vertical slice khiến người ngoài chơi 10 phút không muốn dừng.
- **QG3 (cuối tuần 28):** 60 FPS ổn định trên máy Android tầm trung + 0 crash trong 2 giờ chơi liên tục.

---

## 0.1. TRẠNG THÁI THỰC TẾ (cập nhật 2026-08-18 — nguồn số liệu: [object-map.md §12](object-map.md))

> **Lưu ý cập nhật 2026-08-18:** bảng này (và object-map.md §12) không được cập nhật từ 2026-08-12
> tới nay dù có ≥18 commit thật đã lên `main` trong lúc đó (`task-ui-vfx-polish.md`,
> `task-hero-list.md`, `task-splash-loading.md`, `task-defeat-screen.md`, `task-chapter-arena.md`)
> — phát hiện qua `git log -- roadmap.md object-map.md` chỉ ra đúng 1 commit (initial commit). Đã
> ghi bù ở dòng P6 bên dưới + object-map.md §12.1. **Không có việc mới bị bỏ sót ở P0/P1/P3/P4/P5/P7
> so với lần cập nhật trước** — 5 task file kể trên đều thuộc phạm vi P6 (màn hình/UI).

> **Đọc trước khi dùng checklist tuần bên dưới.** Dự án KHÔNG đi theo đúng thứ tự phase gốc (art
> P2 trước → content P5 → polish P6) — thứ tự thật là **combat core → vòng lặp kinh tế/meta
> (gacha/ascend/quest/loot/equipment) → edge case** trước, gần như chưa đụng art/UI/content quy mô
> lớn. Vì vậy **KHÔNG tick lùi các ô `[ ]` theo tuần bên dưới** (rủi ro tick sai vì thứ tự khác hẳn)
> — dùng bảng phase này + [object-map.md §12](object-map.md) làm nguồn sự thật, checklist tuần chỉ
> còn giá trị tham khảo nội dung từng hạng mục.

| Phase | Trạng thái | Bằng chứng / còn thiếu gì |
|---|---|---|
| **P0** Nền móng | 🟡 Phần lớn xong | 11/11 assembly definition, `IPlayerRepository`+`LocalPlayerRepository`, Boot→Meta→Battle chạy được. **Addressables PILOT đã xây thật** (task-addressables-pilot.md, follow-up "tiếp tục cho tôi" — 1 trong 3 hạng mục lớn người dùng chọn làm, thực hiện lần lượt không gộp) — cài package `com.unity.addressables` 4.0.1, đánh dấu 24 asset `HeroDefinitionSO` Addressable (address = path `Resources` cũ, không đổi key lookup), đổi 3/23 file thật dùng `Resources.Load` cho loại dữ liệu này (`TeamSelectScreen`/`HeroDetailScreen`/`BattleSceneInstaller` — chỗ nóng nhất, spawn hero mọi trận) sang `Addressables.LoadAssetAsync(...).WaitForCompletion()` (đồng bộ, giữ nguyên hành vi caller — codebase chưa dùng async/await ở tầng Meta, refactor bất đồng bộ thật là việc lớn hơn hẳn, để ngoài phạm vi pilot). **Còn 21 file/~25 chỗ gọi khác CHƯA migrate** (Enemy/Skill/Equipment/LootTable SO, mọi prefab UI, sprite, AudioClip) — cố ý, pilot chỉ để verify luồng migrate an toàn trước khi mở rộng. Verify Play-mode thật: HP 4 hero sau khi đổi khớp CHÍNH XÁC với số liệu đã ghi trước đó (task-damage-meter.md, cùng phiên) — xác nhận không lệch dữ liệu. 416/416 test xanh (415 + 1 test mẫu tự kéo theo từ chính package Addressables, vô hại). **Localization PILOT đã xây thật** (task-localization-pilot.md, follow-up "tiếp tục cho tôi" — hạng mục lớn thứ 2/3) — plan.md có spec (`ILocalizationService`, CSV key→value VI/EN) nhưng chưa ai xây, dù `SettingsDto.Language="vi"` đã tồn tại sẵn không ai đọc (cùng mẫu "hạ tầng có sẵn, chưa dùng"). Xây `Game.Services.Localization` (parser CSV tự viết — KHÔNG dùng lại `Game.Tools.CsvReader` vì asmdef đó chỉ chạy Editor) đọc `Resources/Localization/strings.csv` (10 key), đăng ký qua `ServiceInstaller` + `WireSettingsToLocalization` (đúng mẫu Audio). Pilot 2 màn trọn vẹn: Title screen (task-title-screen.md) + `SettingsScreen` (thêm mới nút Language cycle VI↔EN). Verify Play-mode thật: Title hiện đúng tiếng Việt mặc định (kể cả placeholder động "24 Tướng · 935390 Vàng"), bấm nút Language trong Settings → TOÀN BỘ label đổi ngay sang tiếng Anh, xác nhận đúng cả chuỗi wiring (Settings→OnChanged→Localization→OnLanguageChanged→RefreshLabels) lẫn trạng thái service dùng chung toàn app. 423/423 test xanh. **Localization mở rộng — PHẦN LÕI NAY ĐÃ XÂY** (2026-08-12, task-phase-5-gaps.md Phần D) — từ 10 key
pilot lên 165 key: `LocalizationKeyGenerator.cs` (`Tools/Localization/Generate Name Keys`) quét
24 hero/66 enemy/65 skill — phát hiện `NameKey` đã gán sẵn 100% đúng pattern
`{kind}.{id}.name` từ trước (khác giả định "phải tự gán key" ban đầu, việc thật chỉ là điền
`strings.csv` cho key đã có sẵn) — chạy thật thêm đúng 155 dòng (VI=EN=tên title-case, không bịa
dịch danh từ riêng, đúng kiểu "AETHER LEGION" đã làm), idempotent (chạy lại không trùng).
`ILocalizationService.GetName(id, kind)` mới + fallback title-case khi thiếu key — **kiến trúc lặp
lại đúng bài học Phần E**: `Game.Services` không được tham chiếu `Game.Meta`
(`HeroDisplayUtil` sống ở đó) nên viết lại logic title-case nhỏ trùng lặp có chủ đích thay vì
import chéo. Migrate **5/5 file thật sự dùng `HeroDisplayUtil`** (9 call site: `SummonScreen`,
`MetaSceneInstaller`, `TeamSelectScreen`, `HeroDetailScreen`, `CodexScreen`) sang
`LocalizationService.GetName`, giữ `HeroDisplayUtil` làm fallback cuối khi service chưa đăng ký.
**Phát hiện + tự sửa 1 nhận định sai giữa chừng**: lúc đầu tưởng `BattleHudScreen` không hiện tên
hero/enemy nào nên bỏ qua — grep lại thấy NÓ CÓ hiện (`actor.DefId.ToUpperInvariant()`, không qua
`HeroDisplayUtil`, một quy ước khác hẳn chưa từng title-case) — sửa lại tài liệu, xếp vào phạm vi
"nhãn khác" để dành lượt sau thay vì tuyên bố sai "đã ổn". 7 test mới `LocalizationServiceTests`
(đọc thẳng `strings.csv` thật). Verify thật qua `execute_code`: gọi `CodexScreen.Open()` PUBLIC
(không mock) với profile sở hữu hero/enemy thật, đọc `_nameLabels[i].text` — hiện đúng "Beast
Tamer"/"Boss Alpha Wolf" thay vì "???"/key thô, đổi ngôn ngữ + mở lại vẫn đúng. **571/571 xanh**
(564 cũ + 7 mới). **Còn để dành lượt sau (item 4, phạm vi quá lớn cho 1 lượt)**: nhãn nút/tiêu đề
hard-code trong `ShopScreen`/`QuestScreen`/`MailScreen`/`DungeonScreen`/`TowerScreen`/
`TrialBossScreen`/`NodeChoiceScreen`/`InventoryScreen`, và tên hero/enemy TRONG TRẬN
(`BattleHudScreen` vẫn hiện `DefId` thô); không có `LocalizationScanner` CI như plan.md §7 mô tả.
**CẬP NHẬT 2026-08-18 — nội dung hardcode còn lại NAY ĐÃ DỊCH** (task-content-i18n.md, người dùng
chọn qua AskUserQuestion cùng đợt Arena/AI diversity/Accessibility): `NodeChoiceOption`/
`NodeChoiceResult` thêm `LabelKey`/`FlavorKey`/`ResultKey`+`Args` (kết quả có SỐ ĐỘNG như gold/tên
hero nên phải mang `(key, args[])` thay vì 1 string tĩnh); `ShopScreen.CatalogItem` thêm `LabelKey`
cho 10 dòng CATALOG; `MailDto` thêm `TitleKey` (rỗng = fallback English, JsonUtility tự điền cho
save cũ, không cần migration) cho thư "welcome" ở `LocalPlayerRepository.CreateNew()` — đọc key thay
vì bake string đã dịch vào save (tránh đóng băng ngôn ngữ tại lúc tạo profile). 19 key mới vào
`strings.csv`. Verify thật: dựng `new LocalizationService()` thật, tra toàn bộ key mới cả VI/EN
(kể cả `Get(key,args)` format số động, và 1 dòng có dấu phẩy trong giá trị `"Chào mừng, Chỉ huy!"`
— xác nhận CSV parser tự viết xử lý field-trong-ngoặc-kép đúng, không bị `Split(',')` ngây thơ cắt
nhầm). 647/647 xanh (không đổi hành vi combat lõi). Còn ngoài phạm vi: giá tiền Shop (tên enum
`CurrencyType`, không phải "tên vật phẩm"), `MailDto.Body` (trường chết theo UI hiện tại).
**`Tools/Validate Object Map` NAY ĐÃ XÂY** (task-phase-5-gaps.md Phần A) — `Assets/Tools/ObjectMap/ObjectMapValidator.cs`, 2 `[MenuItem]`: `Tools/Validate Object Map` (chỉ log Console) + `Tools/Object Map/Generate Report` (ghi thêm `object-map-validation.md`). Đọc thẳng `object-map.md` làm nguồn sự thật (parser markdown tự viết theo cột "Script"/"Prefab", không hardcode danh sách) — quét 3 scene + 14 prefab bằng cách đọc text YAML thô (regex `m_Script` GUID → `AssetDatabase.GUIDToAssetPath`) thay vì mở scene trong Editor, để an toàn với phiên Editor sống của người dùng (không đổi scene đang mở, không gặp lại MCP frame-stall). Đối chiếu 3 chiều: (a) 88 script trong docs không tồn tại file .cs nào (đúng dự đoán §12.1 — object-map.md §3–§9 là kiến trúc ĐÍCH, phần lớn chưa xây), (b) 0 script thật (gắn tĩnh trong scene/prefab) chưa đăng ký docs sau khi lọc built-in Unity/package (Button/Image/Volume...), (c) 34 tên prefab trong docs không có asset khớp. **Giới hạn đã biết ghi rõ trong code**: script gắn qua `AddComponent<T>()` lúc runtime (đa số màn Meta code-dựng — `TeamSelectScreen`, `ShopScreen`...) không serialize vào scene/prefab nên KHÔNG bị quét thấy — tool chỉ bắt bề mặt tĩnh. `validate_script` 0 lỗi, force recompile 0 lỗi console, chạy cả 2 menu thật (qua `execute_menu_item` + `execute_code` gọi trực tiếp `Validate()`) không exception, report file sinh đúng. **524/524 test xanh** (không đổi code gameplay, không thêm test — đúng phạm vi Editor tool thuần). **Font tiếng Việt (rủi ro R6) NAY ĐÃ KIỂM + SỬA THẬT** (2026-08-12, người dùng chọn từ 3 gợi ý sau khi task-phase-5-gaps.md xong cả 5 phần) — kiểm tra thật (không đoán) phát hiện lỗi thật: TMP font mặc định `LiberationSans SDF` (dùng ở `Game.UI`/`Game.CombatView` — `BattleHudScreen`/`ActionCommandUI`/`TutorialOverlay` vừa xây ở Phần B) atlas STATIC gốc THIẾU 54/73 ký tự dấu tiếng Việt test qua (`ố`, `ệ`, `ữ`, `đ`... — mọi dấu sắc/huyền/hỏi/ngã/nặng trên nguyên âm đôi đều thiếu). **Legacy `UnityEngine.UI.Text`/`LegacyRuntime.ttf`** (dùng ở TOÀN BỘ màn `Game.Meta` — nơi chuỗi tiếng Việt thật từ `strings.csv` hiển thị, kể cả sau Phần D vừa migrate — SettingsScreen/TitleCanvas/CodexScreen...) **ĐỦ 73/73**, không có vấn đề gì (font hệ điều hành render động, không phải atlas nung sẵn). Sửa TMP font: gán `sourceFontFile` (trỏ đúng `LiberationSans.ttf` nguồn — bản thân file .ttf ĐÃ CÓ đủ glyph, chỉ atlas bake sẵn lúc tạo thiếu) + chuyển `atlasPopulationMode` từ `Static` sang `Dynamic` (tự bake glyph theo nhu cầu runtime, không giới hạn bộ ký tự đoán trước lúc tạo) qua `SerializedObject` (3 field `m_SourceFontFile`/`m_SourceFontFileGUID`/`m_SourceFontFilePath` phải set cùng lúc, thiếu 1 field bị reset lại null). **Phát hiện kỹ thuật quan trọng lúc verify**: `TMP_FontAsset.HasCharacter(c, tryAddCharacter:true)` gọi ngoài 1 lượt render TMP_Text thật KHÔNG đáng tin (trả `false` dù font thật sự render đúng) — cách kiểm ĐÚNG duy nhất là dựng `TMP_Text` thật, gọi `ForceMeshUpdate()`, đọc `characterInfo[i].textElement` (null = tofu); test viết theo cách sai ban đầu (`HasCharacter` tĩnh) bị flaky phụ thuộc thứ tự chạy — phát hiện + sửa lại đúng trước khi coi là xong. `VietnameseFontTests.cs` mới (49 test: atlas mode/sourceFontFile + 44 ký tự riêng lẻ tự render+kiểm, không phụ thuộc test khác) — cần thêm `Unity.TextMeshPro` vào `Game.Tests.EditMode.asmdef`. **620/620 xanh** (571 cũ + 49 mới), xác nhận chạy độc lập lẫn sau domain reload fresh. **Còn thiếu khác:** Git LFS, CSV importer chỉ có 4 file DataImport (chưa rõ đã dùng thật cho hero/skill/enemy hay data đang author tay qua Editor MCP), CI (GitHub Actions) chưa xác nhận. |
| **P1** Combat Prototype | 🟢 Xong, vượt cả P3 | Simulation C# thuần deterministic, `IRandomSource`, Action Command 4 loại, AI Utility, Poise/Break — tất cả đã có + test. QG1 (trận vui) chưa có playtest người thật ghi nhận chính thức, nhưng cơ chế đã hoàn chỉnh về mặt kỹ thuật. |
| **P2** Vertical Slice | 🟡 Tutorial xong, còn thiếu art/build | Không có art thật đợt 1 (chỉ 6 hero — số cũ, đã lên 24 ở P5 nhưng chưa audit riêng ở P2 gate), HUD Battle mới ở mức chạy được (`BattleHudScreen`, chưa đối chiếu pixel-art `image_UI.jpg`), node map sinh được nhưng chỉ 1 chương thật (số cũ, thực ra cả 5 đã chơi được — xem P5), chưa build Android/Windows xác nhận. **Tutorial 5 bước NAY ĐÃ XÂY** (task-phase-5-gaps.md Phần B) — dạy đúng thứ tự ① chọn skill ② Action Command ③ hệ khắc chế (đọc thẳng `DamageDealt.FloatValue`=`ElementMultiplier` có sẵn) ④ Break (`PoiseBroken` có sẵn) ⑤ Ultimate (cạnh xuống `BattleState.UltimateGauge` đầy→0) — `TutorialController` (state machine thuần, `Game.CombatView.Tutorial`) + `TutorialOverlay` (banner code-dựng giữa màn, không chặn raycast, nút Skip) + `BattleSceneInstaller.WireTutorial`/`OnCommandResolved` (event mới) + `RunContext.PendingBattle.IsTutorial` + `MetaSceneInstaller.LaunchBattle` set cờ theo `!ProgressDto.TutorialCompleted`. Hoàn tất/Skip lưu `TutorialCompleted=true` ngay trong Battle scene (cùng kỹ thuật `SaveSnapshot` — `ProfileContext.Current`+`IPlayerRepository.SaveAsync`). Verify thật qua `execute_code` dựng `BattleSceneInstaller` thật (không mock), gọi `WireTutorial()` private, đi hết `ChooseSkill→ActionCommand→Skip→Done`, xác nhận `save.json` ghi thật vào thư mục scratch (không đụng save thật) + `TutorialCompleted` lật đúng. 17 test mới `TutorialControllerTests` (cần thêm `Game.CombatView` vào `Game.Tests.EditMode.asmdef` — test đầu tiên chạm assembly này). **564/564 xanh.** QG2 (vertical slice 15 phút) vẫn cần art thật + build xác nhận trước khi coi P2 xong. |
| **P3** Combat đầy đủ | 🟡 ~99% | **24/24 edge case (plan §4.14) có test thật** — toàn bộ danh sách hoàn tất (E12 AutoRevive, E15 Ultimate execution, E17 BattleSnapshot resume đều đã xây thật, không phải giả lập). **Damage Meter UI NAY ĐÃ XÂY** (task-damage-meter.md, follow-up "tiếp tục cho tôi", hạng mục đầu tiên session này đụng `Game.CombatView`/HUD trận đấu) — phát hiện quan trọng: `BattleState.DamageByUnit` (`Dictionary<int,long>`) đã tồn tại sẵn với chính doc-comment "Thống kê để tính thưởng và Damage Meter", `RecordDamage` đã được gọi đúng ở mọi nguồn sát thương thật (đòn trực tiếp, DoT tick, Counter, Reflect) — chỉ thiếu UI tiêu thụ. Thêm panel góc trái-dưới HUD (trống sẵn) vào `BattleHudScreen.cs` (đúng style code-dựng+TextMeshPro của HUD, KHÔNG dùng style prefab+Text của các màn Meta), đọc thẳng `DamageByUnit` mỗi frame — top 5 unit giảm dần, màu theo phe. Cố ý KHÔNG dùng `CombatEventQueue` (tránh tranh event với `CombatPresenter` đang tiêu thụ hàng đợi đó). Phát hiện `DamageByUnit`/`RecordDamage` chưa từng có test trực tiếp — thêm `BattleStateTests.cs` mới. Verify Play-mode THẬT đầy đủ nhất session này: mô phỏng 1 trận thật qua reflection (`LaunchBattle`→`SubmitIntent`/`Advance` nhiều lượt cả 2 phe), panel hiện đúng thứ hạng/tên/màu/số liệu thật khớp `DamageByUnit`. 415/415 test xanh. **Formation Preset + Team Synergy NAY ĐÃ XÂY** (task-formation-synergy.md) — `FormationSystem` 8 preset (`formation_balanced/phalanx/arrowhead/vanguard_line/siege/flanking/turtle/blitz`), `SynergySystem` 5 điều kiện class/element; `BattleSceneInstaller` override `CombatUnit.Row` theo preset thay vì hard-code `index<2`, inject modifier trước trận; `TeamSelectScreen` cycle button + `SelectedFormation` property; `RunContext.PendingBattle.Formation` bridge Meta→Battle; `LocalPlayerRepository` unlock cả 8 sẵn. 37 test mới (FormationSystemTests + SynergySystemTests), 460/460 xanh. **Hàng Tactic (plan.md §5.6) NAY ĐÃ XÂY** (task-tactic-row.md) — `StatusId.Focus=38` + `StatusTable` entry; `ActionIntent.IsSwapRow`/`IsFocus`; `CombatUnit.HasSwappedRowThisTurn`; `CombatSimulation.ExecuteIntent` xử lý SwapRow (hoán hàng, KHÔNG kết thúc lượt) + Focus (duration=2 → active lượt kế, kết thúc lượt); `DamageCalculator` force crit khi `HasStatus(Focus)`; `BattleHudScreen` GRID_ROWS 2→3 (hàng Guard/ESC/SWAP/FOCUS) + 4 sự kiện mới; `BattleSceneInstaller` wire 4 handler. Scope out: Analyze (cần UI reveal riêng). 13 test mới (TacticSystemTests), 473/473 xanh. **Auto-battle policy 7 ưu tiên NAY ĐÃ XÂY** (task-auto-battle.md) — `DefaultAutoIntent()` trước đây uỷ thác cho enemy AI (`_ai.Choose`), nay thay bằng 7 priority thật của player: (1) heal đồng minh HP<35%, (2) revive đồng minh gục, (3) damage cao nhất vào địch Break, (4) Ultimate khi đầy+≥2 địch, (5) skill khắc chế element, (6) damage cao nhất còn dùng, (7) basic attack. `BattleSceneInstaller` nay khôi phục trạng thái Auto từ `SettingsDto.AutoBattle` và persist khi toggle. 13 test mới (AutoBattlePolicyTests), 486/486 xanh. **AssemblyRuleTests NAY ĐÃ XÂY** (task-assembly-rules.md) — 9 test kiểm tra đồ thị assembly plan.md §11.2 không bị vi phạm: `Game.Combat` không ref CombatView/UI/Meta/Services, không có field/property/method nào dùng `UnityEngine.Random`/`Time`, `Game.Core` không ref bất kỳ `Game.*` nào, `Game.Data`/`Game.Services` không tham chiếu lớp cao hơn. Tất cả 9 test xanh (regression safety net cho rủi ro R8). 9 test mới, **494/494 xanh**. **GoldenScenarioTests NAY ĐÃ XÂY** (task-golden-scenario.md) — 20 kịch bản cố định (10 duel: neutral/element/multi-hit/power/stat variant, 5 TeamBattle, 5 edge-case kịch bản khác), mỗi kịch bản hardcode expected `CombatEventQueue.ToSignature()` string (hoặc result+turn count với TeamBattle). Tất cả constants tính bằng `execute_code` thật, verify determinism bằng cách chạy lại 2 lần cho S01 (mirror = TestFactory). Compile 0 error (validate_script). **20 test mới, 514/514 xanh** (xác nhận chạy thật — stale DLL sau session trước cần recompile; sau recompile: 20/20 GoldenScenarioTests xanh, toàn bộ 514/514 xanh). **Analyze tactic (plan.md §5.6) NAY ĐÃ XÂY** (task-analyze-tactic.md) — nốt cuối của hàng TACTIC: `BattleState.AnalyzedEnemyIds` (`HashSet<int>`, cùng pattern `DamageByUnit`); `ActionIntent.IsAnalyze`; `CombatSimulation.ExecuteIntent` trừ 5 SP + `_targeting.AutoSuggest` chọn địch tự động (không có manual target UI, khớp pattern toàn game) + ghi vào set (vĩnh viễn trong trận, không cần `CombatEventType` mới vì HUD đọc thẳng State); `BattleHudScreen` thêm nút ANALYZE (cột 5 hàng TACTIC, màu cam) + panel "ANALYZED" góc phải-dưới (tên/element/HP/SP/ATK/DEF/SPD/hệ số 4 nguyên tố Fire-Water-Earth-Wind qua `ElementTable.Multiplier`), ẩn khi chưa analyze địch nào; `BattleSceneInstaller.HandleAnalyze` wire `OnAnalyzePressed`. Scope out (v1): AI intent reveal (có side-effect RNG). 3 test mới (AnalyzeTacticTests). **518/518 xanh.** **BUG "không chọn Auto mà vẫn chơi Auto" — ĐÃ SỬA** (task-autobattle-hud-sync-fix.md, người dùng báo trực tiếp) — root cause: `BattleSceneInstaller.BuildBattle()` khôi phục `_autoPlay` (field điều khiển HÀNH VI thật) từ `SettingsDto.AutoBattle` đã lưu từ trận trước (persist có chủ đích, task-auto-battle.md), nhưng `BattleHudScreen._auto` (field điều khiển HIỂN THỊ nhãn AUTO ON/OFF) luôn khởi tạo `false` mặc định, không đọc lại `_autoPlay` khi `Bind()` — 2 trạng thái lệch nhau: trận chạy auto ngầm (đúng thiết kế persist) nhưng nút luôn hiện "AUTO OFF" (sai). Fix: `BattleHudScreen.SetAutoState(bool)` mới (set `_auto`+label+màu, KHÔNG phát `OnAutoToggled` — chỉ đồng bộ hiển thị, không phải hành động người chơi), gọi ngay sau `_hud.Bind(Simulation)` trong `BattleSceneInstaller.WireHud()`. Verify qua `execute_code`: dựng `BattleHudScreen` thật, `SetAutoState(true)` đổi đúng label "AUTO OFF"→"AUTO ON". Không có test EditMode phủ khu vực UI này — **524/524 xanh** (không đổi khỏi baseline). **Thiếu thật sự còn lại:** `ReplayVerifier` chống gian lận (server-ready, v1.2+) chưa xây. **AI địch
đa dạng theo archetype NAY ĐÃ XÂY** (task-ai-diversity.md, 2026-08-18, người dùng chọn qua
AskUserQuestion) — trước đây ~85% enemy dùng chung 1 `AIProfile` "ai_special" bất kể archetype.
`BattleSceneInstaller.BuildAi` nay dispatch qua `BuildArchetypeAi(archetype)` mới, mỗi archetype
(Healer/Debuffer/Caster+Bomber/Tank/Brute/Archer+Skirmisher/default) có bộ `AIRule` riêng ghép từ
`AIConditionType` có sẵn (không cần enum/field mới). Verify hành vi thật qua `AIController.Choose()`
trực tiếp trên `BattleState` tối giản: Healer chọn đúng skill hồi máu nhắm đồng minh HP thấp, tự
fallback đánh thường khi không ai cần hồi. Xem object-map.md dòng CẬP NHẬT tương ứng. **Boss Phase/
Enrage/Signature Move (plan.md §4.13.3) NAY ĐÃ XÂY** (task-boss-phase-enrage.md, 2026-08-18) — trước
đây `EnrageRound`/`PhaseChanged`/`PhaseIs` toàn là khung sườn chết, boss đánh y hệt suốt trận bất kể
HP. `CombatSimulation.RefreshBossPhases` đổi phase thật khi HP<60%/30% (chỉ tăng, reset Poise qua
`PoiseSystem.ResetOnPhaseChange` có sẵn từ trước chưa ai gọi); `RefreshBossEnrage` +50%ATK/+30%SPD
mỗi 3 lượt sau round 12, cộng dồn qua `PassiveModifiers`. `AIController.Choose` thêm SignatureMove:
rule `IsSignatureMove` thắng điểm → đếm ngược 3 lượt công khai (không đánh ngay), Break huỷ (counter-
play bắt buộc) — wiring vào `ai_boss` profile có sẵn (rule "chiêu đặc trưng"), mọi boss đơn tự động
có ngay không cần sửa 66 enemy data. 11 test mới, xác nhận 0 test cũ nào set `IsBoss=true` nên không
đụng GoldenScenarioTests. **668/668 xanh.** |
| **P4** Meta & Progression | 🟡 ~75% | **Reforge sub-stat (plan.md §7 — nốt cuối cùng của Equipment) NAY ĐÃ XÂY** (task-phase-5-gaps.md Phần C) — enum `MetaEnums.Reforge=14` tồn tại từ trước nhưng chưa có logic/UI, nay có đủ: `EquipmentService.TryReforge` reroll TOÀN BỘ sub-stat bằng `EquipmentGenerator.RollSubStats` (giữ nguyên SỐ LƯỢNG theo rarity, không có tỉ lệ thất bại khác Enhance, không đổi Level/main stat), `ReforgeCost(level,rarity) = 80*(level+1)*(2+rarity)` (số tự thiết kế, plan.md không cho bảng), `CanReforge` yêu cầu ≥1 sub-stat sẵn có. UI: `ReforgeButton` mới trong `UI_GearSlotRow.prefab`, đặt Ở CỘT PHẢI ngay dưới Equip/Enhance (không tăng chiều cao row — đo thật bằng `execute_code`+`TextGenerator` trước khi chọn kích thước, xác nhận "REFORGE 7200g" worst-case vẫn vừa 120px). **Phát hiện phụ quan trọng lúc verify hình học**: dòng CUỐI (slot 5) của gear panel đã chồng lên `FormationRow` (task-formation-synergy.md) từ TRƯỚC — cùng loại lỗi vừa sửa ở task-teamselect-start-button-fix.md (đè + chặn raycast), chỉ chưa ai bấm trúng để báo; tiện sửa luôn bằng cách giảm `rowH` 66→60 trong `RefreshGearPanel` (đẩy đáy dòng cuối tới content-bottom=92, cách FormationRow 10px, xác nhận qua `execute_code`). 6 test mới (`EquipmentServiceReforgeTests`), **524/524 xanh**. Equipment/Set Bonus/Enhance +0→+15 nay đã ĐỦ 100% theo plan.md §7. Hero level/Ascend/SkillUpgrade/Awakening/InnatePassive: xong + test. Gacha pity: xong + test (±0.05%/5M roll). Equipment: sinh ngẫu nhiên + sub-stat + wiring combat xong (task-equipment.md). **Set Bonus (8/8 bộ, plan.md §7.4) đã xây thật đủ cả 2-món lẫn 4-món** (task-setbonus.md — `SetBonusCatalog`/`SetBonusResolver`, `SetId` roll ngẫu nhiên ở tầng instance khi sinh trang bị; Tempest 4-món ban đầu hoãn, đã làm xong đợt sau — phát hiện + sửa luôn 1 bug lõi combat: `SimPhase.RoundEnd` chưa từng được gán, `BattleState.RoundNumber` không bao giờ tăng quá 1 trong suốt trận, nay round advance đúng; 27 test mới tổng 2 đợt). **Enhance giờ đã full +0→+15 đúng plan.md §7.3** (task-enhance-plus15.md, tiếp nối task-enhance-substat.md) — trần nâng từ +9 lên +15, mốc mở sub-stat mới đủ 4 mốc +3/+6/+9/+12 (không còn nén xuống 3 mốc), Đá cường hoá đủ 8 bracket (1/2/3/5/8/10/14/20 — đúng số plan.md), **tỉ lệ thất bại thật từ +12** (70/55/40/25% cho +12..+15, trượt KHÔNG mất đồ/tụt cấp nhưng VẪN mất Gold+Đá đã trả — `TryEnhance` đổi kiểu trả về từ `bool` sang `enum EnhanceOutcome{Rejected,Failed,Succeeded}` để phân biệt "không đủ tài nguyên" với "đủ nhưng trượt"), **+15 nhân thêm ×1.5 main stat**. Gold KHÔNG đổi công thức cũ (`80*(level+1)`, giữ nguyên xuyên suốt +0..+14) — quyết định có chủ đích: Gold vốn rẻ/dồi dào trong game này, nút thắt thật của dải +12-15 là Đá (giới hạn tuần qua Dungeon) + tỉ lệ thất bại, cả 2 bám đúng số plan.md. UI (`TeamSelectScreen`) hiện tỉ lệ thành công khi <100%, báo "FAILED! (retry)" 1 lần khi trượt (đo thật bằng `TextGenerator` để tránh tràn label 84px, không đoán). 408/408 test xanh. Quest/Achievement: xong. **Mail đã xây** (task-mail.md — plan.md chỉ ghi 1 dòng "Mail = Đền bù LiveOps" không có spec, tự thiết kế tối thiểu: `MailSystem`/`MailScreen` claim danh sách mail, 1 trigger thật duy nhất là quà chào mừng cấp lúc `LocalPlayerRepository.CreateNew()` — không bịa thêm "sự kiện LiveOps" giả nào khác; **NAY ĐÃ CÓ ĐỦ CẢ 3 PHẦN từng cố ý cắt** (task-mail-extras.md) — badge số đỏ trên `MailButton` (đọc `MailSystem.UnclaimedCount` có sẵn từ trước, chưa ai dùng tới), `MailDto.ExpiresAtUtc` + `MailSystem.PurgeExpired` (dọn mail hết hạn, gọi mỗi lần mở Mail — mail Welcome cố ý KHÔNG có hạn, chỉ xây hạ tầng cho trigger tương lai), `MailSystem.ClaimAll` + nút CLAIM ALL mới trong `UI_Mail.prefab`. **Phát hiện phụ lúc làm**: dòng hiển thị reward gốc ("+2000 Gold · +100 Gem") đã tràn khỏi `ProgressLabel` 90px từ trước (đo thật bằng `TextGenerator` mới phát hiện, task-mail.md chưa từng đo) — vá luôn cùng lúc (bỏ dấu "+", fontSize 12→10, nới label 90→128px). 413/413 test xanh. Save migration: chưa test qua version thật (field `Mail` mới thêm không cần bump version — JsonUtility tự null cho save cũ, `SaveMigrationRunner.EnsureNotNull` đã vá). **Vật phẩm tiêu hao (plan.md §7.5) đã xây thật đủ 6/6 loại** (task-consumable-items.md — hạng mục DUY NHẤT trong session này chạm `Game.Combat`): `ItemResolver` mới xử lý Potion/Ether/Antidote/Smoke Bomb/Revive Feather/Elemental Bomb, `ActionIntent.IsUseItem` + `BattleState.ItemLoadout` nối vào lõi `CombatSimulation`, Shop bán bằng Vàng (mở rộng `ShopScreen`/`UI_Shop.prefab` từ 4→10 dòng), Battle HUD có hàng item riêng (`GRID_ROWS` 1→2, `ItemSlotView` mới). Tự động mang tối đa 5 loại×3/trận từ `profile.Inventory.Items` (KHÔNG có màn chọn loadout thủ công — chỉ 6 loại tồn tại, không đáng 1 màn riêng) và tất cả item auto-target (KHÔNG có UI chọn mục tiêu thủ công ở bất kỳ đâu trong game, nhất quán với cách skill đã hoạt động từ trước). Số item dùng thật (không phải đã mang) được trừ vĩnh viễn khỏi Inventory sau trận — verify bằng Play-mode thật, không chỉ EditMode test. **Gacha rate disclosure + pull history NAY ĐÃ XÂY** (task-gacha-disclosure.md, 2026-08-18, plan.md §9.3 "Bắt buộc: hiển thị tỉ lệ trong game, lưu lịch sử 100 lần gần nhất") — lưu trữ lịch sử ĐÃ CÓ SẴN (`GachaStateDto.History`, cap 100, chưa ai hiển thị); `GachaInfoScreen` mới (2 tab RATES/HISTORY, dựng từ `UI_GachaInfo.prefab` clone `UI_Codex.prefab` — tái dùng đúng khuôn 2-tab+pagination có sẵn), mở từ nút `InfoButton` mới trên `UI_Summon.prefab`. Tách hằng số tỉ lệ ra `public const` trong `GachaSystem` (trước là số ma thuật nội bộ `RollRarity`) — UI và logic roll dùng CHUNG 1 nguồn, không thể lệch nhau; verify an toàn qua `GachaPityTests` sẵn có (±0.05%/1M roll không đổi). 668/668 xanh. |
| **P5** Nội dung & Node Map | 🟢 ~75% | **24/24 hero có data thật + Awakening/Innate Passive + art sprite thật** (task-hero-roster.md). **Cả 5/5 chương đã chơi được thật** (task-chapters.md — sửa lại nhận định SAI trước đó "chỉ 1 chương": 65 enemy phân bố đủ 5 chương 11/13/13/14/14, cả 5 boss tồn tại + scale khó dần, `NodeMapGenerator`/`PickBoss`/`PickEnemies` chapter-aware đầy đủ, bằng chứng thật — save hiện tại đã qua tới `ChapterUnlocked=6`; vá thêm node Mystery trước đó rơi vào "not available"). **Toàn bộ Endgame plan.md §8.3 đã xây thật** — Dungeon hằng ngày (4 loại Gold/Exp/Material/Stone) + Trial Boss hằng tuần + **Tháp Vô Tận 100 tầng** (task-endgame.md — `DungeonSystem`/`TrialBossSystem`/`TowerSystem` pure logic + 40 test, `DungeonScreen`/`TrialBossScreen`/`TowerScreen` UI, launch/return-trip qua `RunContext.QueueSpecialBattle`, TopBar 3 nút mới; Tháp dùng cơ chế mới `CombatSimulation.OnEnemyWaveCleared` — nhiều đợt địch trong 1 trận liên tục, HP phe Player không hồi giữa tầng). **Event/Rest node redesign đã xây thật** (task-eventrest.md): trước đây cả 2 tự resolve ngay khi bấm node, không có lựa chọn nào (Rest = auto-heal + gold cố định; Event = 1 roll ẩn bằng `UnityEngine.Random`). Nay mở modal `NodeChoiceScreen` (`UI_NodeChoice.prefab`, clone `UI_Shop.prefab`) — Event có đúng 3 lựa chọn rủi ro thật theo plan.md §8.1 ("Play it safe" cố định · "Take a chance" 50/50 được/mất · "All in" 25% ra trang bị Rare/75% mất gold), Rest có 2 lựa chọn ("Recover" +gold / "Train" +1 skill level miễn phí ngẫu nhiên cho 1 hero đủ điều kiện). Logic tách hẳn ra `NodeChoiceSystem` (pure static, test được qua `IRandomSource` seed cố định) — **phát hiện quan trọng khi làm**: game không có HP dai dẳng giữa các trận node map (`HeroInstanceDto` không có field HP nào, mọi trận luôn full HP), nên "hồi 30% HP" của Rest trong plan.md không có ý nghĩa thật trong kiến trúc hiện tại — thay bằng phần thưởng Gold, giữ đúng tinh thần lựa chọn (an toàn/ít giá trị) mà không giả vờ có cơ chế hồi máu không tồn tại; xây persistent-HP-giữa-trận là việc lớn hơn hẳn, cố ý để ngoài phạm vi. **Loot table nay đã tune theo chương** (task-loottable-chapters.md) — trước đây chỉ 2 asset wildcard (Treasure/Boss) dùng chung mọi chương, nay có đủ 10 asset riêng (5 chương × 2 loại), Gold/material/equipment-rarity tăng dần theo chương, material Boss bám mốc `AscendSystem.COSTS` (chương sớm cấp EssenceI, chương giữa mở EssenceII/Core, chương cuối mở EssenceIII) — số liệu tự thiết kế (plan.md không cho bảng cụ thể). **NAY ĐÃ QUA BALANCE HARNESS THẬT** (task-balance-loottable.md) — sửa 2 bug harness cũ (hardcode chương 1, đếm sai mảnh hero Boss), chạy lại đối chiếu 10 asset với `AscendSystem.COSTS`: đường cong mở khoá vật liệu khớp đúng ý đồ (mỗi loại mở sớm hơn 1 chương so với mốc ★ cần), **KHÔNG sửa số liệu asset nào** — story-only chỉ đủ 23%/0% chi phí từng bậc ★ là DỰ KIẾN (Gacha dupe/Shop-Gem/Material Dungeon mới là 3 nguồn chính bù phần còn lại, đã xây thật ở các session trước). **Animation PILOT đã xây thật** (task-animation-pilot.md, follow-up "tiếp tục cho tôi" — hạng mục lớn thứ 3/3 người dùng chọn làm, sau Addressables/Localization) — `UnitView.cs` trước đó KHÔNG có animation frame-based nào (chỉ juice code thuần: lerp/màu/fade trên 1 `SpriteRenderer` tĩnh). Sau khi báo rủi ro thật (ComfyUI không đảm bảo nhất quán nhiều frame cùng nhân vật), người dùng chọn hướng riêng: **Pillow (code) vẽ nhân vật thủ tục, ComfyUI chỉ vẽ background** — script mới `Tools/pixel-art-pipeline/scripts/character_draw.py` (không sửa `compose.py` gốc) vẽ chibi 32×32 bằng hình khối phẳng (6 màu con từ `palette.json` + viền đen `#0D080E` quanh silhouette, khớp phong cách tham khảo `Character_01.png`), tham số hoá theo pose (head_bob/arm_angle/lean) → nhất quán tuyệt đối giữa các frame vì cùng 1 hàm vẽ. Sinh cho `hero_ember_knight`: 4 frame idle (bob nhẹ) + 4 frame attack (vươn vũ khí + lao người), gộp qua `compose.py spritesheet` có sẵn. 1 background chiến trường qua ComfyUI (`comfy_gen.py`, checkpoint `PixelartSpritesheet_V.1`) — 2/3 variant đầu bị AI vẽ lặp nhân vật dù có negative prompt (loại bỏ), variant còn lại đạt bố cục lớp rõ ràng (sky/tường đá/lửa trại) đúng ý "box layout"; hậu xử lý `post_process.py` (khoá palette) làm HỎNG ảnh (tạo mảng trắng lỗi keying) — dùng thẳng ảnh gốc ComfyUI thay vì cố vá, đúng nguyên tắc skill "đừng cố cứu bằng hậu xử lý". Import Unity thủ công (không có `unity_import.py` thật trong dự án dù skill nhắc tới) — viết `.meta` tay theo đúng mẫu `hero_ember_knight_v1_00.png.meta` đã có (Point filter, Compression None, PPU 32), verify qua `execute_code` đọc `TextureImporter` thật. Hệ chạy animation MỚI trong `UnitView.cs` (không dùng Mecanim `Animator`) — `LoadFrames(defId, state)` tự dò `Animations/{defId}_{state}_00..` (thử cả Heroes/Enemies), fallback nguyên vẹn về sprite tĩnh nếu không có (23 hero còn lại + mọi enemy không đổi hành vi); Idle 8fps loop, Attack 14fps chạy 1 lần rồi tự về Idle (khớp bảng plan.md §2.2), trigger qua `PlayAttackLunge` có sẵn. Verify: MCP Play-mode frame-stall tái diễn (`Time.frameCount` đứng yên) — dùng kỹ thuật check-before-force nâng cấp: spawn `UnitView` thật, gọi `Bind()`/`PlayAttackLunge()`/`Update()` qua reflection với `Time.deltaTime` thật đọc được (0.02s dù frame đứng), xác nhận đúng toán học frame-cycle (idle loop, attack→auto-revert Idle). 423/423 test xanh (không đụng logic Combat lõi). **Follow-up ngay sau đó cùng phiên:** background sinh ở trên ban đầu là asset MỒ CÔI (chưa gắn scene nào, đúng mẫu lỗi "sinh xong không dùng" lặp lại cả session) — đã wiring thật vào `Battle.unity`: GameObject `Background` tĩnh trên Hierarchy (con `__Stage__`, đúng quy ước tĩnh) với `SpriteRenderer` + `sortingOrder=-100`. Phát hiện đẹp: ảnh sinh 512×288 ở PPU 32 dự án = đúng 16×9 world unit, khớp CHÍNH XÁC vùng nhìn `BattleCamera` (orthoSize 4.5, aspect 16:9) — không cần chỉnh scale. Play-mode verify lần 2 (sau khi wiring xong) **KHÔNG bị frame-stall** — chụp `manage_camera screenshot` thật, thấy trực tiếp `hero_ember_knight` (đúng màu mũ xanh/áo đỏ đã vẽ) đứng trong trận cùng background mới, xác nhận bằng mắt. **Follow-up lớn tiếp theo cùng phiên**: người dùng yêu cầu vẽ lại TOÀN BỘ hero/enemy/boss + đủ
idle/move/action/dam/die + VFX mọi skill (task-animation-pilot.md §4, nhiều giai đoạn, còn tiếp).
Audit CSV thật: 24 hero = ĐÚNG 4/class × 6 class, mỗi class 4 hero khác nhau chủ yếu ở `element` —
đòn bẩy tái dùng mạnh (6 rig thân × recolor, không phải 24 thiết kế riêng). 65 skill chỉ 7 element ×
4 type, 9 VFX asset có sẵn đã phủ gần hết. Đã xây thật: kỹ thuật "skeletal rig" mới
(`Tools/pixel-art-pipeline/scripts/character_rig.py`, bộ phận xoay khớp thật qua PIL `rotate()`,
khác hẳn khối chữ nhật tĩnh của `character_draw.py`), tinh chỉnh tỷ lệ + dựng ĐỦ 5 trạng thái cho
`hero_ember_knight` (idle/attack/move/damage/die — move có xoay chân thật lần đầu tiên), **ĐÃ THAY
HẲN** 8 frame `character_draw.py` cũ bằng 21 frame rig mới trong Unity (`UnitView.cs` mở rộng
`AnimState` đủ 5 state, `PlayHit`/`PlayDeath` trigger Damage/Die thật). Đồng thời dựng thật URP
Bloom Volume (hạ tầng có sẵn từ trước nhưng tắt — `DefaultVolumeProfile.asset` có Bloom
`intensity:0`, `BattleCamera.renderPostProcessing:0` — nay bật cả 2 + shader `[HDR]` mới), verify
bằng pixel thật (RenderTexture + nền đen tuyệt đối) thấy quầng sáng rõ ràng — hạ tầng sẵn sàng,
CHƯA gắn vào skill thật nào. 423/423 test xanh xuyên suốt. **Giai đoạn 2 NAY ĐÃ XONG**: Ultimate `hero_ember_knight` (`skill_inferno_bulwark`, slot 4) nay có
VFX riêng dùng Bloom thật — trước đây VFX chỉ chọn theo Element (`VfxPlayer.KeyForElement`), không
skill nào khác biệt dù `CombatEvent.StringValue` đã có sẵn placeholder chết "skillId, vfxKey..." từ
trước. Nối thật: `ActionResolver` truyền `stringValue: data.Id` ở cả 4 điểm emit `DamageDealt`,
`VfxPlayer` thêm key `inferno_bulwark` (asset mới 4 frame 64×64) + material HDR riêng qua
`MATERIAL_OVERRIDES` (9 VFX cũ không đổi), `CombatPresenter` route theo `StringValue` khi khớp đúng
skill (loại trừ Counter/Reflect qua `Status==None`). Verify bằng `CombatSimulation` THẬT (không
mock) — mọi đòn `skill_inferno_bulwark` đều phát đúng event, resource path (frame+material) load
thật không null. **Giai đoạn 3: ĐỦ 6/6 class kit xong** (cùng phiên, nhiều lượt "tiếp tục cho tôi" liên tiếp):
refactor `character_rig.py` thành `CharacterKit` (đầu/thân/vũ khí/khiên riêng theo class, chân/tay
dùng chung chỉ đổi màu) — verify không đổi hành vi `hero_ember_knight` cũ. Dựng đủ 6 class template
với silhouette + bảng màu RIÊNG biệt: Vanguard(`hero_ember_knight`, có sẵn)/Arcanist(`hero_frost_sage`,
mũ nhọn+robe+gậy)/Trickster(`hero_gale_thief`, khăn che mặt+dao ngắn)/Warden(`hero_dawn_cleric`,
khiên TRÒN+chuỳ)/Slayer(`hero_shadow_fang`, đại đao+mắt tím)/Summoner(`hero_bone_caller`, totem
đầu lâu+dấu xương) — mỗi class đủ 5 trạng thái, import Unity 0 thay đổi code (`LoadFrames` đã tổng
quát từ Giai đoạn 1). Script import tách riêng (`import_hero_frames.py`) sau khi lặp tay 3 lần.
423/423 test xanh xuyên suốt 6 lần import, không regress lần nào. **Recolor 18 hero còn lại —
ĐÃ XONG (Giai đoạn 3 hoàn tất)**: 18 bảng màu element thêm vào `character_rig.py` (dùng
`recolor_kit()` + silhouette hàm của class, không thiết kế lại), sinh batch 18×5 state, import
`import_hero_frames.py` batch, verify `Resources.Load` 90/90 OK, 423/423 xanh. **24/24 hero đủ
5 animation state trong Unity.** **CẬP NHẬT 2026-08-18 — Giai đoạn 4+5 thực ra đã HOÀN TẤT từ
trước** (đã ghi ở object-map.md §12.1 nhưng dòng roadmap này chưa từng cập nhật theo, gây lệch —
tự phát hiện khi định "bù" lại VFX Wind/Light/magic tưởng thiếu, đọc kỹ mới thấy asset thật đã có
sẵn, dừng tay không ghi đè): 66/66 enemy/boss đủ 21 frame animation (`enemy_rig.py`,
`gen_all_enemies.sh`), 3 VFX Wind/Light/magic (`vfx_wind_gust`/`vfx_light_radiant`/`vfx_magic_bolt`)
đã có đủ, `VfxPlayer`/`CombatPresenter` đã wiring xong — xác nhận lại bằng `Resources.Load` thật
qua `VfxPlayer.LoadFrames` (không phải đọc code suông). **Còn thiếu thật:** chỉ 1 background
chiến trường (chưa full parallax 3 lớp). |
| **P6** UI/UX Responsive | 🟡 Phần lớn xong (đã sửa marker 2026-08-18 — dòng cũ ghi 🔴 sai thực tế) | Nay đủ **16/70 prefab**, **danh sách màn hình plan.md §10.1 coi như đủ** (xem đoạn 2026-08-18 cuối dòng này). `LayoutProfileSwitcher`/Landscape đã phủ 11/12 màn Meta dạng modal + TeamSelect + 4 panel Battle HUD, còn thiếu đúng 5 màn mới nhất (Splash/Title/Loading/ChapterProgress/HeroList). **Inventory NAY ĐÃ XÂY** (task-inventory-screen.md, follow-up "tiếp tục cho tôi") — `InventoryScreen` (mirror `CodexScreen`, đọc thuần) 2 tab: ITEMS (6 vật phẩm tiêu hao qua `ItemCatalog`+`economy.GetItemCount`) / MATERIALS (5 vật liệu Ascend: EssenceI/II/III/Core/EnhanceStone qua `economy.Get`) — cả 2 tab ≤ 6 mục nên không cần phân trang kiểu Codex, chỉ tab switch. KHÔNG hiện Gold/Gem (đã có TopBar) hay Energy/Ticket/Honor (xác nhận là 3 currency CHẾT — grep ra 0 consumer/producer ngoài lúc khởi tạo, không hiện để tránh gây hiểu lầm chúng có tác dụng). Nút `InventoryButton` mới trên TopBar đặt vào khoảng trống thật giữa Mail/Quest (đo qua `GetWorldCorners()`, không tin `anchoredPosition` hand math — phát hiện kỹ thuật: quy đổi vị trí và quy đổi kích thước dùng 2 hệ số scale KHÁC nhau dù cùng nhìn giống 1 con số, phải nội suy từ 2 điểm đo thật). Verify Play-mode thật đầy đủ nhất trong nhiều task UI gần đây — không gặp frame-stall, cả 2 tab hiện đúng dữ liệu save thật (Potion×3/Ether×1/Antidote×1, EssenceI×999/EssenceII×1020/EssenceIII×10/Core×10), Close hoạt động đúng. 413/413 test xanh (không đụng logic có test). **Title/Home NAY ĐÃ XÂY** (task-title-screen.md, follow-up "tiếp tục cho tôi") — game trước đây `GameBootstrap` auto-advance thẳng vào Meta không dừng màn nào (field `_autoAdvanceToMeta` tồn tại sẵn từ trước nhưng luôn `true`, chưa ai implement nhánh `false`). Nay có `TitleCanvas` tĩnh trong `Boot.unity` (sibling `MetaCanvas`, không tách scene thứ 4 — mọi màn khác trong dự án đều là Canvas overlay, không phải scene riêng): hiện "AETHER LEGION" + tóm tắt profile thật ("{N} Heroes · {Gold} Gold") + nút START, gộp thẳng logic vào `GameBootstrap` (không tách class riêng — Title chỉ có đúng 1 việc, khác Mail/Codex có logic claim/phân trang thật). Verify Play-mode thật: Title hiện đúng dữ liệu save thật, bấm START chuyển đúng sang scene Meta, TopBar/MetaCanvas bật lại đúng lúc — gặp lại MCP frame-stall khi verify sâu hơn (`MetaSceneInstaller.Start()` chưa kịp chạy) nhưng không cản được phần chính vì test qua `GameObject.Find`/`onClick.Invoke()` trực tiếp, không cần frame thật. 413/413 test xanh (thay đổi thuần Boot-flow, không đụng logic có test). **Mail và Codex/Collection nay đã có màn hình thật** (task-mail.md, task-codex.md — cả 2 chỉ có 1 dòng spec trong plan.md, tự thiết kế tối thiểu). Codex là màn ĐẦU TIÊN trong dự án cần phân trang (24 hero/66 enemy đều vượt 6 mục 1 trang) — phát hiện lúc làm: `TeamSelectScreen`'s `HeroListContainer` bị tràn khỏi khung y hệt (không Mask/ScrollRect) với 24 hero, đã báo riêng qua task nền lúc đó. **NAY ĐÃ SỬA** (task-teamselect-scroll.md) — thêm `HeroListViewport` (RectMask2D+ScrollRect) bọc `HeroListContainer` trong `UI_TeamSelect.prefab`, container tự resize theo đúng số hero thật mỗi lần rebuild; xác minh cấu trúc qua `execute_code` (24 card, content 1680px, viewport cố định 380px, Clamped) — không chụp ảnh/kéo thử thật được do môi trường MCP frame-stall (giới hạn đã biết, không phải lỗi bản sửa), 402/402 test vẫn xanh. **BUG NGHIÊM TRỌNG "Start Battle" bị đè, không bấm được — ĐÃ SỬA** (task-teamselect-start-button-fix.md, người dùng báo trực tiếp) — root cause xác nhận qua `execute_code` đọc `RectTransform` thật của `UI_TeamSelect.prefab`: `Footer` chỉ cao 40px (chứa `SelectedLabel`/`BackButton`/`StartButton` neo giữa), nhưng `task-formation-synergy.md` (session trước) parent `FormationRow` (dải cam full-width, nền mờ, có `Button` riêng) THẲNG VÀO `footer`, neo TOP với chiều cao 36px — gần như phủ HẾT 40px của Footer, đè lên `StartButton` (vẽ sau nên nằm trên + chặn luôn raycast). Sửa `TeamSelectScreen.BuildFormationButton`: đổi parent từ `footer` sang `content`, neo bottom-of-content với `anchoredPosition.y = footer.sizeDelta.y + 6` (đọc chiều cao Footer thật, không hardcode) — dải nằm NGAY TRÊN Footer thay vì đè lên nó. Verify bằng cách gọi `BuildShell()` thật qua reflection (không cần `PlayerProfileDto`), đọc `RectTransform` sống: `FormationRow` band content-bottom `[46,82]` không chồng `Footer` band `[0,40]` (cách 6px), còn dư 18px trước khi chạm `HeroListViewport`/`GearPanelContainer`. `validate_script` + force recompile 0 lỗi. Không có test EditMode nào phủ UI dựng-bằng-code của Meta — **518/518 xanh xác nhận sau khi Editor rảnh Play Mode** (thay đổi chỉ ở `Game.Meta`, không đụng `Game.Combat`/logic có test). **`LayoutProfileSwitcher`/`SafeAreaFitter` NAY ĐÃ XÂY** (task-phase-5-gaps.md Phần E) — 3 class mới `LayoutProfile`/`LayoutProfileSwitcher`/`SafeAreaFitter`, đặt ở `Assets/_Project/Scripts/Core/UI/` (namespace `Game.Core.UI`, KHÔNG `Game.UI` như bản nháp gốc của task file) — **phát hiện kiến trúc quan trọng lúc làm**: `Game.Meta` không được phép tham chiếu `Game.UI` (structure.md §6, `AssemblyRuleTests` enforce thật), nhưng 1 trong 3 màn pilot bắt buộc (`SettingsScreen`) nằm ở `Game.Meta` — nếu đặt ở `Game.UI` như task file viết ban đầu sẽ không compile được. Giải pháp đúng precedent có sẵn: `Game.Core.UI` (cùng namespace `IUiRootHost` đã dùng cho lý do y hệt), mọi assembly đều gián tiếp phụ thuộc `Game.Core` nên cả 3 pilot đều dùng được. `LayoutProfileSwitcher.PickProfile(w,h,portrait,landscape)` + `IsLandscape(w,h)=w>h` và `SafeAreaFitter.GetSafeAreaRect(screen,safeArea)` (trả Rect chuẩn hoá 0..1, không chia-cho-0 khi screen rỗng) là 2 hàm thuần, test bằng `ResponsiveLayoutTests.cs` mới (23 test: 6 tỉ lệ `IsLandscape`/`PickProfile` — 9:16/3:4/16:9/20:9/21:9/1:1 — + `GetSafeAreaRect` không-notch/notch-trên/cutout-2-bên/identity-5-tỉ-lệ/chia-0). Gắn pilot THẬT cả 3 màn (không phải chỉ viết class rồi để đó): `SettingsScreen`/`BattleHudScreen` code-dựng nên gắn ngay trong `Build()`/`BuildHeroPanel()` (Portrait = `LayoutProfile.CaptureFrom` số liệu hiện có — không đổi hành vi cũ; Landscape = số liệu mới tự thiết kế cho pilot, không phải bản responsive cuối cùng); `TitleCanvas` tĩnh trong `Boot.unity` nên gắn qua `manage_components` + gọi `SetProfiles()` thật qua `execute_code` rồi lưu scene (đúng [[feedback_hierarchy_means_static]] — không dựng bằng code runtime cho phần tĩnh). Verify thật cả 3 qua reflection (gọi `Build()`/`BuildLayout()` thật trên GameObject tạm, đọc lại field `_portrait`/`_landscape` của chính instance đã gắn, áp `PickProfile` với 2 kích thước giả lập xác nhận `RectTransform` đổi đúng số) — không phải chỉ đọc code bằng mắt. TitleCanvas xác nhận thêm bằng cách đọc trực tiếp `Boot.unity` sau khi lưu (`grep` thấy đúng `Game.Core.UI.SafeAreaFitter`/`LayoutProfileSwitcher` trong YAML). Cố ý KHÔNG đổi `CanvasScaler.referenceResolution` (mọi Canvas vẫn 960×540 cố định, đúng §E0 finding) — chỉ thêm 1 lớp preset RectTransform con. **Ngoài phạm vi (đúng §E1):** áp cho toàn bộ 23 màn hình, số liệu Landscape hiện tại chỉ là pilot demo chưa phải bản thiết kế cuối. **547/547 test xanh** (524 cũ + 23 mới). **CẬP NHẬT 2026-08-18 — 5 task-*.md ghi bù (làm xong nhưng chưa từng cập nhật dòng này):** `task-ui-vfx-polish.md` mở rộng Landscape lên 11/12 màn Meta dạng modal + TeamSelect + toàn bộ 4 panel Battle HUD còn lại (đủ bộ pixel-art UI kit dùng chung, TopBar đổi hẳn sang icon-only), verify lại bằng world-space geometry thật sau khi bắt được 1 lần đo giả (Portrait/Landscape đọc trùng số do quên gọi `ApplyTo` tường minh trước khi đo); SkillGrid có Portrait thật qua field `LayoutProfile.Scale` (trước đó chưa ai dùng field này, chỉ map identity giả). `task-hero-list.md`: `HeroListScreen` mới (roster sở hữu, filter Class + sort Level/Rarity/Name, phân trang 6/trang) — **màn "Hero roster riêng" từng bị liệt là thiếu ở đầu dòng này nay đã có**. `task-splash-loading.md`: `SplashCanvas`/`LoadingCanvas` mới + tip xoay ngẫu nhiên mỗi lần đổi scene (8 điểm gọi); tìm+sửa 1 bug crash thật đang ngủ (`TitleCanvas` đã mất khỏi scene, `ShowTitleScreen()` vẫn `.Find()` không điều kiện — chỉ chưa lộ vì `_autoAdvanceToMeta=true`) và 1 lỗi circular assembly reference thật (Meta/CombatView gọi ngược `Game.Bootstrap`) — sửa bằng `Game.Core.Scenes.ISceneTransitionService`, đúng mẫu `IUiRootHost`. `task-defeat-screen.md`: màn Defeat trước chỉ có 1 nút "CONTINUE" chung chung dù plan.md §4.15 tả rõ 3 lựa chọn — nay đủ Retry/Return-to-map/Revive-with-Gem (`CombatSimulation.TryReviveWithGem`, hồi 40% MaxHp, resume CHÍNH simulation đang chạy thay vì tạo trận mới), 5 test mới. `task-chapter-arena.md`: Arena "Coming Soon" (icon 11 trên TopBar, đúng scope v1.0) + `ChapterProgressScreen` (mở qua chạm `TitleLabel` có sẵn thay vì thêm nút, thuần đọc tiến trình — không cho chọn lại chương vì `NodeMapGenerator` chỉ sinh theo chương cao nhất, cần rework riêng để làm thật, ngoài phạm vi). **Với 2 màn Arena+ChapterSelect này, danh sách 23 màn hình plan.md §10.1 coi như đã đủ hết** (còn thiếu Landscape verify cho 5 màn mới nhất: Splash/Title/Loading/ChapterProgress/HeroList — ghi rõ như 1 khoản còn thiếu thật). **637/637 test xanh** (đo trực tiếp 2026-08-18, xem object-map.md §12 vì sao không cộng dồn theo delta cũ được nữa). **Trợ năng (plan.md §10.7) NAY ĐÃ XÂY 3/6 mục** (task-accessibility.md, 2026-08-18, người dùng chọn qua AskUserQuestion) — `SettingsDto.TextScale`/`ColorblindMode` tồn tại sẵn từ đầu dự án nhưng chưa ai dùng (đúng mẫu "hạ tầng có sẵn, chưa dùng" lặp lại nhiều lần). `TextScaleApplier` (`Game.Meta.Accessibility`, mới) quét đệ quy `Text`/`TextMeshProUGUI` dưới 1 root nhân `fontSize` gốc (nhớ qua `ConditionalWeakTable`, không cộng dồn khi gọi lặp) — gắn phản ứng ở `ServiceInstaller.WireSettingsToTextScale` (toàn `uiRoot`) + chủ động ở `BattleHudScreen.Bind()`. `BattleHudScreen.HpColor` đổi static→instance đọc `ColorblindMode`, bảng màu khác cả hue lẫn ĐỘ SÁNG (không chỉ hue, đúng khuyến nghị cho protanopia/deuteranopia) — phạm vi chỉ HP bar Battle HUD. PC hotkeys (1-5 chọn skill, Enter kết turn) qua `Unity.InputSystem.Keyboard.current` (thêm `Unity.InputSystem` vào `Game.UI.asmdef`), tái dùng ĐÚNG đường click thật `SkillSlotView.OnClicked` (không viết luồng chọn song song). `SettingsScreen` thêm nút cycle Text Scale 100/125/150% + toggle Colorblind. Verify: `TextScaleApplier` đo thật fontSize 20→25→30→20 không cộng dồn; `HpColor` xác nhận đổi đúng palette qua reflection; `SettingsScreen` cycle 3 lần đúng chuỗi. **Giới hạn đã biết**: màn Meta dựng-lười lần đầu sau khi đổi TextScale chưa áp ngay (chỉ áp khi setting đổi tiếp hoặc khởi động lại — sửa triệt để cần thêm vào ~20 `BuildShell()`, quá lớn 1 lượt); gamepad hotkeys/colorblind cho Element-status-rarity màn khác/text speed 1×-2×-tức thì/hiện số damage lớn CHƯA làm (3/6 mục còn lại của §10.7). 647/647 test xanh (571 sau task trước + không thêm test mới — verify qua reflection trực tiếp thay vì EditMode test, đúng mẫu UI code-dựng khác trong dự án). **Trợ năng 6/6 mục §10.7 NAY ĐÃ ĐỦ** (task-accessibility-part2.md, 2026-08-18) — gamepad hotkey (song song bàn phím, cùng `TrySelectSlot`), `SettingsDto.ShowLargeDamageNumbers` mới → `FloatingTextLayer` ×1.5 scale (chỉ damage/heal, không Miss/Perfect/Break), icon nguyên tố hình dạng khác nhau cho mù màu qua glyph ký tự (●▲▼■◆★✚ — thư mục sprite Elements RỖNG, không có art riêng, dùng glyph thay vì đợi pixel-art pipeline) trên Skill Grid khi bật Colorblind. Tốc độ chữ 1×/2×/tức thì XÁC NHẬN N/A (grep 0 kết quả — không có hiệu ứng gõ chữ nào trong game để tăng tốc). **Red Dot notification (plan.md §10.6) NAY ĐÃ XÂY** (task-reddot.md, 2026-08-18) — `RedDotService` (`Game.Meta.Notifications`, pull-model đơn giản hoá thay vì cây dirty-propagation gốc, TopBar chỉ 1 tầng phẳng không cần cây thật) phủ 4 chấm đỏ tĩnh (`Boot.unity`, cùng mẫu `MailBadge`): Hero (nâng skill được + đủ Gold), Dungeon (còn lượt hằng ngày — Energy không hề bị enforce ở đâu trong code thật nên "vào được" đã là miễn phí), TrialBoss/Tower (thưởng mốc tuần chưa nhận, 2 hàm `HasClaimable` PEEK mới). Summon/Shop bị bỏ có chủ đích (không có tín hiệu miễn phí thật/không có nút Shop trên TopBar). 10 test mới (`RedDotServiceTests.cs`). **668/668 test xanh** (cùng đợt AskUserQuestion với Boss Phase/Enrage [xem dòng P3] + Gacha disclosure [xem dòng P4] — 4 hạng mục build trong 1 lượt). |
| **P7** Cân bằng/Tối ưu | 🟡 Có khởi đầu | `BalanceHarness` tồn tại + đã chạy thật ít nhất 2 lần (task-ascend.md §8 phát hiện + vá lỗ hổng Essence III/Core/Gem thời PlaceholderLootTable; task-balance-loottable.md chạy lại sau khi loot table chuyển sang 10 asset theo chương thật — sửa 2 bug báo cáo cũ, xác nhận đường cong vẫn đúng, không cần vá số liệu). **CẬP NHẬT 2026-08-18 — `PerformanceBudgetTests.cs` mới (2 test)**: đo THẬT dòng duy nhất trong bảng plan.md §11.11 có số tuyệt đối test được trong EditMode — `SteadyStateTurnLoop_DoesNotAllocateGC` cô lập đúng vòng lặp Advance/SubmitIntent (tách khỏi chi phí dựng trận 1 lần) bằng `Is.Not.AllocatingGCMemory()` (hạ tầng UnityEngine.TestRunner có sẵn, không cần package rời) — **xác nhận PASS thật: 0 B/trận**, đúng mục tiêu "GC alloc/frame khi chiến đấu = 0 B". `Batch2000RandomBattles_CompletesWithinTimeBudget` — "soak" rút gọn (KHÔNG phải 2 giờ thật, ngưỡng 10s/2000 trận cố ý rộng, chỉ bắt hồi quy độ phức tạp thuật toán rõ rệt) — PASS. **Vẫn CHƯA làm được** (cần thiết bị thật/build thật, không giả lập được trong EditMode): FPS/draw call/RAM (§11.11 còn 3 dòng), Boot→Home/Vào trận (đo thời gian thật), build size AAB ≤150MB, soak 2 giờ thật, ma trận thiết bị thật. |
| **P8-P9** Phát hành/Hậu ra mắt | 🔴 Chưa bắt đầu | — |

**Ước lượng công sức đã dùng vs kế hoạch (cập nhật 2026-08-18, đoạn dưới đây đã lỗi thời — số liệu
26/45 là của 2026-08-10):** theo `object-map.md §12` hiện tại, Script `Game.Meta` đã đạt **46/45
(vượt kế hoạch gốc)** trong khi Prefab mới **16/70 (23%)** và `Game.UI` gần như vẫn 0% (2 script,
màn hình thật nằm ở `Game.Meta` theo lối đơn giản hoá đã chốt, xem object-map.md §12.1) — độ lệch
"logic/kinh tế trước, art/UI/content sau" của nhận định gốc vẫn đúng HƯỚNG, chỉ khác là UI/màn hình
nay đã bắt kịp đáng kể (P6 §0.1 ở trên) trong khi Prefab/Content (nội dung nghệ thuật thô, không
phải màn hình) vẫn là phần tụt lại xa nhất theo số liệu.

---

## PHASE 0 — Nền móng & Tiền sản xuất (Tuần 1–2)

**Mục tiêu:** dựng bộ khung để 30 tuần sau không phải làm lại.

### Tuần 1 — Hạ tầng dự án
- [ ] `git init` + `.gitignore` Unity + **Git LFS** cho `*.png *.jpg *.aseprite *.wav *.ogg *.psd`
- [ ] Di chuyển `Assets/UI_SAMPLE/` ra ngoài `Assets/` (thành `_Reference/`) — tránh lọt vào build & vấn đề bản quyền (xem plan.md §18)
- [ ] Dựng cây thư mục `Assets/_Project/...` theo [structure.md §2](structure.md)
- [ ] Tạo **11 assembly definition** theo [structure.md §6](structure.md): `Game.Core, Game.Data, Game.Combat, Game.CombatView, Game.Meta, Game.UI, Game.Services, Game.Bootstrap, Game.Tools` + 2 asmdef test
- [ ] Cấu hình Project Settings: Color Space **Linear**, 2D URP renderer, Input System, Company/Product name, bundle id
- [ ] Cài **Pixel Perfect Camera**, PPU = 32, chốt reference resolution 540×960 / 960×540
- [ ] Thêm package: `com.unity.addressables`, `com.unity.textmeshpro` (nếu chưa), `com.unity.localization`
- [ ] **Kiểm tra font pixel có đủ dấu tiếng Việt** (rủi ro R6 — làm ngay, không hoãn)

### Tuần 2 — Tooling, CI, Design System
- [ ] Scene `Boot` + `ServiceLocator` + composition root; luồng `Boot → Meta → Battle`
- [ ] `IPlayerRepository` + `LocalPlayerRepository` (JSON + AES + atomic write + version/migration)
- [ ] **CSV → ScriptableObject importer** (Editor menu `Tools/Import Game Data`) + `DataValidator`
- [x] **`ObjectMapValidator`** (Editor menu `Tools/Validate Object Map`) — quét scene/prefab, đối chiếu [object-map.md](object-map.md) §3–§4, báo script/prefab chưa đăng ký — **XONG 2026-08-12**, xem §0.1 P0
- [ ] **Balance Harness** rỗng: chạy headless N trận, xuất CSV (mở rộng dần ở P2)
- [ ] GitHub Actions + GameCI: build Android + chạy EditMode test mỗi push
- [ ] **Design System**: chốt palette 48 màu, 9-slice panel gốc, button state, font size scale, spacing 4px grid
- [ ] Roslyn analyzer / test chặn `UnityEngine.Random`, `Time.*` trong `Game.Combat` (rủi ro R8)
- [ ] Khóa phạm vi: 24 hero / 60 enemy / 8 boss / 5 chương — **viết ra và không đổi** (rủi ro R2)

**✅ DoD Phase 0:** Nhấn Play → Boot chạy → vào Meta scene trống → chuyển sang Battle scene trống, CI xanh, import 1 file CSV mẫu ra SO thành công.

---

## PHASE 1 — Prototype chiến đấu (Tuần 3–6)

**Mục tiêu:** chứng minh **combat có vui không**, bằng hình khối xám cũng được.

### Tuần 3 — Simulation lõi (C# thuần, không Unity)
- [ ] `PrimaryStats`, `DerivedStats`, `StatCalculator` (công thức plan.md §4.3)
- [ ] `Unit` runtime model (HP/SP/ATB/Poise/status list/formation slot)
- [ ] `TurnScheduler` — ATB tick rời rạc, tie-break ổn định
- [ ] `IRandomSource` + `XorShiftRandom(seed)` — deterministic
- [ ] `DamageCalculator` — đủ 4 bước công thức plan.md §4.4
- [ ] `CombatSimulation.Step(ActionIntent) → List<CombatEvent>`
- [ ] **Unit test:** 30 test cho damage/turn order/crit — chạy xanh

### Tuần 4 — Trình diễn & Skill Grid
- [ ] `CombatPresenter` — tiêu thụ `CombatEvent` tuần tự, có hàng đợi + tốc độ phát
- [ ] Unit view: sprite tạm, idle/attack/hit/death, HP bar nổi
- [ ] **Skill Grid 5×3** (uGUI): icon, cost SP, số CD, trạng thái khả dụng/xám
- [ ] Turn Order Bar hiển thị 8 lượt kế
- [ ] Nút **END TURN** + chọn mục tiêu (tap/click, có auto-suggest)
- [ ] Damage number pooling + hit-stop cơ bản

### Tuần 5 — ⭐ Action Command (tính năng rủi ro nhất, làm sớm)
- [ ] `ActionCommandController`: 4 loại (single tap / combo n-nhịp / charge / guard)
- [ ] UI cửa sổ nhịp: vòng tròn thu nhỏ + vùng Perfect, đọc được trên màn nhỏ
- [ ] Input buffer 100 ms; đo **input latency thật trên Android** (rủi ro R1)
- [ ] Kết quả Perfect/Good/Miss vào công thức damage + phản hồi hình/âm riêng biệt
- [ ] Toggle tắt Action Command trong Settings (Accessibility từ ngày 1)
- [ ] **Playtest 5 người** — hỏi đúng 1 câu: *"Bạn có muốn chơi thêm trận nữa không?"*

### Tuần 6 — AI, Poise/Break, đóng gói prototype
- [ ] `AIController` Utility AI + `AIProfile` SO (plan.md §4.11)
- [ ] **Intent Preview** — icon ý định trên đầu địch
- [ ] Hệ thống **Poise → Break** + VFX chớp trắng/freeze frame
- [ ] Điều kiện thắng/thua, màn hình Result tạm
- [ ] Auto-battle v0 + tốc độ ×1/×2/×3
- [ ] **Fuzz test:** 10.000 trận ngẫu nhiên không exception, kết thúc ≤ 200 lượt

**🚦 QUALITY GATE 1:** cho 8 người lạ chơi 3 trận. **≥ 6 người phải muốn chơi tiếp.** Không đạt → dừng, chỉnh cơ chế, lặp lại tuần 5–6. *Đây là điểm dừng quan trọng nhất của cả dự án.*

**✅ DoD Phase 1:** trận 4v3 hoàn chỉnh, chơi tay có lợi thế rõ so với auto, deterministic (cùng seed = cùng kết quả), test xanh.

---

## PHASE 2 — VERTICAL SLICE ⭐ (Tuần 7–11)

**Mục tiêu:** **một lát cắt dọc hoàn chỉnh, chất lượng phát hành** — 1 chương, 4 hero, 8 enemy, 1 boss, có node map, có phần thưởng, có art thật, có âm thanh. Đây là thứ dùng để gọi vốn / tuyển người / quyết định đi tiếp.

### Tuần 7 — Art thật đợt 1
- [ ] 4 hero (mỗi class 1: Vanguard, Slayer, Arcanist, Warden) — đủ 7 animation
- [ ] 8 enemy chương 1 + 1 boss (Sói Đầu Đàn) 2 phase
- [ ] Tileset biome Đồng Cỏ + parallax background 3 lớp
- [ ] Sprite Atlas riêng: `UI`, `Units`, `VFX`, `Icons`

### Tuần 8 — Battle HUD thật (theo `image_UI.jpg`)
- [ ] Hero panel trái: portrait, HP/SP bar, LV, hàng icon buff
- [ ] Enemy panel phải: HP/ATK/DEF, hệ nguyên tố, intent
- [ ] Skill Grid pixel-art hoàn chỉnh + tooltip mô tả (kiểu `FIRE / BURN` trong `game_4.jpg`)
- [ ] Card/Item slot góc dưới-trái (3 ô tiêu hao)
- [ ] Panel STATS/EQ bên phải
- [ ] Zone indicator `SWAMPS [1/3]`

### Tuần 9 — Node Map & vòng lặp chương
- [ ] Sinh node map phân nhánh (12 node, 3 tầng, thuật toán bảo đảm luôn có đường đi)
- [ ] Node types: Battle, Elite, Treasure, Rest, Event, Shop, Boss
- [ ] Màn hình Pre-Battle (chọn đội 4 hero) → Battle → Result → về map
- [ ] Phần thưởng: vàng, EXP, 1 trang bị ngẫu nhiên (`LootRoller` + unit test)
- [ ] Lưu tiến trình run vào save (thoát giữa chừng vào lại đúng chỗ)

### Tuần 10 — Juice, VFX, Audio
- [ ] Toàn bộ bảng phản hồi ở plan.md §10.4 (hit-stop, shake, flash, dissolve chết, PERFECT!, BREAK!)
- [ ] 12 VFX nguyên tố + 6 VFX buff/debuff
- [ ] BGM: 1 menu + 1 biome + 1 boss + victory/defeat
- [ ] ~40 SFX, AudioMixer 4 group, slider trong Settings
- [ ] Camera: zoom nhẹ khi crit, pan khi Ultimate

### Tuần 11 — Tutorial, đánh bóng, đóng gói
- [ ] Tutorial 5 bước dạy: chọn skill → Action Command → hệ khắc chế → Break → Ultimate (dạy trong trận thật, không phải slide)
- [ ] Màn hình Title + Settings tối thiểu
- [ ] Build Android + build Windows chạy được
- [ ] **Playtest 15 người, đo:** thời gian chơi trung bình, tỉ lệ hoàn thành chương 1, độ chính xác Action Command, câu hỏi "chỗ nào khó hiểu?"

**🚦 QUALITY GATE 2:** người chơi mới hoàn thành chương 1 mà không cần hỏi, ≥ 60% chơi tiếp run thứ 2.

**✅ DoD Phase 2:** build cài lên điện thoại, đưa cho người lạ, họ chơi trọn 15 phút và hiểu game.

---

## PHASE 3 — Combat đầy đủ (Tuần 12–15)

### Tuần 12 — Hệ trạng thái đầy đủ
- [ ] Cài đặt cả 16 status ở plan.md §4.9 + luật stack/thời lượng/tick
- [ ] Luật kháng: `RES` vs `EFFECT_ACC`, hiển thị "RESIST!"
- [ ] Cleanse / Dispel / Immunity / Reflect / Counter
- [ ] Unit test riêng cho từng status (16 bộ test)

### Tuần 13 — Skill & Class hoàn chỉnh
- [ ] 6 class × 5 skill × 2 hero mẫu = template hoàn chỉnh
- [ ] Combo Action Command đa nhịp (Slayer 5 nhịp)
- [ ] Summoner: minion có slot ATB riêng
- [ ] Ultimate Gauge chung của đội + cutscene ngắn khi kích hoạt
- [ ] Skill Level 1→8 ảnh hưởng power/CD

### Tuần 14 — Formation, hàng trận, synergy
- [ ] 2 hàng × 2 cột, modifier hàng sau, skill `Swap Row`
- [ ] 8 preset đội hình + bonus
- [ ] Team Synergy theo class/element
- [ ] Passive Trait (Awakening) — 6 trait mẫu

### Tuần 15 — Boss & AI nâng cao
- [ ] `PhaseController` (đổi AI theo mốc HP)
- [ ] `SignatureMove` đếm ngược công khai 3 lượt
- [ ] Enrage timer sau lượt 12
- [ ] 3 boss hoàn chỉnh (chương 1–3) đủ 4 yếu tố thiết kế (plan.md §6.2)
- [ ] **Golden test:** 20 kịch bản trận cố định, so log — bắt regression

**✅ DoD Phase 3:** mọi cơ chế chiến đấu đã hoàn tất; từ đây chỉ thêm *nội dung*, không thêm *hệ thống* chiến đấu.

---

## PHASE 4 — Meta & Progression (Tuần 16–19)

### Tuần 16 — Hero management
- [ ] Màn hình Hero list (lọc/sắp xếp theo class, rarity, level, power)
- [ ] Hero detail: stat, skill, trang bị, tiểu sử, preview animation
- [ ] Level up (EXP), Ascend (mảnh + vật liệu), Skill up (sách)
- [ ] Tính **Power Score** để so sánh nhanh

### Tuần 17 — Trang bị & Inventory
- [ ] 6 slot, 5 rarity, main/sub stat, sinh ngẫu nhiên có kiểm soát
- [ ] Enhance +0→+15, tỉ lệ thất bại từ +12
- [ ] Set Bonus 2/4 món (8 set v1)
- [ ] Inventory: lọc, khóa món, bán hàng loạt, đánh dấu "so với đang đeo"
- [ ] Reforge sub stat

### Tuần 18 — Kinh tế, Gacha, Shop
- [ ] `IEconomyService.TryConsume/Grant` — mọi giao dịch đi qua đây (chuẩn bị server)
- [ ] 7 loại tiền tệ + Energy hồi theo thời gian (chống đổi giờ máy cơ bản)
- [ ] Summon: tỉ lệ + **soft/hard pity**, lịch sử 100 lần, animation mở thẻ
- [ ] Shop: gói thường, gói Honor, refresh hằng ngày
- [ ] Unit test gacha: 1 triệu lần roll → tỉ lệ khớp bảng ±0.05%, pity luôn kích hoạt

### Tuần 19 — Nhiệm vụ, Mail, Thành tựu, Save hoàn chỉnh
- [ ] Quest hằng ngày/tuần + Quest chuỗi (như `Quest No.152` trong `Game_1.jpg`)
- [ ] Achievement + Collection/Codex (hero, enemy, item đã gặp)
- [ ] Mail box (chuẩn bị cho đền bù LiveOps)
- [ ] Save đầy đủ mọi hệ thống + **test migration** từ save version cũ
- [ ] Battle Pass khung (chưa bật nội dung)

**✅ DoD Phase 4:** chơi 3 ngày liên tục, thoát/vào lại nhiều lần, không mất dữ liệu, không lỗi kinh tế.

---

## PHASE 5 — Nội dung & Progression đầy đủ (Tuần 20–23)

### Tuần 20–21 — Sản xuất nội dung hàng loạt
- [ ] 5 biome tileset + background
- [ ] 24 hero hoàn chỉnh (art + skill + balance sơ bộ)
- [ ] 60 enemy + 8 boss
- [ ] 5 chương × 12–15 node, đường cong độ khó tăng dần
- [ ] 30 sự kiện Event node (mỗi cái 2–3 lựa chọn có rủi ro)
- [ ] ~200 icon (skill/item/buff/currency)

### Tuần 22 — Endgame content
- [ ] 4 Dungeon hằng ngày × 10 tầng (Vàng, EXP, Vật liệu, Đá cường hóa)
- [ ] Tháp Vô Tận 100 tầng, HP không hồi, reset tuần
- [ ] Trial Boss tuần + **Damage Meter** (như `Game_1.jpg`)
- [ ] Bảng thưởng theo mốc

### Tuần 23 — Bản địa hóa & Codex
- [ ] Toàn bộ chuỗi ra file localization, dịch VI + EN đầy đủ
- [ ] Test tràn chữ bằng chuỗi giả dài 1.6×
- [ ] Lore/mô tả cho 24 hero + 60 enemy
- [ ] Kiểm tra không còn chuỗi hard-code (script quét tự động)

**✅ DoD Phase 5:** chơi được từ tutorial đến hết chương 5 + endgame, không có nội dung placeholder.

---

## PHASE 6 — UI/UX Responsive & Polish (Tuần 24–26)

### Tuần 24 — Hệ thống responsive
- [ ] `LayoutProfileSwitcher` + `RectTransformPreset` (plan.md §10.3)
- [ ] Áp cho cả 23 màn hình: 1 prefab, 2 preset
- [ ] `SafeAreaFitter` cho notch/lỗ camera
- [ ] Test 5 tỉ lệ: 9:16, 3:4, 16:9, 20:9, 21:9 — chụp ảnh so sánh tự động

### Tuần 25 — Hoàn thiện màn hình meta
- [ ] Bottom nav 6 mục (Growth/Hero/Summon/Dungeon/Arena/Shop) theo `Game_1.jpg`
- [ ] Side rail sự kiện/nhiệm vụ + badge chấm đỏ (`RedDotService` theo cây phân cấp)
- [ ] Top bar tiền tệ + hiệu ứng số chạy khi thay đổi
- [ ] Chuyển cảnh mượt giữa các màn (fade/slide, không giật)
- [ ] Auto ON/OFF + tốc độ, ghi nhớ lựa chọn

### Tuần 26 — Polish & Accessibility
- [ ] Rà toàn bộ bảng juice plan.md §10.4 — không tương tác nào "câm"
- [ ] Accessibility đầy đủ: tắt Action Command, tắt shake, chế độ mù màu (icon có hình dạng khác nhau), scale chữ 100/125/150%
- [ ] Điều khiển gamepad + phím tắt cho bản PC
- [ ] Loading screen có mẹo chơi
- [ ] Rà soát toàn bộ text (chính tả, giọng văn nhất quán)

**✅ DoD Phase 6:** xoay ngang/dọc bất kỳ màn nào, không vỡ layout; chơi được hoàn toàn bằng gamepad trên PC.

---

## PHASE 7 — Cân bằng, Tối ưu, Kiểm thử (Tuần 27–28)

### Tuần 27 — Cân bằng bằng dữ liệu
- [ ] Balance harness: mô phỏng **1000 trận/stage** headless, xuất CSV (win rate, số lượt, TTK, hero nào bị bỏ xó)
- [ ] Chỉnh cho mọi stage nằm trong dải thắng **55–90%** (boss 40–65%)
- [ ] Không hero nào có tỉ lệ sử dụng < 3% hoặc > 60% ở playtest
- [ ] Đường cong kinh tế: mô phỏng 30 ngày F2P vs người trả tiền
- [ ] Kiểm tra không có combo "vô đối" (thử fuzz đội hình cực đoan)

### Tuần 28 — Hiệu năng & Ổn định
- [ ] Profiler: đạt ngân sách plan.md §11.9 — **0 B GC/frame trong trận**
- [ ] Pool hóa mọi thứ còn sót; gỡ `GetComponent`/`Find` trong `Update`
- [ ] Addressables: nhóm theo màn hình, giải phóng khi rời
- [ ] Giảm build < 150 MB (nén texture ASTC, audio setting)
- [ ] Test **soak 2 giờ** liên tục — không rò rỉ bộ nhớ, không crash
- [ ] Test trên tối thiểu 6 thiết bị thật (2 low / 2 mid / 2 high) + 1 iOS
- [ ] Test mất mạng, gọi điện xen ngang, tắt app đột ngột khi đang lưu

**🚦 QUALITY GATE 3:** 60 FPS ổn định trên máy Android tầm trung, 0 crash trong 2 giờ, mọi stage trong dải cân bằng.

---

## PHASE 8 — Beta → Soft Launch → Phát hành (Tuần 29–30)

### Tuần 29 — Closed Beta
- [ ] Bật Analytics (Firebase/UGS) với bộ sự kiện plan.md §14
- [ ] Google Play Internal Testing / TestFlight — 100–300 người
- [ ] Kênh thu thập lỗi (form + Discord) + crash reporting
- [ ] Theo dõi: D1 retention (mục tiêu ≥ 40%), tỉ lệ hoàn thành tutorial (≥ 85%), điểm rớt người chơi

### Tuần 30 — Store & Phát hành
- [ ] Trang store: icon, 8 screenshot × 2 hướng, video 30 giây, mô tả VI/EN
- [ ] Tuân thủ: **công khai tỉ lệ gacha trong game**, chính sách quyền riêng tư, phân loại độ tuổi, GDPR consent
- [ ] Bản PC: build Steam, tắt Energy & Gacha bằng `MonetizationProfile`, thẻ Steam, trang store
- [ ] Soft launch 1 thị trường nhỏ (VD: Philippines/Việt Nam) 2 tuần → đọc số liệu → chỉnh → phát hành rộng
- [ ] Quy trình hotfix + Remote Config cho các hằng số cân bằng

---

## PHASE 9 — Hậu ra mắt (Tuần 31+)

| Bản | Nội dung | Ước lượng |
|---|---|---|
| **v1.1** | ~~Arena async PvP (snapshot đội hình), leaderboard cục bộ~~ **CẬP NHẬT 2026-08-18: Arena kéo lên làm sớm** (task-arena.md) — `ArenaSystem`/`ArenaScreen` thật, 5 bậc đối thủ snapshot hero do RNG sinh (không backend nên không phải người chơi thật khác, đúng diễn giải trung thực của "PvP async...do AI điều khiển"), mùa 14 ngày, Honor (currency đã tồn tại từ đầu dự án nhưng chưa từng dùng). Leaderboard cục bộ nhiều người chơi VẪN chưa có (đúng — cần server thật). | 4 tuần |
| **v1.2** | Backend thật: `RemotePlayerRepository`, tài khoản, cloud save, xác thực trận bằng replay seed | 8 tuần |
| **v1.3** | Guild + Guild Boss + chat | 6 tuần |
| **v1.4** | Chương 6–8, 8 hero mới, biome mới | 6 tuần |
| **Mùa** | Battle Pass mới + sự kiện giới hạn mỗi 30 ngày | định kỳ |

---

## 1. Ma trận phụ thuộc (thứ tự bắt buộc)

```
P0 Assembly + Save + CSV importer
      ↓
P1 Simulation ──→ Presenter ──→ Action Command ──→ AI
      ↓                              ↓
P2 Art thật + HUD + Node Map + Juice  (cần Action Command chốt xong)
      ↓
P3 Status/Skill/Formation/Boss  ←─ cần Simulation ổn định
      ↓
P4 Meta (Hero/Equip/Gacha)      ←─ cần Save + Economy
      ↓
P5 Nội dung hàng loạt           ←─ cần CSV importer + template skill từ P3
      ↓
P6 Responsive                   ←─ cần toàn bộ màn hình tồn tại
      ↓
P7 Cân bằng                     ←─ cần harness + toàn bộ nội dung
      ↓
P8 Phát hành
```

**Có thể làm song song:** Art (P2 trở đi) ∥ Code · Audio (P2, P5) ∥ mọi thứ · Localization (P5) ∥ Polish.

---

## 2. Phân công theo vai trò (đội 4 người)

| Vai trò | Tải chính | Phase nặng nhất |
|---|---|---|
| **Lead/Gameplay Programmer** | Simulation, Action Command, AI, kiến trúc | P1, P3 |
| **UI/Tools Programmer** | 23 màn hình, responsive, editor tools, save | P2, P4, P6 |
| **Pixel Artist** | 24 hero, 60 enemy, 6 biome, 200 icon, VFX | P2, P5 |
| **Designer/Producer** | Cân bằng số liệu, nội dung stage, kinh tế, playtest | P5, P7 |
| *(Part-time)* Audio | 11 BGM + 120 SFX | P2, P5 |

---

## 3. Nhịp làm việc

- **Sprint 2 tuần**, mỗi sprint kết thúc bằng **build chạy được trên điện thoại thật**.
- **Playtest bắt buộc:** tuần 5, 11, 19, 23, 27 — mỗi lần ≥ 8 người, ghi hình màn hình.
- **Bug bash** nửa ngày cuối mỗi phase, cả đội cùng chơi phá.
- **Nightly CI:** build + EditMode test + balance harness → cảnh báo nếu win-rate stage lệch > 10% so với hôm trước.
- **Mỗi PR bắt buộc:** chạy đúng checklist ở [object-map.md §7](object-map.md) tương ứng với loại thay đổi, và cập nhật [structure.md](structure.md) + [object-map.md](object-map.md) trong cùng commit.

---

## 4. Chỉ số theo dõi mỗi sprint

| Chỉ số | Ngưỡng |
|---|---|
| EditMode test xanh | 100% |
| Bao phủ `Game.Combat` | ≥ 80% |
| Cảnh báo Console | 0 |
| FPS trên máy tham chiếu | ≥ 60 |
| GC alloc/frame trong trận | 0 B |
| Số bug P0/P1 mở | 0 khi kết thúc phase |
| Kích thước build | ≤ 150 MB |
| `Tools/Validate Object Map` | 0 chênh lệch |
| Cột "Đã tạo" ở [object-map.md §12](object-map.md) | cập nhật đúng thực tế |

---

## 5. Danh sách công việc tuần 1 (bắt đầu ngay)

Theo thứ tự, có thể làm xong trong 2–3 ngày:

1. `git init` + `.gitignore` Unity + Git LFS
2. Chuyển `Assets/UI_SAMPLE/` → `_Reference/` (ngoài Assets)
3. Tạo cây thư mục `Assets/_Project/...`
4. Tạo 10 assembly definition với quy tắc tham chiếu đúng
5. Cài Addressables + Localization
6. Cấu hình Pixel Perfect Camera, PPU 32, reference resolution
7. **Tìm/đặt font pixel có dấu tiếng Việt** ← rủi ro chặn, làm trước
8. Viết `IPlayerRepository` + `LocalPlayerRepository` + 3 unit test
9. Dựng scene `Boot` + `ServiceLocator` + luồng chuyển scene ([object-map.md §3.1](object-map.md))
10. Dựng GitHub Actions chạy EditMode test + `AssemblyRuleTests`
11. Cập nhật cột "Đã tạo" ở [object-map.md §12](object-map.md)

---

## 6. Cột mốc "có thể trình diễn" (demo được cho người ngoài)

| Tuần | Demo được gì |
|---|---|
| 6 | "Đây là combat của chúng tôi" — 1 trận, hình khối xám, nhưng vui |
| 11 | "Đây là game của chúng tôi" — 15 phút chơi hoàn chỉnh, đẹp |
| 19 | "Đây là vòng lặp giữ chân" — hero, gacha, trang bị, chơi 3 ngày |
| 23 | "Đây là toàn bộ nội dung" — 5 chương + endgame |
| 28 | "Đây là bản sẵn sàng bán" |

---

## 7. Ước lượng công sức (person-week)

| Phase | Prog | Art | Design | Audio | Tổng |
|---|---|---|---|---|---|
| P0 | 4 | 1 | 1 | 0 | **6** |
| P1 | 8 | 2 | 2 | 0 | **12** |
| P2 | 8 | 6 | 3 | 2 | **19** |
| P3 | 7 | 2 | 3 | 0 | **12** |
| P4 | 8 | 3 | 3 | 0 | **14** |
| P5 | 4 | 10 | 6 | 3 | **23** |
| P6 | 5 | 3 | 1 | 0 | **9** |
| P7 | 3 | 1 | 3 | 0 | **7** |
| P8 | 3 | 1 | 2 | 0 | **6** |
| | | | | | **≈ 108 person-week** |

---

## 8. Ngân sách rủi ro
Cộng thêm **20% buffer** (≈ 6 tuần) vào lịch. Kinh nghiệm ngành: dự án game vượt lịch trung bình 30–50%. Buffer này **không** dùng cho tính năng mới — chỉ dùng cho việc đã lên kế hoạch bị chậm.

---

## 9. Nguyên tắc cắt giảm khi trễ hạn

Nếu phải cắt, cắt theo **đúng thứ tự này** (từ trên xuống):
1. Tháp Vô Tận → v1.1
2. Trial Boss tuần → v1.1
3. Giảm 24 hero → 16 hero (giữ đủ 6 class)
4. Giảm 5 chương → 4 chương
5. Giảm 8 set trang bị → 5 set
6. Battle Pass → v1.1

**KHÔNG BAO GIỜ cắt:** Action Command · Break/Poise · juice của trận đấu · responsive · tutorial · cân bằng. Đây là những thứ quyết định game hay hay dở.

---

## 10. Danh sách kiểm tra trước khi phát hành

- [ ] Chơi thông từ đầu đến hết trên 6 thiết bị thật, không crash
- [ ] Save/load qua 3 version app liên tiếp (test migration)
- [ ] Tỉ lệ gacha hiển thị trong game, khớp code, có unit test chứng minh
- [ ] Chính sách quyền riêng tư + điều khoản + GDPR/CCPA consent
- [ ] Phân loại độ tuổi (IARC) đã nộp
- [ ] Tất cả art là tự làm hoặc có giấy phép thương mại — **`UI_SAMPLE/` không có trong build**
- [ ] Không còn chuỗi hard-code, không còn text tiếng Anh sót trong bản VI
- [ ] Crash reporting + Analytics hoạt động trên bản release
- [ ] Remote Config cho hằng số cân bằng đã kết nối
- [ ] Có quy trình hotfix < 24 giờ
- [ ] Test mua IAP thật (sandbox + 1 giao dịch thật) trên cả 2 store
- [ ] Test chế độ máy bay, mất mạng giữa chừng, đổi giờ hệ thống

---

## 11. Nếu làm một mình (solo dev)

Nhân lịch **×1.9** → **56–64 tuần**. Cắt giảm khuyến nghị ngay từ đầu:

| Điều chỉnh | Lý do |
|---|---|
| 12 hero thay vì 24 | Art là nút thắt lớn nhất của solo dev |
| 30 enemy thay vì 60 | Tái sử dụng palette swap + đổi skill |
| 3 chương thay vì 5 | Đủ để bán, mở rộng sau |
| **Chỉ Portrait ở v1** | Bỏ landscape → tiết kiệm ~4 tuần, thêm sau |
| Mua asset pack pixel có license thương mại | Đổi tiền lấy thời gian, tập trung vào code + design |
| Bỏ gacha, bán buy-to-play trên itch.io/Steam | Không cần cân bằng kinh tế phức tạp, không cần tuân thủ loot box |
| Bỏ dungeon/tháp ở v1 | Tập trung vào campaign chất lượng |

**Thứ tự ưu tiên tuyệt đối cho solo dev:** P0 → P1 → **QG1** → P2 → **QG2** → nếu QG2 đạt thì mới đầu tư tiếp. Đừng làm meta systems trước khi biết combat có vui không.

---

*Tài liệu thiết kế chi tiết: [plan.md](plan.md)*

# OBJECT-MAP.md — Bản đồ đối tượng & Ma trận tác động

> **Mục đích:** trả lời 3 câu hỏi trong 30 giây:
> 1. **"Object/Prefab này gắn script nào, đọc data nào, nghe event nào?"** → §3, §4
> 2. **"Sửa script/data này thì ảnh hưởng những đâu?"** → §5, §6, §8
> 3. **"Thêm 1 hero/skill/status/màn hình mới thì phải làm những bước nào?"** → §7 — **Checklist bắt buộc**
>
> **Bộ tài liệu:** [plan.md](plan.md) · [structure.md](structure.md) · [object-map.md](object-map.md) *(file này)* · [roadmap.md](roadmap.md)
>
> ⚠️ **Luật vàng:** mọi thay đổi động tới Scene / Prefab / Script / ScriptableObject **phải** cập nhật file này trong cùng commit. Đây là mục 8 & 9 của Definition of Done ([plan.md §16](plan.md)).

---

## 1. Cách dùng file này

| Tình huống | Đọc mục |
|---|---|
| Sắp sửa 1 script trong scene Battle | §3.3 → tìm GameObject → xem cột "Nghe event" và "Ai phụ thuộc" |
| Sắp đổi công thức damage | §6 hàng `DamageCalculator` → danh sách nơi bị ảnh hưởng |
| Thêm hero/skill/status/enemy/màn hình mới | §7 → chạy đúng checklist |
| Thêm `CombatEvent` mới | §5 → đăng ký publisher + subscriber |
| Không biết prefab nào chứa widget X | §4 |
| Kiểm tra đã có test chưa | §8 |
| Sắp xoá 1 script | §6 → cột "Ai phụ thuộc" phải rỗng trước khi xoá |

---

## 2. Hệ thống mã định danh

Mọi thực thể có mã ổn định để tham chiếu chéo giữa các tài liệu và trong commit message.

| Tiền tố | Loại | Ví dụ |
|---|---|---|
| `SC-` | Scene | `SC-BATTLE` |
| `GO-` | GameObject trong scene | `GO-BTL-SKILLGRID` |
| `PF-` | Prefab | `PF-UI-SKILLSLOT` |
| `S-` | Script | `S-CMB-DMGCALC` |
| `D-` | ScriptableObject / data asset | `D-SKILL-BLAZING_BASH` |
| `E-` | CombatEvent / GameEvent | `E-DAMAGE_DEALT` |
| `SV-` | Service | `SV-ECONOMY` |
| `T-` | Test | `T-CMB-EDGECASE` |
| `UI-` | Màn hình | `UI-BATTLEHUD` |

**Quy ước commit:** `feat(S-CMB-DMGCALC): thêm DefIgnore` — grep được ngay mọi thay đổi liên quan.

---

## 3. BẢN ĐỒ SCENE

### 3.1. `SC-BOOT` — `Boot.unity`

| Mã | GameObject (path) | Script gắn vào | Đọc data | Phát event | Ghi chú |
|---|---|---|---|---|---|
| `GO-BOOT-ROOT` | `/GameBootstrap` | `GameBootstrap`, `ServiceInstaller` | `GameDatabase` | `E-SERVICES_READY` | Composition root — **nơi duy nhất** `new` service |
| `GO-BOOT-SVC` | `/ServiceRoot` | — (`DontDestroyOnLoad`) | — | — | Cha của mọi service MonoBehaviour |
| `GO-BOOT-AUDIO` | `/ServiceRoot/AudioRoot` | `AudioService`, `MusicPlayer`, `SfxPool` | `AudioCueRegistry` | — | Pool AudioSource 16 |
| `GO-BOOT-UI` | `/ServiceRoot/UIRoot` | `UIManager`, `UIScreenStack`, `ToastService` | `UIRegistry` | `E-SCREEN_CHANGED` | Canvas overlay, sort order 100 |
| `GO-BOOT-INPUT` | `/ServiceRoot/InputRoot` | `InputService`, `PlayerInput` | `GameInputActions` | `E-BACK_PRESSED` | Trừu tượng touch/chuột/gamepad |
| `GO-BOOT-CAM` | `/BootCamera` | `Camera` | — | — | Xoá sau khi vào Meta |
| `GO-BOOT-SPLASH` | `/SplashCanvas` | `SplashScreen` | — | — | Hiện 2 giây |

**Luồng khởi động:** `GameBootstrap.Awake()` → `ServiceInstaller.Install()` → `IPlayerRepository.LoadAsync()` → `GameDatabase` load qua Addressables → `SettingsService.Apply()` → `GameStateMachine.Change(MetaState)`.

### 3.2. `SC-META` — `Meta.unity`

| Mã | GameObject (path) | Script | Đọc data | Nghe event | Ghi chú |
|---|---|---|---|---|---|
| `GO-META-INST` | `/MetaSceneInstaller` | `MetaSceneInstaller` | — | — | Dựng scene, hiện `UI-HOME` |
| `GO-META-CAM` | `/MetaCamera` | `Camera`, `PixelPerfectCamera` | — | — | PPU 32 |
| `GO-META-CANVAS` | `/MetaCanvas` | `Canvas`, `CanvasScaler`, `GraphicRaycaster` | — | — | Ref 540×960 / 960×540 |
| `GO-META-SAFE` | `/MetaCanvas/SafeArea` | `SafeAreaFitter`, `LayoutProfileSwitcher` | — | `E-ORIENTATION_CHANGED` | |
| `GO-META-TOPBAR` | `.../SafeArea/TopBar` | `CurrencyBar` | `WalletDto` | `E-CURRENCY_CHANGED` | Số chạy khi thay đổi |
| `GO-META-HOST` | `.../SafeArea/ScreenHost` | — | — | — | `UIScreenStack` instantiate màn vào đây |
| `GO-META-NAV` | `.../SafeArea/BottomNav` | `BottomNavBar`, 6× `RedDot` | — | `E-REDDOT_DIRTY` | Home/Hero/Summon/Dungeon/Arena/Shop |
| `GO-META-OVERLAY` | `/MetaCanvas/OverlayHost` | `ConfirmDialog`, `Toast`, `LoadingOverlay` | — | — | Không nằm trong screen stack |
| `GO-META-BG` | `/Background` | `ParallaxLayer` ×3 | — | — | |

### 3.3. `SC-BATTLE` — `Battle.unity` ⭐ (scene quan trọng nhất)

| Mã | GameObject (path) | Script | Đọc data | Nghe event | Phát event |
|---|---|---|---|---|---|
| `GO-BTL-INST` | `/BattleSceneInstaller` | `BattleSceneInstaller` | `BattleConfig`, `StageDefinition` | — | `E-BATTLE_SCENE_READY` |
| `GO-BTL-CTRL` | `/BattleController` | `BattleController`, `CombatPresenter`, `EventPlaybackScheduler` | `BalanceConstants` | `E-BACK_PRESSED` | `ActionIntent` → Simulation |
| `GO-BTL-CAM` | `/BattleCamera` | `Camera`, `PixelPerfectCamera`, `CameraDirector`, `ScreenShake` | — | `E-DAMAGE_DEALT`, `E-POISE_BROKEN`, `E-PHASE_CHANGED`, `E-ULT_USED` | — |
| `GO-BTL-STAGE` | `/Stage` | `BattleStageLayout` | `ChapterDefinition` (biome) | `E-ORIENTATION_CHANGED` | — |
| `GO-BTL-PSLOT` | `/Stage/PartySlots/P0..P3` | — (Transform rỗng) | — | — | — |
| `GO-BTL-ESLOT` | `/Stage/EnemySlots/E0..E4` | — (Transform rỗng) | — | — | — |
| `GO-BTL-VFX` | `/Stage/VfxLayer` | `VfxPlayer` | Addressables `VfxKey` | mọi event có `VfxKey` | — |
| `GO-BTL-CANVAS` | `/BattleCanvas` | `Canvas`, `CanvasScaler` | — | — | — |
| `GO-BTL-HUD` | `/BattleCanvas/SafeArea` | `BattleHudScreen`, `SafeAreaFitter`, `LayoutProfileSwitcher` | — | `E-BATTLE_INITIALIZED` | — |
| `GO-BTL-HEROPANEL` | `.../SafeArea/HeroPanel` | `HeroPanelView` | `HeroInstance` | `E-TURN_STARTED`, `E-DAMAGE_DEALT`, `E-HEAL_APPLIED`, `E-SP_CHANGED`, `E-STATUS_APPLIED/EXPIRED` | — |
| `GO-BTL-ENEMYPANEL` | `.../SafeArea/EnemyPanel` | `EnemyPanelView` | `EnemyDefinition` | như trên + `E-POISE_DAMAGED/BROKEN`, `E-INTENT_CHANGED` | — |
| `GO-BTL-TURNBAR` | `.../SafeArea/TurnOrderBar` | `TurnOrderBar` + n× `TurnOrderCell` | `TurnScheduler.PreviewOrder(8)` | `E-TURN_ENDED`, `E-STATUS_APPLIED`(SPD), `E-UNIT_DIED` | — |
| `GO-BTL-SKILLGRID` | `.../SafeArea/SkillGrid` | `SkillGridView` + 15× `SkillSlotView` | `SkillDefinition`, `SkillRuntime` | `E-TURN_STARTED`, `E-SP_CHANGED`, `E-COOLDOWN_CHANGED`, `E-ULT_CHARGED` | `E-SKILL_SELECTED` |
| `GO-BTL-ITEMBAR` | `.../SafeArea/ItemBar` | `ItemSlotBar` | `InventoryDto` | `E-TURN_STARTED` | `E-ITEM_SELECTED` |
| `GO-BTL-STATS` | `.../SafeArea/StatsEqPanel` | `StatsEqPanel` | `HeroInstance`, `EquipmentInstance` | `E-TURN_STARTED`, `E-STATUS_APPLIED` | — |
| `GO-BTL-ENDTURN` | `.../SafeArea/EndTurnButton` | `EndTurnButton` | — | `E-TURN_STARTED` | `E-END_TURN_PRESSED` |
| `GO-BTL-AUTO` | `.../SafeArea/AutoSpeedToggle` | `AutoSpeedToggle` | `SettingsDto` | — | `E-AUTO_CHANGED`, `E-SPEED_CHANGED` |
| `GO-BTL-ZONE` | `.../SafeArea/ZoneIndicator` | `ZoneIndicator` | `RunState` | — | — |
| `GO-BTL-METER` | `.../SafeArea/DamageMeter` | `DamageMeterView` + n× `UI_DamageMeterRow` | — | `E-DAMAGE_DEALT` | — |
| `GO-BTL-TARGET` | `.../SafeArea/TargetHighlighter` | `TargetHighlighter` | `TargetSelector` | `E-SKILL_SELECTED` | `E-TARGET_SELECTED` |
| `GO-BTL-AC` | `/BattleCanvas/ActionCommandLayer` | `ActionCommandUI`, `TimingRing`, `ComboPrompt`, `ChargeMeter`, `GuardPrompt` | `SettingsDto.actionCommandOffsetMs` | `E-COMMAND_WINDOW_OPENED` | `E-COMMAND_GRADED` |
| `GO-BTL-FLOAT` | `/BattleCanvas/FloatingTextLayer` | `FloatingTextLayer`, `DamageNumberPool` | — | `E-DAMAGE_DEALT`, `E-HEAL_APPLIED`, `E-STATUS_RESISTED`, `E-STATUS_TICKED`, `E-COMMAND_GRADED` | — |
| `GO-BTL-OVERLAY` | `/BattleCanvas/OverlayHost` | `BattleMenuPopup`, `TutorialOverlay` | `QuestDefinition`(tutorial) | `E-BACK_PRESSED` | — |
| `GO-BTL-AUDIO` | `/AudioZone` | `BattleAudioDirector` | `ChapterDefinition.bgm` | `E-PHASE_CHANGED`, `E-BATTLE_ENDED` | — |

> **Quy tắc quan trọng:** trong scene Battle, **không MonoBehaviour nào được gọi trực tiếp vào `CombatSimulation`** ngoài `BattleController`. Mọi thứ khác chỉ **nghe event**. Vi phạm quy tắc này là nguyên nhân số 1 gây bug "UI không khớp logic".

---

## 4. BẢN ĐỒ PREFAB

### 4.1. Prefab Unit

| Mã | Prefab | Script gốc | Con | Được spawn bởi | Pool | Data |
|---|---|---|---|---|---|---|
| `PF-UNIT-HERO` | `Unit_HeroBase` | `UnitView`, `UnitAnimator` | `Sprite`, `HealthBar`, `PoiseBar`, `StatusIcons`, `SelectionRing`, `HitFlash` | `BattleSceneInstaller` | ✘ (4 cố định) | `HeroDefinition.BattlePrefab` |
| `PF-UNIT-ENEMY` | `Unit_EnemyBase` | `UnitView`, `UnitAnimator`, `UnitIntentIcon` | như trên + `IntentIcon` | `BattleSceneInstaller` | ✘ (5 cố định) | `EnemyDefinition` |
| `PF-UNIT-BOSS` | `Unit_BossBase` | `UnitView`, `UnitAnimator`, `BossPhaseView` | như trên + `PhaseAura`, `SignatureCountdown` | `BattleSceneInstaller` | ✘ | `BossPhaseDefinition` |
| `PF-UNIT-MINION` | `Unit_Minion` | `UnitView`, `UnitAnimator` | rút gọn | `SummonSystem` → `CombatPresenter` | ✔ 6 | `SkillDefinition.SummonId` |

### 4.2. Prefab UI Widget

| Mã | Prefab | Script | Dùng ở màn | Pool |
|---|---|---|---|---|
| `PF-UI-SKILLSLOT` | `UI_SkillSlot` | `SkillSlotView` | `UI-BATTLEHUD` (×15) | ✘ |
| `PF-UI-TURNCELL` | `UI_TurnOrderCell` | `TurnOrderCell` | `UI-BATTLEHUD` (×8) | ✔ |
| `PF-UI-DMGNUM` | `UI_DamageNumber` | `DamageNumber` | `UI-BATTLEHUD` | ✔ 30 |
| `PF-UI-STATUSICON` | `UI_StatusIcon` | `StatusIconView` | Battle + HeroDetail | ✔ 40 |
| `PF-UI-HEROCARD` | `UI_HeroCard` | `HeroCard` | `UI-HEROLIST`, `UI-PREBATTLE`, `UI-SUMMON` | ✔ |
| `PF-UI-EQUIPCARD` | `UI_EquipCard` | `EquipCard` | `UI-INVENTORY`, `UI-EQUIPMENT` | ✔ |
| `PF-UI-STATROW` | `UI_StatRow` | `StatRow`, `StatCompareRow` | `UI-HERODETAIL`, `UI-EQUIPMENT`, Battle StatsEq | ✔ |
| `PF-UI-NODEMAPNODE` | `UI_NodeMapNode` | `NodeMapNodeView` | `UI-NODEMAP` | ✔ |
| `PF-UI-METERROW` | `UI_DamageMeterRow` | `DamageMeterRow` | `UI-BATTLEHUD` | ✔ 8 |
| `PF-UI-REWARDROW` | `UI_RewardRow` | `RewardRow` | `UI-RESULT`, `UI-QUEST`, `UI-MAIL` | ✔ |
| `PF-UI-TOOLTIP` | `UI_Tooltip` | `TooltipController` | Toàn cục (overlay) | ✘ (1) |
| `PF-UI-CONFIRM` | `UI_ConfirmDialog` | `ConfirmDialog` | Toàn cục | ✘ (1) |
| `PF-UI-TOAST` | `UI_Toast` | `ToastView` | Toàn cục | ✔ 5 |
| `PF-UI-CURRENCYBAR` | `UI_CurrencyBar` | `CurrencyBar` | `SC-META` TopBar | ✘ |
| `PF-UI-REDDOT` | `UI_RedDot` | `RedDot` | Mọi nút có thông báo | ✔ |

### 4.3. Prefab màn hình (23) — nạp qua Addressables

| Mã | Prefab | Script | Service dùng | Push từ đâu |
|---|---|---|---|---|
| `UI-SPLASH` | `UI_Splash` | `SplashScreen` | — | `BootState` |
| `UI-TITLE` | `UI_Title` | `TitleScreen` | `IPlayerRepository` | `BootState` |
| `UI-HOME` | `UI_Home` | `HomeScreen` | `QuestService`, `EconomyService` | `MetaState` |
| `UI-HEROLIST` | `UI_HeroList` | `HeroListScreen` | `HeroService` | BottomNav |
| `UI-HERODETAIL` | `UI_HeroDetail` | `HeroDetailScreen` | `HeroService`, `AscendSystem`, `SkillUpgradeSystem` | `UI-HEROLIST` |
| `UI-EQUIPMENT` | `UI_Equipment` | `EquipmentScreen` | `EquipmentService`, `SetBonusResolver` | `UI-HERODETAIL` |
| `UI-ENHANCE` | `UI_Enhance` | `EnhanceScreen` | `EnhanceSystem`, `EconomyService` | `UI-EQUIPMENT` |
| `UI-INVENTORY` | `UI_Inventory` | `InventoryScreen` | `InventoryService`, `EquipmentService` | `UI-HOME` |
| `UI-FORMATION` | `UI_Formation` | `FormationScreen` | `TeamService`, `FormationService`, `SynergyResolver` | `UI-PREBATTLE` |
| `UI-SUMMON` | `UI_Summon` | `SummonScreen` | `GachaService`, `EconomyService` | BottomNav |
| `UI-SHOP` | `UI_Shop` | `ShopScreen` | `EconomyService`, `IStoreService` | BottomNav |
| `UI-DUNGEON` | `UI_Dungeon` | `DungeonScreen` | `DungeonService`, `TowerService`, `TrialBossService` | BottomNav |
| `UI-ARENA` | `UI_Arena` | `ArenaScreen` | *(v1.1)* | BottomNav |
| `UI-QUEST` | `UI_Quest` | `QuestScreen` | `QuestService`, `BattlePassService` | `UI-HOME` |
| `UI-ACHIEVEMENT` | `UI_Achievement` | `AchievementScreen` | `AchievementService` | `UI-QUEST` |
| `UI-COLLECTION` | `UI_Collection` | `CollectionScreen` | `CollectionService` | `UI-HOME` |
| `UI-MAIL` | `UI_Mail` | `MailScreen` | `IPlayerRepository` | `UI-HOME` |
| `UI-SETTINGS` | `UI_Settings` | `SettingsScreen` | `SettingsService`, `InputLatencyCalibrator` | `UI-HOME` |
| `UI-CHAPTER` | `UI_ChapterSelect` | `ChapterSelectScreen` | `ChapterService` | `UI-HOME` |
| `UI-NODEMAP` | `UI_NodeMap` | `NodeMapScreen` | `RunController`, `NodeMapGenerator` | `UI-CHAPTER` |
| `UI-PREBATTLE` | `UI_PreBattle` | `PreBattleScreen` | `TeamService`, `BattleLauncher` | `UI-NODEMAP` |
| `UI-BATTLEHUD` | *(trong scene Battle)* | `BattleHudScreen` | — | `BattleState` |
| `UI-RESULT` | `UI_Result` | `ResultScreen` | `BattleResultProcessor`, `RewardResolver` | Sau trận |
| `UI-DEFEAT` | `UI_Defeat` | `DefeatScreen` | `EconomyService` | Sau trận |

---

## 5. DANH MỤC EVENT — publisher & subscriber

### 5.1. `CombatEvent` (trong trận, 27 loại)

| Mã | Event | Phát bởi | Nghe bởi |
|---|---|---|---|
| `E-BATTLE_INITIALIZED` | `BattleInitialized` | `CombatSimulation` | `BattleHudScreen`, `CombatPresenter`, `BattleStageLayout` |
| `E-BATTLE_STARTED` | `BattleStarted` | `CombatSimulation` | `CameraDirector`, `BattleAudioDirector`, `TutorialOverlay` |
| `E-ROUND_STARTED` | `RoundStarted` | `TurnScheduler` | `BattleHudScreen`, `BossPhaseController` |
| `E-TURN_STARTED` | `TurnStarted` | `TurnScheduler` | `SkillGridView`, `HeroPanelView`, `EnemyPanelView`, `StatsEqPanel`, `EndTurnButton`, `UnitSelectionRing` |
| `E-SP_CHANGED` | `SpChanged` | `ActionResolver` | `HeroPanelView`, `SkillGridView` |
| `E-COOLDOWN_CHANGED` | `CooldownChanged` | `ActionResolver` | `SkillGridView` |
| `E-ACTION_REQUESTED` | `ActionRequested` | `CombatSimulation` | `BattleController`, `AutoBattlePolicy` |
| `E-ACTION_DECLARED` | `ActionDeclared` | `ActionResolver` | `CombatPresenter`, `UnitAnimator` |
| `E-COMMAND_WINDOW_OPENED` | `CommandWindowOpened` | `ActionResolver` | `ActionCommandUI` |
| `E-COMMAND_GRADED` | `CommandWindowClosed` | `ActionCommandEvaluator` | `FloatingTextLayer`, `VfxPlayer`, `AudioService` |
| `E-DAMAGE_DEALT` | `DamageDealt` | `DamageCalculator` | `UnitView`, `FloatingTextLayer`, `DamageMeterView`, `HitStop`, `ScreenShake`, `CameraDirector`, `HeroPanelView`, `EnemyPanelView` |
| `E-SHIELD_ABSORBED` | `ShieldAbsorbed` | `StatusProcessor` | `UnitView`, `FloatingTextLayer` |
| `E-SHIELD_BROKEN` | `ShieldBroken` | `StatusProcessor` | `VfxPlayer`, `AudioService` |
| `E-HEAL_APPLIED` | `HealApplied` | `HealCalculator` | `UnitView`, `FloatingTextLayer`, `HeroPanelView` |
| `E-STATUS_APPLIED` | `StatusApplied` | `StatusProcessor` | `UnitStatusIcons`, `HeroPanelView`, `TurnOrderBar`(nếu SPD) |
| `E-STATUS_RESISTED` | `StatusResisted` | `StatusProcessor` | `FloatingTextLayer` |
| `E-STATUS_TICKED` | `StatusTicked` | `StatusProcessor` | `FloatingTextLayer`, `UnitView` |
| `E-STATUS_EXPIRED` | `StatusExpired` | `StatusProcessor` | `UnitStatusIcons` |
| `E-POISE_DAMAGED` | `PoiseDamaged` | `PoiseSystem` | `UnitPoiseBar`, `EnemyPanelView` |
| `E-POISE_BROKEN` | `PoiseBroken` | `PoiseSystem` | `BreakEffect`, `HitStop`, `ScreenShake`, `AudioService`, `TurnOrderBar` |
| `E-UNIT_DIED` | `UnitDied` | `ActionResolver` | `UnitView`(dissolve), `TurnOrderBar`, `DamageMeterView`, `PassiveProcessor` |
| `E-UNIT_REVIVED` | `UnitRevived` | `ActionResolver` | `UnitView`, `TurnOrderBar` |
| `E-MINION_SUMMONED` | `MinionSummoned` | `SummonSystem` | `CombatPresenter`, `TurnOrderBar` |
| `E-PHASE_CHANGED` | `PhaseChanged` | `BossPhaseController` | `CameraDirector`, `BattleAudioDirector`, `BossPhaseView` |
| `E-ULT_CHARGED` | `UltimateCharged` | `UltimateGauge` | `SkillGridView` |
| `E-INTENT_CHANGED` | `IntentChanged` | `IntentPredictor` | `UnitIntentIcon`, `EnemyPanelView` |
| `E-TURN_ENDED` | `TurnEnded` | `TurnScheduler` | `TurnOrderBar`, `BattleHudScreen` |
| `E-BATTLE_ENDED` | `BattleEnded` | `CombatSimulation` | `BattleResultProcessor`, `ResultScreen`, `BattleAudioDirector` |

### 5.2. Event meta (qua `IEventBus`)

| Mã | Event | Phát bởi | Nghe bởi |
|---|---|---|---|
| `E-CURRENCY_CHANGED` | `CurrencyChanged` | `EconomyService` | `CurrencyBar`, `RedDotService`, `QuestService`, `IAnalyticsService` |
| `E-HERO_LEVELUP` | `HeroLevelUp` | `HeroLevelSystem` | `HeroDetailScreen`, `QuestService`, `AchievementService` |
| `E-HERO_ASCENDED` | `HeroAscended` | `AscendSystem` | `HeroDetailScreen`, `CollectionService`, `AchievementService` |
| `E-EQUIP_CHANGED` | `EquipmentChanged` | `EquipmentService` | `HeroDetailScreen`, `StatsEqPanel`, `PowerScoreCalculator` |
| `E-ITEM_OBTAINED` | `ItemObtained` | `InventoryService` | `RedDotService`, `CollectionService` |
| `E-SUMMON_RESULT` | `SummonResult` | `GachaService` | `SummonScreen`, `CollectionService`, `AchievementService` |
| `E-QUEST_PROGRESS` | `QuestProgress` | `QuestService` | `RedDotService`, `HomeScreen` |
| `E-BATTLE_RESULT` | `BattleResultProcessed` | `BattleResultProcessor` | `QuestService`, `ChapterService`, `IAnalyticsService`, `CollectionService` |
| `E-ENERGY_CHANGED` | `EnergyChanged` | `EnergySystem` | `CurrencyBar`, `NodeMapScreen` |
| `E-SETTINGS_CHANGED` | `SettingsChanged` | `SettingsService` | `AudioService`, `ActionCommandUI`, `ScreenShake`, `LocalizationService` |
| `E-ORIENTATION_CHANGED` | `OrientationChanged` | `ScreenOrientationService` | Mọi `LayoutProfileSwitcher` |
| `E-SCREEN_CHANGED` | `ScreenChanged` | `UIScreenStack` | `IAnalyticsService`, `IAssetService`(release scope) |
| `E-REDDOT_DIRTY` | `RedDotDirty` | `RedDotService` | `RedDot` widget |
| `E-SAVE_COMPLETED` | `SaveCompleted` | `IPlayerRepository` | `ToastService`(debug), `AppLifecycleHandler` |

> **Khi thêm event mới:** thêm 1 hàng ở đây **và** trong [plan.md §11.4](plan.md) → nếu không, subscriber sẽ bị bỏ sót.

---

## 6. MA TRẬN PHỤ THUỘC SCRIPT — "sửa X thì kiểm Y"

### 6.1. Combat (rủi ro cao nhất)

| Script | Ai gọi nó | Nếu sửa thì phải kiểm |
|---|---|---|
| `DamageCalculator` | `ActionResolver` | `T-DamageCalculatorTests` · `T-GoldenScenarioTests` · Balance harness · bảng damage ở [plan §4.6](plan.md) · `DamageMeterView` |
| `TurnScheduler` | `CombatSimulation` | `TurnOrderBar` (preview 8 lượt) · `T-TurnSchedulerTests` · `T-DeterminismTests` · `IntentPredictor` |
| `StatusProcessor` | `ActionResolver`, `TurnScheduler` | 22 `StatusDefinition` · `UnitStatusIcons` · `StatBlock` (cache) · `T-StatusProcessorTests` · `T-EdgeCaseTests` E05/E06/E08 |
| `PoiseSystem` | `ActionResolver` | `UnitPoiseBar` · `BreakEffect` · `EnemyPanelView` · công thức ×1.5 ở `DamageCalculator` · `T-PoiseSystemTests` |
| `TargetSelector` | `ActionResolver`, `AIController`, `TargetHighlighter` | 11 `TargetMode` · taunt override · `T-TargetSelectorTests` · auto-suggest UI |
| `StatBlock` | mọi system | **Cache invalidation** — mọi nơi đổi stat phải gọi `MarkDirty()` · `StatsEqPanel` · `HeroStatResolver` |
| `ActionResolver` | `CombatSimulation` | **Pipeline 14 bước** ([plan §4.3](plan.md)) · toàn bộ `CombatEvent` · `T-EdgeCaseTests` (24 case) |
| `ActionCommandEvaluator` | `ActionResolver` | `ActionCommandUI` (4 loại) · `InputLatencyCalibrator` · `SettingsDto.actionCommandOffsetMs` · `AutoBattlePolicy` |
| `AIController` | `CombatSimulation` | mọi `AIProfileDefinition` · `IntentPredictor` · `BossPhaseController` · `T-FuzzBattleTests` |
| `CombatEvent` (thêm loại) | — | §5.1 bảng này · [plan §11.4](plan.md) · `CombatPresenter` (switch) · `EventPlaybackScheduler` |
| `CombatSimulation` | `BattleController` | `ReplayVerifier` · `T-DeterminismTests` · `BattleSnapshot` (E17) |
| `IRandomSource` | tất cả | **Mọi thứ deterministic** — đổi impl = mọi golden test phải sinh lại |

### 6.2. Meta

| Script | Ai gọi | Nếu sửa thì phải kiểm |
|---|---|---|
| `EconomyService` | mọi hệ thống tiêu tiền | `CurrencyBar` · `TransactionLog` · `T-EconomyServiceTests` · analytics `currency_change` · save schema |
| `HeroStatResolver` | `BattleLauncher`, `HeroDetailScreen`, `PowerScoreCalculator` | Thứ tự áp modifier ([plan §4.5](plan.md)) · `StatsEqPanel` · `T-HeroStatResolverTests` |
| `EquipmentGenerator` | `LootRoller`, `GachaService` | Bảng sub stat ([plan §7.2](plan.md)) · `T-EquipmentGeneratorTests` · cân bằng loot |
| `EnhanceSystem` | `EnhanceScreen` | Bảng +0→+15 ([plan §7.3](plan.md)) · `T-EnhanceSystemTests` · sink vàng/đá |
| `GachaService` / `PitySystem` | `SummonScreen` | Tỉ lệ công khai trong game · `T-GachaPityTests` · `GachaStateDto` trong save · tuân thủ store |
| `NodeMapGenerator` | `RunController` | `NodeMapScreen` · `RunStateDto` · `T-NodeMapGeneratorTests` |
| `BattleLauncher` | `PreBattleScreen`, `DungeonService` | `BattleConfig` · `SC-BATTLE` installer · `DifficultyScaler` |
| `BattleResultProcessor` | sau `E-BATTLE_ENDED` | `RewardResolver` · `QuestService` · `ChapterService` · analytics · `ResultScreen` |
| `EnergySystem` | `NodeMapScreen`, `DungeonScreen` | Chống đổi giờ máy · `IGameClock` · `T-EnergySystemTests` |

### 6.3. UI & Services

| Script | Ai gọi | Nếu sửa thì phải kiểm |
|---|---|---|
| `UIScreenStack` | `UIManager` | 23 màn hình · nút Back Android · `IAssetService.ReleaseScope` · `T-UIStackTests` |
| `LayoutProfileSwitcher` | mọi màn | **Cả 23 prefab màn hình** · `T-ResponsiveLayoutTests` · 5 tỉ lệ test |
| `IPlayerRepository` | mọi service ghi dữ liệu | Save schema version · `SaveMigrationRunner` · `T-SaveMigrationTests` · **kế hoạch lên server** |
| `SettingsService` | `SettingsScreen` | `AudioService` · `ActionCommandUI` · `ScreenShake` · `LocalizationService` · `SettingsDto` |
| `IAssetService` | mọi nơi load asset | Nhóm Addressables ([plan §11.9](plan.md)) · rò rỉ bộ nhớ · thời gian vào trận |
| `PoolService` | `DamageNumberPool`, `VfxPlayer` | Ngân sách 0 GC · Profiler |

---

## 7. ✅ CHECKLIST KHI THÊM NỘI DUNG MỚI

> **Đây là phần chống sót logic quan trọng nhất.** Chạy đúng checklist tương ứng, tick từng dòng.

### 7.1. Thêm 1 **SKILL** mới (14 bước)

```
[ ]  1. Thêm dòng vào CSV `skills.csv` (id, cost, cd, power, hitCount, target, element,
        poiseDamage, commandType, applies, vfxKey, sfxKey, animTrigger)
[ ]  2. Chạy Tools/Import Game Data → sinh `Data/Skills/Skill_{Name}.asset`
[ ]  3. Vẽ icon `Art/UI/Icons/Skills/icon_skill_{id}.png` (32×32) → thêm vào Sprite Atlas Icons
[ ]  4. Thêm key localization: `skill.{id}.name`, `skill.{id}.desc` vào vi.csv + en.csv
[ ]  5. Nếu VFX mới → tạo `VFX_{tên}.prefab` + đăng ký `VfxKey` trong VfxPlayer registry
[ ]  6. Nếu SFX mới → thêm `sfx_battle_{tên}.wav` + đăng ký trong AudioCueRegistry
[ ]  7. Nếu animation mới → thêm clip vào `.aseprite` của hero + animTrigger trong UnitAnimator
[ ]  8. Nếu status mới → chạy checklist §7.3 TRƯỚC
[ ]  9. Nếu ActionCommandType mới → cập nhật ActionCommandUI + ActionCommandEvaluator (§7.6)
[ ] 10. Gán skill vào `HeroDefinition.Skills[]` đúng ô (0=Basic..4=Ultimate)
[ ] 11. Thêm vào AIProfile nếu là skill của enemy (điều kiện + weight)
[ ] 12. Chạy `Tools/Validate Data` — không được có tham chiếu chết
[ ] 13. Chạy Balance Harness cho stage liên quan → win-rate vẫn trong dải 55–90%
[ ] 14. Cập nhật object-map.md nếu có prefab/event mới
```

### 7.2. Thêm 1 **HERO** mới (18 bước)

```
[ ]  1. Chốt: class, element, rarity, vai trò trong meta (không trùng hero có sẵn)
[ ]  2. Thêm dòng `heroes.csv` (base stats, growth, poise, class, element, rarity)
[ ]  3. Tạo 5 skill theo checklist §7.1 (Basic, A, B, C, Ultimate)
[ ]  4. Tạo 1 Passive + 1 Awakening → `Data/Passives/`
[ ]  5. Nếu PassiveTrigger mới → cập nhật `PassiveProcessor` + enum + test
[ ]  6. Vẽ 7 animation `Art/Characters/Heroes/{id}/hero_{id}_{clip}.aseprite`
[ ]  7. Vẽ portrait 64×64 → `Art/UI/Portraits/portrait_{id}.png`
[ ]  8. Tạo prefab variant từ `PF-UNIT-HERO` → `Unit_Hero_{Name}.prefab`
[ ]  9. Gán `HeroDefinition.BattlePrefab` + `Portrait` (AssetReference)
[ ] 10. Thêm vào Addressables group `Heroes` với label `hero_{id}`
[ ] 11. Localization: `hero.{id}.name`, `hero.{id}.lore`
[ ] 12. Thêm vào `GachaPoolDefinition` đúng bậc rarity → **kiểm tổng tỉ lệ vẫn = 100%**
[ ] 13. Thêm mảnh hero vào `LootTableDefinition` (Trial/Shop) → đường lấy F2P
[ ] 14. Thêm vào `CollectionService` codex
[ ] 15. Kiểm `SynergyResolver` — class/element mới có phá cân bằng synergy không
[ ] 16. Chạy Balance Harness: hero mới không được có win-rate lệch > 15% so với trung bình
[ ] 17. Test trên cả Portrait & Landscape (portrait không tràn khung)
[ ] 18. Cập nhật object-map.md §4.1 + structure.md §2
```

### 7.3. Thêm 1 **STATUS EFFECT** mới (12 bước)

```
[ ]  1. Thêm giá trị vào enum `StatusId`
[ ]  2. Thêm dòng vào bảng plan.md §4.11 (nhóm, tick, stack, duration, dispel)
[ ]  3. Tạo `Data/Status/Status_{Name}.asset`
[ ]  4. Cài logic trong `StatusProcessor`: apply / tick / expire / stack / resist
[ ]  5. Nếu ảnh hưởng stat → thêm `StatModifier` và gọi `StatBlock.MarkDirty()`
[ ]  6. Nếu chặn hành động (control) → cập nhật `ActionResolver` bước 5
[ ]  7. Nếu tương tác với status khác (VD Freeze tan khi Fire) → ghi vào §4.14 Edge Case + test
[ ]  8. Vẽ icon `Art/UI/Icons/Status/icon_status_{id}.png` — **phải phân biệt được khi mù màu**
[ ]  9. Localization: `status.{id}.name`, `status.{id}.desc`
[ ] 10. Cập nhật `UnitStatusIcons` nếu vượt 6 icon (cần gộp/cuộn)
[ ] 11. Viết test trong `StatusProcessorTests` (apply/tick/expire/stack/resist = 5 test)
[ ] 12. Kiểm `AutoBattlePolicy` — Auto có biết xử lý status này không (cleanse?)
```

### 7.4. Thêm 1 **ENEMY / BOSS** mới (15 bước)

```
[ ]  1. Chốt archetype (12 mẫu ở plan §6.2)
[ ]  2. Thêm dòng `enemies.csv` (stats, archetype, poise, element)
[ ]  3. Tạo skill theo §7.1
[ ]  4. Tạo `AIProfileDefinition`: danh sách rule + weight + điều kiện
[ ]  5. Nếu điều kiện AI mới → thêm vào enum `AICondition` + `UtilityScorer` + test
[ ]  6. Vẽ 4 animation (boss: + phase 2)
[ ]  7. Tạo prefab variant từ `PF-UNIT-ENEMY` / `PF-UNIT-BOSS`
[ ]  8. Boss: tạo `BossPhaseDefinition` (ngưỡng HP, aiProfile, signatureMove, enrageRound)
[ ]  9. Boss: kiểm đủ 4 yếu tố thiết kế (Telegraph, ≥2 Counterplay, Escalation, Tell riêng)
[ ] 10. Thêm vào `StageDefinition` (đội hình địch) của chương tương ứng
[ ] 11. Gán `LootTableDefinition`
[ ] 12. Addressables group `Enemies_{chapter}`
[ ] 13. Localization: `enemy.{id}.name`, `enemy.{id}.desc`
[ ] 14. Chạy `FuzzBattleTests` — enemy mới không gây stalemate/exception
[ ] 15. Balance Harness stage đó → win-rate trong dải
```

### 7.5. Thêm 1 **MÀN HÌNH UI** mới (13 bước)

```
[ ]  1. Thêm giá trị vào enum `ScreenId`
[ ]  2. Tạo prefab `Prefabs/UI/Screens/UI_{Name}.prefab`
[ ]  3. Tạo `Scripts/UI/Screens/{Name}Screen.cs` kế thừa `UIScreen`
[ ]  4. Cài `OnShow(data)` / `OnHide()` / `OnBack()`
[ ]  5. Gắn `LayoutProfileSwitcher` + tạo **cả 2 preset** Portrait & Landscape
[ ]  6. Gắn `SafeAreaFitter` vào panel gốc
[ ]  7. Đăng ký trong `UIRegistry`: ScreenId → Addressable key
[ ]  8. Thêm vào Addressables group `Meta` (hoặc group phù hợp)
[ ]  9. Mọi text dùng `LocalizedText` — thêm key vào vi.csv + en.csv
[ ] 10. Mọi nút có SFX (`sfx_ui_*`) và hiệu ứng scale 0.92 (không màn nào "câm")
[ ] 11. Nếu list dài → dùng `VirtualScrollList`, không Instantiate hàng loạt
[ ] 12. Test 5 tỉ lệ (9:16, 3:4, 16:9, 20:9, 21:9) + nút Back Android
[ ] 13. Cập nhật object-map.md §4.3 + structure.md §3.6
```

### 7.6. Thêm 1 **COMBAT EVENT** mới (7 bước)

```
[ ]  1. Thêm struct vào `Combat/Events/CombatEvents.*.cs` + giá trị enum
[ ]  2. Xác định NƠI PHÁT (system nào) — thêm vào bảng object-map §5.1
[ ]  3. Xác định MỌI NƠI NGHE — thêm vào cùng bảng
[ ]  4. Cập nhật `CombatPresenter` switch-case (nếu cần diễn hoạt)
[ ]  5. Cập nhật `EventPlaybackScheduler` (event này có chặn chờ animation không?)
[ ]  6. Cập nhật plan.md §11.4
[ ]  7. Kiểm `T-DeterminismTests` — chuỗi event phải vẫn ổn định
```

### 7.7. Đổi **CÔNG THỨC / HẰNG SỐ CÂN BẰNG** (8 bước)

```
[ ]  1. Sửa trong `BalanceConstants.asset` — KHÔNG hard-code trong script
[ ]  2. Cập nhật bảng công thức tương ứng trong plan.md (§4.5/§4.6/§4.15/§7.3/§9)
[ ]  3. Chạy toàn bộ EditMode test — golden test sẽ đỏ, đây là mong đợi
[ ]  4. Sinh lại file golden (`Tests/Fixtures/golden/*.json`) và ĐỌC KỸ chênh lệch
[ ]  5. Chạy Balance Harness 1000 trận/stage → so CSV trước/sau
[ ]  6. Kiểm mọi stage vẫn trong dải win-rate 55–90% (boss 40–65%)
[ ]  7. Kiểm không hero nào rơi khỏi dải sử dụng 3–60%
[ ]  8. Ghi lý do thay đổi vào `Docs/balance/CHANGELOG.md`
```

### 7.8. Thêm 1 **TIỀN TỆ / VẬT PHẨM** mới (10 bước)

```
[ ]  1. Thêm vào enum `CurrencyType` (hoặc tạo `ItemDefinition`)
[ ]  2. Thêm field vào `WalletDto` → **TĂNG save version + viết `ISaveMigration`**
[ ]  3. Cài faucet (nguồn thu) — ghi rõ vào bảng plan.md §9.1
[ ]  4. Cài sink (nơi tiêu) — không được có tiền tệ chỉ vào mà không ra
[ ]  5. Mọi thay đổi phải qua `EconomyService.TryConsume/Grant` (không sửa wallet trực tiếp)
[ ]  6. Vẽ icon `Art/UI/Icons/Currency/icon_currency_{id}.png`
[ ]  7. Thêm vào `CurrencyBar` nếu hiện ở top bar
[ ]  8. Localization: `currency.{id}.name`
[ ]  9. Thêm vào analytics `currency_change` (reason enum)
[ ] 10. Test `EconomyServiceTests`: không bao giờ âm, log đủ, migration chạy đúng
```

### 7.9. Thêm 1 **SERVICE** mới (8 bước)

```
[ ]  1. Định nghĩa interface `I{Name}Service` trong `Services/`
[ ]  2. Cài impl v1 (và `Null{Name}Service` nếu là dịch vụ ngoài)
[ ]  3. Đăng ký trong `ServiceInstaller` (composition root) — đúng thứ tự phụ thuộc
[ ]  4. Thêm vào bảng plan.md §11.7 + structure.md §3.7
[ ]  5. Nếu là MonoBehaviour → thêm vào `GO-BOOT-SVC` hierarchy + object-map §3.1
[ ]  6. Nếu có state cần lưu → thêm DTO + save version + migration
[ ]  7. Cài `Dispose()` — huỷ đăng ký event, giải phóng asset
[ ]  8. Viết test với mock các service phụ thuộc
```

### 7.10. Đổi **SAVE SCHEMA** (7 bước — rủi ro mất dữ liệu người chơi)

```
[ ]  1. TĂNG `SaveData.Version`
[ ]  2. Viết `Migration_{old}_to_{new}.cs` — phải xử lý cả trường hợp field cũ thiếu
[ ]  3. Đăng ký migration trong `SaveMigrationRunner`
[ ]  4. Thêm test trong `SaveMigrationTests`: load save version cũ THẬT → kỳ vọng đúng
[ ]  5. Lưu 1 file save mẫu version cũ vào `Tests/Fixtures/saves/v{n}.json`
[ ]  6. Cập nhật schema trong plan.md §11.6
[ ]  7. Test thủ công: cài bản cũ → chơi → cập nhật bản mới → dữ liệu còn nguyên
```

### 7.11. Thêm 1 **CHƯƠNG / BIOME** mới (12 bước)

```
[ ]  1. Tạo `ChapterDefinition` (biome, node count, stageLevel range, enemy pool, boss)
[ ]  2. Tạo ~13 `StageDefinition` (đội hình địch, loot, giới hạn lượt)
[ ]  3. Tạo enemy mới theo §7.4
[ ]  4. Tạo boss theo §7.4
[ ]  5. Vẽ tileset + 3 lớp background
[ ]  6. Tạo BGM biome + BGM boss (nếu cần)
[ ]  7. Thêm Addressables group `Biome_{n}` + `Enemies_{n}`
[ ]  8. Thêm ≥6 EventNode riêng cho biome
[ ]  9. Localization: `chapter.{n}.name`, mô tả node event
[ ] 10. Kiểm `NodeMapGenerator` với chapter mới: 10.000 seed đều có đường Start→Boss
[ ] 11. Cân bằng: `DifficultyScaler` cho stageLevel mới
[ ] 12. Kiểm dung lượng build không vượt 150 MB
```

### 7.12. Sửa **PREFAB dùng chung** (6 bước)

```
[ ]  1. Tìm mọi nơi dùng prefab đó (object-map §4, cột "Dùng ở màn")
[ ]  2. Nếu là prefab variant gốc → kiểm mọi variant con không bị override phá vỡ
[ ]  3. Kiểm cả Portrait & Landscape preset
[ ]  4. Nếu prefab được pool → kiểm `IPoolable.OnReturn()` reset đủ trạng thái
[ ]  5. Chạy PlayMode test các màn liên quan
[ ]  6. Cập nhật object-map §4
```

---

## 8. BẢN ĐỒ TEST — logic nào được test ở đâu

| Vùng logic | File test | Mã |
|---|---|---|
| Công thức damage | `DamageCalculatorTests` | `T-CMB-DMG` |
| Thứ tự lượt / ATB | `TurnSchedulerTests` | `T-CMB-TURN` |
| 22 status | `StatusProcessorTests` | `T-CMB-STATUS` |
| Poise/Break | `PoiseSystemTests` | `T-CMB-POISE` |
| 11 TargetMode | `TargetSelectorTests` | `T-CMB-TARGET` |
| Bảng nguyên tố 6×6 | `ElementTableTests` | `T-CMB-ELEM` |
| Cửa sổ Action Command | `ActionCommandEvaluatorTests` | `T-CMB-AC` |
| Passive trigger | `PassiveProcessorTests` | `T-CMB-PASSIVE` |
| **24 edge case (plan §4.14)** | `EdgeCaseTests` | `T-CMB-EDGE` |
| Deterministic | `DeterminismTests` | `T-CMB-DET` |
| 10.000 trận fuzz | `FuzzBattleTests` | `T-CMB-FUZZ` |
| 20 kịch bản golden | `GoldenScenarioTests` | `T-CMB-GOLD` |
| Loot | `LootRollerTests` | `T-META-LOOT` |
| Gacha + pity | `GachaPityTests` | `T-META-GACHA` |
| Enhance | `EnhanceSystemTests` | `T-META-ENH` |
| Roll trang bị | `EquipmentGeneratorTests` | `T-META-EQGEN` |
| Kinh tế | `EconomyServiceTests` | `T-META-ECO` |
| Energy + chống đổi giờ | `EnergySystemTests` | `T-META-ENERGY` |
| Sinh node map | `NodeMapGeneratorTests` | `T-META-MAP` |
| Thứ tự modifier stat | `HeroStatResolverTests` | `T-META-STAT` |
| Migration save | `SaveMigrationTests` | `T-SVC-MIG` |
| Toàn vẹn save | `SaveIntegrityTests` | `T-SVC-SAVE` |
| **Luật assembly** | `AssemblyRuleTests` | `T-ARCH-ASM` |
| Luồng Boot | `BootFlowTests` | `T-PM-BOOT` |
| Luồng trận | `BattleFlowTests` | `T-PM-BATTLE` |
| UI stack | `UIStackTests` | `T-PM-UI` |
| Responsive 5 tỉ lệ | `ResponsiveLayoutTests` | `T-PM-LAYOUT` |

**Quy tắc:** sửa script ở §6 → chạy **ít nhất** các test ở cột tương ứng trước khi commit.

---

## 9. BẢN ĐỒ DATA ASSET → AI ĐỌC

| Data asset | Đọc bởi | Nếu sửa thì ảnh hưởng |
|---|---|---|
| `HeroDefinition` | `HeroService`, `HeroStatResolver`, `BattleLauncher`, `HeroCard`, `CollectionService` | Stat trận, UI hero, gacha pool, codex |
| `SkillDefinition` | `ActionResolver`, `SkillSlotView`, `AIController`, `TooltipController` | Damage, UI grid, AI, tooltip |
| `StatusDefinition` | `StatusProcessor`, `UnitStatusIcons`, `TooltipController` | Logic trận, icon, mô tả |
| `EnemyDefinition` | `BattleSceneInstaller`, `DifficultyScaler`, `CollectionService` | Trận, codex |
| `AIProfileDefinition` | `AIController`, `IntentPredictor` | Hành vi địch, intent preview |
| `EquipmentDefinition` | `EquipmentGenerator`, `EquipCard`, `SetBonusResolver` | Loot, UI, set bonus |
| `StageDefinition` | `BattleLauncher`, `RunController`, `RewardResolver` | Đội hình địch, thưởng |
| `ChapterDefinition` | `ChapterService`, `NodeMapGenerator`, `BattleStageLayout`, `BattleAudioDirector` | Map, biome, BGM |
| `LootTableDefinition` | `LootRoller`, `RewardResolver` | Kinh tế, drop |
| `GachaPoolDefinition` | `GachaService`, `SummonScreen` | Tỉ lệ công khai — **ràng buộc pháp lý** |
| `BalanceConstants` | **Toàn bộ `Game.Combat` + `Game.Meta`** | ⚠️ Sửa 1 giá trị = ảnh hưởng toàn game → chạy §7.7 |
| `GameDatabase` | `ServiceInstaller`, mọi service tra cứu | Thiếu 1 entry = `NullReferenceException` runtime |
| `LevelCurveDefinition` | `HeroLevelSystem`, `EconomyService` | Tốc độ tiến trình |
| `FormationDefinition` | `FormationSystem`, `FormationScreen` | Bonus đội hình |

---

## 10. LUỒNG DỮ LIỆU TỔNG THỂ

```
CSV (Google Sheet)
   │ Tools/Import Game Data
   ▼
ScriptableObject (Assets/_Project/Data/)
   │ Addressables
   ▼
GameDatabase ──────────────┐
                           │
save.json ──▶ IPlayerRepository ──▶ PlayerProfileDto
                           │              │
                           ▼              ▼
                    HeroService / EquipmentService / EconomyService  (Game.Meta)
                           │
                           ▼
                    BattleLauncher ──▶ BattleConfig
                                            │
                                            ▼
                                   CombatSimulation  (Game.Combat — C# thuần)
                                            │ Queue<CombatEvent>
                                            ▼
                                   CombatPresenter  (Game.CombatView)
                                            │
                                   ┌────────┴────────┐
                                   ▼                 ▼
                              UnitView          BattleHudScreen  (Game.UI)
                                   │
                                   ▼
                          VFX / SFX / Camera / FloatingText
                                            │
                                   E-BATTLE_ENDED
                                            ▼
                              BattleResultProcessor
                                            │
                           ┌────────────────┼────────────────┐
                           ▼                ▼                ▼
                    RewardResolver    QuestService    IAnalyticsService
                           │
                           ▼
                    EconomyService.Grant ──▶ IPlayerRepository.SaveAsync
```

---

## 11. QUY TẮC BẢO TRÌ FILE NÀY

0. **Mỗi `task-*.md` mới xong** (từ 2026-08-10 trở đi) — cập nhật §12 (đếm file thật) và, nếu tương
   ứng phase nào rõ ràng thay đổi trạng thái, cập nhật bảng [roadmap.md §0.1](roadmap.md). Đây là
   quy ước "gắn task với roadmap/object-map" — 2 file này là nguồn sự thật về tiến độ, task-*.md là
   nhật ký CHI TIẾT của từng lượt việc.
1. **Cùng commit** — thay đổi scene/prefab/script/SO thì cập nhật file này ngay, không để "sau này".
2. **Trước khi xoá script** — kiểm cột "Ai gọi nó" ở §6 phải rỗng.
3. **Trước khi merge** — chạy `Tools/Validate Object Map` (`Assets/Tools/ObjectMap/ObjectMapValidator.cs`,
   task-phase-5-gaps.md Phần A, xây 2026-08-12):
   - Quét mọi `MonoBehaviour` gắn tĩnh trong 3 scene và mọi prefab (đọc YAML thô, không mở scene)
   - So với cột "Script"/"Prefab" ở bảng §3, §4 (đọc thẳng file này, không hardcode)
   - **Log Console** (không chặn merge, giống `DataValidator`) 3 loại chênh lệch: script docs không
     tồn tại file · script thật chưa đăng ký · prefab docs không có asset
   - Giới hạn: script gắn qua `AddComponent<T>()` lúc runtime (đa số màn Meta) không bị quét thấy
4. **Mỗi cuối phase** — chạy `Tools/Object Map/Generate Report` để ghi `object-map-validation.md`, đọc diff.
5. File này nằm trong **Definition of Done** (plan.md §16, mục 8 & 9).
6. Khi bảng §3.3 (scene Battle) vượt 40 dòng → tách thành `object-map-battle.md`.

---

## 12. TRẠNG THÁI HIỆN TẠI (cập nhật mỗi sprint)

> Đếm file thật qua `find`, đối chiếu trực tiếp với `Assets/_Project/Scripts` — **không** suy diễn từ
> §3/§4/§6/§9 (các bảng đó vẫn là kiến trúc THIẾT KẾ, nhiều class trong đó chưa tồn tại — xem §12.1).

| Hạng mục | Kế hoạch | Đã tạo | Trạng thái |
|---|---|---|---|
| Scene | 4 | 3 (`Boot`,`Meta`,`Battle`; thiếu `Sandbox` — chưa từng cần) | 🟡 P0 |
| Assembly definition | 11 | 11 (8 production + `Game.Tools` + 2 test) | 🟢 P0 |
| Script `Game.Core` | 18 | 13 (tăng từ 12 — thêm `Scenes/ISceneTransitionService.cs`, task-splash-loading.md — mirror `IUiRootHost` để tránh circular assembly reference Bootstrap↔Meta/CombatView) | 🟡 P0 |
| Script `Game.Data` | 55 | 5 (DTO/enum/struct thuần — phần lớn "data" thật nằm ở `Game.Meta/Content/*SO.cs`, xem §12.1) | 🟡 P0 |
| Script `Game.Combat` | 42 | **17** (tăng từ 16 — thêm `Systems/ItemResolver.cs`, task-consumable-items.md; `CombatSimulation.OnEnemyWaveCleared` cho Tháp Vô Tận; `PassiveProcessor` thêm `SetBonus` slot + `RequiresCrit`/`RequiresPerfectGrade`/`TargetAllAllies`/`SpRefundPercent`/`HealPercentMaxHp`/`ShieldPercentMaxHp` cho Set Bonus — xem §12.1) | 🟡 P1/P3 |
| Script `Game.CombatView` | 28 | **10** (tăng từ 8 — thêm `Tutorial/TutorialController.cs`+`Tutorial/TutorialOverlay.cs`, task-phase-5-gaps.md Phần B) | 🟡 P1/P2 |
| Script `Game.Meta` | 45 | **46** (tăng từ 44 — thêm `HeroList/HeroListScreen.cs` [task-hero-list.md] + `ChapterProgressScreen.cs` [task-chapter-arena.md]; các task còn lại trong đợt này [`task-ui-vfx-polish.md`, `task-defeat-screen.md`, `task-splash-loading.md`] chỉ sửa file có sẵn, không thêm class mới) | 🟡 P4 |
| Script `Game.UI` | 60 | 2 | 🔴 P2–P6 — **hầu hết "màn hình" thực ra nằm trong `Game.Meta`** (ShopScreen/SummonScreen/QuestScreen/HeroDetailScreen/TeamSelectScreen/SettingsScreen/DungeonScreen/TrialBossScreen/TowerScreen/NodeChoiceScreen/MailScreen/CodexScreen/InventoryScreen/HeroListScreen/ChapterProgressScreen), không tách sang `Game.UI` như kiến trúc gốc dự tính |
| Script `Game.Services` | 32 | 9 (gồm `EconomyService`, `SettingsService`, save/profile, `Localization/LocalizationService.cs`, **`Audio/AudioService.cs`** — có từ commit đầu tiên nhưng CHƯA TỪNG được ghi vào bảng này trước đợt cập nhật 2026-08-18, xem §12.1) | 🟡 P0 |
| `Game.Tools` (Editor) | — | 7 (tăng từ 6 — thêm `Localization/LocalizationKeyGenerator.cs`, task-phase-5-gaps.md Phần D: CSV importer 4 file + `BalanceHarness` + `ObjectMapValidator` + `LocalizationKeyGenerator`) | 🟢 (không nằm trong kế hoạch gốc, tự phát sinh khi cần) |
| Prefab | ~70 | **16** (tăng từ 13 — thêm `UI_Inventory` [đã xây từ trước nhưng chưa từng đếm ở bảng này], `UI_HeroList` [task-hero-list.md], `UI_ChapterProgress` [task-chapter-arena.md]; danh sách đủ: `UI_TeamSelect`, `UI_HeroCard`, `UI_GearSlotRow`, `UI_Shop`, `UI_Summon`, `UI_Quest`, `UI_HeroDetail`, `UI_Dungeon`, `UI_TrialBoss`, `UI_Tower`, `UI_NodeChoice`, `UI_Mail`, `UI_Codex`, `UI_Inventory`, `UI_HeroList`, `UI_ChapterProgress`) | 🔴 |
| ScriptableObject (data) | ~600 | **181** (24 hero · 65 skill · 14 equip · 66 enemy [+`boss_trial_champion`] · 12 loot table [tăng từ 2 — 5 chương × Treasure/Boss + 2 wildcard cũ, task-loottable-chapters.md] — Set Bonus KHÔNG cần asset riêng, `SetId` roll ở tầng instance lúc sinh trang bị, xem §12.1) | 🟡 P5 |
| Test file | 28 | **42** (tăng từ 40 — thêm `Combat/CombatSimulationReviveTests.cs` [task-defeat-screen.md] — HeroList/Splash-Loading/Arena-ChapterSelect không thêm test file mới, UI-orchestration thuần) | 🟢 P0 — **637/637 test xanh** ✓ đếm lại thật qua `run_tests` 2026-08-18 (con số tuyệt đối giữa các mốc không cộng dồn khớp 1-1 được nữa vì repo đã gộp lại thành 1 `Initial commit` ở đâu đó giữa chừng — che mất lịch sử test-theo-task trước điểm đó; **dùng số tuyệt đối đo trực tiếp mỗi lần, đừng cộng dồn theo delta cũ**) |

### 12.1. Ghi chú thực tế khác với thiết kế gốc (đọc TRƯỚC khi tra §3/§4/§6/§9)

Các bảng §3–§9 của file này mô tả **kiến trúc ĐÍCH** (service-oriented, `IEventBus`, `UIScreenStack`,
23 màn hình tách biệt...). Code thật hiện đi theo lối **đơn giản hoá có chủ đích** ở nhiều chỗ —
liệt kê ở đây để không ai tìm nhầm class không tồn tại:

- **24/24 hero có data thật + đủ Awakening/Innate Passive + art sprite thật** (task-hero-roster.md)
  — dùng đúng pipeline CSV→SO sẵn có (`Tools/Import Game Data`) cho data, skill `pixel-art-pipeline`
  (đã cài sẵn trong dự án, `Tools/pixel-art-pipeline/`) cho art — KHÔNG author tay phần nào. 18
  hero mới dùng NGUYÊN stat + 4 skill đầu của hero mẫu cùng class (plan.md §5.8 tự gợi ý cách
  này), chỉ khác Element + 1 skill Ultimate riêng + Awakening/Innate riêng + art riêng. Vào
  `GachaSystem.HeroPool` đúng tỉ lệ tự động. **Phát hiện quan trọng:**
  `AscendSystem.CanAscend` không enforce star cap theo rarity (plan.md §5.4) — MỌI hero kể cả
  Common đều lên được ★6, nên cả 24 hero đều thật sự cần Awakening. Game chỉ dùng **1 sprite tĩnh
  32×32/hero** (`{id}_v1_00.png`), KHÔNG phải 7-clip animation set như plan.md §2.2 mô tả — kể cả
  6 hero gốc cũng vậy, không phải khoảng thiếu riêng của 18 hero mới. **Vẫn thiếu:** animation đa
  clip (idle/walk/attack/...) cho cả 24 hero. 65 enemy đã đủ/vượt mục tiêu 60 (nhưng chưa audit
  nội dung từng con).
- **Animation PILOT NAY ĐÃ XÂY** (task-animation-pilot.md — hạng mục lớn thứ 3/3 người dùng chọn
  làm "thực hiện cả 3 mục trên", sau Addressables + Localization) — dòng "vẫn thiếu animation" ở
  trên vẫn đúng cho 23/24 hero + toàn bộ enemy, nhưng `hero_ember_knight` (dùng làm ví dụ xuyên
  suốt session) nay có thật 4 frame idle + 4 frame attack, chạy runtime qua hệ MỚI trong
  `UnitView.cs` (KHÔNG dùng Mecanim `Animator`, không có `.controller`/`AnimationClip` nào trong
  dự án). **Nguồn frame: Pillow thủ tục, KHÔNG phải ComfyUI** — người dùng tự chọn hướng này sau khi
  được báo rủi ro thật (AI không đảm bảo nhất quán nhân vật giữa nhiều lần sinh riêng lẻ); script
  mới `Tools/pixel-art-pipeline/scripts/character_draw.py` vẽ khối màu phẳng + viền đen tham số hoá
  theo pose, đứng ngoài luồng `compose.py` gốc (không sửa). ComfyUI chỉ dùng cho 1 background chiến
  trường (`comfy_gen.py`) — 2/3 variant AI vẽ lặp nhân vật dù có negative prompt (loại, không cố
  cứu), hậu xử lý palette-lock (`post_process.py`) làm HỎNG ảnh còn lại (mảng trắng lỗi keying) nên
  dùng thẳng ảnh gốc ComfyUI, đúng nguyên tắc skill "đừng cố cứu bằng hậu xử lý khi nó làm hỏng
  thêm". `unity_import.py` mà skill nhắc tới **KHÔNG tồn tại thật** trong `Tools/pixel-art-pipeline/
  scripts/` của dự án này — import Unity làm thủ công (viết `.meta` tay theo đúng mẫu import 1
  sprite hero cũ, verify qua `execute_code` đọc `TextureImporter` thật, không đoán). `UnitView.
  LoadFrames(defId, state)` tự dò `Animations/{defId}_{state}_00..` (Heroes rồi Enemies), **fallback
  nguyên vẹn về sprite tĩnh cũ nếu không tìm thấy** — 23 hero còn lại + mọi enemy/boss KHÔNG đổi
  hành vi. Idle 8fps loop / Attack 14fps chạy 1 lần rồi tự về Idle (khớp bảng plan.md §2.2), trigger
  qua `PlayAttackLunge` có sẵn (không đụng `PlayHit`/`PlayMiss`/`PlayDeath`). **Verify:** MCP
  Play-mode frame-stall tái diễn (`Time.frameCount` đứng yên dù `Application.isPlaying=true`) — xử
  lý bằng cách nâng cấp kỹ thuật check-before-force: thay vì cố chờ Player Loop, spawn `UnitView`
  thật trong scene rồi gọi `Bind()`/`PlayAttackLunge()`/`Update()` (private, qua reflection) trực
  tiếp, đọc `Time.deltaTime` THẬT tại thời điểm gọi (≈0.02s, vẫn có giá trị dù `frameCount` không
  tăng) — xác nhận đúng toán học frame-cycle qua nhiều lần gọi liên tiếp (idle lặp đúng chu kỳ,
  attack chạy hết 4 frame rồi tự chuyển `_animState` về `Idle`). Không thêm test file mới (ngoài
  phạm vi pilot — xem task file §1 "ngoài phạm vi"). 423/423 test EditMode vẫn xanh (không đụng
  logic `Game.Combat`). **Follow-up ngay sau (cùng phiên "tiếp tục cho tôi")**: phát hiện background
  sinh ở trên là asset MỒ CÔI — `BattleSceneInstaller`/`Battle.unity` chưa từng có bất kỳ background
  rendering nào (grep xác nhận 0 field/logic liên quan). Wiring thật: GameObject `Background` tĩnh
  trên Hierarchy (con `__Stage__`, `SpriteRenderer` + `sortingOrder=-100`, dưới mọi unit/VFX), lưu
  scene qua `manage_scene save`. Ảnh 512×288 ở PPU 32 = đúng 16×9 world unit, khớp CHÍNH XÁC vùng
  nhìn `BattleCamera` (orthoSize 4.5, aspect 16:9, xác nhận qua `SpriteRenderer.bounds` thật) —
  không cần chỉnh scale tay. Play-mode verify LẦN 2 không gặp frame-stall (khác lần đầu) — chụp
  `manage_camera screenshot` thật, thấy `hero_ember_knight` (đúng art mới) + background trong 1 trận
  thật, bằng chứng hình ảnh mạnh nhất cho riêng pilot Animation.
- **Follow-up lớn: yêu cầu vẽ lại TOÀN BỘ hero/enemy/boss + animation đầy đủ + VFX skill** (đang
  làm, nhiều giai đoạn — task-animation-pilot.md §4). Audit CSV thật trước khi lập kế hoạch: 24 hero
  = ĐÚNG 4/class × 6 class (`HeroClass`), mỗi class 4 hero khác nhau chủ yếu ở `element` — đòn bẩy
  tái dùng mạnh (6 rig thân × recolor theo element, không phải 24 thiết kế riêng, khớp cách
  task-hero-roster.md đã tái dùng stat/skill template). 65 skill chỉ 7 `element` × 4 `type`, 9 VFX
  asset có sẵn (`Art/VFX/vfx_*`) đã phủ gần hết lưới đó — gap thật chỉ ~2-3 archetype (Light/Wind/
  neutral bolt), không phải 65 việc riêng. 66 enemy đa dạng hơn hero nhiều (field `Archetype` 11/13
  giá trị không chia đều) —

> **KẾT LUẬN 2026-08-18 (điều tra xong, không còn "chưa kết luận"):** đo thật phân bố `Archetype`
> trên cả 66 file `.asset` (`ArchetypeId`, plan.md 13 giá trị) — Skirmisher 15 · Tank 9 · Brute 9 ·
> Grunt 8 · Boss 6 · Caster 6 · Debuffer 4 · Bomber 3 · Archer 3 · Healer 2 · Swarm 1 · **Buffer 0 ·
> Elite 0** (2/13 giá trị hoàn toàn không dùng). Nhưng grep xác nhận `EnemyDefinitionSO.Archetype`
> chỉ được ĐỌC ở ĐÚNG 1 chỗ logic thật trong toàn bộ codebase — loại Boss khỏi pool địch thường
> (`MetaSceneInstaller.PickEnemies`/`BattleSceneInstaller`) — ngoài ra chỉ hiện text trong Codex
> (`CodexScreen.cs:190`). **`Archetype` KHÔNG điều khiển hành vi AI dù tên gọi (Healer/Tank/
> Debuffer...) gợi ý điều đó** — hành vi chiến đấu thật đến từ field RIÊNG `AiProfileId`
> (`BattleSceneInstaller.BuildAi`), và ở đó phát hiện gap thật sự đáng kể hơn nhiều: chỉ 3 giá trị
> `AiProfileId` tồn tại trong toàn bộ 66 enemy — `ai_boss` (6, đúng 6 boss, luân phiên 3 skill có
> nhịp điệu riêng) · `ai_basic` (chỉ 4) · **`ai_special` (56/66 = 85%)**, và `ai_special` là ĐÚNG 1
> bộ luật y hệt cho mọi enemy dùng nó — "55% dùng skill ô 1, 40% đánh thường, luôn luôn" — không
> phân biệt enemy đó là Healer (nên ưu tiên hồi máu khi đồng minh yếu), Debuffer (nên ưu tiên gây
> debuff sớm), hay Tank (nên ưu tiên giữ aggro) dù skill kit của từng con THẬT SỰ khác nhau (skill
> ID khác nhau, stat khác nhau). **Tóm lại: đa dạng NỘI DUNG (stat/skill/element) là thật, nhưng đa
> dạng HÀNH VI AI gần như không tồn tại** — 85% enemy "trông" khác nhau (tên, art, skill) nhưng
> "chơi" giống hệt nhau. Đây là gap thiết kế/nội dung thật (không phải bug), quy mô sửa lớn hơn hẳn
> phạm vi 1 audit (cần thiết kế lại `AIProfile` theo từng archetype — quyết định "Healer ưu tiên gì
> khi nào", "Debuffer nhắm ai trước"... không thể tự suy ra, cần người dùng quyết định hướng) — để
> dành làm task riêng nếu cần, không tự ý mở rộng ở đây. **Giai đoạn 1 (hero pilot đủ 5 trạng
  thái) ĐÃ XONG và ĐÃ ADOPT:** kỹ thuật "skeletal rig" mới (`Tools/pixel-art-pipeline/scripts/
  character_rig.py`, bộ phận đầu/thân/tay/chân/vũ khí tách rời + XOAY thật qua `PIL Image.rotate()`
  quanh pivot — khác hẳn khối chữ nhật tĩnh dịch theo tham số của `character_draw.py`) — port từ
  `Tools/pixel-art-pipeline/charator/pixel_character_generator_v2.py` (đã có sẵn trong repo, trỏ tới
  1 project ngoài dự án `Art_python`; điều tra thấy bản walk-cycle đẹp ban đầu tham khảo KHÔNG đến
  từ code còn tồn tại trên đĩa — bản trung gian đã mất, không port được, chỉ port đúng kỹ thuật Mage
  rig THẬT SỰ có). Lần review đầu lộ lỗi thật (mỗi bộ phận tự vẽ viền riêng → seam đen xấu) — sửa
  bằng viền TOÀN silhouette 1 lần sau khi ghép (dilate, giống `character_draw.add_outline`). Tinh
  chỉnh tỷ lệ (torso/leg tăng ngân sách pixel) cho gần bản gốc hơn, dựng ĐỦ 5 trạng thái
  idle/attack/move/damage/die cho `hero_ember_knight` (move có chân XOAY thật lần đầu — tham số mới
  `leg_angle_deg`). Theo quyết định người dùng (AskUserQuestion), **ĐÃ THAY HẲN 8 frame
  `character_draw.py` cũ bằng 21 frame rig mới trong Unity** — `UnitView.cs` mở rộng `AnimState` từ
  `{StaticSprite,Idle,Attack}` lên đủ `{...,Move,Damage,Die}`, tổng quát hoá qua `FramesFor()`/
  `FpsFor()`/`Loops()` (thay hard-code if/else riêng Attack); `PlayHit()` nay trigger `Damage`
  (16fps, hết tự về Idle), `PlayDeath()` trigger `Die` (10fps, GIỮ frame cuối, chạy SONG SONG với
  fade-alpha cũ, không thay thế); `Move` nạp sẵn nhưng CHƯA có điểm trigger gameplay thật (trận lượt
  không có "đi bộ" tự nhiên). 23 hero khác + mọi enemy/boss vẫn fallback sprite tĩnh nguyên vẹn.
  **VFX song song: dựng thật URP Bloom Volume** — phát hiện hạ tầng URP đã đủ từ trước (2D Renderer,
  HDR, PostProcessData) nhưng CHƯA BẬT (`DefaultVolumeProfile.asset` có sẵn 1 override Bloom
  `intensity:0`, `BattleCamera.renderPostProcessing:0` — đúng mẫu "infra có sẵn, chưa ai bật" lặp
  lại nhiều lần trong dự án); bật cả 2 + shader mới `Assets/_Project/Shaders/HDREmissiveSprite.shader`
  (property `[HDR] _Color` — sprite LDR thường không bao giờ vượt ngưỡng Bloom dù đặt trắng tuyệt
  đối). Verify bằng pixel thật (render `BattleCamera` ra `RenderTexture` riêng qua code, nền ép
  `SolidColor` đen tuyệt đối để loại nhiễu art nền) — quầng sáng ấm lan toả rõ, mềm, đối xứng quanh
  sprite test, bằng chứng pixel không suy đoán. Hạ tầng sẵn sàng, CHƯA gắn vào skill thật nào (Giai
  đoạn 2). 423/423 test xanh xuyên suốt (chỉ đụng scene/shader/material/CombatView, không đụng
  `Game.Combat`). **Giai đoạn 2 NAY ĐÃ XONG** (VFX skill thật đầu tiên, cùng phiên): Ultimate
  `hero_ember_knight` = `skill_inferno_bulwark` (slot 4, xác nhận qua comment
  `BattleSceneInstaller.cs:477`, KHÔNG có field `IsUltimate` thật trong `SkillData` — hoàn toàn theo
  vị trí). VFX trước đây CHỈ chọn theo `Element` (`VfxPlayer.KeyForElement`,
  `CombatPresenter.PresentDamage`), không skill nào có VFX riêng dù `CombatEvent.StringValue` đã có
  sẵn comment "skillId, vfxKey..." làm placeholder CHẾT chưa ai đọc/ghi — đúng mẫu "hạ tầng có sẵn,
  chưa dùng" lặp lại nhiều lần cả dự án. Nối thật: `ActionResolver.cs` truyền
  `stringValue: data.Id`/`skill.Data.Id` ở cả 4 điểm `Emit(CombatEventType.DamageDealt...)`
  (hit/miss/Counter/Reflect — 0 rủi ro, grep xác nhận không test nào assert `StringValue`);
  `VfxPlayer.cs` thêm key `inferno_bulwark` (asset mới `Art/VFX/vfx_inferno_bulwark`, 4 frame
  64×64, sinh bằng `character_rig.py --vfx inferno_bulwark` — lõi trắng-nóng + tia ember Bresenham,
  cùng phong cách soft-glow với 9 VFX cũ) + dict `MATERIAL_OVERRIDES` gán riêng
  `Mat_HDREmissiveSprite` (Bloom thật, mục trên) cho key này qua `ResolveMaterial()` — 9 key VFX cũ
  giữ nguyên `_defaultMaterial`, không đổi hành vi; `CombatPresenter.PresentDamage` route
  `e.StringValue=="skill_inferno_bulwark" && e.Status==None` (loại Counter/Reflect) sang key mới
  thay vì key theo Element. **Verify mạnh nhất cho riêng phần Combat của cả pilot Animation**: dựng
  1 `CombatSimulation` THẬT qua `execute_code` (không mock), cho hero lặp lại
  `skill_inferno_bulwark` nhiều lượt — MỌI `DamageDealt` event từ đòn đó đều mang đúng
  `StringValue=skill_inferno_bulwark Status=None`, khớp chính xác điều kiện Presenter cần; các
  đường dẫn `Resources.Load` (4 frame + material HDR) xác nhận trả về non-null thật. 423/423 test
  xanh (không đụng logic có test cũ, chỉ thêm 1 tham số optional vào API sẵn có). **Còn lại, CHƯA
  làm:** Giai đoạn 4 (điều tra 66 enemy/boss), Giai đoạn 5 (phủ nốt VFX archetype thiếu + wiring 64
  skill còn lại). **Giai đoạn 3 bắt đầu (1/6 class xong)**: refactor `character_rig.py` — tách
  `CharacterKit` (đầu/thân/vũ khí/khiên riêng theo class, `get_leg`/`get_arm` DÙNG CHUNG mọi class
  chỉ đổi màu qua tham số), `build_frame(kit=VANGUARD_KIT)` mặc định giữ nguyên 100% hành vi
  `hero_ember_knight` cũ (verify: regenerate idle, ảnh y hệt trước refactor). Dựng xong Arcanist =
  `hero_frost_sage` (class template gốc Water, xác nhận thứ tự 6 hero đầu `heroes.csv`) — silhouette
  khác hẳn Vanguard: mũ trùm nhọn, áo choàng loe, gậy phép dài, KHÔNG khiên; bảng màu băng giá riêng
  (`FROST_ROBE/HOOD/ICE` từ `palette.json`). Đủ 5 trạng thái, import Unity 21 file — **0 thay đổi
  code C#** (`UnitView.LoadFrames(defId,state)` đã tổng quát từ Giai đoạn 1, tự nạp đúng theo
  `defId` mới không cần sửa gì). Verify `Bind()` thật nạp đúng 5/5 bộ qua `execute_code`. 423/423
  test xanh. **Cùng phiên, tiếp tục hết 4 class còn lại → ĐỦ 6/6 class kit**: Trickster
  (`hero_gale_thief`, Wind — khăn che mặt, dao ngắn, KHÔNG khiên), Warden (`hero_dawn_cleric`,
  Light — khiên TRÒN khác khiên chữ nhật Vanguard, chuỳ đầu cầu sáng), Slayer (`hero_shadow_fang`,
  Dark — mắt tím phát sáng, đại đao dài, KHÔNG khiên), Summoner (`hero_bone_caller`, Dark — dấu
  xương trán, totem đầu lâu, KHÔNG khiên) — mỗi class bảng màu + silhouette riêng biệt (không chỉ
  đổi màu), đủ 5 trạng thái, import qua script mới `import_hero_frames.py` (tách khỏi copy+meta lặp
  tay 3 lần). 423/423 test xanh sau MỖI lần import (6 lần liên tiếp), không regress lần nào. **Giai
  đoạn 3 HOÀN TẤT:** 18 palette element thêm vào `character_rig.py` (`recolor_kit()`, tái dùng
  silhouette hàm class), batch sinh 18×5 state, import batch, verify 90/90 OK — **24/24 hero đủ
  5 animation state**. **Giai đoạn 4 HOÀN TẤT:** `enemy_rig.py` (12 humanoid kits + 10 creature
  draw fn: wolf/bat/slime/wisp/golem/serpent/spider/horror/toad/swarm + boss_drake), `gen_all_enemies.sh`,
  `import_enemy_frames.py` — 66/66 enemy/boss × 21 frames = 1386 PNG vào
  `Enemies/{defId}/Animations/`. **Giai đoạn 5 HOÀN TẤT:** 3 VFX mới (wind_gust/light_radiant/
  magic_bolt, 64×64 × 4 frame), VfxPlayer thêm "wind"/"light"/"magic" key, fix KeyForElement
  (Wind→"wind", Light→"light"), CombatPresenter.ResolveVfxKey() với neutral-magic whitelist.
  423/423 tests. **Animation initiative XONG: 24 hero + 66 enemy × 21 frame + 14 VFX key.**
- **Hàng Tactic (plan.md §5.6) ĐÃ XÂY** (task-tactic-row.md) — `StatusId.Focus=38` + entry `StatusTable`
  (Buff, dur default=2, Tick=None, Dispel=Dispel); `ActionIntent.IsSwapRow`/`IsFocus` (2 field mới,
  constructor tương thích hoàn toàn); `CombatUnit.HasSwappedRowThisTurn` (reset bởi BeginTurn);
  `CombatSimulation.ExecuteIntent` xử lý SwapRow (swap `Row`, set flag, KHÔNG gọi FinishTurn — actor
  vẫn ở AwaitInput và có thể chọn skill/item sau đó) + Focus (Apply duration=2 → sau FinishTurn còn
  1 → active lượt kế, FinishTurn(0)); `DamageCalculator.Calculate` force `crit=true` khi
  `attacker.HasStatus(StatusId.Focus)` trước chance roll; `BattleHudScreen` GRID_ROWS 2→3 +
  `BuildTacticRow()` (4 button Guard/ESC/SWAP/FOCUS bằng code thuần TextMeshPro) +
  `RefreshTacticRow()` disable SWAP khi đã dùng/ESC khi boss + 4 event mới (`OnGuardPressed/
  OnEscapePressed/OnSwapRowPressed/OnFocusPressed`); `BattleSceneInstaller.WireHud()` thêm 4 handler
  (`HandleGuard/HandleEscape/HandleSwapRow/HandleFocus`). Scope out lúc đó: Analyze (cần UI reveal
  riêng) — đã hoàn tất ở mục kế tiếp. 13 test mới (`TacticSystemTests`), **473/473 xanh**.
- **Analyze tactic ĐÃ XÂY** (task-analyze-tactic.md, plan.md §5.6 — nốt cuối hàng TACTIC) —
  `BattleState.AnalyzedEnemyIds` (`HashSet<int>`, cùng pattern doc-comment với `DamageByUnit`, HUD
  đọc thẳng State mỗi frame, KHÔNG cần `CombatEventType` mới); `ActionIntent.IsAnalyze` (field mới,
  constructor tương thích hoàn toàn); `CombatSimulation.ExecuteIntent` xử lý Analyze: nếu
  `Sp >= 5` thì trừ 5 SP + `_targeting.AutoSuggest(actor, TeamSide.Enemy)` chọn địch tự động (không
  đủ SP thì bỏ qua, không mất lượt vô lý) rồi `Add` vào set, `FinishTurn(0)`; `BattleHudScreen` thêm
  nút ANALYZE (cột 5 hàng TACTIC, màu cam) + `BuildAnalyzePanel()` panel góc phải-dưới (ẩn khi chưa
  analyze địch nào) hiện tên/element/HP/SP/ATK/DEF/SPD + 4 hệ số nguyên tố qua
  `ElementTable.Multiplier(Fire/Water/Earth/Wind, defender.Element)`; `RefreshTacticRow` disable nút
  khi actor SP &lt; 5; `BattleSceneInstaller.HandleAnalyze` wire `OnAnalyzePressed`. Scope out: AI
  intent reveal (side-effect RNG state, để v2). 3 test mới (`AnalyzeTacticTests`), **518/518 xanh**.
- **Formation Preset + Team Synergy ĐÃ XÂY** (task-formation-synergy.md, plan.md §5.7) — `FormationSystem`
  (`Game.Meta/Battle/FormationSystem.cs`, pure static) định nghĩa 8 preset: `GetRow(formationId, slotIndex)`
  thay thế hard-code `index<2` cũ trong `BattleSceneInstaller`, `GetModifiers(formationId, row)` inject
  `StatModifier` vào `EquipmentModifiers` trước trận. `SynergySystem` (`Game.Meta/Battle/SynergySystem.cs`,
  pure static) tính 5 điều kiện class/element và `Apply(playerUnits)` bơm modifier cho cả đội. Cầu nối
  Meta→Battle qua `RunContext.PendingBattle.Formation` (string, default "formation_balanced"). UI:
  `TeamSelectScreen.SelectedFormation` property + cycle button trong footer. Tất cả 3 launch path
  (`LaunchBattle`/`LaunchDungeonBattle`/`LaunchTrialBossBattle`) đều pass `SelectedFormation`.
  `LocalPlayerRepository.CreateNew` unlock tất cả 8 sẵn (hardcoded strings — luật kiến trúc: Game.Services
  không ref Game.Meta). 37 test mới, **460/460 xanh**.
- **CẢ 5/5 CHƯƠNG ĐÃ CHƠI ĐƯỢC THẬT** (task-chapters.md) — sửa lại 1 nhận định SAI ghi ở đây/
  roadmap.md §0.1 lúc trước ("chỉ 1 chương chơi thật"), dựa trên suy đoán không đủ chứng cứ. Thật
  ra: 65 enemy phân bố đủ 5 chương (11/13/13/14/14), cả 5 boss tồn tại + độ khó tăng dần đúng
  thiết kế, `NodeMapGenerator.Generate`/`PickBoss`/`PickEnemies` đã chapter-aware từ trước. Bằng
  chứng mạnh nhất: save profile của chính dự án đã đạt `ChapterUnlocked=6` (vượt cả 5 chương
  v1.0) từ các phiên trước. Gap thật duy nhất tìm thấy: node `Mystery` rơi vào nhánh "not
  available" (đã vá — `MetaSceneInstaller.ResolveMystery`). **Bài học:** khi đánh giá "X chưa xong"
  cho §12.1, phải đếm/verify trực tiếp (field data, execute_code), không suy đoán từ số liệu gián
  tiếp (VD "tổng số enemy" không nói lên "phân bố theo chương").
- **Không có `GachaService`/`PitySystem`** (§6.2) — tên thật là `GachaSystem`
  (`Meta/Gacha/GachaSystem.cs`), static class, không qua DI.
- **`SetBonusResolver` NAY ĐÃ XÂY** (sửa ghi chú cũ ở đây từng nói "hoàn toàn chưa xây" — nay đã
  stale, xem task-setbonus.md) — `Meta/Equipment/SetBonusCatalog.cs`/`SetBonusResolver.cs`, đủ
  8/8 bộ cả 2-món lẫn 4-món, `SetId` roll ở tầng instance khi sinh trang bị (không cần asset
  riêng).
- **Vật phẩm tiêu hao (plan.md §7.5) NAY ĐÃ XÂY THẬT ĐỦ 6/6 LOẠI** (task-consumable-items.md —
  hạng mục DUY NHẤT trong session này chạm `Game.Combat`) — `Game.Combat.Systems.ItemResolver`
  mới xử lý Potion/Ether/Antidote/Smoke Bomb/Revive Feather/Elemental Bomb;
  `ActionIntent.IsUseItem` (tái dùng field `SkillSlot` có sẵn làm item-index, không thêm field
  mới) + `BattleState.ItemLoadout`/`ItemsUsed` nối vào lõi `CombatSimulation.ExecuteIntent`.
  **Phát hiện quan trọng lúc thiết kế** (đọc code thật trước khi quyết định route, không đoán):
  Potion/Antidote KHÔNG tái dùng được cơ chế heal/cleanse có sẵn của `ActionResolver`/
  `StatusProcessor` (heal skill phụ thuộc stat người dùng chứ không phải %MaxHP của target;
  `Cleanse` xoá MỌI debuff Cleanse-type chứ không riêng DoT) — xử lý trực tiếp thay vì ép vào
  pipeline không khớp; Revive Feather/Elemental Bomb NGƯỢC LẠI khớp đúng 100% cơ chế có sẵn
  (`RevivePercent` đã tính theo MaxHP target, `PoiseDamage` tự áp dụng qua `ApplyOneHit`) nên tái
  dùng thẳng `ActionResolver.Execute` với `SkillData` tổng hợp (cùng mẫu `BasicAttackFor` cho
  minion). Shop (`ShopScreen`/`UI_Shop.prefab`) mở rộng từ 4→10 dòng để bán 6 item bằng Vàng (khác
  4 dòng cũ bán vật liệu Ascend bằng Gem). Battle HUD Skill Grid `GRID_ROWS` 1→2 (hàng ITEM đã
  chừa sẵn chỗ từ trước, comment cũ ghi rõ "chưa có hệ thống đứng sau" — nay đã có), `ItemSlotView`
  mới (sibling đơn giản hoá `SkillSlotView`). Tự động mang tối đa 5 loại×3/trận, auto-target 100%
  (không có UI chọn loadout/target thủ công ở đâu — nhất quán cách skill đã hoạt động), số item
  dùng thật (không phải mang) trừ vĩnh viễn khỏi `profile.Inventory.Items` sau trận — verify bằng
  Play-mode thật đầy đủ chuỗi Shop→Battle→persistence, không chỉ EditMode test.
- **Loot table NAY ĐÃ TUNE THEO CHƯƠNG** (task-loottable-chapters.md, khác dòng cũ ghi ở đây
  "chỉ wildcard chưa theo chương") — thêm 10 asset `loottable_{treasure,boss}_ch{1..5}.asset`
  (thuần data, không sửa `LootRoller`/`LootTableDefinitionSO`, hạ tầng đã đủ từ task-loottable.md
  trước đó). Gold/material/equipment-rarity Treasure tăng dần theo chương; material Boss chia
  theo mốc `AscendSystem.COSTS` (chương 1 EssenceI, chương 3 mở Core, chương 4 mở EssenceIII...).
  **Phát hiện quan trọng lúc làm**: thêm data khiến 2 test cũ trong `LootRollerTests.cs` đổi ý
  nghĩa — `Resolve_PrefersExactChapterMatch_OverWildcard` trước đây chỉ verify được nhánh
  fallback (chưa có bảng riêng để so sánh), và bài test "1 bảng Boss phải tự cấp đủ mọi vật liệu"
  không còn đúng khi vật liệu CỐ Ý chia theo chương — đổi thành verify HỘI của cả 5 chương cộng
  lại. Số liệu là TỰ THIẾT KẾ (plan.md không cho bảng cụ thể). **NAY ĐÃ QUA BALANCE HARNESS THẬT**
  (task-balance-loottable.md, follow-up "tiếp tục cho tôi") — `BalanceHarness.MaterialDropReport()`
  cũ có 2 bug thật (hardcode chương 1 dù đã có 5 bảng riêng; cộng cứng mảnh hero Boss theo
  `SIMULATED_OWNED_HEROES` dù Boss ch1-5 đều `HeroShardChance=0`, sai hoàn toàn với code thật
  `MetaSceneInstaller.GrantBossAscendMaterials`) — đã sửa cả 2, chạy lại thật cho số liệu chính xác
  (TỔNG 1 playthrough 5 chương: ~2.32 mảnh, ~15.8 EssenceI, ~15.4 EssenceII, ~8.5 EssenceIII, ~8.2
  Core). Đối chiếu `AscendSystem.COSTS`: đường cong mở khoá vật liệu khớp đúng ý đồ (mỗi loại mở
  sớm hơn 1 chương so với mốc ★ cần) — **KHÔNG sửa số liệu asset nào**, story-only chỉ đủ 23%/0%
  chi phí từng bậc ★ là DỰ KIẾN (3 nguồn khác đã xây thật bù phần còn lại: Gacha dupe →
  `GachaSystem.DuplicateShards` là nguồn mảnh CHÍNH; Shop mua Gem → `ShopScreen` bán Core/Essence;
  Material Dungeon → cày dài hạn, task-endgame.md), không phải lỗ hổng cân bằng.
- **Codex/Collection NAY ĐÃ XÂY** (task-codex.md, khác dòng cũ liệt kê "Codex" ở danh sách màn
  hình chưa có) — `CodexSystem` (pure, `Meta/Codex/`) + `CodexScreen` (UI, `UI_Codex.prefab`
  clone `UI_Quest.prefab`). Chỉ làm hero + enemy (KHÔNG làm "item" — hệ vật phẩm tiêu hao chưa
  xây, xem plan.md §7.5, ngoài phạm vi). Không có tracking "đã gặp" enemy thật — enemy unlock
  dùng proxy `enemyDef.Chapter <= profile.Progress.ChapterUnlocked` (tái dùng field tiến trình có
  sẵn thay vì thêm hệ theo dõi mới). **Màn ĐẦU TIÊN trong dự án cần phân trang** — 24 hero/66
  enemy đều vượt khuôn 6-mục-cố-định mà Quest/Mail dùng. **Phát hiện phụ trong lúc làm**:
  `TeamSelectScreen`'s `HeroListContainer` (danh sách hero chọn đội hình) không có Mask/ScrollRect
  nào, với 24 hero thật danh sách tràn khỏi khung nhìn không kiểm soát được — bug UI có thật, đã
  báo riêng qua task nền (`task_26720454`), KHÔNG tự sửa trong task Codex. **NAY ĐÃ SỬA**
  (task-teamselect-scroll.md, follow-up "tiếp tục cho tôi" request) — thêm `HeroListViewport`
  (`RectMask2D`+`ScrollRect`) bọc ngoài `HeroListContainer` trong `UI_TeamSelect.prefab`,
  `TeamSelectScreen.RefreshHeroList` set `sizeDelta.y` theo đúng số hero thật mỗi lần rebuild. Xác
  minh cấu trúc qua `execute_code` đọc trực tiếp `RectTransform`/`ScrollRect` sống (24 card, content
  cao 1680px = 24×70, viewport cố định 380px, `movementType=Clamped`) — không chụp được ảnh/kéo
  chuột thật do môi trường MCP bị "frame-stall" (`Time.frameCount` đứng yên suốt phiên, mẫu hình đã
  biết — xem §11 lịch sử ghi chú kỹ thuật MCP), giới hạn đã ghi rõ trong task file, không phải lỗi
  của bản sửa này. 402/402 test vẫn xanh (thay đổi thuần UI/prefab, không đụng logic).
- **BUG "Start Battle" bị đè, không bấm được — ĐÃ SỬA** (task-teamselect-start-button-fix.md, người
  dùng báo trực tiếp trong Play-mode thật). Root cause xác nhận bằng `execute_code` đọc
  `RectTransform` thật của `UI_TeamSelect.prefab`: `Footer` chỉ cao 40px cố định, 3 con
  `SelectedLabel`/`BackButton`/`StartButton` neo GIỮA chiều cao đó — nhưng
  `task-formation-synergy.md` (session trước) thêm `FormationRow` (dải cam full-width, nền mờ
  alpha=0.90, có `Button` riêng chặn raycast) bằng cách **parent thẳng vào `footer`**, neo TOP cao
  36px → phủ gần HẾT 40px của Footer, đè lên `StartButton` (vẽ sau = trên cùng, chặn cả click).
  Sửa `TeamSelectScreen.BuildFormationButton`: đổi parent từ `footer` sang `content` (cha của
  footer), neo bottom-of-content, `anchoredPosition.y = footer.sizeDelta.y + 6` (đọc chiều cao
  Footer thật lúc runtime, không hardcode 40) — dải nằm NGAY TRÊN Footer thay vì đè lên. Verify:
  gọi `BuildShell()` thật qua reflection trên instance mới (không cần dữ liệu profile), đọc
  `RectTransform` sống sau khi chạy — `FormationRow` band content-bottom `[46,82]` không chồng
  `Footer` band `[0,40]` (cách 6px), còn dư 18px trước khi chạm `HeroListViewport`(đáy=100)/
  `GearPanelContainer`(đáy=120). `validate_script` + force recompile 0 lỗi. Không đổi prefab, chỉ
  đổi 1 hàm runtime; 0 test EditMode phủ khu vực Meta UI dựng-bằng-code này — **518/518 xanh xác
  nhận lại** sau khi Editor rảnh Play Mode (phiên chia sẻ, gián đoạn tạm thời lúc chạy test).
- **BUG "không chọn Auto mà vẫn chơi Auto" (màn Battle) — ĐÃ SỬA** (task-autobattle-hud-sync-fix.md,
  người dùng báo trực tiếp). Root cause: `BattleSceneInstaller.BuildBattle()` khôi phục `_autoPlay`
  (field điều khiển HÀNH VI thật — đọc ở `ExecuteAiOrAuto`: `if (_autoPlay) Simulation.
  SubmitIntent(Simulation.DefaultAutoIntent())`) từ `SettingsDto.AutoBattle` đã lưu từ TRẬN TRƯỚC
  (persist có chủ đích, task-auto-battle.md), nhưng `BattleHudScreen._auto` (field điều khiển
  HIỂN THỊ nhãn "AUTO ON"/"AUTO OFF") luôn khởi tạo `false` mặc định — KHÔNG đọc lại `_autoPlay`
  khi HUD `Bind()`. Hệ quả: nếu từng bật Auto ở 1 trận trước, MỌI trận sau tự chạy Auto ngầm
  (đúng thiết kế persist) nhưng nút HUD luôn hiện sai "AUTO OFF" — đúng triệu chứng người dùng
  thấy. Sửa: `BattleHudScreen.SetAutoState(bool)` mới — set `_auto` + label + màu giống hệt logic
  trong nút bấm, nhưng KHÔNG phát `OnAutoToggled` (chỉ đồng bộ hiển thị, không phải hành động
  người chơi, tránh double-persist thừa); `BattleSceneInstaller.WireHud()` gọi
  `_hud.SetAutoState(_autoPlay)` ngay sau `_hud.Bind(Simulation)`. Verify qua `execute_code`: dựng
  `BattleHudScreen` thật (`BuildLayout()` qua reflection), `SetAutoState(true)` đổi đúng
  `_auto=false→true` và label "AUTO OFF"→"AUTO ON". Không có test EditMode phủ khu vực này (UI
  dựng-bằng-code, xác nhận qua grep) — **524/524 xanh** (không đổi khỏi baseline).
- **Mail NAY ĐÃ XÂY** (task-mail.md, khác dòng cũ ghi ở đây "Mail: chưa") — `MailSystem` (pure
  logic, `Meta/Mail/`) + `MailScreen` (UI, dựng từ `UI_Mail.prefab` clone `UI_Quest.prefab`).
  plan.md gần như không có spec cho Mail (chỉ 1 dòng "Đền bù LiveOps") nên toàn bộ nội dung tự
  thiết kế tối thiểu: 1 trigger thật duy nhất — quà chào mừng cấp lúc `LocalPlayerRepository.
  CreateNew()` (construct `MailDto` thô trực tiếp, KHÔNG gọi `Game.Meta.Mail.MailSystem` vì
  `Game.Services` không được phép ref `Game.Meta` — structure.md §6, cùng lý do Star/Level hero
  trong `CreateNew()` không gọi thẳng `HeroLevelSystem`). `MailButton` mới trên TopBar đặt vào 1
  khoảng trống 210px vốn đã tồn tại sẵn giữa `QuestButton` và `DungeonButton` (phát hiện bằng
  cách đọc thật `RectTransform` mọi nút qua `execute_code`, không đoán) — không cần mở rộng
  TopBar hay đụng bất kỳ nút nào khác. **NAY ĐÃ CÓ ĐỦ badge/expiry/Claim-All** (task-mail-extras.md,
  follow-up "tiếp tục cho tôi") — badge đỏ tĩnh `MailBadge` dựng trong Boot.unity dưới `MailButton`
  (mặc định ẩn, `MetaSceneInstaller.RefreshMailBadge` bật lên khi `MailSystem.UnclaimedCount > 0`,
  nghe `MailScreen.OnMailChanged` mới thêm để cập nhật ngay cả khi modal đang mở);
  `MailDto.ExpiresAtUtc` + `MailSystem.PurgeExpired` (gọi đầu `MailScreen.Open()`, xoá kể cả mail
  đã claim — mail Welcome CỐ Ý không có hạn, hạ tầng mở cho trigger tương lai);
  `MailSystem.ClaimAll` + nút `ClaimAllButton` mới trong `UI_Mail.prefab` (clone `CloseButton` qua
  `PrefabUtility.LoadPrefabContents`). **Phát hiện phụ**: dòng reward gốc "+2000 Gold · +100 Gem"
  đã tràn `ProgressLabel` 90px từ trước (task-mail.md chưa từng đo thật bằng `TextGenerator`) — vá
  cùng lúc (bỏ "+", fontSize 12→10, nới 90→128px qua rút bớt `NameLabel`/`ClaimButton`); giới hạn
  còn lại: 3+ loại currency trong 1 mail vẫn sẽ tràn (không xảy ra với nội dung hiện có, chỉ 1
  trigger Welcome 2-currency). 413/413 test xanh. Môi trường Play-mode phiên đó tự thoát/reload
  giữa chừng lúc verify nút Claim All thật (ServiceLocator rỗng đột ngột, không phải bug — cùng lớp
  vấn đề "MCP session instability" đã gặp ở task-teamselect-scroll.md/task-enhance-plus15.md, biểu
  hiện khác nhau mỗi lần) — bù bằng EditMode test đã cover đúng logic claim/grant qua service đăng
  ký chuẩn trong `[SetUp]`.
- **Title/Home screen NAY ĐÃ XÂY** (task-title-screen.md, follow-up "tiếp tục cho tôi") — trước đây
  `GameBootstrap.Awake()` auto-advance thẳng vào Meta, không dừng màn nào (`_autoAdvanceToMeta`
  tồn tại sẵn từ trước nhưng luôn `true`, chưa ai implement nhánh `false`). Nay `TitleCanvas` tĩnh
  trong `Boot.unity` (`GameBootstrap/__UI__/UIRoot/TitleCanvas`, sibling `MetaCanvas`,
  `sortingOrder=150`) — **KHÔNG tách scene thứ 4** (mọi màn khác trong dự án đều là Canvas overlay
  trong scene có sẵn, không phải scene riêng — 4 scene gốc plan.md dự tính vẫn chỉ có 3, `Sandbox`
  "chưa từng cần"). Hiện "AETHER LEGION" (codename dự án theo plan.md/roadmap.md, khác `productName`
  Unity "TurnBase") + tóm tắt profile thật + nút START. **KHÔNG tách class `TitleScreen` riêng** —
  khác Mail/Codex/Quest (có logic claim/phân trang thật), Title chỉ có đúng 1 việc nên gộp thẳng vào
  `GameBootstrap` (orchestrator Boot scene duy nhất). Verify Play-mode thật: `TitleCanvas.active=
  True`/`MetaCanvas.active=False` ngay lúc boot với `SubtitleLabel` đúng số liệu save thật, bấm
  `StartButton` thật → `SceneManager.activeScene` đổi đúng sang `"Meta"`, 2 canvas đảo trạng thái
  đúng. **Phát hiện phụ lúc verify**: sau `DontDestroyOnLoad(gameObject)`, `GameBootstrap` không
  còn nằm trong `Scene.GetRootGameObjects()` của "Boot" nữa (chuyển sang scene giả
  `DontDestroyOnLoad` — hành vi Unity chuẩn, không phải bug) — phải dùng `GameObject.Find` thay vì
  duyệt scene. Gặp lại MCP frame-stall khi đào sâu hơn (`MetaSceneInstaller.Start()` chưa kịp chạy
  nên field vẫn null) nhưng không cản phần chính vì test qua `onClick.Invoke()` trực tiếp. 413/413
  test xanh (thay đổi thuần Boot-flow, không đụng logic có test).
- **Inventory screen NAY ĐÃ XÂY** (task-inventory-screen.md, follow-up "tiếp tục cho tôi") —
  `InventoryScreen` (`Meta/`, mirror `CodexScreen`, đọc thuần) dựng từ `UI_Inventory.prefab` (clone
  `UI_Codex.prefab`, xoá `PrevButton`/`NextButton` — cả 2 tab đều ≤ 6 mục, không cần phân trang).
  2 tab: ITEMS (6 vật phẩm tiêu hao, `ItemCatalog`+`economy.GetItemCount`) / MATERIALS (5 vật liệu
  Ascend: EssenceI/II/III/Core/EnhanceStone, `economy.Get`). **Xác nhận `CurrencyType.Energy`/
  `Ticket`/`Honor` là 3 currency CHẾT** (grep 0 kết quả ngoài khai báo enum + set 1 lần trong
  `LocalPlayerRepository.CreateNew()`, không consumer/producer nào khác) — KHÔNG hiện trong
  Inventory để tránh ngụ ý chúng có tác dụng thật. Nút `InventoryButton` mới trên TopBar (Boot.unity)
  đặt vào khoảng trống thật giữa `MailButton`/`QuestButton` (world x [513,579], đo qua
  `GetWorldCorners()`). **Phát hiện kỹ thuật quan trọng**: `anchoredPosition` (canvas-local) KHÔNG
  quy đổi sang world-pixel bằng CÙNG hệ số `lossyScale` cho cả vị trí lẫn kích thước — size dùng
  scale riêng của chính RectTransform đó, position dùng phép dịch chuyển trong không gian PARENT —
  2 hệ quy chiếu khác nhau dù cùng "trông giống" 1 con số scale; phải nội suy tuyến tính từ ≥2 điểm
  đo THẬT qua `GetWorldCorners()`, không suy luận từ tỉ lệ ước lượng. **Phát hiện phụ**:
  `UI_Codex.prefab` chính nó vẫn còn `Title` ghi "QUEST" (sót từ lúc clone `UI_Quest.prefab`,
  `CodexScreen.cs` chưa từng set Title runtime) — báo riêng qua `spawn_task`, không tự sửa (không
  phải file Inventory). Format hàng danh sách rút gọn còn NameLabel + `×N` (bỏ hẳn
  `ItemDef.Description` — đo thật thấy tràn `ProgressLabel` 90px nặng nếu ghép cả mô tả, cùng cách
  ShopScreen/CodexScreen cũng không hiện mô tả đầy đủ trong hàng). Verify Play-mode thật ĐẦY ĐỦ
  NHẤT trong nhiều task UI gần đây — không gặp frame-stall lần này: cả 2 tab hiện đúng dữ liệu save
  thật (Potion×3/Ether×1/Antidote×1/…, EssenceI×999/EssenceII×1020/EssenceIII×10/Core×10,
  EnhanceStone×0 — row thứ 6 đúng ẩn vì Materials chỉ 5 mục), Close hoạt động đúng. 413/413 test
  xanh (không đụng logic có test).
- **Damage Meter UI NAY ĐÃ XÂY** (task-damage-meter.md, follow-up "tiếp tục cho tôi" — hạng mục
  ĐẦU TIÊN trong nhiều task UI gần đây thật sự đụng `Game.CombatView`/HUD trận đấu thay vì
  `Game.Meta`, được chọn sau cùng vì rủi ro cao hơn). **Phát hiện quan trọng nhất**:
  `BattleState.DamageByUnit` (`Dictionary<int,long>`) đã tồn tại sẵn từ trước, với chính doc-comment
  "Thống kê để tính thưởng và Damage Meter" — `RecordDamage` đã được gọi ĐÚNG ở mọi nguồn sát
  thương thật (`ActionResolver` cho đòn trực tiếp VÀ Counter/Reflect, `StatusProcessor` cho DoT
  tick) — chỉ thiếu UI tiêu thụ, cùng mẫu "hạ tầng có sẵn, chưa ai dùng" đã gặp nhiều lần session
  này (`UnclaimedCount`, `HeroInstanceDto.Awakened`...). Thêm panel góc trái-dưới HUD (trống sẵn,
  không đụng HeroPanel/EnemyPanel/TurnOrderBar/SkillGrid/EndTurn/AutoSpeed) vào `BattleHudScreen
  .cs` — **ĐÚNG style code-dựng runtime + TextMeshPro của HUD** (khác hẳn style prefab+`UnityEngine
  .UI.Text` của mọi màn Meta session này đã làm — xác nhận qua ảnh tham khảo
  `_Reference/UI_SAMPLE/UI_01.jpg` người dùng gửi: quyết định giữ nguyên style hiện có của TỪNG khu
  vực, không trộn, không áp style ảnh tham khảo ngay). Đọc thẳng `_sim.State.DamageByUnit` mỗi
  frame trong `Update()` có sẵn — **CỐ Ý KHÔNG dùng `CombatEventQueue`** dù nó cũng có đủ dữ liệu,
  vì `TryDequeue` đã bị `CombatPresenter` tiêu thụ, tự dequeue thêm sẽ tranh event của Presenter.
  Top 5 unit giảm dần theo damage, tên qua `Short(u.DefId)` (helper có sẵn trong file), màu theo
  `Side` (`HERO_ACCENT`/`ENEMY_ACCENT` có sẵn). **Phát hiện phụ**: `DamageByUnit`/`RecordDamage`
  chưa từng có test trực tiếp (chỉ coverage gián tiếp qua HP sau trận) — thêm `BattleStateTests.cs`
  mới (2 test) trước khi UI bắt đầu hiển thị cho người chơi. Verify Play-mode: gặp lại MCP
  frame-stall ngay từ đầu phiên — áp dụng "check-before-force" (ép tay `MetaSceneInstaller.Start()`
  → `LaunchBattle` → `BattleSceneInstaller.Start()`), sau đó mô phỏng THẬT nhiều lượt
  `SubmitIntent`/`Advance` (cả 4 hero lẫn AI địch, gồm cả phản đòn) rồi ép tay `BattleHudScreen
  .Update()` — xác nhận panel hiện ĐÚNG thứ hạng/tên/màu khớp `DamageByUnit` thật (215/158/135/82/77
  đúng thứ tự giảm dần, hero=xanh/enemy=tím đúng phe, hạng 6 đúng bị cắt). Đây là lượt verify
  Play-mode mô phỏng CHIẾN ĐẤU THẬT đầy đủ nhất toàn session (không chỉ đọc field tĩnh). 415/415
  test xanh.
- **Addressables PILOT đã xây thật** (task-addressables-pilot.md, follow-up "tiếp tục cho tôi" —
  người dùng chọn làm cả 3 hạng mục lớn Addressables/Localization/Animation, thực hiện lần lượt;
  Addressables trước tiên vì thuần kỹ thuật, không phụ thuộc 2 việc kia). **Chưa cài Addressables
  trong project trước đây** (grep `manifest.json` ra 0 kết quả) — cài `com.unity.addressables`
  4.0.1 qua `manage_packages`. Sau khi khảo sát thật thấy quy mô đầy đủ (23 file/~30 chỗ gọi
  `Resources.Load`/`LoadAll` trải khắp `Game.Meta`+`Game.CombatView`+`Game.Services.Audio`, API
  Addressables vốn BẤT ĐỒNG BỘ trong khi codebase này 100% đồng bộ ở tầng Meta/CombatView), xác
  nhận qua `AskUserQuestion` đây là thay đổi rủi ro cao nhất session — chọn **pilot hẹp**: CHỈ 3/23
  file, CHỈ `HeroDefinitionSO` nhóm lookup-đơn-theo-defId (`TeamSelectScreen.FindHeroDef`,
  `HeroDetailScreen`, `BattleSceneInstaller` — chỗ nóng nhất, spawn hero mọi trận). Dùng
  `Addressables.LoadAssetAsync<T>(key).WaitForCompletion()` — API CHÍNH THỨC Unity cung cấp riêng
  cho migrate code đồng bộ mà chưa tái cấu trúc async ngay, giữ hành vi caller y hệt cũ. 24 asset
  `HeroDefinitionSO` đánh dấu Addressable với address = ĐÚNG path `Resources` cũ (không đổi key
  lookup trong code, giảm rủi ro gõ sai chuỗi). **CỐ Ý ĐỂ NGOÀI PHẠM VI**: nhóm `LoadAll`+Label
  (`CodexSystem`/`GachaSystem` — kỹ thuật khác, nhạy cảm hơn), mọi asset loại khác (Enemy/Skill/
  Equipment/LootTable SO, prefab, sprite, AudioClip — 21 file/~25 chỗ còn lại), refactor bất đồng
  bộ thật, di dời asset khỏi `Resources/`. **Phát hiện lúc build**: asmdef cần CẢ `Unity.Addressables`
  LẪN `Unity.ResourceManager` (chứa `AsyncOperationHandle<T>`) — thiếu cái sau gây lỗi CS0012 dù đã
  có cái trước, asmdef không tự kéo dependency bắc cầu — thêm vào `Game.Meta.asmdef`/`Game.
  CombatView.asmdef`. Verify Play-mode thật (frame-stall lại xảy ra, dùng check-before-force):
  `TeamSelectScreen` hiện đúng 24 hero/màu rarity qua đường Addressables mới; **bằng chứng trước/
  sau mạnh nhất session** — launch 1 trận thật, HP 4 hero spawn qua `BattleSceneInstaller` (
  688/456/324/369) khớp CHÍNH XÁC với số liệu đã ghi lại trước đó ở task-damage-meter.md (cùng
  profile, trước khi đổi sang Addressables) — xác nhận migrate không làm lệch dữ liệu dù đổi hẳn
  đường load. 416/416 test xanh (415 cũ + 1 test mẫu tự kéo theo từ chính package Addressables,
  không phải của dự án, vô hại).
- **Localization PILOT đã xây thật** (task-localization-pilot.md, follow-up "tiếp tục cho tôi" —
  2/3 hạng mục lớn người dùng chọn làm hết, sau Addressables). plan.md có spec đủ (`ILocalization
  Service`/`LocalizationService`, "CSV key→value, VI/EN" §11.7, quy ước key `{màn}.{nhóm}.{tên}`
  §18) nhưng chưa ai xây — `SettingsDto.Language="vi"` đã tồn tại sẵn từ trước (có null-guard
  trong `SettingsService`) nhưng KHÔNG có gì đọc, cùng mẫu "hạ tầng có sẵn, chưa ai dùng" gặp lại.
  `HeroDisplayUtil.cs` tự ghi nhận gap này trong chính doc-comment cũ: "Chưa có ILocalizationService
  để tra NameKey ra chuỗi hiển thị thật" — tên hero/enemy/skill hiện tại chỉ là `FormatId()`
  title-case hoá DefId, KHÔNG PHẢI dịch thật, luôn ra tiếng Anh bất kể `Language`.
  Xây `Game.Services.Localization` (thư mục stub có sẵn từ lúc khởi tạo dự án, rỗng từ đầu) —
  `ILocalizationService`/`LocalizationService` đọc `Resources/Localization/strings.csv` (10 key
  thật, cột `key,vi,en`). **KHÔNG dùng lại `Game.Tools.CsvReader`** (parser CSV tốt nhất dự án,
  đã dùng cho pipeline heroes/skills/enemies.csv) vì `Game.Tools.asmdef` chỉ chạy Editor
  (`"includePlatforms": ["Editor"]`) trong khi `LocalizationService` phải chạy runtime thật — viết
  parser nhỏ riêng, không di dời hạ tầng Editor tooling đang chạy tốt. Đăng ký qua
  `ServiceInstaller` + `WireSettingsToLocalization()` (đúng mẫu `WireSettingsToAudio` — cài đặt đổi
  tự lan, không màn nào phải gọi tay). **Pilot 2 màn TRỌN VẸN** (không chỉ 1 label như dự tính ban
  đầu — đang sửa nguyên `SettingsScreen.cs` nên dịch hết luôn, không tăng phạm vi file): Title
  screen (3 key, có placeholder động `{0} Tướng · {1} Vàng`) + `SettingsScreen` (7 key + nút
  Language mới, mẫu nút Speed của `BattleHudScreen` — hiện giá trị hiện tại, bấm cycle). Đổi
  `SettingsScreen.NewToggle` thành `NewToggleWithLabel` (trả kèm `Text` để đổi được lúc runtime —
  bản gốc "chôn" label không giữ tham chiếu). Verify Play-mode thật (frame-stall lại xảy ra,
  check-before-force): Title hiện đúng tiếng Việt mặc định kể cả placeholder ("24 Tướng · 935390
  Vàng"), mở Settings thật → nhãn tiếng Việt đúng, bấm nút Language thật → TOÀN BỘ nhãn đổi ngay
  sang tiếng Anh, xác nhận đúng cả chuỗi wiring lẫn trạng thái `ILocalizationService` singleton
  dùng chung app-wide (đọc trực tiếp qua `ServiceLocator`, không qua SettingsScreen, vẫn khớp).
  423/423 test xanh (416 + 7 `LocalizationServiceTests` mới). **Còn ~28 file khác vẫn hard-code
  chuỗi** (cố ý, pilot chỉ verify luồng trước khi mở rộng — đúng kỷ luật Addressables đã dùng),
  chưa có `LocalizationScanner` CI (plan.md §7), chưa dịch tên hero/enemy/skill.
- **Event/Rest node NAY CÓ lựa chọn thật** (task-eventrest.md, khác thiết kế cũ ghi ở đây từng
  nói "còn nông") — `NodeChoiceSystem` (pure logic, `Meta/Dungeon/`) + `NodeChoiceScreen` (UI,
  `Meta/`, dựng từ `UI_NodeChoice.prefab` clone `UI_Shop.prefab`). Event 3 lựa chọn rủi ro thật
  (plan.md §8.1), Rest 2 lựa chọn (Gold / +1 skill level miễn phí ngẫu nhiên). **Phát hiện quan
  trọng**: game KHÔNG có HP dai dẳng giữa các trận trên node map (`HeroInstanceDto` không có field
  HP nào — mọi trận luôn bắt đầu full HP), nên "Hồi 30% HP" của Rest trong plan.md §8.1 không có ý
  nghĩa thật trong kiến trúc hiện tại (ngoại lệ duy nhất là Tháp Vô Tận, nhưng đó là 1
  `CombatSimulation` liên tục xuyên tầng, khác cơ chế node map hẳn) — Rest "Recover" cấp Gold thay
  vì giả vờ hồi máu không tồn tại; xây persistent-HP-giữa-trận là việc lớn hơn hẳn 1 task redesign
  Event/Rest, cố ý để ngoài phạm vi.
- **`EquipmentService.TryEnhance` nay cũng chạm sub-stat** (task-enhance-substat.md, khác thiết
  kế `EnhanceSystem`/`EnhanceScreen` riêng ở §4.2/§9 — tên thật vẫn là `EquipmentService` +
  `TeamSelectScreen`'s inline Enhance UI, không tách screen riêng) — mốc level 3/6/9 mở thêm 1
  sub-stat mới (loại trừ trùng loại, cùng range rarity với lúc sinh item qua
  `EquipmentGenerator.TryRollAdditionalSubStat`), Enhance giờ tốn cả Gold lẫn
  `CurrencyType.EnhanceStone` (trước đây Dungeon Đá grant được nhưng không ai tiêu — currency
  chết, nay đã có chỗ dùng thật). **NAY ĐÃ MỞ RỘNG ĐỦ +0→+15 ĐÚNG plan.md §7.3**
  (task-enhance-plus15.md) — trần +9→+15, mốc sub-stat đủ 4 {3,6,9,12}, `EnhanceStoneCost` đủ 8
  bracket (1/2/3/5/8/10/14/20), thêm `SuccessChance` (100% dải 0-10, 70/55/40/25% dải 11-14) và
  `EquipmentService.EnhanceOutcome` enum (`Rejected`/`Failed`/`Succeeded` — thay `bool` cũ, phân
  biệt "không đủ tài nguyên, không trừ gì" với "đủ tài nguyên nhưng trượt, VẪN trừ" đúng plan.md
  "thất bại không mất đồ/tụt cấp, chỉ mất tài nguyên"), `EffectiveStatValue` nhân thêm ×1.5 ở
  level 15 (diễn giải: cộng thêm lên công thức tuyến tính nền, không thay thế — plan.md không nói
  rõ, xem task-enhance-plus15.md §0). **Gold KHÔNG đổi công thức cũ** `80*(level+1)` — giữ nguyên
  xuyên suốt +0..+14 thay vì nhảy sang số Vàng plan.md (200-22.000), có chủ đích: Gold vốn rẻ/dồi
  dào trong game này, nút thắt thật của dải rủi ro là Đá (giới hạn tuần) + tỉ lệ thất bại, cả 2 bám
  đúng số plan.md 100%. Verify: 408/408 test (seed-scan thật cho cả nhánh Failed/Succeeded ở level
  11, không đoán seed), `EnhanceLabel` đo thật bằng `TextGenerator` (phát hiện: dùng số Vàng LITERAL
  của plan.md sẽ tràn hộp 84px, dùng đúng công thức thật của code thì vừa — không cần rút gọn thêm).
  Gặp lại MCP frame-stall (giống task-teamselect-scroll.md) nên không verify Play-mode UI bằng mắt
  được, bù bằng test+đo chữ thật thay vì screenshot.
- **Reforge sub-stat ĐÃ XÂY** (task-phase-5-gaps.md Phần C, plan.md §7 — nốt cuối cùng của
  Equipment còn thiếu, enum `MetaEnums.Reforge=14` tồn tại từ trước nhưng logic/UI chưa ai làm).
  `EquipmentService.TryReforge` reroll TOÀN BỘ `SubStats` bằng chính `EquipmentGenerator.
  RollSubStats(rarity, rng)` (hàm ĐÃ CÓ SẴN dùng lúc sinh item — tái dùng, không viết logic roll
  mới) → tự động giữ đúng SỐ LƯỢNG sub-stat theo rarity, không đổi Level/main stat. Khác
  `TryEnhance`: KHÔNG có tỉ lệ thất bại (`ReforgeOutcome{Rejected,Succeeded}` — chỉ 2 trạng thái,
  không có `Failed`). `ReforgeCost(level,rarity) = 80*(level+1)*(2+(int)rarity)` — số tự thiết kế
  bám mốc `EnhanceCost`, nhân thêm theo rarity (item hiếm hơn/nhiều sub-stat hơn → đắt hơn).
  UI: `ReforgeButton`+`ReforgeLabel` thêm vào `UI_GearSlotRow.prefab` (qua `manage_prefabs`
  open/save prefab stage, KHÔNG runtime-create như `FormationRow` — đúng convention Equip/Enhance
  đã có sẵn trong prefab đó). Đặt Ở CỘT PHẢI (x:245-430) NGAY DƯỚI Equip/Enhance thay vì CÙNG
  HÀNG — đo thật bằng `execute_code`+`Text.GetGenerationSettings`+`TextGenerator.
  GetPreferredWidth` xác nhận 2 nút cũ ĐÃ CHIẾM HẾT 185px khả dụng (worst-case EnhanceLabel
  "1200g·20◆·25%" cần 79/84px, gần như khít tuyệt đối — không còn chỗ chèn ngang), nên đặt dọc
  vào vùng TRỐNG dưới 2 nút đó (ItemLabel cột trái không lấn sang cột phải) — không tăng chiều
  cao row. **Phát hiện phụ quan trọng lúc verify hình học sống**: dòng CUỐI (slot 5) của gear
  panel với `rowH=66` cũ đã chồng lên `FormationRow` (task-formation-synergy.md, band
  content-bottom `[46,82]`) — xác nhận bằng `execute_code` đọc `RectTransform` rằng đáy dòng
  cuối = content-bottom 62 dù CÓ hay KHÔNG có `ReforgeButton` (y hệt `ItemLabel` cũ) → lỗi CÓ
  SẴN TỪ TRƯỚC, không phải do `ReforgeButton` gây ra. `FormationRow` vẽ SAU (đè lên, có `Image`
  chặn raycast) — cùng LOẠI lỗi vừa sửa ở task-teamselect-start-button-fix.md, chỉ chưa ai bấm
  trúng để báo. Tiện sửa luôn (cùng hàm `RefreshGearPanel` đang đụng): giảm `rowH` 66→60 — vẫn
  giữ 6px hở giữa các dòng liên tiếp, đẩy đáy dòng cuối lên content-bottom=92 (cách
  `FormationRow` 10px, xác nhận lại qua `execute_code` sau khi sửa). 6 test mới
  (`EquipmentServiceReforgeTests`: cost scale level+rarity, `CanReforge`, Succeeded giữ count +
  trừ Gold đúng, đổi được sub-stat thật qua scan 30 seed, Rejected khi không sub-stat/thiếu
  Gold — không đụng gì). **524/524 xanh.**
- **`EquipmentGenerator` ĐÃ TỒN TẠI** (khác dòng "Bảng sub stat... chưa xây" cũ ở §6.2/§9) —
  `Meta/Equipment/EquipmentGenerator.cs`, roll rarity + 8 loại sub-stat đúng plan §7.2, nối vào
  `LootRoller`/Treasure node, wiring `CombatUnit.EquipmentModifiers` thật (task-equipment.md).
- **Không có `IEventBus`/`RewardResolver`/`BattleResultProcessor`/`UIScreenStack`/`UIManager`** —
  kiến trúc thật gọi method trực tiếp: `MetaSceneInstaller` là 1 God-object điều phối toàn bộ Meta
  (mở/đóng từng "Screen" component bằng `Open()/Close()` thủ công, không qua stack/registry).
  Đây là quyết định có chủ đích xuyên suốt các task-*.md (giữ tối giản, "không phá cái đang chạy"),
  không phải nợ kỹ thuật ngẫu nhiên.
- **Không có `ILocalizationService`/`IAssetService`** — mọi asset qua `Resources.Load` trực tiếp,
  chưa Addressables, chưa localization (mọi string hard-code tiếng Anh/Việt lẫn lộn trong code).
- **`HeroLevelSystem`/`AscendSystem`/`SkillUpgradeSystem`/`QuestSystem`/`LootRoller`/
  `InnatePassiveCatalog`/`AwakeningCatalog`/`PassiveProcessor` đều ĐÃ xây** (khác ghi chú cũ nói
  "chưa có") — tất cả static class hoặc sealed class thuần C#, có test EditMode, không qua DI/
  ServiceLocator (trừ `EconomyService`/`AudioService`/`SettingsService`).
- **`EconomyService.TryConsume/Grant`** — mọi thay đổi Wallet đi qua đây, không sửa `WalletDto`
  trực tiếp. Đã mở rộng đọc/ghi `Materials`/`HeroShards` (task-ascend.md §1).
  **`GetEquipmentModifiers`** mới (task-equipment.md §3) — sub-stat trang bị → `CombatUnit.
  EquipmentModifiers`, độc lập với `GetBonusPrimary` (main stat → `PrimaryStats`).
- **Combat edge case (plan §4.14): 24/24 có test thật — TOÀN BỘ danh sách đã hoàn tất**
  (`SimulationTests.EdgeCaseTests` + `CoreSystemTests`/`DamageCalculatorTests` +
  `BattleReplayTests`). Lượt cuối (task-edgecases.md §7-9) xây thật cả 3 case còn khó nhất:
  - **E12 AutoRevive**: `PassiveTrigger.OnTeamWipe` + `PassiveProcessor.TryAutoRevive`.
  - **E15 Ultimate execution**: `BattleState.ConsumeUltimate()` + gate
    `CanUseSkill(skill, ultimateReady)` — phát hiện quan trọng: trước đó Ultimate hoàn toàn miễn
    phí/spam được vô hạn (gauge tích nhưng không gì tiêu), không chỉ thiếu edge-case.
  - **E17 BattleSnapshot resume**: `Game.Combat.BattleReplay` (pure C#, chỉ cần replay intent
    PHE PLAYER — enemy tự tái tạo qua AI+seed xác định) + wiring `BattleSceneInstaller.
    OnApplicationPause`/`MetaSceneInstaller.TryResumeBattleFromSnapshot`. Verify bằng Play mode
    THẬT (không chỉ execute_code giả lập): chơi 3 lượt thật → pause → thoát/vào lại Play mode
    (buộc load lại save AES thật từ đĩa) → xác nhận scene tự nhảy vào Battle, HP 7 unit khớp
    tuyệt đối với lúc pause. Bắt được 1 bug thật lúc verify (`_canvasRoot` null vì thứ tự gọi
    sai) mà EditMode test không thể phát hiện — minh chứng vì sao bước Play mode thật bắt buộc.
- UI 3 scene (Boot/Meta/Battle) đã chuyển từ code-generate sang Hierarchy-authored theo quy ước
  `__UI__`/`__Stage__`/`__Systems__` (xem memory `project_ui_prefab_migration.md`).
- **Dungeon hằng ngày + Trial Boss hằng tuần ĐÃ XÂY THẬT** (task-endgame.md, plan.md §8.3) —
  `DungeonSystem`/`TrialBossSystem` (`Meta/Endgame/`) là pure static class giống
  `QuestSystem`/`AscendSystem`, có 25 test EditMode. Dungeon: 4 loại (Gold/Exp/Material/Stone,
  mỗi loại chỉ mở vài ngày/tuần), 10 tầng/loại, reset UTC hằng ngày. Trial Boss: 1 boss HP cực cao
  (`boss_trial_champion`, tái dùng skill kit `boss_void_king`), đo tổng damage phe Player trong
  `TRIAL_BOSS_TURN_LIMIT=30` lượt (`BattleSceneInstaller`), 3 bậc thưởng nhận CỘNG DỒN tự động
  ngay sau trận (không cần bấm Claim riêng). Trận không gắn node map — `RunContext.
  QueueSpecialBattle` dùng `NodeId=-1` làm sentinel để né hẳn nhánh xử lý theo node map cũ.
  `DungeonScreen`/`TrialBossScreen` nhân bản từ `UI_Quest.prefab` (đúng khuôn Row NameLabel/
  ProgressLabel/ClaimButton đã có), 2 nút mới `DungeonButton`/`TrialBossButton` thêm vào TopBar
  (Boot.unity, static authoring). **Phát hiện phụ khi làm task này:** `SummonButton`/`QuestButton`
  trong TopBar mỗi cái có 1 Label con THỪA (rớt lại từ lần nhân bản nút trước đó, đè chữ lên
  nhau tại cùng vị trí) — đã dọn sạch. **Cũng phát hiện:** `Text` component mặc định kế thừa từ
  `UI_Quest.prefab` (NameLabel 150×26, ProgressLabel 90×26, WalletLabel 200×26, đều
  Wrap+Truncate) đủ cho text ngắn của QuestScreen nhưng KHÔNG đủ cho câu dài hơn — text đúng dữ
  liệu nhưng bị wrap dòng 2 rồi mất vì Vertical Overflow=Truncate; DungeonScreen/TrialBossScreen
  đã né bằng cách nới rộng box trong code, nhưng **QuestScreen gốc CHƯA được kiểm tra lại xem có
  bị lỗi tương tự với entry tên dài hay không** (ngoài phạm vi task này).
- **Tháp Vô Tận (`DungeonKind.Tower`) ĐÃ XÂY THẬT** (task-endgame.md, plan.md §8.3) — 100 tầng,
  xếp hạng hằng tuần theo tầng cao nhất (`TowerSystem`, cùng khuôn `TrialBossSystem`), 5 bậc
  thưởng cộng dồn (bậc cuối cấp 1 trang bị Mythic thật qua `EquipmentGenerator.Roll`).
  **Cơ chế mới, khác hẳn Dungeon/TrialBoss**: 1 lượt leo = 1 `CombatSimulation` LIÊN TỤC nhiều đợt
  địch — KHÔNG quay lại Meta giữa các tầng như Dungeon. Thêm hook
  `CombatSimulation.OnEnemyWaveCleared` (`Func<CombatSimulation, bool>`, mặc định null = hành vi
  cũ không đổi cho mọi trận khác) — khi phe Enemy bị wipe, `CheckEnd()` hỏi hook này TRƯỚC khi
  chốt Victory; nếu hook spawn đợt địch mới và trả `true`, trận tiếp tục thay vì kết thúc.
  `BattleSceneInstaller.TryAdvanceTowerWave` implement hook này: destroy view đợt địch cũ (đã
  chết — không destroy sẽ đè hình lên đợt mới cùng slot), spawn đợt mới qua `SpawnTeamFromDefinitions`
  y hệt code path spawn ban đầu, chọn địch qua `Simulation.State.Rng` (KHÔNG phải
  `UnityEngine.Random`, giữ xác định theo seed cho BattleReplay E17). Phe Player KHÔNG bị đụng tới
  gì cả (HP/SP/status/cooldown) — "HP không hồi giữa tầng" (plan.md §8.3) đến MIỄN PHÍ từ việc đơn
  giản là không reset gì, không cần cơ chế lưu HP mới. Verify: 3 test `MultiWaveTests` (EditMode,
  Combat layer — HP hero giữ nguyên khi sang đợt, trả `false` thì kết thúc bình thường, `null` thì
  hành vi y hệt trước khi có tính năng) + Play mode thật (kill hết địch tầng 1 → xác nhận tầng 2
  spawn với đúng 3 địch mới, mọi hero HP giữ nguyên tuyệt đối; leo tầng 1 rồi thua tầng 2 → floorsCleared
  báo về đúng = 1 không phải 2). **Giới hạn xác nhận được:** round-trip Meta→Battle→Meta đầy đủ
  (reward crediting thật) chưa verify end-to-end trong phiên Play mode MCP-driven này do
  ServiceLocator mất đăng ký giữa chừng (cùng vấn đề môi trường đã ghi ở Dungeon/TrialBoss bên
  trên, không phải lỗi Tower) — logic `TowerSystem.RecordClimb`/`TryClaimRewards` đã test riêng
  (12 test EditMode) và `ApplySpecialBattleResult`'s Tower branch là glue code đơn giản giống hệt
  cấu trúc Dungeon/TrialBoss đã verify.
- **Set Bonus 8 bộ trang bị ĐÃ XÂY THẬT** (task-setbonus.md, plan.md §7.4) —
  `SetBonusCatalog`/`SetBonusResolver` (`Meta/Equipment/`) là pure static class, không qua DI, hard-code
  giống `AwakeningCatalog` (cùng lý do: `StatModifier`/`StatusApplication` có field readonly Unity
  serializer bỏ qua). `EquipmentInstanceDto.SetId` (đã có field từ task-equipment.md, chưa ai gán)
  giờ roll ngẫu nhiên 1/8 đều ở TẦNG INSTANCE trong `EquipmentGenerator.RollFrom` — giống hệt cách
  Rarity/sub-stat đã roll độc lập với def, **KHÔNG cần author thêm CSV/asset equipment nào**. 2-món
  (8/8 bộ) chỉ là `StatModifier` cộng vào `EquipmentModifiers` có sẵn — mọi `StatType` liên quan
  (`LifestealPct`/`PoiseDmgPct`/`DmgBonusPct`/`DmgReductPct`) hoá ra ĐÃ được `DamageCalculator`/
  `ActionResolver` đọc thật từ trước (không phải dead field như lo ban đầu, xác nhận bằng grep trực
  tiếp). 4-món **đủ 8/8 bộ** dùng slot passive thứ 3 mới `CombatUnit.SetBonus` (song song
  `.Passive`/`.Awakening`, cùng đi qua `PassiveProcessor.Fire`/`CheckHpThreshold`). **7 field/tham
  số nhỏ mới thêm vào `PassiveData`/`StatusApplication`/`PassiveProcessor`/`CombatUnit` để lấp các
  khoảng trống engine không có sẵn** (mỗi field độc lập, mặc định giữ hành vi cũ, không có bộ nào
  chồng lấn): `PassiveData.SpRefundPercent` (Sage — hoàn SP), `HealPercentMaxHp` (Vampire/Guardian
  — heal tức thời, khác Regen là DoT/HoT theo lượt), `ShieldPercentMaxHp` (Bastion — đường
  `Applies[]`/`StatusApplication` chung tính `Shield.Value=0` nên phải gọi thẳng
  `StatusProcessor.ApplyShield`), `RequiresCrit` (Assassin), `RequiresPerfectGrade` (Ember — phát
  hiện `OnPerfectCommand` không có địch làm contextTarget nên đổi dùng `OnHitDealt` + gate grade),
  `RequiresFirstActionOfRound` (Tempest — đọc `CombatUnit.IsFirstActorThisRound` trực tiếp trong
  `DamageCalculator`, không cần đổi chữ ký `Calculate` nhận `BattleState`),
  `StatusApplication.TargetAllAllies` (Breaker — buff cả đội, trước đó chỉ hỗ trợ
  Self/contextTarget đơn).

  **Bug lõi combat phát hiện + sửa khi làm Tempest (task-setbonus.md §1.3, không liên quan trực
  tiếp Set Bonus):** `SimPhase.RoundEnd` có sẵn trong switch của `CombatSimulation.Advance()`
  nhưng KHÔNG BAO GIỜ được gán ở bất kỳ đâu — `BeginRound()` (tăng `BattleState.RoundNumber`, tick
  `AIController.TickRuleCooldowns`) vì vậy chỉ chạy ĐÚNG 1 LẦN mỗi trận (lúc `Start()`), không phải
  mỗi round như tên gọi. `AIConditionType.RoundAtLeast` — đã kiểm, KHÔNG enemy/boss nào trong data
  thật hiện dùng (fix an toàn, không đổi hành vi nội dung đang có), nhưng cơ chế chưa từng hoạt
  động đúng. Sửa bằng field mới `CombatUnit.HasActedThisRound` (tính CẢ turn bị skip — khác
  `IsFirstActorThisRound` của Tempest) + `CombatSimulation.AllAliveUnitsActedThisRound()`: khi mọi
  unit còn sống đã hành động (kể cả bị skip) trong round, chuyển `Phase = RoundEnd` thay vì thẳng
  `TurnStart`, để `Advance()` tự nối sang `RoundStart` → `BeginRound()` chạy lại thật. Verify: chạy
  lại TOÀN BỘ suite (đụng core loop, rủi ro cao nhất session) — 350/350 xanh, gồm cả
  `FuzzBattleTests` (2000 trận ngẫu nhiên) và `MultiWaveTests` (Tháp Vô Tận — unit mới thêm giữa
  round không làm round kết thúc sớm).

  Verify tổng: 350/350 test EditMode xanh (27 mới cho cả 2 đợt Tempest) + smoke test qua
  `execute_code` với catalog trang bị THẬT (800 roll → cả 8 bộ phân bố đều; hero mặc đủ 4 món
  Vampire thật → cả 2-món lẫn 4-món kích hoạt đúng).

> Cập nhật cột "Đã tạo" ở cuối mỗi sprint/lượt task lớn. Bảng + §12.1 cập nhật thủ công 2026-08-10
> dựa trên đếm file thật (`find` toàn bộ `Assets/_Project/Scripts`) + chạy test suite thật.
> **`Tools/Validate Object Map` NAY ĐÃ XÂY** (2026-08-12, task-phase-5-gaps.md Phần A) — chạy lần
> đầu (`Tools/Object Map/Generate Report` → `object-map-validation.md`, xem file đó để đọc đầy đủ):
> 88 script ở §3–§9 chưa tồn tại file (khớp đúng cảnh báo §12.1 rằng các bảng đó là kiến trúc ĐÍCH),
> 0 script thật (gắn tĩnh scene/prefab) chưa đăng ký, 34 tên prefab trong docs chưa có asset. Số 88
> KHÔNG phải lỗi cần sửa ngay — là số liệu nền để theo dõi drift từ đây trở đi.
> **`LayoutProfileSwitcher`/`SafeAreaFitter` NAY ĐÃ XÂY** (2026-08-12, task-phase-5-gaps.md Phần E)
> — 3 class thật (`LayoutProfile`/`LayoutProfileSwitcher`/`SafeAreaFitter`) ở
> `Assets/_Project/Scripts/Core/UI/` (namespace `Game.Core.UI`, KHÔNG `Game.UI`/`UI/Core/` như bản
> nháp gốc của task file — `Game.Meta` không được tham chiếu `Game.UI`, và 1 trong 3 màn pilot bắt
> buộc là `SettingsScreen` (Game.Meta); đúng precedent `IUiRootHost` đã dùng cùng lý do). **CHƯA
> xây** `ScreenOrientationService`/`E-ORIENTATION_CHANGED` mà bảng §3.2/§3.3/§5.2 mô tả — 2 class
> mới đọc thẳng `Screen.width/height` mỗi `Update()` (`[ExecuteAlways]`), không qua service/event
> nào; các dòng `GO-META-SAFE`/`GO-BTL-HUD` ở §3 vẫn là kiến trúc ĐÍCH chưa khớp thật (điểm gắn pilot
> thật KHÁC vị trí "SafeArea" mà bảng đó giả định — xem chi tiết bên dưới), cố ý KHÔNG sửa các bảng
> đó để tránh tuyên bố sai. Gắn pilot thật vào đúng 3 màn task file yêu cầu: `SettingsScreen`
> (panel dialog, `Game.Meta`), `BattleHudScreen` (HeroPanel, `Game.UI`), `TitleCanvas`
> (`TitleLabel`+`SafeAreaFitter` trên root, tĩnh trong `Boot.unity` — gắn qua `manage_components`+
> `SetProfiles()` qua `execute_code` rồi lưu scene, KHÔNG dựng bằng code runtime, đúng
> [[feedback_hierarchy_means_static]]). Portrait luôn = số liệu hiện có (không đổi hành vi cũ);
> Landscape = số liệu mới tự thiết kế cho pilot (không phải bản responsive cuối cùng — §E1 ngoài
> phạm vi "áp toàn bộ 23 màn"). **547/547 xanh** (524 cũ + 23 test mới `ResponsiveLayoutTests`).
> **Tutorial 5 bước NAY ĐÃ XÂY** (2026-08-12, task-phase-5-gaps.md Phần B) — `TutorialController`
> (state machine thuần, `Game.CombatView.Tutorial`, KHÔNG MonoBehaviour — test được không cần
> scene) + `TutorialOverlay` (MonoBehaviour, banner code-dựng giữa màn, dim nền
> `raycastTarget=false` nên KHÔNG chặn thao tác thật — người chơi vẫn bấm skill/Action Command
> bình thường trong lúc overlay hiện). Không cần event `CombatEvent` mới nào — đọc thẳng payload có
> sẵn: bước 3 (khắc chế) dùng `DamageDealt.FloatValue` (đã = `ElementMultiplier` từ
> `ActionResolver.cs`), bước 4 (Break) dùng `PoiseBroken` có sẵn, bước 5 (Ultimate) dùng cạnh xuống
> của `BattleState.UltimateGauge` (đầy→0) thay vì event `UltimateCharged` (event đó chỉ báo "đầy",
> không báo "đã dùng"). Đọc `CombatEventQueue.All` (không `TryDequeue`) để không tranh index đọc
> với `CombatPresenter`. 1 event mới thật sự cần thêm: `BattleSceneInstaller.OnCommandResolved`
> (grade Action Command vốn chỉ tồn tại trong closure private, không nơi nào khác quan sát được).
> `RunContext.PendingBattle.IsTutorial` (bool, theo đúng pattern `Formation`) +
> `MetaSceneInstaller.LaunchBattle` set cờ = `!ProgressDto.TutorialCompleted`, không phân biệt loại
> node (node map luôn đặt Boss cuối cùng nên trận đầu tiên tự nhiên luôn là Battle thường). Hoàn
> tất/Skip lưu ngay trong Battle scene (cùng kỹ thuật `ProfileContext.Current`+
> `IPlayerRepository.SaveAsync` như `SaveSnapshot` E17 có sẵn), không vòng qua
> `BattleOutcome`/`BattleResultProcessor`. **Test đầu tiên chạm `Game.CombatView`** — phải thêm
> `"Game.CombatView"` vào `references` của `Game.Tests.EditMode.asmdef` (an toàn 1 chiều, không
> asmdef sản phẩm nào tham chiếu ngược). **564/564 xanh** (547 cũ + 17 mới
> `TutorialControllerTests`). Verify qua `execute_code` dựng `BattleSceneInstaller` thật (không
> mock), đi hết 1 lượt `WireTutorial()`→`Skip()`→`Done`, xác nhận `TutorialCompleted` lật đúng +
> file save ghi thật (dùng `LocalPlayerRepository` trỏ thư mục scratch tạm, dọn sạch
> `ServiceLocator.Clear()` ngay sau — phát hiện quan trọng: `ServiceLocator.Register` THROW nếu gọi
> 2 lần, phải dọn residual registration trước khi trả lại phiên Editor cho người dùng).
> **Localization mở rộng — PHẦN LÕI NAY ĐÃ XÂY** (2026-08-12, task-phase-5-gaps.md Phần D, phần
> cuối cùng trong 5 phần) — `NameKey` hoá ra đã gán sẵn 100% trên cả 24 hero/66 enemy/65 skill
> đúng pattern `{kind}.{id}.name` (khác giả định ban đầu "phải tự gán key"); việc thật chỉ là điền
> `strings.csv` — `Tools/Localization/LocalizationKeyGenerator.cs` (`Tools/Localization/Generate
> Name Keys`) thêm đúng 155 dòng thật, idempotent. `ILocalizationService.GetName(id,kind)` mới +
> fallback title-case — **kiến trúc lặp lại đúng bài học Phần E**: `Game.Services` không tham
> chiếu được `Game.Meta` (nơi `HeroDisplayUtil` sống) nên viết lại logic title-case nhỏ trùng lặp
> có chủ đích thay vì import chéo. Migrate 5/5 file thật sự dùng `HeroDisplayUtil` (9 call site:
> `SummonScreen`/`MetaSceneInstaller`/`TeamSelectScreen`/`HeroDetailScreen`/`CodexScreen`) sang
> `GetName`, giữ `HeroDisplayUtil` làm fallback cuối khi service chưa đăng ký. **Tự phát hiện và
> sửa 1 nhận định sai giữa chừng**: tưởng `BattleHudScreen` không hiện tên hero/enemy nên bỏ qua —
> grep lại thấy NÓ CÓ hiện (`DefId.ToUpperInvariant()` thô, không qua `HeroDisplayUtil`, một quy
> ước khác hẳn) — sửa lại tài liệu thay vì để sai. 7 test mới vào `LocalizationServiceTests.cs` có
> sẵn (đọc thẳng `strings.csv` thật, không mock). Verify qua `execute_code` gọi `CodexScreen.
> Open()` PUBLIC thật với profile sở hữu hero/enemy thật, đọc `_nameLabels[i].text` — hiện đúng
> "Beast Tamer"/"Boss Alpha Wolf" thay vì "???"/key thô. **571/571 xanh** (564 cũ + 7 mới). **Còn
> để dành lượt sau (item 4 §D1, phạm vi quá lớn cho 1 lượt)**: nhãn nút/tiêu đề hard-code ở
> Shop/Quest/Mail/Dungeon/Tower/TrialBoss/NodeChoice/Inventory Screen, và tên hero/enemy TRONG
> TRẬN (`BattleHudScreen` vẫn `DefId` thô). **5/5 phần task-phase-5-gaps.md (C/A/E/B/D) nay đã
> xong phần lõi** — không còn phần nào trong danh sách gốc, chỉ còn các "để dành lượt sau" đã ghi
> rõ trong từng phần.
> **Font tiếng Việt (rủi ro R6) NAY ĐÃ KIỂM + SỬA THẬT** (2026-08-12, người dùng chọn sau khi cả 5
> phần task-phase-5-gaps.md xong — không thuộc task file nào, việc mới độc lập) — kiểm tra thật lần
> đầu tiên phát hiện: TMP `LiberationSans SDF` (atlas STATIC, dùng ở `Game.UI`/`Game.CombatView` —
> `BattleHudScreen`/`ActionCommandUI`/`TutorialOverlay`) thiếu 54/73 ký tự dấu tiếng Việt; Legacy
> `UnityEngine.UI.Text`/`LegacyRuntime.ttf` (mọi màn `Game.Meta`) đủ 73/73, không vấn đề gì. Sửa:
> gán `sourceFontFile`=`LiberationSans.ttf` (file .ttf nguồn ĐÃ có đủ glyph, chỉ atlas bake sẵn lúc
> tạo thiếu) + `atlasPopulationMode` `Static`→`Dynamic` qua `SerializedObject` (3 field
> `m_SourceFontFile`/`m_SourceFontFileGUID`/`m_SourceFontFilePath` phải set cùng lúc). **Phát hiện
> kỹ thuật quan trọng**: `TMP_FontAsset.HasCharacter(c, tryAddCharacter:true)` gọi ngoài 1 lượt
> render TMP_Text thật KHÔNG đáng tin (trả `false` dù font render đúng, kết quả phụ thuộc thứ tự
> test chạy trước trong cùng phiên) — cách kiểm ĐÚNG duy nhất: dựng `TMP_Text` thật, gọi
> `ForceMeshUpdate()`, đọc `characterInfo[i].textElement` (null = tofu). Test viết lần đầu theo
> cách sai (`HasCharacter` tĩnh) bị flaky — phát hiện qua chính lần chạy full suite (fail) khác
> lần chạy riêng lẻ (pass, do "rò" state từ test khác chạy trước) — sửa lại đúng trước khi báo
> xong. `Assets/Tests/EditMode/UI/VietnameseFontTests.cs` mới (49 test, mỗi ký tự tự
> dựng+render+dọn độc lập, không phụ thuộc test khác) — thêm `Unity.TextMeshPro` vào
> `Game.Tests.EditMode.asmdef`. **620/620 xanh** (571 cũ + 49 mới), xác nhận chạy độc lập lẫn sau
> domain reload fresh (không phải may mắn nhờ state cũ).

> **CẬP NHẬT 2026-08-18 — dọn nợ 5 task-*.md chưa từng ghi vào §12/§12.1** (roadmap.md/object-map.md
> không được cập nhật từ commit đầu tiên tới giờ dù có sẵn quy tắc §11 yêu cầu, phát hiện qua audit
> `git log -- roadmap.md object-map.md` chỉ ra đúng 1 commit — bản thân initial commit). Ghi bù, KHÔNG
> đổi thực chất phần đã làm:
> - **`task-ui-vfx-polish.md`** — bộ pixel-art UI kit dùng chung (`pixel_blue/green/bronze/metal
>   panel`), TopBar redesign icon-only (nay 10→9→11 icon qua các đợt sau, xem dưới), Landscape
>   (`LayoutProfileSwitcher`) phủ 11/12 màn Meta dạng modal + TeamSelect + 4 panel Battle HUD còn
>   lại, verify bằng world-space geometry thật (không chỉ đọc field) sau khi phát hiện 1 lần đo giả
>   (Portrait/Landscape đọc trùng nhau do chưa gọi `ApplyTo` tường minh). SkillGrid đổi từ Portrait
>   giả (map 1-1 identity) sang thật qua field `LayoutProfile.Scale` (co đều, trước đó không ai
>   dùng field này).
> - **`task-hero-list.md`** — `HeroListScreen`/`UI_HeroList.prefab` mới, roster sở hữu có filter
>   Class + sort Level/Rarity/Name, phân trang 6/trang, nút TopBar thứ 10.
> - **`task-splash-loading.md`** — `SplashCanvas`/`LoadingCanvas` mới trong `Boot.unity`, tip xoay
>   ngẫu nhiên mỗi lần đổi scene. Phát hiện + sửa 1 bug crash THẬT đang ngủ: `TitleCanvas` đã biến
>   mất khỏi scene dù `GameBootstrap.ShowTitleScreen()` vẫn `.Find()` nó không điều kiện (chỉ chưa lộ
>   vì `_autoAdvanceToMeta=true` bỏ qua nhánh đó — cờ này CỐ Ý không đổi, thuộc quyết định người
>   dùng). Cũng gặp + sửa 1 lỗi circular assembly reference thật (Meta/CombatView gọi ngược
>   `Game.Bootstrap` — chặn compile) bằng cách thêm `Game.Core.Scenes.ISceneTransitionService`,
>   đúng mẫu `IUiRootHost` đã có.
> - **`task-defeat-screen.md`** — màn Defeat trước đây chỉ có "CONTINUE" chung chung dù plan.md §4.15
>   mô tả 3 lựa chọn — nay đủ Retry (trận lại, seed mới) / Return to map / Revive with Gem
>   (`CombatSimulation.TryReviveWithGem`, hồi 40% MaxHp giống Revive Feather, resume CHÍNH
>   `CombatSimulation` đang chạy qua `BattleResult.InProgress` thay vì tạo trận mới). 5 test mới.
> - **`task-chapter-arena.md`** — Arena placeholder (icon thứ 11 trên TopBar, giữ 8 icon cũ resize
>   44→38px, bấm ra Toast "Coming Soon", đúng scope v1.0 của plan.md) + `ChapterProgressScreen` (mở
>   qua chạm `TitleLabel` có sẵn thay vì thêm nút, thuần đọc tiến trình 5 chương, không cho chọn lại
>   chương vì `NodeMapGenerator` chỉ sinh theo `ChapterUnlocked` cao nhất — chọn lại cần rework riêng,
>   để ngoài phạm vi).
>
> **Phát hiện phụ khi audit — không thuộc 5 task trên:** `Game.Services.Audio.AudioService.cs` (9
> BGM + SFX thật, `.wav`) tồn tại từ commit đầu tiên nhưng CHƯA TỪNG xuất hiện trong bảng §12 hàng
> `Game.Services` — thuần lỗi ghi chép, không phải việc mới; đã bổ sung vào bảng ở trên. `Landscape`
> vẫn CHƯA phủ cho `Splash`/`Title`/`Loading`/`ChapterProgress`/`HeroList` (5 màn mới nhất) — ghi
> nhận như 1 khoản còn thiếu thật, không phải bỏ sót tài liệu. **637/637 test xanh** (đo trực tiếp
> qua `run_tests`, xem ghi chú ở hàng "Test file" phía trên về lý do không cộng dồn theo delta cũ
> được nữa).

---

*Thiết kế: [plan.md](plan.md) · Cấu trúc: [structure.md](structure.md) · Lộ trình: [roadmap.md](roadmap.md)*

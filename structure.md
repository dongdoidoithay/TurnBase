# STRUCTURE.md — Cây cấu trúc Project

> **Mục đích:** liệt kê **mọi thư mục và file** dự kiến của project, kèm trách nhiệm 1 dòng.
> Dùng để: biết đặt file mới ở đâu · tra nhanh file nào làm gì · kiểm tra không tạo trùng chức năng.
>
> **Bộ tài liệu:** [plan.md](plan.md) · [structure.md](structure.md) *(file này)* · [object-map.md](object-map.md) · [roadmap.md](roadmap.md)
>
> **Ký hiệu trạng thái:** `[ ]` chưa tạo · `[~]` đang làm · `[x]` xong · `[!]` cần refactor
> **Quy ước:** mỗi khi tạo/xoá/đổi tên file → cập nhật file này **và** [object-map.md](object-map.md).

---

## 1. Tổng quan cấp cao nhất

```
TurnBase/                          ← project root
├── _Reference/                    ← ẢNH THAM KHẢO — NGOÀI Assets, không vào build
│   └── UI_SAMPLE/                 ← image_UI.jpg, Game_1.jpg, game_2..4.jpg, palette.gpl
├── Assets/
│   ├── _Project/                  ← TOÀN BỘ nội dung tự làm (dấu _ để nổi lên đầu)
│   ├── Plugins/                   ← thư viện bên thứ 3
│   ├── Settings/                  ← URP asset, Input Actions (Unity sinh sẵn)
│   ├── StreamingAssets/           ← CSV dữ liệu gốc (nếu cần đọc runtime)
│   ├── Tests/                     ← EditMode + PlayMode
│   └── Tools/                     ← Editor tools (asmdef Editor-only)
├── Packages/                      ← manifest.json
├── ProjectSettings/
├── Docs/                          ← tài liệu bổ sung (balance CSV export, ADR)
├── plan.md · structure.md · object-map.md · roadmap.md
├── .gitignore · .gitattributes    ← Git LFS
└── TurnBase.slnx
```

---

## 2. `Assets/_Project/` — cây đầy đủ

```
_Project/
├── Art/
│   ├── Characters/
│   │   ├── Heroes/{hero_id}/          hero_{id}_{clip}.aseprite  (7 clip/hero)
│   │   ├── Enemies/{chapter}/         enemy_{id}_{clip}.aseprite (4 clip)
│   │   └── Bosses/{boss_id}/          boss_{id}_{clip}.aseprite  (+ phase2)
│   ├── Environment/
│   │   ├── Tilesets/{biome}/          tileset_{biome}.png + RuleTile asset
│   │   └── Backgrounds/{biome}/       bg_{biome}_{layer1..3}.png
│   ├── VFX/                           vfx_{tên}.png (sprite sheet 8–12 frame)
│   ├── UI/
│   │   ├── Frames/                    9-slice panel, khung, viền rarity
│   │   ├── Buttons/                   normal/hover/pressed/disabled
│   │   ├── Icons/
│   │   │   ├── Skills/                icon_skill_{id}.png
│   │   │   ├── Items/                 icon_item_{id}.png
│   │   │   ├── Status/                icon_status_{id}.png
│   │   │   ├── Currency/              icon_currency_{id}.png
│   │   │   └── Elements/              icon_element_{id}.png (có hình dạng khác nhau — mù màu)
│   │   └── Portraits/                 portrait_{id}.png (64×64)
│   ├── Fonts/                         font pixel + TMP asset (ĐỦ DẤU TIẾNG VIỆT)
│   └── Atlases/                       SpriteAtlas: UI, Icons, Units, VFX
├── Audio/
│   ├── BGM/                           bgm_{tên}.ogg (11 file)
│   ├── SFX/
│   │   ├── Battle/                    sfx_battle_{tên}.wav
│   │   ├── UI/                        sfx_ui_{tên}.wav
│   │   └── Ambient/                   sfx_amb_{tên}.wav
│   └── Mixers/                        MasterMixer.mixer (4 group)
├── Data/                              ← ScriptableObject (sinh từ CSV)
│   ├── Heroes/                        Hero_{Name}.asset          (24)
│   ├── Skills/                        Skill_{Name}.asset         (~140)
│   ├── Passives/                      Passive_{Name}.asset       (~48)
│   ├── Enemies/                       Enemy_{Name}.asset         (60)
│   ├── Bosses/                        Boss_{Name}.asset + BossPhase_*.asset (8)
│   ├── AIProfiles/                    AI_{Name}.asset            (~20)
│   ├── Status/                        Status_{Name}.asset        (22)
│   ├── Items/                         Item_{Name}.asset          (~30)
│   ├── Equipment/                     Eq_{Name}.asset            (~80)
│   ├── SetBonuses/                    Set_{Name}.asset           (8)
│   ├── Chapters/                      Chapter_{n}.asset          (5)
│   ├── Stages/                        Stage_{ch}_{n}.asset       (~70)
│   ├── EventNodes/                    Event_{Name}.asset         (30)
│   ├── LootTables/                    Loot_{Name}.asset          (~25)
│   ├── Gacha/                         GachaPool_{Name}.asset     (3)
│   ├── Shops/                         Shop_{Name}.asset          (4)
│   ├── Quests/                        Quest_{Name}.asset         (~60)
│   ├── Achievements/                  Ach_{Name}.asset           (~50)
│   ├── Formations/                    Formation_{Name}.asset     (8)
│   ├── Dungeons/                      Dungeon_{Name}.asset       (4 + Tower + Trial)
│   ├── Curves/                        LevelCurve, EnhanceTable, AscendCost
│   ├── Balance/                       BalanceConstants.asset  ← MỌI hằng số cân bằng
│   └── GameDatabase.asset             ← registry tra cứu mọi Definition theo Id
├── Localization/
│   ├── vi.csv · en.csv                key,value
│   └── LocalizationTable.asset
├── Prefabs/
│   ├── Units/
│   │   ├── Unit_HeroBase.prefab       ← gốc chung cho mọi hero (variant theo hero)
│   │   ├── Unit_EnemyBase.prefab
│   │   ├── Unit_BossBase.prefab
│   │   └── Unit_Minion.prefab
│   ├── VFX/                           VFX_{tên}.prefab (pooled)
│   ├── UI/
│   │   ├── Screens/                   UI_{ScreenId}.prefab (23 màn)
│   │   ├── Widgets/                   UI_SkillSlot, UI_TurnOrderCell, UI_HeroCard,
│   │   │                              UI_EquipSlot, UI_StatRow, UI_RedDot,
│   │   │                              UI_CurrencyBar, UI_Tooltip, UI_ConfirmDialog,
│   │   │                              UI_Toast, UI_DamageMeterRow, UI_NodeMapNode
│   │   └── Battle/                    UI_HeroPanel, UI_EnemyPanel, UI_SkillGrid,
│   │                                  UI_TurnOrderBar, UI_ItemBar, UI_StatsEqPanel,
│   │                                  UI_ActionCommand, UI_DamageNumber
│   └── Systems/                       Sys_ServiceRoot, Sys_UIRoot, Sys_AudioRoot
├── Scenes/
│   ├── Boot.unity                     ← khởi tạo service, load save, chuyển Meta
│   ├── Meta.unity                     ← toàn bộ màn hình ngoài trận
│   ├── Battle.unity                   ← sân khấu + Battle HUD
│   └── Sandbox.unity                  ← scene thử nghiệm cho dev (không vào build)
├── Scripts/                           ← xem §3
├── Settings/
│   ├── URP/                           UniversalRP.asset, Renderer2D.asset
│   ├── Input/                         GameInputActions.inputactions
│   └── Addressables/                  AddressableAssetSettings + group
└── Shaders/                           PixelDissolve.shader, HitFlash.shader, Grayscale.shader
```

---

## 3. `Assets/_Project/Scripts/` — từng file & trách nhiệm

### 3.1. `Core/` — `Game.Core.asmdef` (không phụ thuộc gì)

| File | Trách nhiệm |
|---|---|
| `ServiceLocator.cs` | Đăng ký/lấy service; điểm truy cập duy nhất được phép "toàn cục" |
| `IService.cs` | Marker + `Initialize()`/`Dispose()` |
| `Events/IEventBus.cs` · `EventBus.cs` | Pub/sub cho meta layer (không dùng cho combat) |
| `Events/GameEvents.cs` | Danh mục event meta: `CurrencyChanged`, `HeroLevelUp`, `QuestCompleted`... |
| `Fsm/IState.cs` · `StateMachine.cs` | FSM chung (dùng cho GameState & BattleState) |
| `Pooling/IPoolable.cs` · `ObjectPool.cs` · `PoolService.cs` | Pool generic, 0 GC |
| `Random/IRandomSource.cs` | Interface RNG — **mọi** ngẫu nhiên đi qua đây |
| `Random/XorShiftRandom.cs` | RNG deterministic, seed 64-bit |
| `Random/WeightedPicker.cs` | Chọn theo trọng số (loot, gacha, AI) |
| `Math/FixedMath.cs` | Clamp/Lerp/Round không phụ thuộc `UnityEngine.Mathf` |
| `Time/IGameClock.cs` · `SystemGameClock.cs` | Thời gian (v2 đổi sang server time) |
| `Collections/RingBuffer.cs` · `PooledList.cs` | Cấu trúc dữ liệu 0-alloc |
| `Result.cs` | `Result<T>` — trả lỗi không dùng exception |
| `Logging/GameLog.cs` | Log có tag, tắt được ở release |
| `Extensions/*.cs` | Extension methods (List, string, Transform) |

### 3.2. `Data/` — `Game.Data.asmdef` (← Core)

**Definitions (ScriptableObject) — 24 loại:**

| File | Nội dung chính |
|---|---|
| `Definitions/HeroDefinition.cs` | Id, class, element, rarity, base/growth stats, 5 skill, passive, awakening, poise, portrait, prefab |
| `Definitions/SkillDefinition.cs` | Cost, CD, power, hitCount, target, element, poiseDamage, commandType, statusApplies, vfx/sfx key |
| `Definitions/PassiveDefinition.cs` | Trigger (`OnBattleStart/OnHit/OnKill/OnDamaged/OnTurnStart/HpBelow`), effect |
| `Definitions/EnemyDefinition.cs` | Archetype, stats, skill, aiProfile, poise, loot |
| `Definitions/BossPhaseDefinition.cs` | Ngưỡng HP, aiProfile phase, signatureMove, enrageRound |
| `Definitions/AIProfileDefinition.cs` | Danh sách `AIRule{Condition, SkillId, Weight, Cooldown}` |
| `Definitions/StatusDefinition.cs` | Nhóm, stack max, duration, tick timing, dispel type, icon, modifier stat |
| `Definitions/ItemDefinition.cs` | Loại, hiệu ứng, giá, stack max |
| `Definitions/EquipmentDefinition.cs` | Slot, rarity, mainStat, subStatPool, setId |
| `Definitions/SetBonusDefinition.cs` | Hiệu ứng 2 món / 4 món |
| `Definitions/ChapterDefinition.cs` | Biome, số node, stageLevel range, boss, enemy pool |
| `Definitions/StageDefinition.cs` | Đội hình địch, stageLevel, loot table, giới hạn lượt |
| `Definitions/NodeDefinition.cs` | Loại node, tỉ lệ, cost energy |
| `Definitions/EventNodeDefinition.cs` | Mô tả + 2–3 lựa chọn (điều kiện, kết quả) |
| `Definitions/LootTableDefinition.cs` | Danh sách `{itemId, weight, minMax}` |
| `Definitions/GachaPoolDefinition.cs` | Tỉ lệ, pity, danh sách hero theo rarity |
| `Definitions/ShopDefinition.cs` | Danh mục hàng, giá, refresh |
| `Definitions/QuestDefinition.cs` | Loại (daily/weekly/chain), điều kiện, thưởng |
| `Definitions/AchievementDefinition.cs` | Điều kiện, thưởng, ẩn/hiện |
| `Definitions/FormationDefinition.cs` | Bố trí slot, bonus |
| `Definitions/DungeonDefinition.cs` | 10 tầng, ngày mở, thưởng |
| `Definitions/BattlePassDefinition.cs` | 50 mốc, free/premium track |
| `Definitions/LevelCurveDefinition.cs` | AnimationCurve/bảng EXP & vàng theo level |
| `Definitions/BalanceConstants.cs` | **Mọi hằng số cân bằng** (ATB threshold, grade multiplier, cap stat...) |

**Enums / Struct / DTO:**

| File | Nội dung |
|---|---|
| `Enums/Element.cs` | Fire, Water, Earth, Wind, Light, Dark, Neutral |
| `Enums/HeroClass.cs` | Vanguard, Slayer, Arcanist, Warden, Trickster, Summoner |
| `Enums/Rarity.cs` | Common..Mythic |
| `Enums/StatType.cs` | STR..LUK + derived |
| `Enums/DamageType.cs` | Physical, Magical, True |
| `Enums/SkillType.cs` | Physical, Magical, Heal, Support, Summon, Tactic |
| `Enums/TargetMode.cs` | 11 mode (plan §4.12) |
| `Enums/ActionCommandType.cs` | SingleTap, Combo, Charge, Guard, None |
| `Enums/StatusId.cs` | 22 status (plan §4.11) |
| `Enums/NodeType.cs` | Battle, Elite, Boss, Shop, Rest, Treasure, Event, Mystery |
| `Enums/CurrencyType.cs` | Gold, Gem, Energy, Ticket, Honor, Shard, Essence, Core, Stone |
| `Enums/EquipSlot.cs` | Weapon..Amulet |
| `Enums/Row.cs` | Front, Back |
| `Structs/PrimaryStats.cs` | 6 stat gốc, có toán tử `+`, `×` |
| `Structs/DerivedStats.cs` | 15 stat dẫn xuất |
| `Structs/StatusApplication.cs` | `{statusId, chance, duration, stacks, targetSelf}` |
| `Structs/StatModifier.cs` | `{statType, flat, percent, source}` |
| `Dto/PlayerProfileDto.cs` | Gốc của save (plan §11.6) |
| `Dto/HeroInstanceDto.cs` | uid, defId, level, exp, star, skillLevels, equipped |
| `Dto/EquipmentInstanceDto.cs` | uid, defId, rarity, level, mainStat, subStats, locked |
| `Dto/WalletDto.cs` · `InventoryDto.cs` | Tiền tệ, vật phẩm |
| `Dto/RunStateDto.cs` | Chapter, seed, mapNodes, currentNode, team, battleSnapshot |
| `Dto/GachaStateDto.cs` | pullsSinceLegendary/Epic, history |
| `Dto/QuestProgressDto.cs` · `SettingsDto.cs` · `StatsDto.cs` | |
| `Dto/TeamSnapshotDto.cs` | Cho Arena async (v1.1) — thiết kế sẵn từ v1 |
| `Database/GameDatabase.cs` | Registry `Id → Definition` cho mọi loại; `Get<T>(id)` |
| `Database/IDefinitionProvider.cs` | Trừu tượng để test không cần Unity asset |

### 3.3. `Combat/` — `Game.Combat.asmdef` (← Core, Data) ⛔ C# THUẦN

| File | Trách nhiệm |
|---|---|
| `CombatSimulation.cs` | Điểm vào: `Start(config)`, `SubmitIntent(intent)`, `Advance()`; sở hữu `BattleState` |
| `BattleConfig.cs` | Input dựng trận: seed, đội hình 2 phe, stage rule, giới hạn lượt |
| `BattleState.cs` | Toàn bộ trạng thái trận: units, round, turnQueue, ultimateGauge, result |
| `ActionIntent.cs` | `{actorId, skillId, targetIds[], grade}` — cũng là đơn vị của replay |
| `Model/CombatUnit.cs` | Runtime unit: HP/SP/ATB/Poise/statuses/cooldowns/row/element |
| `Model/CombatTeam.cs` | Nhóm unit + truy vấn (sống, hàng trước, thấp HP nhất) |
| `Model/StatusInstance.cs` | `{statusId, stacks, remainingTurns, sourceUnitId, value}` |
| `Model/SkillRuntime.cs` | Trạng thái skill của 1 unit: cooldown còn lại, level |
| `Model/StatBlock.cs` | Tính stat cuối = base + equip + set + buff − debuff, có cache invalidation |
| `Systems/TurnScheduler.cs` | Tick ATB, chọn actor, `PreviewOrder(n)` |
| `Systems/ActionResolver.cs` | **Pipeline 14 bước** (plan §4.3) |
| `Systems/DamageCalculator.cs` | Công thức damage (plan §4.6) |
| `Systems/HealCalculator.cs` | Hồi máu, chặn bởi `curse`, cắt tại MaxHP |
| `Systems/StatusProcessor.cs` | Apply/tick/expire/stack/resist/dispel/cleanse |
| `Systems/PoiseSystem.cs` | Trừ Poise, Break, hồi, kéo dài debuff |
| `Systems/TargetSelector.cs` | 11 TargetMode + taunt override + auto-suggest |
| `Systems/ElementTable.cs` | Bảng 6×6 (plan §4.7) |
| `Systems/FormationSystem.cs` | Row modifier, tự động dồn hàng, preset bonus |
| `Systems/SynergyResolver.cs` | Bonus đội hình theo class/element |
| `Systems/UltimateGauge.cs` | Nạp/tiêu gauge chung |
| `Systems/ActionCommandEvaluator.cs` | Chấm `Perfect/Good/Miss` từ timing (thuần logic, không UI) |
| `Systems/PassiveProcessor.cs` | Kích hoạt passive theo trigger |
| `Systems/SummonSystem.cs` | Minion: tạo, ATB riêng, hết hạn, chủ chết |
| `Ai/AIController.cs` | Chọn hành động cho unit AI |
| `Ai/UtilityScorer.cs` | Chấm điểm rule |
| `Ai/AICondition.cs` | 12 loại điều kiện (plan §4.13) |
| `Ai/BossPhaseController.cs` | Đổi phase, signature move, enrage |
| `Ai/IntentPredictor.cs` | Tính intent lượt sau bằng RNG peek |
| `Ai/AutoBattlePolicy.cs` | Policy 7 bước cho Auto (plan §4.16) |
| `Events/CombatEvent.cs` | Base struct + enum loại |
| `Events/CombatEvents.*.cs` | 22 struct event (plan §11.4) |
| `Events/CombatEventQueue.cs` | Hàng đợi 0-alloc |
| `Replay/ReplayData.cs` | `{seed, configId, intents[]}` |
| `Replay/ReplayVerifier.cs` | Chạy lại và so kết quả |
| `Rewards/BattleRewardCalculator.cs` | Vàng/EXP theo công thức plan §4.15 |
| `Exceptions/BattleStalemateException.cs` | Chống vòng lặp vô hạn |

### 3.4. `CombatView/` — `Game.CombatView.asmdef` (← Core, Data, Combat, Services)

| File | Trách nhiệm |
|---|---|
| `BattleSceneInstaller.cs` | Dựng scene Battle: spawn unit, gắn HUD, khởi tạo simulation |
| `BattleController.cs` | Cầu nối Input ↔ Simulation; quản lý `AwaitInput` |
| `CombatPresenter.cs` | Đọc `CombatEventQueue`, diễn tuần tự, quản lý tốc độ ×1/×2/×3 |
| `EventPlaybackScheduler.cs` | Định thời từng event (chờ animation xong mới sang event kế) |
| `BattleStageLayout.cs` | Toạ độ 4 slot hero + 5 slot địch (plan §4.1), khác nhau P/L |
| `Units/UnitView.cs` | Đại diện 1 unit trên màn: nhận event, gọi animator/vfx |
| `Units/UnitAnimator.cs` | Điều khiển clip, phát animation event `OnHit`/`OnCast` |
| `Units/UnitHealthBar.cs` | HP bar nổi (theo `game_2.jpg`) |
| `Units/UnitPoiseBar.cs` | Poise bar vàng |
| `Units/UnitStatusIcons.cs` | ≤6 icon status + tooltip |
| `Units/UnitIntentIcon.cs` | Icon ý định địch |
| `Units/UnitSelectionRing.cs` | Vòng sáng dưới chân khi được chọn/đang tới lượt |
| `Effects/DamageNumber.cs` · `DamageNumberPool.cs` | Số damage bay lên (pooled) |
| `Effects/FloatingTextLayer.cs` | MISS / RESIST / PERFECT / BREAK |
| `Effects/VfxPlayer.cs` | Phát VFX theo `VfxKey` (pooled, Addressables) |
| `Effects/HitStop.cs` | Dừng thời gian ngắn |
| `Effects/ScreenShake.cs` | Rung camera theo cường độ |
| `Effects/CameraDirector.cs` | Zoom crit, pan boss, cutscene Ultimate |
| `Effects/BreakEffect.cs` | Chớp trắng + shatter |
| `Effects/DissolveDeath.cs` | Shader dissolve khi chết |
| `ActionCommand/ActionCommandUI.cs` | Điều phối 4 loại cửa sổ nhịp |
| `ActionCommand/TimingRing.cs` | Vòng tròn thu nhỏ (SingleTap) |
| `ActionCommand/ComboPrompt.cs` | Metronome n nhịp |
| `ActionCommand/ChargeMeter.cs` | Thanh giữ-nhả |
| `ActionCommand/GuardPrompt.cs` | Cửa sổ đỡ đòn |
| `ActionCommand/InputLatencyCalibrator.cs` | Đo & lưu offset độ trễ (plan §4.8.3) |

### 3.5. `Meta/` — `Game.Meta.asmdef` (← Core, Data, Combat, Services)

| File | Trách nhiệm |
|---|---|
| `Hero/HeroService.cs` | CRUD hero instance, truy vấn, sắp xếp |
| `Hero/HeroInstance.cs` | Model runtime: def + level + star + skillLevels + equip |
| `Hero/HeroStatResolver.cs` | Tính stat cuối cùng (base + level + star + equip + set + synergy) |
| `Hero/HeroLevelSystem.cs` | Cộng EXP, lên level, cap theo star |
| `Hero/AscendSystem.cs` | Nâng sao: kiểm tài nguyên, trừ, mở slot/ultimate/awakening |
| `Hero/SkillUpgradeSystem.cs` | Nâng skill 1→8 |
| `Hero/AwakeningSystem.cs` | Mở passive awakening ở ★6 |
| `Hero/PowerScoreCalculator.cs` | Điểm sức mạnh để so sánh nhanh |
| `Equipment/EquipmentService.cs` | Gắn/tháo/khoá/bán |
| `Equipment/EquipmentInstance.cs` | Model runtime |
| `Equipment/EquipmentGenerator.cs` | Roll main/sub stat theo rarity (deterministic) |
| `Equipment/EnhanceSystem.cs` | +0→+15, tỉ lệ thất bại, mở sub stat |
| `Equipment/ReforgeSystem.cs` | Đổi 1 sub stat |
| `Equipment/SetBonusResolver.cs` | Tính bonus 2/4 món |
| `Inventory/InventoryService.cs` | Item tiêu hao, vật liệu, stack |
| `Economy/EconomyService.cs` | **Cổng duy nhất** cho mọi thay đổi tiền tệ (`TryConsume`/`Grant`) |
| `Economy/CurrencyWallet.cs` | Lưu số dư |
| `Economy/EnergySystem.cs` | Hồi theo thời gian, cap, chống đổi giờ máy |
| `Economy/TransactionLog.cs` | Nhật ký giao dịch (debug + analytics + chống gian lận) |
| `Gacha/GachaService.cs` | Summon 1/10 lần, chuyển trùng thành mảnh |
| `Gacha/PitySystem.cs` | Soft/hard pity (plan §9.3) |
| `Progression/RunController.cs` | Vòng đời 1 run: bắt đầu, chọn node, kết thúc |
| `Progression/NodeMapGenerator.cs` | Thuật toán sinh map (plan §8.1) + BFS xác thực |
| `Progression/RunState.cs` | Trạng thái run hiện tại |
| `Progression/ChapterService.cs` | Mở khoá chương, tiến độ |
| `Progression/DifficultyScaler.cs` | Scale stat địch theo stageLevel |
| `Progression/RewardResolver.cs` | Ghép thưởng từ loot table + bonus |
| `Progression/LootRoller.cs` | Roll loot deterministic |
| `Progression/EventNodeResolver.cs` | Xử lý lựa chọn ở node Event |
| `Battle/BattleLauncher.cs` | Dựng `BattleConfig` từ team + stage → sang scene Battle |
| `Battle/BattleResultProcessor.cs` | Nhận kết quả → cộng thưởng → cập nhật quest/analytics |
| `Team/TeamService.cs` | Đội hình đang chọn, 5 preset lưu sẵn |
| `Team/FormationService.cs` | Áp preset đội hình |
| `Quest/QuestService.cs` | Tiến độ, hoàn thành, nhận thưởng |
| `Quest/DailyResetService.cs` | Reset 00:00 UTC+7, chống đổi giờ |
| `Quest/AchievementService.cs` | Thành tựu |
| `Quest/BattlePassService.cs` | 50 mốc, free/premium |
| `Dungeon/DungeonService.cs` | 4 hầm luân phiên theo ngày |
| `Dungeon/TowerService.cs` | Tháp 100 tầng, HP không hồi |
| `Dungeon/TrialBossService.cs` | Boss tuần + bảng xếp hạng damage |
| `Collection/CollectionService.cs` | Codex hero/enemy/item đã gặp |
| `MetaEvents.cs` | Danh mục event meta phát qua `IEventBus` |

### 3.6. `UI/` — `Game.UI.asmdef` (← Core, Data, Meta, Services)

**Core UI:**

| File | Trách nhiệm |
|---|---|
| `Core/UIScreen.cs` | Base: `OnShow(data)`, `OnHide()`, `OnBack()` |
| `Core/UIScreenStack.cs` | Push/Pop/Replace/PopToRoot (plan §10.3) |
| `Core/UIManager.cs` | Điểm vào: `Show(ScreenId, data)`; quản lý overlay |
| `Core/ScreenId.cs` | Enum 23 màn |
| `Core/UIRegistry.cs` | `ScreenId → Addressable prefab key` |
| `Core/UITransition.cs` | Fade/slide 200 ms |
| `Core/LayoutProfileSwitcher.cs` | **Chuyển preset Portrait/Landscape** |
| `Core/RectTransformPreset.cs` | Struct lưu anchor/pivot/size/pos |
| `Core/SafeAreaFitter.cs` | Notch/lỗ camera |
| `Core/ScreenOrientationService.cs` | Phát `OnOrientationChanged` |
| `Core/UIBinder.cs` | Bind ViewModel → widget, tự huỷ đăng ký |

**Widgets dùng chung:**

| File | Trách nhiệm |
|---|---|
| `Widgets/CurrencyBar.cs` | Top bar tiền tệ + số chạy khi thay đổi |
| `Widgets/RedDot.cs` · `RedDotService.cs` | Chấm đỏ có phân cấp (plan §10.6) |
| `Widgets/TooltipController.cs` | Tooltip skill/item/status |
| `Widgets/ConfirmDialog.cs` · `ToastService.cs` | Hộp thoại, thông báo ngắn |
| `Widgets/LoadingOverlay.cs` | Loading + mẹo chơi |
| `Widgets/VirtualScrollList.cs` | List ảo hoá (inventory nhiều nghìn món) |
| `Widgets/HeroCard.cs` · `EquipCard.cs` | Ô hiển thị hero/trang bị |
| `Widgets/StatRow.cs` · `StatCompareRow.cs` | Dòng stat, có so sánh ↑↓ |
| `Widgets/RarityFrame.cs` · `ElementIcon.cs` · `StarDisplay.cs` | |
| `Widgets/RewardPopup.cs` | Popup nhận thưởng dùng chung |

**Screens (mỗi màn 1 file `*Screen.cs` + tuỳ chọn `*ViewModel.cs`):**

`SplashScreen · TitleScreen · HomeScreen · HeroListScreen · HeroDetailScreen · EquipmentScreen · EnhanceScreen · InventoryScreen · FormationScreen · SummonScreen · ShopScreen · DungeonScreen · ArenaScreen · QuestScreen · AchievementScreen · CollectionScreen · MailScreen · SettingsScreen · ChapterSelectScreen · NodeMapScreen · PreBattleScreen · ResultScreen · DefeatScreen`

**Battle HUD (`UI/Battle/`):**

| File | Trách nhiệm |
|---|---|
| `BattleHudScreen.cs` | Gốc HUD, điều phối mọi widget trận |
| `SkillGridView.cs` | Lưới 5×3 |
| `SkillSlotView.cs` | 1 ô, **8 trạng thái** (plan §5.5) |
| `TurnOrderBar.cs` · `TurnOrderCell.cs` | 8 lượt kế |
| `HeroPanelView.cs` · `EnemyPanelView.cs` | Panel 2 bên |
| `ItemSlotBar.cs` | 3 ô tiêu hao |
| `StatsEqPanel.cs` | Bảng STATS/EQ |
| `EndTurnButton.cs` | Nút END TURN |
| `AutoSpeedToggle.cs` | Auto ON/OFF + ×1/×2/×3 |
| `DamageMeterView.cs` | Bảng damage (theo `Game_1.jpg`) |
| `ZoneIndicator.cs` | `SWAMPS [1/3]` |
| `TargetHighlighter.cs` | Viền mục tiêu + auto-suggest |
| `BattleMenuPopup.cs` | Tạm dừng / cài đặt / bỏ chạy |
| `TutorialOverlay.cs` | 5 bước tutorial (plan §10.8) |

### 3.7. `Services/` — `Game.Services.asmdef` (← Core, Data)

| File | Trách nhiệm |
|---|---|
| `Save/IPlayerRepository.cs` | `LoadAsync/SaveAsync` — **cổng server-ready** |
| `Save/LocalPlayerRepository.cs` | JSON + AES + HMAC + ghi atomic + backup |
| `Save/SaveSerializer.cs` | DTO ↔ JSON |
| `Save/SaveMigrationRunner.cs` · `ISaveMigration.cs` | Nâng version tuần tự |
| `Save/Migrations/Migration_001_to_002.cs` | Ví dụ migration |
| `Save/CryptoUtil.cs` | AES + HMAC-SHA256 |
| `Audio/IAudioService.cs` · `AudioService.cs` | Phát BGM/SFX, ducking |
| `Audio/SfxPool.cs` · `MusicPlayer.cs` | Pool AudioSource, crossfade |
| `Audio/AudioCueRegistry.cs` | `sfxKey → clip` (Addressables) |
| `Assets/IAssetService.cs` · `AddressableAssetService.cs` | Load/Release theo scope |
| `Scene/ISceneFlowService.cs` · `SceneFlowService.cs` | Additive load + loading screen |
| `Localization/ILocalizationService.cs` · `LocalizationService.cs` | key→value, đổi ngôn ngữ runtime |
| `Localization/LocalizedKey.cs` · `LocalizedText.cs` | Struct key + component TMP tự dịch |
| `Input/IInputService.cs` · `InputService.cs` | Trừu tượng touch/chuột/gamepad, back button |
| `Settings/ISettingsService.cs` · `SettingsService.cs` | Đọc/ghi `SettingsDto`, áp dụng ngay |
| `Analytics/IAnalyticsService.cs` · `NullAnalyticsService.cs` | Bật thật ở P8 |
| `Ads/IAdsService.cs` · `NullAdsService.cs` | Rewarded ad |
| `Store/IStoreService.cs` · `NullStoreService.cs` | IAP |
| `RemoteConfig/IRemoteConfigService.cs` · `LocalRemoteConfig.cs` | Hằng số cân bằng chỉnh từ xa |
| `Platform/IPlatformService.cs` | Haptic, safe area, quyền, ứng dụng nền |
| `Monetization/MonetizationProfile.cs` | Cờ `MobileF2P` / `PremiumPC` (plan §9.5) |

### 3.8. `Bootstrap/` — `Game.Bootstrap.asmdef` (← tất cả)

| File | Trách nhiệm |
|---|---|
| `GameBootstrap.cs` | Entry point trong scene `Boot` |
| `ServiceInstaller.cs` | **Composition root** — đăng ký mọi service |
| `GameStateMachine.cs` | FSM cấp cao |
| `States/BootState.cs` | Khởi tạo service, load save, load database |
| `States/MetaState.cs` | Vào Meta scene, hiện Home |
| `States/BattleState.cs` | Vào Battle scene, chạy trận |
| `States/ShutdownState.cs` | Lưu + dọn dẹp |
| `AppLifecycleHandler.cs` | `OnApplicationPause/Focus/Quit` → auto-save |

---

## 4. `Assets/Tools/` — Editor (asmdef Editor-only)

| File | Trách nhiệm |
|---|---|
| `DataImport/CsvToScriptableObject.cs` | Menu `Tools/Import Game Data` — CSV → SO |
| `DataImport/CsvSchema.cs` | Định nghĩa cột cho từng loại dữ liệu |
| `DataImport/DataValidator.cs` | Kiểm id trùng, tham chiếu chết, số ngoài khoảng, thiếu icon/localization key |
| `Balance/BalanceHarnessWindow.cs` | Chạy N trận headless → CSV win-rate/TTK/số lượt |
| `Balance/BalanceReportExporter.cs` | Xuất báo cáo sang `Docs/balance/` |
| `Map/ObjectMapValidator.cs` | **Đối chiếu `object-map.md` với project thật** — báo script/prefab chưa đăng ký |
| `Map/ObjectMapGenerator.cs` | Sinh lại phần bảng tự động của `object-map.md` |
| `Localization/LocalizationScanner.cs` | Quét chuỗi hard-code trong code & prefab |
| `Localization/PseudoLocaleGenerator.cs` | Sinh locale giả dài 1.6× để test tràn chữ |
| `Addressables/AddressableGroupBuilder.cs` | Tự gán asset vào group theo quy ước |
| `Art/SpriteAtlasBuilder.cs` | Dựng atlas theo thư mục |
| `Art/PixelImportPreset.cs` | Áp preset import (Point filter, PPU 32) cho sprite mới |
| `Scene/StageEditorWindow.cs` | Sửa đội hình địch của Stage trực quan |
| `Debug/BattleDebugWindow.cs` | Chỉnh HP/SP/status runtime, ép Break, ép Perfect |
| `Build/BuildPipelineRunner.cs` | Build Android/iOS/Windows theo profile |

---

## 5. `Assets/Tests/`

```
Tests/
├── EditMode/                      Game.Tests.EditMode.asmdef  (← Combat, Meta, Core, Data)
│   ├── Combat/
│   │   ├── DamageCalculatorTests.cs        25 case
│   │   ├── TurnSchedulerTests.cs           thứ tự, haste/slow, preview
│   │   ├── StatusProcessorTests.cs         22 status × 5 khía cạnh
│   │   ├── PoiseSystemTests.cs
│   │   ├── TargetSelectorTests.cs          11 mode + taunt
│   │   ├── ElementTableTests.cs            36 ô bảng nguyên tố
│   │   ├── ActionCommandEvaluatorTests.cs  cửa sổ timing, buffer
│   │   ├── PassiveProcessorTests.cs
│   │   ├── EdgeCaseTests.cs                ★ 24 case ở plan §4.14
│   │   ├── DeterminismTests.cs             cùng seed → cùng event
│   │   ├── FuzzBattleTests.cs              10.000 trận
│   │   └── GoldenScenarioTests.cs          20 kịch bản + file log kỳ vọng
│   ├── Meta/
│   │   ├── LootRollerTests.cs
│   │   ├── GachaPityTests.cs               1 triệu roll
│   │   ├── EnhanceSystemTests.cs
│   │   ├── EquipmentGeneratorTests.cs
│   │   ├── EconomyServiceTests.cs          không bao giờ âm, log đủ
│   │   ├── EnergySystemTests.cs            chống đổi giờ máy
│   │   ├── NodeMapGeneratorTests.cs        10.000 seed
│   │   └── HeroStatResolverTests.cs        thứ tự áp modifier
│   ├── Services/
│   │   ├── SaveMigrationTests.cs
│   │   └── SaveIntegrityTests.cs           checksum, atomic write
│   └── Architecture/
│       └── AssemblyRuleTests.cs            ★ Combat không ref UI/Random/Time
├── PlayMode/                      Game.Tests.PlayMode.asmdef
│   ├── BootFlowTests.cs                    Boot → Meta không lỗi
│   ├── BattleFlowTests.cs                  vào trận → thắng → Result
│   ├── UIStackTests.cs                     Push/Pop/Back
│   └── ResponsiveLayoutTests.cs            5 tỉ lệ, chụp ảnh so sánh
└── Fixtures/                      Dữ liệu test dùng chung (SO giả, seed cố định)
```

---

## 6. Assembly definitions — bảng tham chiếu

| asmdef | Tham chiếu | Cấm |
|---|---|---|
| `Game.Core` | — | mọi module khác |
| `Game.Data` | Core | Combat, UI, Meta |
| `Game.Combat` | Core, Data | **UI, CombatView, Meta, UnityEngine.Random, Time, DateTime** |
| `Game.CombatView` | Core, Data, Combat, Services, UI | Meta |
| `Game.Meta` | Core, Data, Combat, Services | UI, CombatView |
| `Game.UI` | Core, Data, Meta, Services, Combat *(chỉ đọc)* | CombatView |
| `Game.Services` | Core, Data | Combat, Meta, UI |
| `Game.Bootstrap` | tất cả | — |
| `Game.Tools` (Editor) | tất cả | — |
| `Game.Tests.EditMode` | Core, Data, Combat, Meta, Services | — |
| `Game.Tests.PlayMode` | tất cả | — |

> `AssemblyRuleTests.cs` kiểm tra bảng này tự động ở CI.

---

## 7. Ba scene — nội dung hierarchy

### `Boot.unity` (nhẹ nhất, load đầu tiên)
```
Boot
├── [GameBootstrap]        GameBootstrap.cs, ServiceInstaller.cs
├── [ServiceRoot]          DontDestroyOnLoad — chứa mọi service MonoBehaviour
│   ├── AudioRoot          AudioService, MusicPlayer, SfxPool
│   ├── UIRoot             UIManager, UIScreenStack, Canvas(overlay), ToastService
│   └── InputRoot          InputService, PlayerInput
├── [Camera] BootCamera
└── [Canvas] SplashCanvas  SplashScreen.cs
```

### `Meta.unity`
```
Meta
├── [MetaSceneInstaller]
├── [Camera] MetaCamera (orthographic, Pixel Perfect)
├── [Canvas] MetaCanvas  (Scale With Screen Size)
│   ├── SafeArea          SafeAreaFitter
│   │   ├── TopBar        CurrencyBar
│   │   ├── ScreenHost    ← UIScreenStack instantiate màn hình vào đây
│   │   └── BottomNav     6 nút + RedDot
│   └── OverlayHost       ConfirmDialog, Toast, LoadingOverlay
└── [Background] parallax
```

### `Battle.unity`
```
Battle
├── [BattleSceneInstaller]   BattleSceneInstaller.cs
├── [BattleController]       BattleController.cs, CombatPresenter.cs, EventPlaybackScheduler.cs
├── [Camera] BattleCamera    CameraDirector, ScreenShake, Pixel Perfect
├── [Stage]
│   ├── Background           3 lớp parallax
│   ├── Tilemap              Grid + Tilemap
│   ├── PartySlots           P0..P3 (Transform rỗng, toạ độ plan §4.1)
│   ├── EnemySlots           E0..E4
│   └── VfxLayer             VfxPlayer host (pooled)
├── [Canvas] BattleCanvas
│   ├── SafeArea
│   │   ├── HeroPanel        HeroPanelView
│   │   ├── EnemyPanel       EnemyPanelView
│   │   ├── TurnOrderBar     TurnOrderBar
│   │   ├── SkillGrid        SkillGridView (15 × SkillSlotView)
│   │   ├── ItemBar          ItemSlotBar
│   │   ├── StatsEqPanel     StatsEqPanel
│   │   ├── EndTurnButton    EndTurnButton
│   │   ├── AutoSpeedToggle  AutoSpeedToggle
│   │   ├── ZoneIndicator    ZoneIndicator
│   │   └── DamageMeter      DamageMeterView
│   ├── ActionCommandLayer   ActionCommandUI + 4 biến thể
│   ├── FloatingTextLayer    DamageNumberPool
│   └── OverlayHost          BattleMenuPopup, TutorialOverlay
└── [AudioZone]              BGM biome
```

Chi tiết từng GameObject ↔ script ↔ asset: xem **[object-map.md](object-map.md)**.

---

## 8. Thứ tự tạo file (khớp roadmap)

| Phase | Thư mục ưu tiên |
|---|---|
| P0 | `Core/`, `Services/Save`, `Bootstrap/`, `Tools/DataImport` |
| P1 | `Combat/` (toàn bộ), `CombatView/` (cơ bản), `UI/Battle/SkillGrid` |
| P2 | `Art/`, `Audio/`, `UI/Screens` (NodeMap, PreBattle, Result), `Meta/Progression` |
| P3 | `Combat/Systems` (status/passive/summon), `Combat/Ai` |
| P4 | `Meta/` (Hero, Equipment, Economy, Gacha, Quest) |
| P5 | `Data/` (nội dung), `Localization/` |
| P6 | `UI/Core` (responsive), toàn bộ `UI/Screens` |
| P7 | `Tools/Balance`, tối ưu |
| P8 | `Services/Analytics|Ads|Store`, `Tools/Build` |

---

## 9. Quy tắc bảo trì file này

1. **Tạo file mới** → thêm dòng vào bảng tương ứng ở §3, ghi trách nhiệm 1 dòng.
2. **Xoá/đổi tên** → sửa ở đây trước, rồi mới sửa code (tránh quên).
3. **Thêm thư mục mới** → phải giải thích được tại sao không thuộc thư mục có sẵn.
4. Chạy `Tools/Validate Object Map` trước khi merge — nó so sánh cây thật với tài liệu và báo chênh lệch.
5. File này + [object-map.md](object-map.md) là **một phần của Definition of Done** (plan §16 mục 8).

---

*Thiết kế: [plan.md](plan.md) · Bản đồ đối tượng: [object-map.md](object-map.md) · Lộ trình: [roadmap.md](roadmap.md)*

# object-map-validation.md — Báo cáo Tools/Object Map/Generate Report

> Sinh lúc 2026-08-12 08:38:55 · chỉ báo cáo, KHÔNG tự sửa object-map.md hay code — con người quyết định sửa bên nào.

Quét 3 scene, 14 prefab · docs khai báo 123 script/42 prefab · (a) 88 script trong docs không tồn tại file · (b) 0 script thật chưa đăng ký docs · (c) 34 prefab trong docs không có asset.

## (a) Script trong docs không tồn tại file .cs nào trong Assets/ — 88

- AchievementScreen
- ArenaScreen
- AutoSpeedToggle
- BattleAudioDirector
- BattleController
- BattleLauncher
- BattleMenuPopup
- BattleResultProcessor
- BattleStageLayout
- BossPhaseView
- BottomNavBar
- Camera
- CameraDirector
- Canvas
- CanvasScaler
- ChapterSelectScreen
- ChargeMeter
- CollectionScreen
- ComboPrompt
- ConfirmDialog
- CurrencyBar
- DamageMeterRow
- DamageMeterView
- DamageNumber
- DamageNumberPool
- DefeatScreen
- DontDestroyOnLoad
- EndTurnButton
- EnemyPanelView
- EnergySystem
- EnhanceScreen
- EnhanceSystem
- EquipCard
- EquipmentScreen
- EventPlaybackScheduler
- FloatingTextLayer
- FormationScreen
- GachaService
- GraphicRaycaster
- GuardPrompt
- HeroCard
- HeroListScreen
- HeroPanelView
- HeroStatResolver
- HomeScreen
- IAssetService
- InputService
- ItemSlotBar
- LayoutProfileSwitcher
- LoadingOverlay
- MusicPlayer
- NodeMapNodeView
- NodeMapScreen
- ParallaxLayer
- PitySystem
- PixelPerfectCamera
- PlayerInput
- PoolService
- PreBattleScreen
- RedDot
- ResultScreen
- RewardRow
- SafeAreaFitter
- ScreenShake
- SfxPool
- SkillGridView
- SplashScreen
- StatBlock
- StatCompareRow
- StatRow
- StatsEqPanel
- StatusIconView
- TargetHighlighter
- TimingRing
- TitleScreen
- Toast
- ToastService
- ToastView
- TooltipController
- TurnOrderBar
- TurnOrderCell
- TutorialOverlay
- UI_DamageMeterRow
- UIManager
- UIScreenStack
- UnitAnimator
- UnitIntentIcon
- ZoneIndicator

## (b) Script thật (gắn trong scene/prefab) chưa đăng ký ở object-map.md §3/§4 — 0

_Không có._

## (c) Prefab nêu trong docs không có asset khớp tên — 34

- UI_Achievement
- UI_Arena
- UI_ChapterSelect
- UI_Collection
- UI_ConfirmDialog
- UI_CurrencyBar
- UI_DamageMeterRow
- UI_DamageNumber
- UI_Defeat
- UI_Enhance
- UI_EquipCard
- UI_Equipment
- UI_Formation
- UI_HeroList
- UI_Home
- UI_NodeMap
- UI_NodeMapNode
- UI_PreBattle
- UI_RedDot
- UI_Result
- UI_RewardRow
- UI_Settings
- UI_SkillSlot
- UI_Splash
- UI_StatRow
- UI_StatusIcon
- UI_Title
- UI_Toast
- UI_Tooltip
- UI_TurnOrderCell
- Unit_BossBase
- Unit_EnemyBase
- Unit_HeroBase
- Unit_Minion


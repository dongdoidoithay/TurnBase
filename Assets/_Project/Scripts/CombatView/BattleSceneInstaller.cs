using System.Collections.Generic;
using System.Linq;
using Game.Combat;
using Game.Combat.Ai;
using Game.Combat.Model;
using Game.CombatView.ActionCommand;
using Game.CombatView.Effects;
using Game.CombatView.Tutorial;
using Game.CombatView.Units;
using Game.Core;
using Game.Data;
using Game.Data.Dto;
using Game.Meta.Content;
using Game.Meta.Battle;
using Game.Meta.Dungeon;
using Game.Meta.Endgame;
using Game.Meta.Equipment;
using Game.Services.Economy;
using Game.Services.Save;
using Game.Services.Settings;
using Game.UI.Battle;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.CombatView
{
    /// <summary>
    /// Dựng scene Battle: sinh unit, gắn view, khởi tạo simulation.
    /// Ở P1 dùng đội hình mẫu; P4 sẽ nhận BattleConfig từ BattleLauncher (Game.Meta).
    /// Toạ độ slot theo plan.md §4.1.
    /// </summary>
    public sealed class BattleSceneInstaller : MonoBehaviour
    {
        [Header("Cấu hình trận")]
        [SerializeField] private ulong _seed = 20260807;
        [SerializeField] private int _turnLimit;
        [SerializeField] private bool _autoPlay = false;
        [SerializeField, Range(1, 3)] private int _speed = 1;

        [Header("Sprite tạm (P1) — P2 thay bằng Addressables")]
        [SerializeField] private Sprite[] _heroSprites;
        [SerializeField] private Sprite[] _enemySprites;

        [Header("__Stage__/__UI__ dựng sẵn trên Hierarchy — không new GameObject() nữa")]
        [SerializeField] private BattleHudScreen _hud;
        [SerializeField] private ActionCommandUI _actionCommandUI;
        [SerializeField] private FloatingTextLayer _floating;
        [SerializeField] private VfxPlayer _vfxPlayer;
        // task-phase-5-gaps.md Phần B
        [SerializeField] private TutorialOverlay _tutorialOverlay;

        // plan.md §4.1 — 4 slot người chơi, 5 slot địch
        private static readonly Vector3[] PLAYER_SLOTS =
        {
            new(-4.0f, -1.2f, 0f), new(-3.4f, -2.0f, 0f),
            new(-5.6f, -1.2f, 0f), new(-5.0f, -2.0f, 0f)
        };

        private static readonly Vector3[] ENEMY_SLOTS =
        {
            new(3.0f, -1.0f, 0f), new(3.8f, -1.8f, 0f), new(4.6f, -1.0f, 0f),
            new(5.4f, -2.4f, 0f), new(6.2f, -1.4f, 0f)
        };

        public CombatSimulation Simulation { get; private set; }
        public CombatPresenter Presenter { get; private set; }

        private readonly List<UnitView> _views = new();

        private PendingBattle _pending;
        private bool _resultShown;
        private GameObject _resultOverlayGo;

        /// <summary>plan.md §4.15 Defeat — "Hồi sinh bằng Gem". Số tự thiết kế (plan.md không cho
        /// số cụ thể) — chưa bằng 1 lượt gacha đơn (GachaSystem.SINGLE_PULL_COST=300), đủ để là
        /// lựa chọn có cân nhắc thật chứ không phải "luôn đáng dùng" hay "không bao giờ đáng".</summary>
        private const long REVIVE_GEM_COST = 100;

        // task-phase-5-gaps.md Phần B — null khi trận không phải tutorial (mọi trận khác không đổi
        // hành vi, mọi hook dưới đây đều null-conditional).
        private TutorialController _tutorial;

        /// <summary>Phát MỖI LẦN cửa sổ Action Command trả về grade — TutorialController (Phần B)
        /// là consumer đầu tiên, nhưng để public vì đây đúng lỗ hổng object-map.md §B0 đã chỉ ra
        /// (không có cách quan sát grade từ ngoài closure của HandleSkillChosen).</summary>
        public event System.Action<CommandGrade> OnCommandResolved;

        // Tháp Vô Tận (task-endgame.md) — chỉ có ý nghĩa khi _pending.SpecialMode == Tower.
        private int _towerFloor;
        private int _towerFloorsCleared;

        private void Start() => BuildBattle();

        // =====================================================================

        /// <summary>Trial Boss (task-endgame.md) đo damage trong 1 cửa sổ lượt cố định — boss HP
        /// quá cao để hạ gục thật, cần TurnLimit riêng để trận LUÔN kết thúc bằng Timeout ở quy mô
        /// hợp lý thay vì chạy tới SAFETY_TURN_LIMIT (200, quá dài cho UX 1 lượt thử).</summary>
        private const int TRIAL_BOSS_TURN_LIMIT = 30;

        public void BuildBattle()
        {
            // task-auto-battle.md — khôi phục trạng thái Auto từ lần chơi trước.
            if (ServiceLocator.TryGet<ISettingsService>(out var svc)) _autoPlay = svc.Current.AutoBattle;
            _pending = RunContext.Pending;
            int turnLimit = _pending != null && _pending.SpecialMode == DungeonKind.TrialBoss
                ? TRIAL_BOSS_TURN_LIMIT : _turnLimit;
            Simulation = new CombatSimulation(_pending != null ? (ulong)_pending.Seed : _seed, turnLimit);

            if (_pending != null && _pending.SpecialMode == DungeonKind.Tower)
            {
                _towerFloor = 1;
                _towerFloorsCleared = 0;
                Simulation.OnEnemyWaveCleared = TryAdvanceTowerWave;
            }

            EnsureInfrastructure();

            if (_pending != null)
            {
                SpawnTeamFromDefinitions(TeamSide.Player, _pending.HeroDefIds);
                if (_pending.SpecialMode == DungeonKind.Arena) SpawnArenaOpponentTeam();
                else SpawnTeamFromDefinitions(TeamSide.Enemy, _pending.EnemyDefIds);
            }
            else
            {
                SpawnTeam(TeamSide.Player, 4);
                SpawnTeam(TeamSide.Enemy, 3);
            }

            Simulation.Start();

            // Edge case E17 (plan.md §4.14): trận đang dở khi thoát app giữa chừng — nếu profile
            // hiện tại có snapshot hợp lệ KHỚP đúng node này, replay lại intent của Player để đưa
            // Simulation về đúng điểm dừng TRƯỚC KHI trao quyền điều khiển sống cho người chơi.
            TryResumeFromSnapshot();

            Presenter.Setup(Simulation.Events, _floating, _vfxPlayer, Camera.main);
            Presenter.Speed = _speed;
            Presenter.IsBossFight = _pending != null && _pending.EnemyDefIds.Count == 1;
            // Edge case E18 (plan.md §4.14) — Escape bị chặn ở node Boss. Tái dùng đúng heuristic
            // IsBossFight đã có (1 địch = Boss) thay vì tự suy luận lại từ NodeType.
            Simulation.State.AllowEscape = !Presenter.IsBossFight;

            // task-consumable-items.md — nạp vật phẩm phe Player mang vào trận (rỗng nếu Pending
            // null hoặc không mang gì — mọi trận khác không đổi hành vi).
            if (_pending?.ItemLoadout != null)
                foreach (var kv in _pending.ItemLoadout)
                    Simulation.State.ItemLoadout[kv.Key] = kv.Value;

            foreach (var v in _views) Presenter.RegisterView(v);

            WireHud();

            Debug.Log($"[Battle] Bắt đầu — seed {Simulation.Seed}, " +
                      $"{Simulation.State.CountAlive(TeamSide.Player)} vs " +
                      $"{Simulation.State.CountAlive(TeamSide.Enemy)}");
        }

        // =====================================================================
        // Edge case E17 (plan.md §4.14) — lưu/khôi phục trận đang dở khi thoát app giữa chừng.
        // Chỉ hoạt động khi vào trận qua RunContext.Pending (node map thật) — trận test/standalone
        // (_pending == null) không có đủ dữ liệu (NodeId/HeroDefIds/EnemyDefIds) để tái tạo nên bỏ
        // qua, không phải bug.
        // =====================================================================

        private void TryResumeFromSnapshot()
        {
            var profile = ProfileContext.Current;
            var snap = profile?.Run?.BattleSnapshot;
            if (snap == null || !snap.Valid || _pending == null || snap.NodeId != _pending.NodeId) return;

            var recorded = new List<ActionIntent>(snap.IntentActorIds.Count);
            for (int i = 0; i < snap.IntentActorIds.Count; i++)
                recorded.Add(new ActionIntent(snap.IntentActorIds[i], snap.IntentSkillSlots[i],
                                               snap.IntentTargetIds[i], (CommandGrade)snap.IntentGrades[i]));

            bool ok = BattleReplay.ReplayPlayerIntents(Simulation, recorded);
            if (!ok)
                Debug.LogWarning("[Battle] BattleSnapshot không khớp trạng thái hiện tại (đội hình/seed lệch) — bỏ qua, chơi trận mới bình thường.");

            // Dùng 1 lần — snapshot mới (nếu app pause lần nữa) sẽ tự ghi đè từ IntentLog hiện tại,
            // không cần giữ lại bản cũ.
            profile.Run.BattleSnapshot = new BattleSnapshotDto();
        }

        /// <summary>Unity gọi tự động khi app chuyển nền/tiền cảnh (mobile) — đúng điểm kích hoạt
        /// của E17 ("người chơi thoát app giữa trận"). Có thể gọi trực tiếp để test (execute_code/
        /// PlayMode) mà không cần OS thật tạm dừng app.</summary>
        private void OnApplicationPause(bool pauseStatus)
        {
            if (!pauseStatus || Simulation == null || Simulation.IsFinished) return;
            SaveSnapshot();
        }

        private void SaveSnapshot()
        {
            var profile = ProfileContext.Current;
            if (profile?.Run == null || _pending == null) return;

            var playerIds = new HashSet<int>();
            foreach (var u in Simulation.State.Units)
                if (u.Side == TeamSide.Player) playerIds.Add(u.Id);

            var snap = new BattleSnapshotDto
            {
                Valid = true,
                Seed = (long)Simulation.Seed,
                NodeId = _pending.NodeId,
            };
            snap.HeroDefIds.AddRange(_pending.HeroDefIds);
            snap.EnemyDefIds.AddRange(_pending.EnemyDefIds);
            foreach (var intent in Simulation.IntentLog)
            {
                if (!playerIds.Contains(intent.ActorId)) continue;
                snap.IntentActorIds.Add(intent.ActorId);
                snap.IntentSkillSlots.Add(intent.SkillSlot);
                snap.IntentTargetIds.Add(intent.TargetId);
                snap.IntentGrades.Add((int)intent.Grade);
            }

            profile.Run.BattleSnapshot = snap;
            if (ServiceLocator.TryGet<IPlayerRepository>(out var repo)) repo.SaveAsync(profile);
        }

        private void EnsureInfrastructure()
        {
            // BattleCamera đã có PixelPerfectCamera dựng sẵn trên Hierarchy; Presenter đã gắn
            // sẵn trên BattleSceneInstaller; FloatingTextLayer/VfxLayer (__Stage__) và
            // BattleHud/ActionCommandUI (__UI__) dựng sẵn — không new GameObject() nữa.
            // EventSystem KHÔNG dựng sẵn ở đây (khác Meta) vì Battle còn được test scene lẻ
            // không qua Boot — EnsureEventSystem() là lưới an toàn cho trường hợp đó.
            EnsureEventSystem();

            Presenter = GetComponent<CombatPresenter>();

            if (_hud == null) _hud = FindFirstObjectByType<BattleHudScreen>();
            if (_actionCommandUI == null) _actionCommandUI = FindFirstObjectByType<ActionCommandUI>();
            if (_tutorialOverlay == null) _tutorialOverlay = FindFirstObjectByType<TutorialOverlay>();
        }

        /// <summary>KHÔNG có EventSystem thì mọi Button trong HUD (chọn skill, END TURN,
        /// AUTO/tốc độ) không bao giờ nhận được input chuột/chạm thật.
        /// Dùng InputSystemUIInputModule (không phải StandaloneInputModule) vì Player Settings
        /// đã chốt Active Input Handling = Input System package — StandaloneInputModule đọc
        /// UnityEngine.Input mỗi frame và ném InvalidOperationException, làm mọi Button câm.</summary>
        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;

            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        // =====================================================================
        // HUD ↔ Simulation — HUD chỉ đọc/phát event, mọi lệnh đi qua đây (object-map.md §3.3)
        // =====================================================================

        private void WireHud()
        {
            _hud.Bind(Simulation);
            // Bug báo trực tiếp: "không chọn auto mà vẫn chơi auto" — _autoPlay đã được khôi phục
            // thật từ SettingsDto.AutoBattle ở BuildBattle() (dòng ~88), nhưng HUD luôn khởi tạo
            // hiển thị "AUTO OFF" mặc định, không đọc lại _autoPlay — 2 trạng thái lệch nhau, gây
            // đúng triệu chứng người dùng thấy (nút hiện OFF nhưng trận chạy auto ngầm).
            _hud.SetAutoState(_autoPlay);
            _hud.OnSkillChosen += HandleSkillChosen;
            _hud.OnItemChosen += HandleItemChosen;
            _hud.OnEndTurnPressed += HandlePassTurn;
            _hud.OnAutoToggled += auto =>
            {
                _autoPlay = auto;
                // task-auto-battle.md — persist qua SettingsService (sẽ được lưu cùng profile khi kết thúc trận).
                if (ServiceLocator.TryGet<ISettingsService>(out var s)) s.Modify(dto => dto.AutoBattle = auto);
            };
            _hud.OnSpeedChanged += speed => { _speed = speed; Presenter.Speed = speed; };
            // task-tactic-row.md
            _hud.OnGuardPressed   += HandleGuard;
            _hud.OnEscapePressed  += HandleEscape;
            _hud.OnSwapRowPressed += HandleSwapRow;
            _hud.OnFocusPressed   += HandleFocus;
            _hud.OnAnalyzePressed += HandleAnalyze;

            WireTutorial();
        }

        // task-phase-5-gaps.md Phần B — chỉ trận battle-node ĐẦU TIÊN mỗi profile mới
        // (RunContext.PendingBattle.IsTutorial, xem MetaSceneInstaller.LaunchBattle) mới có
        // _tutorial khác null; mọi trận khác (kể cả IsTutorial nhưng chạy standalone không qua
        // node map — _pending == null) giữ nguyên hành vi cũ.
        private void WireTutorial()
        {
            if (_pending == null || !_pending.IsTutorial) return;

            _tutorial = new TutorialController();
            _tutorial.OnStepChanged += HandleTutorialStepChanged;
            OnCommandResolved += grade => _tutorial?.NotifyCommandResolved(grade);
            if (_tutorialOverlay != null) _tutorialOverlay.OnSkipPressed += () => _tutorial?.Skip();

            // Bước đầu (ChooseSkill) không có trigger sự kiện nào để chờ — hiện chỉ dẫn ngay khi
            // vào trận, khác 4 bước sau đều đợi HandleTutorialStepChanged qua OnStepChanged.
            _tutorialOverlay?.Show(_tutorial.Step);
        }

        private void HandleTutorialStepChanged(TutorialStep step)
        {
            if (step == TutorialStep.Done)
            {
                _tutorialOverlay?.Hide();
                CompleteTutorial();
            }
            else
            {
                _tutorialOverlay?.Show(step);
            }
        }

        /// <summary>Lưu ngay trong Battle scene (giống <see cref="SaveSnapshot"/> — cùng kỹ thuật
        /// ProfileContext.Current + IPlayerRepository.SaveAsync) thay vì đợi vòng BattleOutcome/
        /// BattleResultProcessor ở Meta — tránh phải thêm field mới xuyên suốt chuỗi
        /// RunContext.ReportResult/BattleOutcome chỉ để mang 1 bool, khi Battle scene đã có sẵn
        /// đủ quyền truy cập profile + repo tại đây.</summary>
        private void CompleteTutorial()
        {
            var profile = ProfileContext.Current;
            if (profile == null) return;
            profile.Progress.TutorialCompleted = true;
            if (ServiceLocator.TryGet<IPlayerRepository>(out var repo)) repo.SaveAsync(profile);
        }

        private void HandleGuard()
        {
            if (Simulation == null || !Simulation.NeedsPlayerInput) return;
            Simulation.SubmitIntent(new ActionIntent(Simulation.CurrentActor.Id, 0, -1,
                                                     isGuard: true));
            Presenter.PlayPending();
        }

        private void HandleEscape()
        {
            if (Simulation == null || !Simulation.NeedsPlayerInput) return;
            Simulation.SubmitIntent(new ActionIntent(Simulation.CurrentActor.Id, 0, -1,
                                                     isEscape: true));
            Presenter.PlayPending();
        }

        private void HandleSwapRow()
        {
            if (Simulation == null || !Simulation.NeedsPlayerInput) return;
            Simulation.SubmitIntent(new ActionIntent(Simulation.CurrentActor.Id, 0, -1,
                                                     isSwapRow: true));
            // SwapRow không kết thúc lượt — không gọi PlayPending (chờ input kế).
        }

        private void HandleFocus()
        {
            if (Simulation == null || !Simulation.NeedsPlayerInput) return;
            Simulation.SubmitIntent(new ActionIntent(Simulation.CurrentActor.Id, 0, -1,
                                                     isFocus: true));
            Presenter.PlayPending();
        }

        private void HandleAnalyze()
        {
            if (Simulation == null || !Simulation.NeedsPlayerInput) return;
            Simulation.SubmitIntent(new ActionIntent(Simulation.CurrentActor.Id, 0, -1,
                                                     isAnalyze: true));
            Presenter.PlayPending();
        }

        private void HandleSkillChosen(int slot)
        {
            if (Simulation == null || !Simulation.NeedsPlayerInput) return;

            var actor = Simulation.CurrentActor;
            var skill = actor?.GetSkill(slot);
            if (skill == null) return;

            int targetId = FirstAliveEnemyId();
            if (targetId < 0) return;

            _tutorial?.NotifySkillChosen();

            _actionCommandUI.Open(skill.Data.CommandType, skill.Data.ComboBeats, grade =>
            {
                SubmitPlayerAction(slot, targetId, grade);
                _hud.ClearSelection();
                OnCommandResolved?.Invoke(grade);
            });
        }

        /// <summary>task-consumable-items.md — dùng item KHÔNG qua Action Command minigame
        /// (giống Guard/Escape hiện tại), resolve ngay khi bấm. Target auto-resolve hết ở
        /// <see cref="Game.Combat.Systems.ItemResolver"/> — TargetId truyền -1, không dùng.</summary>
        private void HandleItemChosen(ItemType type)
        {
            if (Simulation == null || !Simulation.NeedsPlayerInput) return;

            Simulation.SubmitIntent(new ActionIntent(
                Simulation.CurrentActor.Id, (int)type, -1, CommandGrade.Miss, isUseItem: true));
            Presenter.PlayPending();
            _hud.ClearSelection();
        }

        private void HandlePassTurn()
        {
            if (Simulation == null || !Simulation.NeedsPlayerInput) return;
            Simulation.SubmitIntent(Simulation.DefaultAutoIntent());
            Presenter.PlayPending();
        }

        private int FirstAliveEnemyId()
        {
            for (int i = 0; i < Simulation.State.Units.Count; i++)
            {
                var u = Simulation.State.Units[i];
                if (u.Side == TeamSide.Enemy && u.IsAlive) return u.Id;
            }
            return -1;
        }

        private void SpawnTeam(TeamSide side, int count)
        {
            var slots = side == TeamSide.Player ? PLAYER_SLOTS : ENEMY_SLOTS;
            var sprites = side == TeamSide.Player ? _heroSprites : _enemySprites;
            count = Mathf.Min(count, slots.Length);

            for (int i = 0; i < count; i++)
            {
                var unit = BuildUnit(side, i);
                Simulation.AddUnit(unit, side == TeamSide.Enemy ? BuildAi() : null);

                var go = new GameObject($"Unit_{side}_{i}");
                go.transform.SetParent(transform, false);
                var view = go.AddComponent<UnitView>();

                Sprite sprite = sprites != null && sprites.Length > 0
                    ? sprites[i % sprites.Length] : null;

                view.SetHome(slots[i]);
                view.Bind(unit, sprite);
                _views.Add(view);
            }
        }

        /// <summary>Sinh đội từ HeroDefinitionSO/EnemyDefinitionSO (CSV → Resources) — dữ liệu thật
        /// truyền từ Meta qua <see cref="RunContext.Pending"/>, thay cho đội mẫu hard-code ở P1.</summary>
        private void SpawnTeamFromDefinitions(TeamSide side, System.Collections.Generic.IReadOnlyList<string> defIds)
        {
            bool player = side == TeamSide.Player;
            var slots = player ? PLAYER_SLOTS : ENEMY_SLOTS;
            int count = Mathf.Min(defIds.Count, slots.Length);

            // task-formation-synergy.md — preset từ RunContext; enemy luôn dùng mặc định.
            string formationId = player
                ? (_pending?.Formation ?? FormationSystem.Default)
                : FormationSystem.Default;

            var spawnedPlayerUnits = player ? new List<CombatUnit>(count) : null;

            for (int i = 0; i < count; i++)
            {
                string defId = defIds[i];
                CombatUnit unit;
                Sprite sprite;
                string aiProfileId = "ai_basic";
                ArchetypeId enemyArchetype = ArchetypeId.Grunt;

                if (player)
                {
                    // task-addressables-pilot.md — chỗ NÓNG NHẤT của pilot (chạy mỗi lần spawn 1
                    // hero vào trận, mọi trận đều qua đây). Cùng khuôn WaitForCompletion đồng bộ đã
                    // ghi ở TeamSelectScreen.FindHeroDef.
                    var def = UnityEngine.AddressableAssets.Addressables
                        .LoadAssetAsync<HeroDefinitionSO>($"Data/Heroes/{defId}").WaitForCompletion();
                    if (def == null) { Debug.LogWarning($"[Battle] Không tìm thấy hero '{defId}'"); continue; }

                    // Cộng level scaling (HeroLevelSystem) + bonus trang bị vào stat gốc trước khi
                    // dựng CombatUnit — trước đây hero.Level không ảnh hưởng gì tới combat cả
                    // (CombatUnit.Level luôn hard-code 10), trang bị cũng vậy.
                    var profile = ProfileContext.Current;
                    Game.Data.Dto.HeroInstanceDto heroInstance = null;
                    if (profile != null) heroInstance = profile.Heroes.Find(h => h.DefId == defId);
                    int heroLevel = heroInstance?.Level ?? 1;

                    var primary = Game.Meta.Hero.HeroLevelSystem.EffectivePrimary(def.BasePrimary, heroLevel);
                    if (heroInstance != null)
                        primary += EquipmentService.GetBonusPrimary(profile, heroInstance);

                    // Chưa tìm thấy heroInstance (VD test độc lập) -> coi như đã max sao, không
                    // khoá skill gì cả (giữ hành vi cũ trước khi có Ascend).
                    int heroStar = heroInstance?.Star ?? Game.Meta.Hero.AscendSystem.MAX_STAR;

                    unit = BuildUnitFromDefinition(def.DefId, def.NameKey, side, i, def.Element,
                                                   def.Class, def.PoiseMax, primary, def.SkillIds, heroLevel,
                                                   heroInstance?.SkillLevels, heroStar);
                    // Sub-stat trang bị (%, RES, EFF_ACC...) không map được vào PrimaryStats nên
                    // đi đường riêng qua EquipmentModifiers — main stat vẫn qua GetBonusPrimary ở
                    // trên (task-equipment.md §3).
                    if (heroInstance != null)
                    {
                        unit.EquipmentModifiers.AddRange(EquipmentService.GetEquipmentModifiers(profile, heroInstance));
                        // task-setbonus.md — Set Bonus (plan.md §7.4): 2-món cộng thẳng vào
                        // EquipmentModifiers (đúng chỗ chứa mọi StatModifier từ trang bị), 4-món
                        // gán vào slot PassiveData riêng (CombatUnit.SetBonus).
                        unit.EquipmentModifiers.AddRange(
                            Game.Meta.Equipment.SetBonusResolver.GetActiveTwoPieceBonuses(heroInstance, profile));
                        unit.SetBonus = Game.Meta.Equipment.SetBonusResolver.GetActiveFourPieceBonus(heroInstance, profile);
                    }

                    // task-formation-synergy.md — gán hàng theo formation + thêm formation modifier.
                    unit.Row = FormationSystem.GetRow(formationId, i);
                    unit.EquipmentModifiers.AddRange(FormationSystem.GetModifiers(formationId, unit.Row));

                    sprite = Resources.Load<Sprite>($"Art/Characters/Heroes/{def.SpriteFolder}/{def.SpriteFolder}_v1_00");
                    spawnedPlayerUnits.Add(unit);
                }
                else
                {
                    var def = Resources.Load<EnemyDefinitionSO>($"Data/Enemies/{defId}");
                    if (def == null) { Debug.LogWarning($"[Battle] Không tìm thấy enemy '{defId}'"); continue; }
                    unit = BuildUnitFromDefinition(def.DefId, def.NameKey, side, i, def.Element,
                                                   HeroClass.Slayer, def.PoiseMax, def.BasePrimary, def.SkillIds, 10, null,
                                                   Game.Meta.Hero.AscendSystem.MAX_STAR);
                    sprite = Resources.Load<Sprite>($"Art/Characters/Enemies/{def.SpriteFolder}/{def.SpriteFolder}_v1_00");
                    aiProfileId = def.AiProfileId;
                    enemyArchetype = def.Archetype;
                }

                Simulation.AddUnit(unit, side == TeamSide.Enemy ? BuildAi(aiProfileId, enemyArchetype) : null);

                var go = new GameObject($"Unit_{side}_{i}_{defId}");
                go.transform.SetParent(transform, false);
                var view = go.AddComponent<UnitView>();
                view.SetHome(slots[i]);
                view.Bind(unit, sprite);
                _views.Add(view);
            }

            // task-formation-synergy.md — Synergy: áp dụng sau khi spawn hết hero, cần thấy đủ đội.
            if (player && spawnedPlayerUnits != null && spawnedPlayerUnits.Count > 0)
                SynergySystem.Apply(spawnedPlayerUnits);
        }

        /// <summary>task-arena.md — spawn đội đối thủ Arena PHE ENEMY nhưng dùng HeroDefinitionSO
        /// (không phải EnemyDefinitionSO như <see cref="SpawnTeamFromDefinitions"/>'s nhánh enemy).
        /// Method MỚI HOÀN TOÀN thay vì sửa <see cref="SpawnTeamFromDefinitions"/> — tránh mọi rủi
        /// ro hồi quy đường spawn nóng nhất của game (mọi trận thường/Dungeon/Tower/TrialBoss đều
        /// qua đó). Đối thủ Arena KHÔNG có trang bị/HeroInstanceDto thật (là snapshot RNG sinh,
        /// xem <see cref="ArenaOpponentDto"/>) — chỉ Level/Star snapshot, không
        /// EquipmentModifiers/SetBonus/Formation (đối thủ ảo, không giả vờ có đội hình thật).</summary>
        private void SpawnArenaOpponentTeam()
        {
            var profile = ProfileContext.Current;
            int opponentIndex = _pending.SpecialFloor;
            if (profile == null || opponentIndex < 0 || opponentIndex >= profile.Arena.Opponents.Count)
            {
                Debug.LogWarning("[Battle] Arena opponent snapshot không hợp lệ — bỏ qua spawn.");
                return;
            }

            var opponent = profile.Arena.Opponents[opponentIndex];
            int count = Mathf.Min(opponent.HeroDefIds.Length, ENEMY_SLOTS.Length);

            for (int i = 0; i < count; i++)
            {
                string defId = opponent.HeroDefIds[i];
                var def = UnityEngine.AddressableAssets.Addressables
                    .LoadAssetAsync<HeroDefinitionSO>($"Data/Heroes/{defId}").WaitForCompletion();
                if (def == null) { Debug.LogWarning($"[Battle] Arena: không tìm thấy hero '{defId}'"); continue; }

                var primary = Game.Meta.Hero.HeroLevelSystem.EffectivePrimary(def.BasePrimary, opponent.Level);
                var unit = BuildUnitFromDefinition(def.DefId, def.NameKey, TeamSide.Enemy, i, def.Element,
                                                   def.Class, def.PoiseMax, primary, def.SkillIds, opponent.Level,
                                                   null, opponent.Star);

                Simulation.AddUnit(unit, BuildAi("ai_special"));

                var sprite = Resources.Load<Sprite>($"Art/Characters/Heroes/{def.SpriteFolder}/{def.SpriteFolder}_v1_00");
                var go = new GameObject($"Unit_Enemy_{i}_{defId}");
                go.transform.SetParent(transform, false);
                var view = go.AddComponent<UnitView>();
                view.SetHome(ENEMY_SLOTS[i]);
                view.Bind(unit, sprite);
                _views.Add(view);
            }
        }

        // =====================================================================
        // Tháp Vô Tận (task-endgame.md, plan.md §8.3) — 1 lượt leo = 1 CombatSimulation liên tục
        // nhiều đợt địch, KHÔNG quay lại Meta giữa các tầng (khác Dungeon/TrialBoss). Đăng ký qua
        // CombatSimulation.OnEnemyWaveCleared — xem BuildBattle().
        // =====================================================================

        /// <summary>Gọi ngay khi phe Enemy vừa bị wipe. Dọn view đợt địch cũ (đã chết — không
        /// destroy sẽ đè hình lên đợt mới cùng slot), spawn đợt mới nếu chưa vượt
        /// <see cref="TowerSystem.MAX_FLOOR"/>. KHÔNG đụng tới unit Player nào (HP/SP/status/
        /// cooldown giữ nguyên) — đúng "HP không hồi giữa tầng".</summary>
        private bool TryAdvanceTowerWave(CombatSimulation sim)
        {
            _towerFloorsCleared++;
            _towerFloor++;
            if (_towerFloor > TowerSystem.MAX_FLOOR) return false; // hết 100 tầng — kết thúc thật (Victory)

            for (int i = _views.Count - 1; i >= 0; i--)
            {
                var unit = sim.State.GetUnit(_views[i].UnitId);
                if (unit == null || unit.Side != TeamSide.Enemy) continue;
                Destroy(_views[i].gameObject);
                _views.RemoveAt(i);
            }

            var enemyIds = PickTowerEnemies(_towerFloor);
            int viewCountBefore = _views.Count;
            SpawnTeamFromDefinitions(TeamSide.Enemy, enemyIds);
            for (int i = viewCountBefore; i < _views.Count; i++) Presenter.RegisterView(_views[i]);

            return true;
        }

        /// <summary>Chọn địch cho 1 tầng Tháp — KHÔNG giới hạn theo chương (nội dung endgame, dùng
        /// chung mọi enemy đã có). Dùng <c>Simulation.State.Rng</c> (KHÔNG phải UnityEngine.Random)
        /// để việc chọn địch xác định theo seed — giữ đúng kỷ luật RNG của Game.Combat, cần thiết
        /// để BattleReplay (edge case E17) resume đúng nếu app thoát giữa 1 lượt leo Tháp.</summary>
        private List<string> PickTowerEnemies(int floor)
        {
            int count = TowerSystem.EnemyCountForFloor(floor);
            bool tougher = TowerSystem.IsTougherFloor(floor);

            var all = Resources.LoadAll<Game.Meta.Content.EnemyDefinitionSO>("Data/Enemies")
                                .Where(e => e.Archetype != ArchetypeId.Boss)
                                .ToArray();
            if (all.Length == 0) return new List<string>();

            var pool = tougher
                ? all.OrderByDescending(e => e.PoiseMax).Take(Mathf.Max(count * 2, count)).ToArray()
                : all;

            var result = new List<string>(count);
            for (int i = 0; i < count; i++)
                result.Add(pool[Simulation.State.Rng.NextInt(pool.Length)].DefId);
            return result;
        }

        private static CombatUnit BuildUnitFromDefinition(string defId, string nameKey, TeamSide side, int index,
            Element element, HeroClass cls, int poiseMax, PrimaryStats basePrimary, string[] skillIds, int level,
            int[] skillLevels, int star)
        {
            var unit = new CombatUnit
            {
                DefId = defId,
                DisplayNameKey = nameKey,
                Side = side,
                Row = index < 2 ? Row.Front : Row.Back,
                SlotIndex = index,
                Element = element,
                Class = cls,
                PoiseMax = poiseMax,
                Level = level,
                BasePrimary = basePrimary
            };

            bool hasSlot0 = false;
            for (int s = 0; s < skillIds.Length; s++)
            {
                // Slot C (3)/Ultimate (4) khoá tới khi đủ sao — task-ascend.md §4.
                if (!Game.Meta.Hero.AscendSystem.IsSkillSlotUnlocked(star, s)) continue;

                var so = Resources.Load<SkillDefinitionSO>($"Data/Skills/{skillIds[s]}");
                if (so == null) continue;
                int skillLevel = skillLevels != null && s < skillLevels.Length ? skillLevels[s] : 1;
                unit.Skills.Add(new SkillRuntime(so.Data.Clone(), s, skillLevel));
                if (s == 0) hasSlot0 = true;
            }

            // AI fallback cần luôn có skill ở ô 0 (plan.md quy ước đánh thường không tốn SP).
            if (!hasSlot0)
                unit.Skills.Insert(0, new SkillRuntime(BasicAttack(element), 0));

            // Awakening thật (task-ascend.md §7 mục A.5) — star đã tương đương Awakened
            // (AscendSystem chỉ set Awakened=true đúng lúc star chạm MAX_STAR).
            if (star >= Game.Meta.Hero.AscendSystem.MAX_STAR)
                unit.Awakening = Game.Meta.Hero.AwakeningCatalog.Get(defId);

            // Passive bẩm sinh (task-innate-passive.md) — CHỈ hero người chơi, có ngay từ ★1
            // (khác Awakening, không điều kiện sao). Enemy dùng AI profile riêng, không có passive
            // bẩm sinh theo thiết kế plan.md.
            if (side == TeamSide.Player)
                unit.Passive = Game.Meta.Hero.InnatePassiveCatalog.Get(defId);

            return unit;
        }

        private static CombatUnit BuildUnit(TeamSide side, int index)
        {
            bool player = side == TeamSide.Player;
            var elements = new[] { Element.Fire, Element.Water, Element.Earth, Element.Wind };

            var unit = new CombatUnit
            {
                DefId = player ? $"hero_{index}" : $"enemy_{index}",
                DisplayNameKey = player ? $"hero.{index}.name" : $"enemy.{index}.name",
                Side = side,
                Row = index < 2 ? Row.Front : Row.Back,
                SlotIndex = index,
                Element = elements[index % elements.Length],
                Class = player ? (HeroClass)(index % 6) : HeroClass.Slayer,
                PoiseMax = player ? 60 : 30,
                Level = 10,
                BasePrimary = player
                    ? new PrimaryStats(18, 16, 14, 16, 12, 12)
                    : new PrimaryStats(14, 12, 10, 12, 8, 8)
            };

            // Ô 0 luôn là đánh thường không tốn SP (yêu cầu của AI fallback)
            unit.Skills.Add(new SkillRuntime(BasicAttack(unit.Element), 0));
            if (player) unit.Skills.Add(new SkillRuntime(ElementalStrike(unit.Element), 1));

            return unit;
        }

        private static SkillData BasicAttack(Element element) => new()
        {
            Id = "skill_basic_attack",
            NameKey = "skill.basic_attack.name",
            Type = SkillType.Physical,
            DamageType = DamageType.Physical,
            Element = element,
            Target = TargetMode.SingleEnemy,
            PowerMultiplier = 1.0f,
            PoiseDamage = 3,
            CommandType = ActionCommandType.SingleTap,
            AnimTrigger = "attack"
        };

        private static SkillData ElementalStrike(Element element) => new()
        {
            Id = "skill_elemental_strike",
            NameKey = "skill.elemental_strike.name",
            Type = SkillType.Magical,
            DamageType = DamageType.Magical,
            Element = element,
            Target = TargetMode.SingleEnemy,
            PowerMultiplier = 1.6f,
            SpCost = 12,
            Cooldown = 2,
            PoiseDamage = 8,
            IsBreaker = true,
            CommandType = ActionCommandType.Charge,
            Applies = new[] { new StatusApplication(ElementStatus(element), 0.6f, 3) }
        };

        private static StatusId ElementStatus(Element e) => e switch
        {
            Element.Fire => StatusId.Burn,
            Element.Water => StatusId.Freeze,
            Element.Earth => StatusId.DefDown,
            Element.Wind => StatusId.SpdDown,
            Element.Light => StatusId.Blind,
            Element.Dark => StatusId.Curse,
            _ => StatusId.AtkDown
        };

        /// <summary>
        /// Hồ sơ AI theo <see cref="EnemyDefinitionSO.AiProfileId"/> — object-map.md §7.4 bước AI.
        /// "ai_boss" luân phiên chiêu đặc trưng (ô 1) / chiêu hỗ trợ (ô 2) / đánh thường (ô 0)
        /// thay vì chỉ spam đánh thường như lính thường, để trận boss có nhịp điệu riêng.
        /// </summary>
        private static AIProfile BuildAi(string profileId = "ai_basic", ArchetypeId archetype = ArchetypeId.Grunt)
        {
            if (profileId == "ai_boss")
            {
                var boss = new AIProfile { Id = "ai_boss", Noise = 6f };
                boss.Rules.Add(new AIRule
                {
                    // task-boss-phase-enrage.md, plan.md §4.13.3 — "chiêu đặc trưng" (ô 1) nay là
                    // SignatureMove thật: thắng điểm không đánh ngay mà đếm ngược 3 lượt công khai
                    // trước (AIController.Choose), Break huỷ ngay (counterplay bắt buộc).
                    When = new AICondition(AIConditionType.Always),
                    SkillSlot = 1, Weight = 70f, RuleCooldown = 2, IsSignatureMove = true
                });
                boss.Rules.Add(new AIRule
                {
                    When = new AICondition(AIConditionType.Always),
                    SkillSlot = 2, Weight = 55f, RuleCooldown = 3
                });
                boss.Rules.Add(new AIRule
                {
                    When = new AICondition(AIConditionType.Always),
                    SkillSlot = 0, Weight = 30f
                });
                return boss;
            }

            // task-ai-diversity.md — trước đây "ai_special" (56/66 = 85% enemy, object-map.md §12.1
            // "CẬP NHẬT 2026-08-18") là ĐÚNG 1 bộ luật y hệt cho MỌI archetype (Healer/Tank/Debuffer/
            // Caster đều đánh giống hệt nhau dù skill kit khác nhau thật). Nay tách theo Archetype
            // thật qua BuildArchetypeAi — dùng AIConditionType đã có sẵn (AllyHpBelow/SelfHpBelow/
            // TargetIsBroken), KHÔNG đổi ai_basic/ai_boss.
            if (profileId == "ai_special") return BuildArchetypeAi(archetype);

            var p = new AIProfile { Id = "ai_basic", Noise = 8f };
            p.Rules.Add(new AIRule
            {
                When = new AICondition(AIConditionType.Always),
                SkillSlot = 0,
                Weight = 50f
            });
            return p;
        }

        /// <summary>task-ai-diversity.md — mỗi archetype ưu tiên khác nhau bằng đúng
        /// <see cref="AIConditionType"/> đã có sẵn (không thêm condition mới). Grunt/Swarm/Elite +
        /// mọi archetype không liệt kê riêng giữ NGUYÊN hành vi "ai_special" cũ (lính thường/số
        /// đông không cần chiến thuật riêng, đúng vai trò thiết kế của chúng).</summary>
        private static AIProfile BuildArchetypeAi(ArchetypeId archetype)
        {
            var p = new AIProfile { Id = "ai_special_" + archetype, Noise = 10f };
            switch (archetype)
            {
                case ArchetypeId.Healer:
                    // Ưu tiên hồi/cứu đồng minh khi có ai dưới 50% HP (PickTarget đã tự chọn đồng
                    // minh HP% thấp nhất cho skill TargetsAllies) — còn lại đánh thường.
                    p.Rules.Add(new AIRule { When = new AICondition(AIConditionType.AllyHpBelow, 50f), SkillSlot = 1, Weight = 90f });
                    p.Rules.Add(new AIRule { When = new AICondition(AIConditionType.Always), SkillSlot = 0, Weight = 35f });
                    break;

                case ArchetypeId.Debuffer:
                    // Áp debuff ĐỊNH KỲ (cooldown 3 lượt — skill riêng thường cũng có Cooldown data
                    // sẵn) thay vì spam mỗi lượt như ai_special cũ.
                    p.Rules.Add(new AIRule { When = new AICondition(AIConditionType.Always), SkillSlot = 1, Weight = 65f, RuleCooldown = 3 });
                    p.Rules.Add(new AIRule { When = new AICondition(AIConditionType.Always), SkillSlot = 0, Weight = 40f });
                    break;

                case ArchetypeId.Caster:
                case ArchetypeId.Bomber:
                    // Ưu tiên phép/AoE gần như luôn luôn — "spam đòn mạnh nhất", ít quan tâm HP bản
                    // thân (đúng vai trò tấn công thuần, khác Tank/Healer phòng thủ).
                    p.Rules.Add(new AIRule { When = new AICondition(AIConditionType.Always), SkillSlot = 1, Weight = 65f, RuleCooldown = 1 });
                    p.Rules.Add(new AIRule { When = new AICondition(AIConditionType.Always), SkillSlot = 0, Weight = 30f });
                    break;

                case ArchetypeId.Tank:
                    // Thiên về đánh thường ổn định (weight cao hơn ai_special cũ) — chỉ dùng skill
                    // riêng khi CHÍNH bản thân trúng đòn nặng, khác Healer (chăm đồng minh).
                    p.Rules.Add(new AIRule { When = new AICondition(AIConditionType.SelfHpBelow, 50f), SkillSlot = 1, Weight = 70f, RuleCooldown = 2 });
                    p.Rules.Add(new AIRule { When = new AICondition(AIConditionType.Always), SkillSlot = 0, Weight = 45f });
                    break;

                case ArchetypeId.Brute:
                    // Đòn nặng ĐỊNH KỲ (cooldown 2 — "tích rồi nện") thay vì đều đặn mỗi lượt.
                    p.Rules.Add(new AIRule { When = new AICondition(AIConditionType.Always), SkillSlot = 1, Weight = 75f, RuleCooldown = 2 });
                    p.Rules.Add(new AIRule { When = new AICondition(AIConditionType.Always), SkillSlot = 0, Weight = 35f });
                    break;

                case ArchetypeId.Archer:
                case ArchetypeId.Skirmisher:
                    // Cơ hội chủ nghĩa — ưu tiên HẲN khi có mục tiêu đã Break (bonus damage đúng
                    // luật Poise/Break sẵn có), còn lại đánh đều như ai_special cũ.
                    p.Rules.Add(new AIRule { When = new AICondition(AIConditionType.TargetIsBroken), SkillSlot = 1, Weight = 85f });
                    p.Rules.Add(new AIRule { When = new AICondition(AIConditionType.Always), SkillSlot = 1, Weight = 45f, RuleCooldown = 1 });
                    p.Rules.Add(new AIRule { When = new AICondition(AIConditionType.Always), SkillSlot = 0, Weight = 30f });
                    break;

                default: // Grunt/Swarm/Elite/Boss (không xảy ra ở đây) — giữ nguyên ai_special cũ.
                    p.Rules.Add(new AIRule { When = new AICondition(AIConditionType.Always), SkillSlot = 1, Weight = 55f, RuleCooldown = 1 });
                    p.Rules.Add(new AIRule { When = new AICondition(AIConditionType.Always), SkillSlot = 0, Weight = 40f });
                    break;
            }
            return p;
        }

        // =====================================================================
        // Vòng lặp trận — P1 chạy auto; P1 tuần sau nối Action Command của người chơi
        // =====================================================================

        private void Update()
        {
            if (Simulation == null) return;

            _tutorial?.Tick(Simulation.Events, Simulation.State);

            if (Simulation.IsFinished)
            {
                if (!_resultShown && !Presenter.IsPlaying) ShowResultOverlay();
                return;
            }

            if (Presenter.IsPlaying) return;      // chờ diễn xong mới chạy tiếp simulation

            bool needsInput = Simulation.Advance();

            if (needsInput)
            {
                if (_autoPlay) Simulation.SubmitIntent(Simulation.DefaultAutoIntent());
                else return;                      // chờ người chơi bấm (nối ở HUD)
            }

            Presenter.PlayPending();
        }

        // =====================================================================
        // Kết quả trận — báo về Meta qua RunContext, hoặc tự đóng nếu test độc lập
        // =====================================================================

        private void ShowResultOverlay()
        {
            _resultShown = true;
            var result = Simulation.State.Result;
            bool victory = result == BattleResult.Victory;
            bool isTrialBoss = _pending?.SpecialMode == DungeonKind.TrialBoss;
            bool isTower = _pending?.SpecialMode == DungeonKind.Tower;
            bool isDungeon = _pending?.SpecialMode.HasValue == true && !isTrialBoss && !isTower;

            // Trial Boss (task-endgame.md, plan.md §8.3): tổng damage phe Player, để MetaSceneInstaller
            // ghi nhận Damage Meter — BattleState.DamageByUnit đã cộng dồn sẵn cho mọi actor, chỉ cần lọc theo phe.
            long totalPlayerDamage = 0;
            if (isTrialBoss)
                foreach (var u in Simulation.State.Units)
                    if (u.Side == TeamSide.Player && Simulation.State.DamageByUnit.TryGetValue(u.Id, out var dmg))
                        totalPlayerDamage += dmg;

            long gold = 0, exp = 0;
            if (_pending != null && !_pending.SpecialMode.HasValue)
            {
                // Node map bình thường — Dungeon/TrialBoss tính thưởng thật ở Meta
                // (DungeonSystem/TrialBossSystem), tránh trùng logic ở 2 nơi.
                gold = _pending.EnemyDefIds.Count switch { 1 => 300, 2 => 120, _ => 60 };
                exp = _pending.EnemyDefIds.Count switch { 1 => 150, 2 => 60, _ => 30 };
            }

            // Trial Boss: Timeout là kết quả BÌNH THƯỜNG/mong đợi (boss HP cực cao, không kỳ
            // vọng hạ gục thật) — hiện như một lượt đánh đã ghi nhận, không phải "thua".
            // Tháp Vô Tận: MỌI kết quả (Defeat/Escaped/Timeout/Victory-hết-100-tầng) đều bank được
            // tầng đã leo — không có khái niệm "thua" ở đây, chỉ là dừng lượt leo tại đâu.
            bool displayAsSuccess = victory || (isTrialBoss && result == BattleResult.Timeout) || isTower;

            BuildResultOverlay(victory, displayAsSuccess, gold, exp, isTrialBoss, isDungeon, isTower, totalPlayerDamage);
        }

        private void BuildResultOverlay(bool victory, bool displayAsSuccess, long gold, long exp,
                                         bool isTrialBoss, bool isDungeon, bool isTower, long totalPlayerDamage)
        {
            // plan.md §4.15 — Defeat có 3 lựa chọn (Thử lại / Về map / Hồi sinh bằng Gem), NHƯNG
            // chỉ áp dụng cho trận node-map THƯỜNG. Tower/TrialBoss/Dungeon có ngữ nghĩa "thua"
            // khác hẳn (xem ShowResultOverlay: Tower luôn bank tầng, TrialBoss Timeout = bình
            // thường, Dungeon thất bại chỉ cần thử lại từ Dungeon screen) — giữ nguyên 1 nút
            // CONTINUE cho các trường hợp đó, không đổi hành vi đã hoạt động đúng.
            bool isRegularDefeat = !victory && _pending != null && !_pending.SpecialMode.HasValue;

            // sizeDelta panel: 200 cao cho 1 nút CONTINUE (giữ nguyên số cũ), 300 cao cho 3 nút.
            float panelHeight = isRegularDefeat ? 300f : 200f;
            float titleY = isRegularDefeat ? 105f : 55f;
            float bodyY = isRegularDefeat ? 60f : 5f;

            var canvasGo = new GameObject("ResultOverlay");
            canvasGo.transform.SetParent(transform, false);
            _resultOverlayGo = canvasGo;
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960, 540);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            var bgGo = new GameObject("Dim", typeof(RectTransform));
            bgGo.transform.SetParent(canvasGo.transform, false);
            var bgRt = (RectTransform)bgGo.transform;
            bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;
            bgGo.AddComponent<Image>().color = new Color(0, 0, 0, 0.75f);

            var panelGo = new GameObject("Panel", typeof(RectTransform));
            panelGo.transform.SetParent(canvasGo.transform, false);
            var panelRt = (RectTransform)panelGo.transform;
            panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(320, panelHeight);
            panelGo.AddComponent<Image>().color = new Color(0.169f, 0.106f, 0.180f, 0.97f);

            string titleText = isTower
                ? (victory ? "TOWER CONQUERED!" : "CLIMB ENDED")
                : isTrialBoss
                    ? (displayAsSuccess ? "ATTEMPT RECORDED" : "DEFEATED")
                    : (victory ? "VICTORY!" : "DEFEAT");
            var title = NewTmp(panelRt, titleText, 28, new Vector2(0, titleY), new Vector2(300, 40));
            title.color = displayAsSuccess ? new Color(1f, 0.820f, 0.400f) : new Color(0.902f, 0.224f, 0.275f);

            // Dungeon: thưởng thật cấp ở Meta (DungeonSystem.GrantFloorReward) sau khi trận này
            // đóng — hiện "+0 Gold +0 EXP" ở đây sẽ trông như bug, nên đổi câu chữ riêng.
            string bodyText = isTower
                ? $"Floor {_towerFloorsCleared} reached!"
                : isTrialBoss
                    ? $"Damage dealt: {totalPlayerDamage:N0}"
                    : isDungeon
                        ? (victory ? "Floor cleared! Rewards granted." : "Floor failed — try again.")
                        : (victory ? $"+{gold} Gold    +{exp} EXP" : "Your team has fallen.");
            NewTmp(panelRt, bodyText, 16, new Vector2(0, bodyY), new Vector2(300, 30));

            if (!isRegularDefeat)
            {
                NewOverlayButton(panelRt, "CONTINUE", new Vector2(0, -55), new Vector2(160, 40),
                                 new Color(0.42f, 0.32f, 0.06f, 0.95f), true,
                                 () => HandleContinue(victory, gold, exp, totalPlayerDamage));
                return;
            }

            NewOverlayButton(panelRt, "RETRY — New Attempt", new Vector2(0, 15), new Vector2(240, 38),
                             new Color(0.271f, 0.482f, 0.616f, 0.95f), true, HandleRetry);
            NewOverlayButton(panelRt, "RETURN TO MAP", new Vector2(0, -28), new Vector2(240, 38),
                             new Color(0.42f, 0.32f, 0.06f, 0.95f), true,
                             () => HandleContinue(false, 0, 0, 0));

            long gem = 0;
            if (ServiceLocator.TryGet<IEconomyService>(out var economy))
                gem = economy.Get(ProfileContext.Current.Wallet, CurrencyType.Gem);
            bool canRevive = gem >= REVIVE_GEM_COST;
            NewOverlayButton(panelRt, $"REVIVE — {REVIVE_GEM_COST} Gem", new Vector2(0, -71),
                             new Vector2(240, 38), new Color(0.647f, 0.365f, 0.898f, 0.95f), canRevive,
                             HandleRevive);
        }

        private static Button NewOverlayButton(RectTransform parent, string label, Vector2 pos, Vector2 size,
                                                 Color color, bool interactable, UnityEngine.Events.UnityAction onClick)
        {
            var btnGo = new GameObject(label, typeof(RectTransform));
            btnGo.transform.SetParent(parent, false);
            var btnRt = (RectTransform)btnGo.transform;
            btnRt.anchorMin = btnRt.anchorMax = new Vector2(0.5f, 0.5f);
            btnRt.anchoredPosition = pos;
            btnRt.sizeDelta = size;
            var btnImg = btnGo.AddComponent<Image>();
            btnImg.color = interactable ? color : new Color(0.3f, 0.28f, 0.3f, 0.7f);
            var btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            btn.interactable = interactable;
            btn.onClick.AddListener(onClick);
            NewTmp((RectTransform)btnGo.transform, label, 14, Vector2.zero, size);
            return btn;
        }

        /// <summary>plan.md §4.15 Defeat — "Thử lại": trận MỚI cùng node/hero/địch/item/formation
        /// (giữ nguyên toàn bộ <see cref="_pending"/> cũ) nhưng seed MỚI — đúng cách LaunchBattle
        /// gốc sinh seed (task-phase-5-gaps.md, `System.DateTime.UtcNow.Ticks`), tránh replay y hệt
        /// trận vừa thua (deterministic — replay cùng input sẽ ra cùng kết quả). Nạp lại scene
        /// Battle từ đầu — tái dùng TOÀN BỘ pipeline khởi tạo có sẵn, không viết lại gì.</summary>
        private void HandleRetry()
        {
            RunContext.QueueBattle(_pending.NodeId, _pending.HeroDefIds, _pending.EnemyDefIds,
                                    System.DateTime.UtcNow.Ticks, _pending.ItemLoadout,
                                    _pending.Formation, _pending.IsTutorial);
            ServiceLocator.Get<Game.Core.Scenes.ISceneTransitionService>().LoadSceneWithOverlay("Battle");
        }

        /// <summary>plan.md §4.15 Defeat — "Hồi sinh bằng Gem": trừ Gem thật qua IEconomyService rồi
        /// gọi <see cref="CombatSimulation.TryReviveWithGem"/> (hồi 40% MaxHP toàn đội, đặt lại
        /// State.Result=InProgress). KHÔNG cần "resume" thủ công — vòng lặp Update() đã đọc
        /// <c>Simulation.IsFinished</c> mỗi frame, tự chạy tiếp ngay khung hình kế tiếp.</summary>
        private void HandleRevive()
        {
            if (!ServiceLocator.TryGet<IEconomyService>(out var economy)) return;
            var profile = ProfileContext.Current;
            if (!economy.TryConsume(profile.Wallet, CurrencyType.Gem, REVIVE_GEM_COST)) return;
            if (!Simulation.TryReviveWithGem())
            {
                // An toàn cuối — không nên xảy ra (nút chỉ hiện khi Result==Defeat) nhưng nếu có,
                // hoàn lại Gem đã trừ thay vì mất trắng không đổi lại gì.
                economy.Grant(profile.Wallet, CurrencyType.Gem, REVIVE_GEM_COST);
                return;
            }

            if (ServiceLocator.TryGet<IPlayerRepository>(out var repo)) repo.SaveAsync(profile);
            Object.Destroy(_resultOverlayGo);
            _resultOverlayGo = null;
            _resultShown = false;
        }

        private static TextMeshProUGUI NewTmp(RectTransform parent, string text, float size, Vector2 pos, Vector2 dim)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = dim;

            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.alignment = TextAlignmentOptions.Center;
            t.color = new Color(0.949f, 0.910f, 0.810f);
            t.raycastTarget = false;
            return t;
        }

        private void HandleContinue(bool victory, long gold, long exp, long totalPlayerDamage = 0)
        {
            if (_pending != null)
            {
                // Tháp Vô Tận: SpecialFloor báo cáo về Meta là TẦNG ĐÃ LEO ĐƯỢC trong lượt này
                // (_towerFloorsCleared, tăng dần trong trận), KHÔNG phải _pending.SpecialFloor
                // (luôn = tầng khởi đầu, cố định = 1 cho mọi lượt leo — khác Dungeon nơi
                // SpecialFloor là tầng cố định của cả trận).
                int reportedFloor = _pending.SpecialMode == DungeonKind.Tower
                    ? _towerFloorsCleared : _pending.SpecialFloor;
                RunContext.ReportResult(_pending.NodeId, victory, gold, exp, _pending.HeroDefIds,
                                         _pending.SpecialMode, reportedFloor, totalPlayerDamage,
                                         Simulation.State.ItemsUsed);
                RunContext.ClearPending();
                ServiceLocator.Get<Game.Core.Scenes.ISceneTransitionService>().LoadSceneWithOverlay("Meta");
            }
            else
            {
                Debug.Log($"[Battle] Kết thúc test độc lập — {(victory ? "thắng" : "thua")}. " +
                          "Không có node đang chờ nên không quay lại Meta.");
            }
        }

        /// <summary>HUD gọi khi người chơi chọn skill + mục tiêu.</summary>
        public void SubmitPlayerAction(int skillSlot, int targetId, CommandGrade grade)
        {
            if (Simulation == null || !Simulation.NeedsPlayerInput) return;
            Simulation.SubmitIntent(new ActionIntent(
                Simulation.CurrentActor.Id, skillSlot, targetId, grade));
            Presenter.PlayPending();
        }
    }
}

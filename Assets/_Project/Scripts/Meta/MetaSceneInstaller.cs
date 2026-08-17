using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Core.Random;
using Game.Core.UI;
using Game.Data;
using Game.Data.Dto;
using Game.Meta.Dungeon;
using Game.Meta.Codex;
using Game.Meta.Endgame;
using Game.Meta.Equipment;
using Game.Meta.Hero;
using Game.Meta.Items;
using Game.Meta.Mail;
using Game.Meta.Quest;
using Game.Services.Audio;
using Game.Services.Economy;
using Game.Services.Localization;
using Game.Services.Save;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Meta
{
    /// <summary>
    /// Dựng scene Meta: node map của chương hiện tại + xử lý kết quả trận vừa đấu.
    /// object-map.md §3.2 (rút gọn cho P2 Vertical Slice — Home/chọn-chương gộp vào
    /// đây, sẽ tách màn hình riêng ở P4/P6). Dựng UI bằng code như BattleHudScreen ở P1.
    ///
    /// QUY TẮC: chỉ Meta mới được ghi <see cref="PlayerProfileDto.Run"/> và gọi
    /// <see cref="NodeMapGenerator"/> — Battle scene chỉ ĐỌC qua <see cref="RunContext.Pending"/>
    /// và TRẢ kết quả qua <see cref="RunContext.ReportResult"/>.
    /// </summary>
    public sealed class MetaSceneInstaller : MonoBehaviour
    {
        private const int BATTLE_ENEMY_COUNT = 3;
        private const int ELITE_ENEMY_COUNT = 2;

        private static readonly Color BG = new(0.106f, 0.067f, 0.114f, 1f);
        private static readonly Color TEXT = new(0.949f, 0.910f, 0.810f);
        private static readonly Color LINE = new(0.42f, 0.34f, 0.44f);

        private static readonly Dictionary<NodeType, Color> NODE_COLORS = new()
        {
            [NodeType.Battle] = new Color(0.902f, 0.400f, 0.275f),
            [NodeType.Elite] = new Color(0.902f, 0.224f, 0.275f),
            [NodeType.Boss] = new Color(0.780f, 0.100f, 0.140f),
            [NodeType.Treasure] = new Color(1.000f, 0.820f, 0.400f),
            [NodeType.Event] = new Color(0.608f, 0.365f, 0.898f),
            [NodeType.Shop] = new Color(0.271f, 0.482f, 0.616f),
            [NodeType.Rest] = new Color(0.482f, 0.788f, 0.314f),
            [NodeType.Mystery] = new Color(0.6f, 0.6f, 0.6f),
        };

        /// <summary>Màu chủ đạo theo biome từng chương — khớp BOSS_BY_CHAPTER (Ch1 Đồng Cỏ/Alpha
        /// Wolf, Ch2 Đầm Lầy/Goblin King, Ch3 Hầm Mộ/Lich, Ch4 Núi Lửa/Magma Drake, Ch5 Vực
        /// Thẳm/Void King). Trước đây mọi chương dùng chung 1 màu nền/viền, không phân biệt
        /// được đang ở chương nào ngoài số hiển thị.</summary>
        private static readonly Color[] CHAPTER_ACCENT =
        {
            new(0.482f, 0.788f, 0.314f),  // Ch1 Meadow — xanh lá
            new(0.596f, 0.545f, 0.223f),  // Ch2 Swamp — vàng bùn
            new(0.443f, 0.541f, 0.808f),  // Ch3 Crypt — xanh lam lạnh
            new(0.902f, 0.400f, 0.196f),  // Ch4 Volcano — cam lửa
            new(0.647f, 0.298f, 0.808f),  // Ch5 Void — tím vực thẳm
        };

        // internal (không private) — ChapterProgressScreen (task-chapter-arena.md) tái dùng đúng
        // bảng màu này, tránh bịa 1 bảng màu chương thứ 2 (cùng assembly Game.Meta).
        internal static Color ChapterAccent(int chapterId)
            => CHAPTER_ACCENT[Mathf.Clamp(chapterId - 1, 0, CHAPTER_ACCENT.Length - 1)];

        internal static readonly string[] CHAPTER_NAMES =
            { "Meadow", "Swamp", "Crypt", "Volcano", "Void" };

        private PlayerProfileDto _profile;
        private IPlayerRepository _repo;
        private IAudioService _audio;
        private IEconomyService _economy;
        private ILocalizationService _loc;
        /// <summary>Chỉ dùng cho loot placeholder (task-ascend.md §7 mục D.1) — Meta layer chưa
        /// theo kỷ luật RNG như Game.Combat, seed KHÔNG lưu vào save (ngoài phạm vi lượt này).</summary>
        private IRandomSource _lootRng;

        [Header("__Systems__ dựng sẵn trong scene Meta")]
        [SerializeField] private Camera _metaCamera;

        // MetaCanvas dựng sẵn trong Boot.unity (GameBootstrap/__UI__/UIRoot/MetaCanvas) —
        // 2 scene khác file nên KHÔNG wire được qua Inspector, phải bind bằng code ở
        // BindCanvasRefs() (chỉ tìm object có sẵn, không new GameObject()/SetParent() nữa).
        private RectTransform _canvasRoot;
        private Image _topBarBorder;
        private Button _settingsButton;
        private Button _summonButton;
        private Button _questButton;
        private Button _dungeonButton;
        private Button _trialBossButton;
        private Button _towerButton;
        private Button _mailButton;
        private Button _codexButton;
        private Button _inventoryButton;
        private Button _heroListButton;
        private Button _arenaButton;
        private Button _titleButton;
        private RectTransform _mapRoot;
        private Text _titleLabel, _walletLabel, _toastLabel;
        private GameObject _toastPanel;
        private GameObject _mailBadge;
        private Text _mailBadgeLabel;
        private Image _leaderPortrait;

        private readonly Dictionary<int, Button> _nodeButtons = new();
        private readonly Dictionary<int, Vector2> _nodePositions = new();
        private SettingsScreen _settingsScreen;
        private TeamSelectScreen _teamSelectScreen;
        private ShopScreen _shopScreen;
        private NodeChoiceScreen _nodeChoiceScreen;
        private SummonScreen _summonScreen;
        private QuestScreen _questScreen;
        private DungeonScreen _dungeonScreen;
        private TrialBossScreen _trialBossScreen;
        private TowerScreen _towerScreen;
        private MailScreen _mailScreen;
        private CodexScreen _codexScreen;
        private InventoryScreen _inventoryScreen;
        private Game.Meta.HeroList.HeroListScreen _heroListScreen;
        private ChapterProgressScreen _chapterProgressScreen;
        private Color _chapterAccent = Color.white;

        private readonly struct PulseNode
        {
            public readonly RectTransform Root;
            public readonly Image Ring;
            public readonly Color BaseColor;
            public PulseNode(RectTransform root, Image ring, Color baseColor)
            { Root = root; Ring = ring; BaseColor = baseColor; }
        }
        private readonly List<PulseNode> _pulseNodes = new();

        // =====================================================================

        private void Start()
        {
            _profile = ProfileContext.Current;
            if (_profile == null)
            {
                Debug.LogError("[Meta] Không có profile — hãy chạy từ scene Boot, không mở thẳng Meta.");
                return;
            }

            ServiceLocator.TryGet(out _repo);
            ServiceLocator.TryGet(out _audio);
            ServiceLocator.TryGet(out _economy);
            ServiceLocator.TryGet(out _loc);
            _lootRng = new XorShiftRandom((ulong)System.DateTime.UtcNow.Ticks);
            _audio?.PlayMusic("bgm_menu");

            QuestSystem.EnsureDailyReset(_profile, System.DateTime.UtcNow);
            ApplyPendingBattleResult();
            EnsureRun();
            // BindCanvasRefs() TRƯỚC check resume — _canvasRoot chỉ được gán bên trong đó, còn
            // TryResumeBattleFromSnapshot() cần _canvasRoot để ẩn Meta UI trước khi qua Battle.
            // BuildUi() ở nhánh KHÔNG resume tự gọi lại BindCanvasRefs() — gọi 2 lần vô hại (chỉ
            // Find() lại đúng những gì đã tìm), nhưng KHÔNG xoá lời gọi trong BuildUi() vì đó vẫn
            // phải là nguồn sự thật cho mọi caller khác của BuildUi().
            BindCanvasRefs();
            if (TryResumeBattleFromSnapshot()) return;
            Equipment.EquipmentService.EnsureStarterEquipment(_profile);
            BuildUi();
            RefreshMap();
        }

        /// <summary>Edge case E17 (plan.md §4.14) — nếu profile có trận đang dở (app bị thoát/kill
        /// giữa chừng, xem <c>BattleSceneInstaller.OnApplicationPause</c>), tự động vào lại Battle
        /// scene ngay khi Meta khởi động thay vì hiện Node Map. KHÔNG có dialog xác nhận ở bản này
        /// (đơn giản hoá tối thiểu, ghi rõ ngoài phạm vi ở task-edgecases.md).</summary>
        private bool TryResumeBattleFromSnapshot()
        {
            var snap = _profile.Run?.BattleSnapshot;
            if (snap == null || !snap.Valid) return false;

            RunContext.QueueBattle(snap.NodeId, snap.HeroDefIds, snap.EnemyDefIds, snap.Seed, ComputeAutoItemLoadout());
            _canvasRoot.gameObject.SetActive(false);
            ServiceLocator.Get<Game.Core.Scenes.ISceneTransitionService>().LoadSceneWithOverlay("Battle");
            return true;
        }

        /// <summary>Thở phồng-xẹp 1 nhịp/giây trên viền + tăng nhẹ scale — kiểu "blink focus"
        /// để mắt tự động bị hút vào node đang bấm được, không cần đọc màu mới hiểu.</summary>
        private void Update()
        {
            if (_pulseNodes.Count == 0) return;

            float pulse = (Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f) + 1f) * 0.5f; // 0..1, 1Hz
            for (int i = 0; i < _pulseNodes.Count; i++)
            {
                var p = _pulseNodes[i];
                p.Ring.color = Color.Lerp(p.BaseColor, Color.white, pulse);
                p.Root.localScale = Vector3.one * (1f + pulse * 0.12f);
            }
        }

        // =====================================================================
        // Kết quả trận vừa xong (nếu quay lại từ Battle)
        // =====================================================================

        private void ApplyPendingBattleResult()
        {
            var result = RunContext.LastResult;
            RunContext.ClearResult();
            if (result == null) return;

            var run = _profile.Run;
            // Edge case E17: trận đã có kết quả thật (Victory/Defeat/Escaped/Timeout) → mọi
            // snapshot cũ (nếu app từng bị pause giữa trận NÀY) hết hiệu lực, xoá luôn — tránh
            // "resume" nhầm vào 1 trận đã kết thúc nếu app pause bình thường (không bị kill) rồi
            // người chơi tự đánh xong trận trong CÙNG 1 scene, không qua TryResumeFromSnapshot.
            if (run != null) run.BattleSnapshot = null;
            SyncConsumedItems(result.ItemsUsed);

            if (result.SpecialMode != null)
            {
                ApplySpecialBattleResult(result);
                SaveProfile();
                return;
            }

            var node = run?.MapNodes.Find(n => n.Id == result.NodeId);
            if (node == null) return;

            if (result.Victory)
            {
                _economy.Grant(_profile.Wallet, CurrencyType.Gold, result.Gold);
                int totalLevelUps = 0;
                foreach (var h in _profile.Heroes)
                    totalLevelUps += HeroLevelSystem.AddExp(h, result.Exp);
                MarkVisitedAndUnlock(run, node);
                run.CurrentNodeId = node.Id;

                // task-quest.md — counter trọn đời + tiến độ Quest hằng ngày (thay Gem faucet tạm
                // ở GrantBossAscendMaterials, xem mục 5 file task-quest.md).
                _profile.Stats.BattlesWon++;
                _profile.Stats.HeroLevelUps += totalLevelUps;
                QuestSystem.IncrementDailyProgress(_profile, QuestConditionType.BattlesWon, 1);
                QuestSystem.IncrementDailyProgress(_profile, QuestConditionType.HeroLevelUps, totalLevelUps);

                if (node.Type == (int)NodeType.Boss)
                {
                    run.Active = false;
                    _profile.Progress.ChapterUnlocked++;
                    GrantBossAscendMaterials(result.HeroDefIds);
                    Toast($"Chapter complete! Chapter {_profile.Progress.ChapterUnlocked} unlocked.");
                }
                else if (totalLevelUps > 0)
                {
                    Toast($"+{result.Gold} Gold — {totalLevelUps} hero level-up{(totalLevelUps > 1 ? "s" : "")}!");
                }
            }
            else
            {
                _profile.Stats.BattlesLost++;
            }
            // Thua: KHÔNG đánh dấu đã qua — người chơi thử lại node đó (đơn giản hoá P2;
            // roguelike "mất run khi thua" để dành cho P3+ khi có đủ nội dung bù đắp).

            SaveProfile();
        }

        /// <summary>Dungeon/Trial Boss (task-endgame.md, plan.md §8.3) — trận KHÔNG gắn node map
        /// (<c>result.NodeId == -1</c>, xem <see cref="RunContext.QueueSpecialBattle"/>). Trial Boss
        /// tính theo damage (mọi kết quả trận đều ghi nhận, kể cả Timeout/Defeat — xem
        /// <see cref="TrialBossSystem.RecordAttempt"/>); Dungeon (Gold/Exp/Material/Stone) chỉ cấp
        /// thưởng khi thắng thật.</summary>
        private void ApplySpecialBattleResult(BattleOutcome result)
        {
            var kind = result.SpecialMode!.Value;
            if (kind == DungeonKind.TrialBoss)
            {
                TrialBossSystem.EnsureWeeklyReset(_profile, System.DateTime.UtcNow);
                TrialBossSystem.RecordAttempt(_profile, result.TotalPlayerDamage);
                bool claimed = TrialBossSystem.TryClaimRewards(_profile, _economy);
                Toast(claimed
                    ? $"Trial Boss — {result.TotalPlayerDamage:N0} damage! Reward claimed."
                    : $"Trial Boss — {result.TotalPlayerDamage:N0} damage.");
                return;
            }

            if (kind == DungeonKind.Tower)
            {
                // result.SpecialFloor ở đây là TẦNG ĐÃ LEO ĐƯỢC trong lượt vừa xong (xem
                // BattleSceneInstaller.HandleContinue) — không phải tầng khởi đầu như Dungeon.
                TowerSystem.EnsureWeeklyReset(_profile, System.DateTime.UtcNow);
                TowerSystem.RecordClimb(_profile, result.SpecialFloor);
                bool claimedTower = TowerSystem.TryClaimRewards(_profile, _economy, _lootRng);
                Toast(claimedTower
                    ? $"Tower — Floor {result.SpecialFloor} reached! Reward claimed."
                    : $"Tower — Floor {result.SpecialFloor} reached.");
                return;
            }

            DungeonSystem.EnsureDailyReset(_profile, System.DateTime.UtcNow);
            if (result.Victory)
            {
                DungeonSystem.MarkFloorCleared(_profile, kind, result.SpecialFloor);
                DungeonSystem.GrantFloorReward(_profile, kind, result.SpecialFloor, _economy);
                Toast($"{kind} Dungeon — Floor {result.SpecialFloor} cleared!");
            }
            else
            {
                _profile.Stats.BattlesLost++;
                Toast($"{kind} Dungeon — defeated on Floor {result.SpecialFloor}.");
            }
        }

        /// <summary>Vật liệu Ascend khi thắng Boss — task-loottable.md. Ưu tiên
        /// <see cref="LootRoller"/> (asset thật theo Chapter/NodeType.Boss); nếu chưa author
        /// asset nào (fallback an toàn) thì giữ nguyên hành vi hằng số cũ
        /// (task-ascend.md §8/§9). Gold KHÔNG lấy từ bảng loot ở đây — Boss đã có Gold riêng từ
        /// `result.Gold` trong `ApplyPendingBattleResult`, tránh cấp Gold 2 lần.</summary>
        private void GrantBossAscendMaterials(System.Collections.Generic.IReadOnlyList<string> heroDefIds)
        {
            var table = LootRoller.Resolve(_profile.Run.ChapterId, NodeType.Boss);
            if (table != null)
            {
                var roll = LootRoller.Roll(table, _lootRng, _profile.Heroes.Count);
                foreach (var m in roll.Materials)
                    _economy.Grant(_profile.Wallet, m.Type, m.Amount);
            }
            else
            {
                _economy.Grant(_profile.Wallet, CurrencyType.EssenceII, PlaceholderLootTable.BOSS_REWARD_ESSENCE_II);
                _economy.Grant(_profile.Wallet, CurrencyType.Core, PlaceholderLootTable.BOSS_REWARD_CORE);
                _economy.Grant(_profile.Wallet, CurrencyType.EssenceIII, PlaceholderLootTable.BOSS_REWARD_ESSENCE_III);
                // Gem KHÔNG còn ở đây — task-quest.md thay Gem faucet tạm (100/Boss) bằng
                // QuestSystem (Daily quest + Achievement thật). Xem task-ascend.md §9.
            }
            if (heroDefIds == null) return;
            foreach (var defId in heroDefIds)
                _economy.GrantShards(_profile.Wallet, defId, 1);
        }

        /// <summary>task-consumable-items.md §0.6 — KHÔNG có màn chọn loadout thủ công (chỉ 6
        /// loại item tồn tại trong cả game, "chọn 5 trong 6" không đáng 1 màn hình riêng). Tự
        /// động mang tối đa 5 loại (theo đúng thứ tự cố định <see cref="ItemCatalog.ALL"/>) ×
        /// tối đa 3 cái/loại, giới hạn bởi số THẬT đang sở hữu.</summary>
        private Dictionary<ItemType, int> ComputeAutoItemLoadout()
        {
            const int MAX_TYPES = 5;
            const int MAX_PER_TYPE = 3;

            var loadout = new Dictionary<ItemType, int>();
            foreach (var def in ItemCatalog.ALL)
            {
                if (loadout.Count >= MAX_TYPES) break;
                long owned = _economy.GetItemCount(_profile.Inventory, def.Type);
                if (owned <= 0) continue;
                loadout[def.Type] = (int)System.Math.Min(MAX_PER_TYPE, owned);
            }
            return loadout;
        }

        /// <summary>Trừ vĩnh viễn số item ĐÃ DÙNG THẬT trong trận (khác đã mang) khỏi
        /// <c>profile.Inventory.Items</c> — gọi ở mọi nhánh xử lý kết quả trận (node map +
        /// Dungeon/TrialBoss/Tower).</summary>
        private void SyncConsumedItems(IReadOnlyDictionary<ItemType, int> itemsUsed)
        {
            if (itemsUsed == null) return;
            foreach (var kv in itemsUsed)
                _economy.TryConsumeItem(_profile.Inventory, kv.Key, kv.Value);
        }

        // =====================================================================
        // Sinh / tải run hiện tại
        // =====================================================================

        private void EnsureRun()
        {
            if (_profile.Run is { Active: true } run && run.MapNodes.Count > 0)
            {
                RunContext.Run = run;
                return;
            }

            long seed = System.DateTime.UtcNow.Ticks;
            var fresh = NodeMapGenerator.Generate(seed, _profile.Progress.ChapterUnlocked);
            _profile.Run = fresh;
            RunContext.Run = fresh;
            SaveProfile();
        }

        private void SaveProfile()
        {
            _profile.Run = RunContext.Run;
            _repo?.SaveAsync(_profile);
        }

        // =====================================================================
        // Xử lý bấm node
        // =====================================================================

        private void OnNodeClicked(MapNodeDto node)
        {
            if (node.State != (int)NodeState.Available) return;

            switch ((NodeType)node.Type)
            {
                case NodeType.Battle:
                case NodeType.Elite:
                case NodeType.Boss:
                    StartBattle(node);
                    break;
                case NodeType.Rest:
                    ResolveRest(node);
                    break;
                case NodeType.Treasure:
                    ResolveTreasure(node);
                    break;
                case NodeType.Event:
                    ResolveEvent(node);
                    break;
                case NodeType.Shop:
                    ResolveShop(node);
                    break;
                case NodeType.Mystery:
                    ResolveMystery(node);
                    break;
                default:
                    ResolveSimple(node, "Not available in this demo — skipping this node.");
                    break;
            }
        }

        /// <summary>Mystery — plan.md §8.1: "ngẫu nhiên trong các loại trên" khi mở, tỉ lệ theo
        /// đúng bảng §8.1 chuẩn hoá lại (bỏ Mystery/Boss, cộng về 100%). KHÔNG đổi
        /// <c>node.Type</c> đã lưu (giữ nguyên Mystery trên map — chỉ chọn NGẪU NHIÊN cách xử lý
        /// ngay lúc bấm, đúng tinh thần "không biết trước sẽ gặp gì").
        /// KHÔNG tách riêng nhánh Elite — <see cref="LaunchBattle"/> đọc thẳng <c>node.Type</c> để
        /// quyết định số lượng/độ khó địch, mà node vẫn giữ nguyên Type=Mystery ở đây (không mutate
        /// map đã lưu), nên gộp chung vào tỉ lệ Battle thường — đơn giản hoá có chủ đích cho 2%
        /// node hiếm gặp này.</summary>
        private void ResolveMystery(MapNodeDto node)
        {
            float r = Random.value;
            if (r < 0.61f) StartBattle(node);
            else if (r < 0.71f) ResolveTreasure(node);
            else if (r < 0.83f) ResolveEvent(node);
            else if (r < 0.91f) ResolveShop(node);
            else ResolveRest(node);
        }

        /// <summary>Boss theo chương — plan.md §8.2 (Ch1 Alpha Wolf, Ch2 Goblin King, Ch3 Lich,
        /// Ch4 Magma Drake, Ch5 Void King).</summary>
        private static readonly string[] BOSS_BY_CHAPTER =
            { "boss_alpha_wolf", "boss_goblin_king", "boss_lich", "boss_magma_drake", "boss_void_king" };

        /// <summary>Mở màn chọn đội hình trước khi vào trận — trước đây tự lấy 4 hero đầu
        /// tiên, người chơi không được chọn ai ra trận dù đã có 6 hero.</summary>
        private void StartBattle(MapNodeDto node)
        {
            if (_profile.Heroes.Count == 0)
            {
                Toast("No heroes in your party!");
                return;
            }

            _teamSelectScreen.Open(_profile, node, chosenHeroIds => LaunchBattle(node, chosenHeroIds));
        }

        private void LaunchBattle(MapNodeDto node, List<string> heroIds)
        {
            if (heroIds == null || heroIds.Count == 0)
            {
                Toast("No heroes in your party!");
                return;
            }

            List<string> enemyIds;
            if (node.Type == (int)NodeType.Boss)
            {
                enemyIds = new List<string> { PickBoss(_profile.Run.ChapterId) };
            }
            else
            {
                int enemyCount = node.Type == (int)NodeType.Elite ? ELITE_ENEMY_COUNT : BATTLE_ENEMY_COUNT;
                enemyIds = PickEnemies(enemyCount, node.Type == (int)NodeType.Elite, _profile.Run.ChapterId);
            }

            long seed = System.DateTime.UtcNow.Ticks;
            // task-phase-5-gaps.md Phần B — trận battle-node ĐẦU TIÊN mỗi profile mới. Không phân
            // biệt loại node (Battle/Elite/Boss) vì node map luôn đặt Boss cuối cùng — trận đầu
            // tiên một profile mới thật sự bấm luôn là Battle thường.
            bool isTutorial = !_profile.Progress.TutorialCompleted;
            RunContext.QueueBattle(node.Id, heroIds, enemyIds, seed, ComputeAutoItemLoadout(),
                                   _teamSelectScreen.SelectedFormation, isTutorial);
            SaveProfile();

            // MetaCanvas sống trong UIRoot toàn cục (DontDestroyOnLoad, xem BindCanvasRefs()) —
            // không tự mất khi đổi scene như trước nữa, nên phải ẩn tay trước khi qua Battle,
            // kẻo đè lên HUD trận đấu. BindCanvasRefs() sẽ hiện lại khi quay về Meta.
            _canvasRoot.gameObject.SetActive(false);
            ServiceLocator.Get<Game.Core.Scenes.ISceneTransitionService>().LoadSceneWithOverlay("Battle");
        }

        private static string PickBoss(int chapterId)
        {
            int index = Mathf.Clamp(chapterId - 1, 0, BOSS_BY_CHAPTER.Length - 1);
            return BOSS_BY_CHAPTER[index];
        }

        /// <summary>Chọn địch cho Battle/Elite — LOẠI TRỪ Boss, chỉ lấy đúng chương hiện tại
        /// (rơi về chương 1 nếu chương đó chưa có địch riêng).</summary>
        private static List<string> PickEnemies(int count, bool tougher, int chapterId)
        {
            var byChapter = Resources.LoadAll<Game.Meta.Content.EnemyDefinitionSO>("Data/Enemies")
                                     .Where(e => e.Archetype != ArchetypeId.Boss && e.Chapter == chapterId)
                                     .ToArray();
            var all = byChapter.Length > 0
                ? byChapter
                : Resources.LoadAll<Game.Meta.Content.EnemyDefinitionSO>("Data/Enemies")
                          .Where(e => e.Archetype != ArchetypeId.Boss).ToArray();
            if (all.Length == 0) return new List<string>();

            var pool = tougher
                ? all.OrderByDescending(e => e.PoiseMax).Take(System.Math.Max(count * 2, count)).ToArray()
                : all;

            var result = new List<string>(count);
            for (int i = 0; i < count; i++)
                result.Add(pool[Random.Range(0, pool.Length)].DefId);
            return result;
        }

        // =====================================================================
        // Dungeon / Trial Boss (task-endgame.md, plan.md §8.3) — vào trận độc lập với node map,
        // gọi từ DungeonScreen/TrialBossScreen (Task 16). Tái dùng TeamSelectScreen + PickEnemies
        // đã có cho node map thường; <c>node</c> truyền null vì TeamSelectScreen chỉ lưu tham
        // chiếu này chứ không đọc field nào của nó (đã kiểm tra toàn bộ TeamSelectScreen.cs).
        // =====================================================================

        public void LaunchDungeonFloor(DungeonKind kind)
        {
            DungeonSystem.EnsureDailyReset(_profile, System.DateTime.UtcNow);
            if (!DungeonSystem.CanEnter(_profile, kind, System.DateTime.UtcNow))
            {
                Toast($"{kind} Dungeon not available right now.");
                return;
            }
            if (_profile.Heroes.Count == 0)
            {
                Toast("No heroes in your party!");
                return;
            }

            int floor = DungeonSystem.NextFloor(_profile, kind);
            _teamSelectScreen.Open(_profile, null, chosenHeroIds => LaunchDungeonBattle(kind, floor, chosenHeroIds));
        }

        private void LaunchDungeonBattle(DungeonKind kind, int floor, List<string> heroIds)
        {
            if (heroIds == null || heroIds.Count == 0)
            {
                Toast("No heroes in your party!");
                return;
            }

            int enemyCount = DungeonSystem.EnemyCountForFloor(floor);
            var enemyIds = PickEnemies(enemyCount, DungeonSystem.IsTougherFloor(floor), _profile.Run.ChapterId);
            long seed = System.DateTime.UtcNow.Ticks;
            RunContext.QueueSpecialBattle(kind, floor, heroIds, enemyIds, seed, ComputeAutoItemLoadout(),
                                         _teamSelectScreen.SelectedFormation);
            SaveProfile();

            _canvasRoot.gameObject.SetActive(false);
            ServiceLocator.Get<Game.Core.Scenes.ISceneTransitionService>().LoadSceneWithOverlay("Battle");
        }

        public void LaunchTrialBoss()
        {
            TrialBossSystem.EnsureWeeklyReset(_profile, System.DateTime.UtcNow);
            if (_profile.Heroes.Count == 0)
            {
                Toast("No heroes in your party!");
                return;
            }

            _teamSelectScreen.Open(_profile, null, LaunchTrialBossBattle);
        }

        private void LaunchTrialBossBattle(List<string> heroIds)
        {
            if (heroIds == null || heroIds.Count == 0)
            {
                Toast("No heroes in your party!");
                return;
            }

            var enemyIds = new List<string> { TrialBossSystem.BOSS_DEF_ID };
            long seed = System.DateTime.UtcNow.Ticks;
            RunContext.QueueSpecialBattle(DungeonKind.TrialBoss, 0, heroIds, enemyIds, seed, ComputeAutoItemLoadout(),
                                         _teamSelectScreen.SelectedFormation);
            SaveProfile();

            _canvasRoot.gameObject.SetActive(false);
            ServiceLocator.Get<Game.Core.Scenes.ISceneTransitionService>().LoadSceneWithOverlay("Battle");
        }

        /// <summary>Mỗi lượt leo Tháp Vô Tận LUÔN bắt đầu lại từ tầng 1 với HP đầy (đúng cách mọi
        /// trận khác luôn full HP lúc bắt đầu) — mốc cao nhất mọi thời đại
        /// (<see cref="Data.Dto.ProgressDto.TowerFloor"/>) và bậc thưởng hằng tuần chỉ cập nhật
        /// SAU khi lượt leo kết thúc (xem <see cref="ApplySpecialBattleResult"/>).</summary>
        public void LaunchTower()
        {
            TowerSystem.EnsureWeeklyReset(_profile, System.DateTime.UtcNow);
            if (_profile.Heroes.Count == 0)
            {
                Toast("No heroes in your party!");
                return;
            }

            _teamSelectScreen.Open(_profile, null, LaunchTowerBattle);
        }

        private void LaunchTowerBattle(List<string> heroIds)
        {
            if (heroIds == null || heroIds.Count == 0)
            {
                Toast("No heroes in your party!");
                return;
            }

            // Tầng 1 chọn ở đây (đúng pattern Dungeon/TrialBoss) — chapterId=0 (không chương nào
            // có id này) để PickEnemies rơi thẳng vào nhánh fallback "mọi enemy" thay vì lọc theo
            // chương, vì Tháp là nội dung endgame dùng chung mọi enemy đã có. Từ tầng 2 trở đi,
            // BattleSceneInstaller.PickTowerEnemies tự chọn TRỰC TIẾP trong Battle scene qua
            // Simulation.State.Rng (xác định theo seed, không phải UnityEngine.Random) — cần thiết
            // để BattleReplay (edge case E17) resume đúng nếu app thoát giữa 1 lượt leo Tháp.
            int enemyCount = TowerSystem.EnemyCountForFloor(1);
            var enemyIds = PickEnemies(enemyCount, TowerSystem.IsTougherFloor(1), chapterId: 0);
            long seed = System.DateTime.UtcNow.Ticks;
            RunContext.QueueSpecialBattle(DungeonKind.Tower, 1, heroIds, enemyIds, seed, ComputeAutoItemLoadout());
            SaveProfile();

            _canvasRoot.gameObject.SetActive(false);
            ServiceLocator.Get<Game.Core.Scenes.ISceneTransitionService>().LoadSceneWithOverlay("Battle");
        }

        /// <summary>task-eventrest.md — trước đây auto-heal + gold cố định, không có lựa chọn nào.
        /// Nay mở <see cref="NodeChoiceScreen"/> (giống <see cref="ResolveShop"/> đã mở
        /// <see cref="ShopScreen"/>) — CHỈ đánh dấu đã ghé/lưu/refresh map SAU khi đóng modal.</summary>
        private void ResolveRest(MapNodeDto node)
        {
            _nodeChoiceScreen.Open(_profile, true, _economy, _lootRng, () =>
            {
                MarkVisitedAndUnlock(_profile.Run, node);
                SaveProfile();
                RefreshMap();
            });
        }

        /// <summary>task-loottable.md — ưu tiên <see cref="LootRoller"/> (asset thật), fallback
        /// <see cref="PlaceholderLootTable"/> nếu chưa author asset nào cho Treasure.</summary>
        private void ResolveTreasure(MapNodeDto node)
        {
            var table = LootRoller.Resolve(_profile.Run.ChapterId, NodeType.Treasure);
            long gold;
            string bonusText;

            if (table != null)
            {
                var roll = LootRoller.Roll(table, _lootRng, _profile.Heroes.Count);
                gold = roll.Gold;
                _economy.Grant(_profile.Wallet, CurrencyType.Gold, gold);

                var parts = new System.Collections.Generic.List<string>();
                foreach (var m in roll.Materials)
                {
                    _economy.Grant(_profile.Wallet, m.Type, m.Amount);
                    parts.Add($"+{m.Amount} {m.Type}");
                }
                if (roll.ShardHeroIndex >= 0)
                {
                    var hero = _profile.Heroes[roll.ShardHeroIndex];
                    _economy.GrantShards(_profile.Wallet, hero.DefId, 1);
                    parts.Add($"+1 {HeroName(hero.DefId)} shard");
                }
                if (roll.Equipment != null)
                {
                    _profile.Equipment.Add(roll.Equipment);
                    var def = EquipmentService.DefOf(roll.Equipment);
                    string itemName = def != null && !string.IsNullOrEmpty(def.NameKey) ? def.NameKey : roll.Equipment.DefId;
                    parts.Add($"+1 {(Rarity)roll.Equipment.Rarity} {itemName}");
                }
                bonusText = parts.Count > 0 ? " · " + string.Join(" · ", parts) : "";
            }
            else
            {
                var roll = PlaceholderLootTable.RollTreasure(_lootRng, _profile.Heroes.Count);
                gold = roll.Gold;
                _economy.Grant(_profile.Wallet, CurrencyType.Gold, gold);

                if (roll.EssenceIAmount > 0)
                {
                    _economy.Grant(_profile.Wallet, CurrencyType.EssenceI, roll.EssenceIAmount);
                    bonusText = $" · +{roll.EssenceIAmount} Essence I";
                }
                else if (roll.ShardHeroIndex >= 0)
                {
                    var hero = _profile.Heroes[roll.ShardHeroIndex];
                    _economy.GrantShards(_profile.Wallet, hero.DefId, 1);
                    bonusText = $" · +1 {HeroName(hero.DefId)} shard";
                }
                else bonusText = "";
            }

            Toast($"Treasure! +{gold} gold{bonusText}.");
            MarkVisitedAndUnlock(_profile.Run, node);
            SaveProfile();
            RefreshMap();
        }

        /// <summary>task-eventrest.md — trước đây 1 roll ẩn tự động (UnityEngine.Random, không
        /// qua IRandomSource), người chơi không thấy hay chọn gì. Nay mở
        /// <see cref="NodeChoiceScreen"/> với 3 lựa chọn rủi ro thật (plan.md §8.1 "2-3 lựa chọn
        /// có rủi ro"), dùng <see cref="_lootRng"/> để xác định/test được — đồng nhất với
        /// <see cref="ResolveTreasure"/> thay vì <c>UnityEngine.Random</c> riêng.</summary>
        private void ResolveEvent(MapNodeDto node)
        {
            _nodeChoiceScreen.Open(_profile, false, _economy, _lootRng, () =>
            {
                MarkVisitedAndUnlock(_profile.Run, node);
                SaveProfile();
                RefreshMap();
            });
        }

        /// <summary>task-ascend.md §7 mục C — mở modal Shop, chỉ đánh dấu đã ghé/lưu/refresh map
        /// SAU khi đóng (khác Treasure/Event resolve ngay) vì đây là màn hình cần thời gian browse.</summary>
        private void ResolveShop(MapNodeDto node)
        {
            _shopScreen.Open(_profile, () =>
            {
                MarkVisitedAndUnlock(_profile.Run, node);
                SaveProfile();
                RefreshMap();
            });
        }

        private void ResolveSimple(MapNodeDto node, string message)
        {
            Toast(message);
            MarkVisitedAndUnlock(_profile.Run, node);
            SaveProfile();
            RefreshMap();
        }

        private static void MarkVisitedAndUnlock(RunStateDto run, MapNodeDto node)
        {
            node.State = (int)NodeState.Visited;
            foreach (var nextId in node.NextIds)
            {
                var next = run.MapNodes.Find(n => n.Id == nextId);
                if (next != null && next.State == (int)NodeState.Locked)
                    next.State = (int)NodeState.Available;
            }
        }

        // =====================================================================
        // UI — dựng bằng code (như BattleHudScreen ở P1), dùng UI.Text (không cần TMP
        // vì Game.Meta.asmdef không tham chiếu TMP — tránh phụ thuộc vòng ngược Game.UI).
        // =====================================================================

        private void BuildUi()
        {
            // MetaCanvas/TopBar/MapRoot/Toast dựng sẵn trong Boot.unity
            // (GameBootstrap/__UI__/UIRoot/MetaCanvas) — 2 scene khác file nên không
            // SetParent() bằng code nữa, chỉ tìm object có sẵn rồi bind.
            // EventSystem toàn cục cũng sống từ Boot (GameBootstrap/__Systems__/EventSystem).
            BindCanvasRefs();

            // Nhuộm nền theo biome chương hiện tại — trước đây mọi chương dùng chung 1 màu
            // nền tím, chỉ phân biệt được qua số "CHAPTER n" chứ không có cảm giác đổi vùng.
            _chapterAccent = ChapterAccent(_profile.Run.ChapterId);

            _metaCamera.backgroundColor = Color.Lerp(BG, _chapterAccent, 0.12f);
            _metaCamera.clearFlags = CameraClearFlags.SolidColor;
            _topBarBorder.color = _chapterAccent;

            _settingsScreen = gameObject.AddComponent<SettingsScreen>();
            _teamSelectScreen = gameObject.AddComponent<TeamSelectScreen>();
            _shopScreen = gameObject.AddComponent<ShopScreen>();
            _nodeChoiceScreen = gameObject.AddComponent<NodeChoiceScreen>();
            _summonScreen = gameObject.AddComponent<SummonScreen>();
            _questScreen = gameObject.AddComponent<QuestScreen>();
            _dungeonScreen = gameObject.AddComponent<DungeonScreen>();
            _trialBossScreen = gameObject.AddComponent<TrialBossScreen>();
            _towerScreen = gameObject.AddComponent<TowerScreen>();
            _mailScreen = gameObject.AddComponent<MailScreen>();
            _codexScreen = gameObject.AddComponent<CodexScreen>();
            _inventoryScreen = gameObject.AddComponent<InventoryScreen>();
            _heroListScreen = gameObject.AddComponent<Game.Meta.HeroList.HeroListScreen>();
            _chapterProgressScreen = gameObject.AddComponent<ChapterProgressScreen>();
            // task-mail-extras.md — MailScreen tự bắn OnMailChanged sau Claim/Claim-All/purge hết
            // hạn; TopBar là script độc lập (BindCanvasRefs comment ở trên) nên không tự biết,
            // cần nghe callback này để vẽ lại badge ngay cả khi modal Mail đang mở (không cần đợi
            // đóng modal).
            _mailScreen.OnMailChanged += RefreshMailBadge;
            RefreshMailBadge();
            // Enhance trang bị trừ Gold ngay trong TeamSelectScreen — TopBar là script khác,
            // không tự biết profile vừa đổi nên không refresh nếu không có callback này.
            _teamSelectScreen.OnProfileChanged += () =>
                _walletLabel.text = $"Gold {_profile.Wallet.Gold}   Gem {_profile.Wallet.Gem}";

            _settingsButton.onClick.AddListener(() =>
            {
                _audio?.PlaySfx("ui/sfx_ui_tick");
                _settingsScreen.Open();
            });

            _summonButton.onClick.AddListener(() =>
            {
                _audio?.PlaySfx("ui/sfx_ui_tick");
                _summonScreen.Open(_profile, () =>
                {
                    SaveProfile();
                    _walletLabel.text = $"Gold {_profile.Wallet.Gold}   Gem {_profile.Wallet.Gem}";
                });
            });

            _questButton.onClick.AddListener(() =>
            {
                _audio?.PlaySfx("ui/sfx_ui_tick");
                _questScreen.Open(_profile, () =>
                {
                    SaveProfile();
                    _walletLabel.text = $"Gold {_profile.Wallet.Gold}   Gem {_profile.Wallet.Gem}";
                });
            });

            _dungeonButton.onClick.AddListener(() =>
            {
                _audio?.PlaySfx("ui/sfx_ui_tick");
                _dungeonScreen.Open(_profile, LaunchDungeonFloor, SaveProfile);
            });

            _trialBossButton.onClick.AddListener(() =>
            {
                _audio?.PlaySfx("ui/sfx_ui_tick");
                _trialBossScreen.Open(_profile, LaunchTrialBoss, SaveProfile);
            });

            _towerButton.onClick.AddListener(() =>
            {
                _audio?.PlaySfx("ui/sfx_ui_tick");
                _towerScreen.Open(_profile, LaunchTower, SaveProfile);
            });

            _mailButton.onClick.AddListener(() =>
            {
                _audio?.PlaySfx("ui/sfx_ui_tick");
                _mailScreen.Open(_profile, () =>
                {
                    SaveProfile();
                    _walletLabel.text = $"Gold {_profile.Wallet.Gold}   Gem {_profile.Wallet.Gem}";
                });
            });

            // Codex thuần đọc dữ liệu, không đổi profile — không cần SaveProfile/refresh gì sau
            // khi đóng, khác mọi screen khác ở trên.
            _codexButton.onClick.AddListener(() =>
            {
                _audio?.PlaySfx("ui/sfx_ui_tick");
                _codexScreen.Open(_profile, null);
            });

            // Inventory cũng thuần đọc dữ liệu (task-inventory-screen.md), giống Codex.
            _inventoryButton.onClick.AddListener(() =>
            {
                _audio?.PlaySfx("ui/sfx_ui_tick");
                _inventoryScreen.Open(_profile, null);
            });

            // HeroList (task-hero-list.md) — bấm 1 dòng mở HeroDetailScreen bên trong, có thể đổi
            // Wallet (Ascend)/stat (skill level) nên refresh lại TopBar giống mọi màn có Ascend.
            _heroListButton.onClick.AddListener(() =>
            {
                _audio?.PlaySfx("ui/sfx_ui_tick");
                _heroListScreen.Open(_profile, null);
            });

            // task-chapter-arena.md — plan.md v1.1 mới làm Arena thật, v1.0 chỉ cần placeholder.
            _arenaButton.onClick.AddListener(() =>
            {
                _audio?.PlaySfx("ui/sfx_ui_tick");
                Toast("Arena — Coming Soon...");
            });

            // TitleLabel ("CHAPTER N") — entry point ChapterProgressScreen, không thêm nút TopBar
            // mới (đã khá đầy, xem doc-comment ChapterProgressScreen).
            _titleButton.onClick.AddListener(() =>
            {
                _audio?.PlaySfx("ui/sfx_ui_tick");
                _chapterProgressScreen.Open(_profile, null);
            });
        }

        /// <summary>MetaCanvas nằm vật lý trong Boot.unity (GameBootstrap/__UI__/UIRoot/
        /// MetaCanvas, DontDestroyOnLoad) — không phải trong Meta.unity — nên không wire được
        /// qua Inspector (2 scene khác file). Chỉ tìm object có sẵn qua ServiceLocator +
        /// Find(), không tạo hay đổi cha object nào.</summary>
        private void BindCanvasRefs()
        {
            var uiRoot = ServiceLocator.Get<IUiRootHost>().Root;
            _canvasRoot = (RectTransform)uiRoot.Find("MetaCanvas");
            // Có thể đang bị ẩn từ lần LaunchBattle() trước đó (xem đó) — hiện lại mỗi khi vào Meta.
            _canvasRoot.gameObject.SetActive(true);
            var topBar = _canvasRoot.Find("TopBar");
            _topBarBorder = topBar.GetComponent<Image>();
            _titleLabel = topBar.Find("TitleLabel").GetComponent<Text>();
            _titleButton = topBar.Find("TitleLabel").GetComponent<Button>();
            _walletLabel = topBar.Find("WalletLabel").GetComponent<Text>();
            _settingsButton = topBar.Find("SettingsButton").GetComponent<Button>();
            _summonButton = topBar.Find("SummonButton").GetComponent<Button>();
            _questButton = topBar.Find("QuestButton").GetComponent<Button>();
            _dungeonButton = topBar.Find("DungeonButton").GetComponent<Button>();
            _trialBossButton = topBar.Find("TrialBossButton").GetComponent<Button>();
            _towerButton = topBar.Find("TowerButton").GetComponent<Button>();
            _mailButton = topBar.Find("MailButton").GetComponent<Button>();
            _codexButton = topBar.Find("CodexButton").GetComponent<Button>();
            _inventoryButton = topBar.Find("InventoryButton").GetComponent<Button>();
            _heroListButton = topBar.Find("HeroListButton").GetComponent<Button>();
            _arenaButton = topBar.Find("ArenaButton").GetComponent<Button>();
            // task-mail-extras.md — MailBadge dựng tĩnh trong Boot.unity (không phải runtime),
            // mặc định inactive, chỉ RefreshMailBadge() mới bật lên khi có mail thật chưa claim.
            _mailBadge = _mailButton.transform.Find("MailBadge").gameObject;
            _mailBadgeLabel = _mailBadge.transform.Find("BadgeLabel").GetComponent<Text>();
            // Icon nhân vật (leader — hero đầu tiên trong roster) trên TopBar, dựng tĩnh trong
            // Boot.unity cạnh TitleLabel — thuần hiển thị, không có hành động bấm.
            _leaderPortrait = topBar.Find("LeaderPortrait/PortraitMask/PortraitSprite").GetComponent<Image>();
            _mapRoot = (RectTransform)_canvasRoot.Find("MapRoot");
            _toastPanel = _canvasRoot.Find("Toast").gameObject;
            _toastLabel = _toastPanel.transform.Find("ToastLabel").GetComponent<Text>();

            // Toast luôn active + có viền LINE dù rỗng — lộ ra thành 1 thanh xám mờ dưới đáy
            // màn hình mỗi khi có overlay bán trong suốt (VD Dim 65% của TeamSelect) đè lên.
            // Ẩn tới khi có message thật (xem Toast()).
            _toastPanel.SetActive(false);
        }

        /// <summary>task-mail-extras.md — badge số mail chưa claim trên MailButton. Đọc
        /// <c>MailSystem.UnclaimedCount</c> (đã có sẵn từ task-mail.md, chưa ai dùng tới lúc đó).
        /// Không tự purge mail hết hạn ở đây (chỉ <c>MailScreen.Open()</c> làm việc đó, xem
        /// task-mail-extras.md §0) — nếu người chơi chưa từng mở Mail, badge có thể đếm dư 1 mail
        /// đã hết hạn nhưng chưa bị dọn; tự đúng lại ngay khi họ mở Mail lần đầu (PurgeExpired chạy
        /// + OnMailChanged bắn). Giới hạn nhỏ, cố ý không mở rộng phạm vi purge ra ngoài MailScreen.
        /// </summary>
        private void RefreshMailBadge()
        {
            int count = MailSystem.UnclaimedCount(_profile);
            _mailBadge.SetActive(count > 0);
            if (count > 0) _mailBadgeLabel.text = count > 9 ? "9+" : count.ToString();
        }

        /// <summary>Icon leader trên TopBar — hero đầu tiên trong roster (không phải "hero mạnh
        /// nhất" hay đội hình trận vừa rồi, chỉ là 1 đại diện cố định đơn giản để TopBar có point
        /// of reference tới nhân vật, giống quy ước <c>_viewedHeroUid</c> mặc định của
        /// TeamSelectScreen). Ẩn hẳn nếu chưa có hero nào (chưa xảy ra ở luồng thật — profile luôn
        /// khởi tạo sẵn hero — nhưng vẫn thủ để không NRE nếu profile trống lúc debug).</summary>
        private void RefreshLeaderPortrait()
        {
            if (_profile.Heroes.Count == 0) { _leaderPortrait.enabled = false; return; }
            var defId = _profile.Heroes[0].DefId;
            var sprite = Resources.Load<Sprite>($"Art/Characters/Heroes/{defId}/{defId}_v1_00");
            _leaderPortrait.sprite = sprite;
            _leaderPortrait.enabled = sprite != null;
        }

        private void RefreshMap()
        {
            RefreshLeaderPortrait();
            foreach (Transform child in _mapRoot) Destroy(child.gameObject);
            _nodeButtons.Clear();
            _nodePositions.Clear();
            _pulseNodes.Clear();

            var run = _profile.Run;
            _titleLabel.text = $"CHAPTER {run.ChapterId}   |   {_profile.Heroes.Count} heroes";
            _walletLabel.text = $"Gold {_profile.Wallet.Gold}   Gem {_profile.Wallet.Gem}";
            RefreshMailBadge();

            int maxRow = 0;
            foreach (var n in run.MapNodes) maxRow = System.Math.Max(maxRow, n.RowIndex);

            var rowGroups = run.MapNodes.GroupBy(n => n.RowIndex).OrderBy(g => g.Key).ToList();

            foreach (var group in rowGroups)
            {
                var nodesInRow = group.OrderBy(n => n.ColIndex).ToList();
                float y = Mathf.Lerp(-190f, 190f, maxRow == 0 ? 0.5f : (float)group.Key / maxRow);

                for (int i = 0; i < nodesInRow.Count; i++)
                {
                    float t = nodesInRow.Count <= 1 ? 0.5f : (float)i / (nodesInRow.Count - 1);
                    float x = Mathf.Lerp(-380f, 380f, t);
                    _nodePositions[nodesInRow[i].Id] = new Vector2(x, y);
                }
            }

            // Vẽ đường nối trước để node đè lên trên
            foreach (var node in run.MapNodes)
                foreach (var nextId in node.NextIds)
                    DrawConnector(_nodePositions[node.Id], _nodePositions[nextId]);

            foreach (var node in run.MapNodes)
                CreateNodeButton(node);
        }

        /// <summary>Đường nối cong (Bezier bậc 2, xấp xỉ bằng nhiều đoạn ngắn bo tròn 2 đầu)
        /// + mũi tên ở cuối chỉ hướng đi — trước đây là 1 thanh chữ nhật thẳng cứng, giao nhau
        /// từng đường thẳng nhìn rất thô, không có gợi ý chiều đi.</summary>
        private void DrawConnector(Vector2 a, Vector2 b)
        {
            Vector2 dir = (b - a).normalized;
            Vector2 perp = new Vector2(-dir.y, dir.x);
            float dist = Vector2.Distance(a, b);
            // Độ cong tỉ lệ theo khoảng cách nhưng chặn trần — đường dài không bị vòng quá lố.
            float bow = Mathf.Min(dist * 0.16f, 26f) * (perp.y < 0 ? 1f : -1f);
            Vector2 control = (a + b) * 0.5f + perp * bow;

            const int SEGMENTS = 10;
            Color lineColor = Color.Lerp(LINE, _chapterAccent, 0.3f);
            Vector2 prev = a;
            for (int i = 1; i <= SEGMENTS; i++)
            {
                float t = i / (float)SEGMENTS;
                Vector2 pt = QuadraticBezier(a, control, b, t);
                DrawLineSegment(prev, pt, 3f, lineColor, $"Line_{i}");
                prev = pt;
            }

            // Mũi tên nhỏ ở ~85% đường đi, hướng đúng theo tiếp tuyến cong tại điểm đó —
            // báo trước "đi tiếp theo hướng này" thay vì chỉ có đường nối trơ.
            Vector2 arrowTip = QuadraticBezier(a, control, b, 0.86f);
            Vector2 arrowBack = QuadraticBezier(a, control, b, 0.78f);
            DrawArrowhead(arrowTip, (arrowTip - arrowBack).normalized, lineColor);
        }

        private static Vector2 QuadraticBezier(Vector2 p0, Vector2 p1, Vector2 p2, float t)
        {
            float u = 1f - t;
            return u * u * p0 + 2f * u * t * p1 + t * t * p2;
        }

        /// <summary>1 đoạn thẳng ngắn (rectangle phẳng) + 1 chấm tròn ở điểm cuối để làm mềm
        /// góc nối với đoạn kế tiếp. LƯU Ý: không dùng Image.Type.Sliced với CircleSprite() ở
        /// đây — sprite đó tạo bằng Sprite.Create KHÔNG truyền border 9-slice, nên Sliced sẽ
        /// chỉ kéo giãn cả hình tròn thành oval dẹt phủ hết thanh, không bo được 2 đầu.</summary>
        private void DrawLineSegment(Vector2 a, Vector2 b, float width, Color color, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(_mapRoot, false);
            var rt = (RectTransform)go.transform;

            Vector2 mid = (a + b) * 0.5f;
            float dist = Vector2.Distance(a, b);
            float angle = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;

            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = mid;
            rt.sizeDelta = new Vector2(dist + width, width);
            rt.localRotation = Quaternion.Euler(0, 0, angle);

            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;

            var dotGo = new GameObject(name + "_cap", typeof(RectTransform));
            dotGo.transform.SetParent(_mapRoot, false);
            var drt = (RectTransform)dotGo.transform;
            drt.anchorMin = drt.anchorMax = new Vector2(0.5f, 0.5f);
            drt.pivot = new Vector2(0.5f, 0.5f);
            drt.anchoredPosition = b;
            drt.sizeDelta = new Vector2(width, width);
            var dimg = dotGo.AddComponent<Image>();
            dimg.sprite = CircleSprite();
            dimg.color = color;
            dimg.raycastTarget = false;
        }

        private void DrawArrowhead(Vector2 tip, Vector2 dir, Color color)
        {
            var go = new GameObject("Arrow", typeof(RectTransform));
            go.transform.SetParent(_mapRoot, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = tip;
            rt.sizeDelta = new Vector2(16, 16);
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            rt.localRotation = Quaternion.Euler(0, 0, angle);

            var img = go.AddComponent<Image>();
            img.sprite = TriangleSprite();
            img.color = color;
            img.raycastTarget = false;
        }

        private void CreateNodeButton(MapNodeDto node)
        {
            const float size = 42f;
            var go = new GameObject($"Node_{node.Id}_{(NodeType)node.Type}", typeof(RectTransform));
            go.transform.SetParent(_mapRoot, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = _nodePositions[node.Id];

            var baseColor = NODE_COLORS.TryGetValue((NodeType)node.Type, out var c) ? c : Color.white;
            var state = (NodeState)node.State;
            var fillColor = state switch
            {
                NodeState.Available => baseColor,
                NodeState.Visited => Color.Lerp(baseColor, Color.black, 0.6f),
                _ => new Color(0.2f, 0.18f, 0.22f)
            };

            // Nút tròn có viền (ring) thay vì ô vuông phẳng — badge node đọc rõ hơn và
            // khớp phong cách "huy hiệu" của image_UI.jpg thay vì khối màu thô.
            var ring = go.AddComponent<Image>();
            ring.sprite = CircleSprite();
            ring.color = state == NodeState.Available ? Color.Lerp(baseColor, Color.white, 0.35f) : LINE;

            var fillGo = new GameObject("Fill", typeof(RectTransform));
            fillGo.transform.SetParent(rt, false);
            var frt = (RectTransform)fillGo.transform;
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
            frt.offsetMin = new Vector2(3, 3); frt.offsetMax = new Vector2(-3, -3);
            var fill = fillGo.AddComponent<Image>();
            fill.sprite = CircleSprite();
            fill.color = fillColor;
            fill.raycastTarget = false;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = ring;
            btn.interactable = state == NodeState.Available;
            btn.onClick.AddListener(() =>
            {
                _audio?.PlaySfx("ui/sfx_ui_confirm");
                OnNodeClicked(node);
            });

            // Icon loại node thay cho chữ cái viết tắt — trước đây "B/E/K/$/?/S/+/..." không
            // đọc trực quan bằng hình, người chơi mới phải nhớ quy ước chữ.
            var iconGo = new GameObject("Icon", typeof(RectTransform));
            iconGo.transform.SetParent(rt, false);
            var iconRt = (RectTransform)iconGo.transform;
            iconRt.anchorMin = new Vector2(0.18f, 0.18f);
            iconRt.anchorMax = new Vector2(0.82f, 0.82f);
            iconRt.offsetMin = iconRt.offsetMax = Vector2.zero;
            var icon = iconGo.AddComponent<Image>();
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            var iconSprite = LoadNodeIcon((NodeType)node.Type);
            if (iconSprite != null)
            {
                icon.sprite = iconSprite;
                icon.color = TEXT;
            }
            else
            {
                // Chưa có icon cho loại node này (fallback an toàn) — vẫn hiện chữ thay vì trống.
                Object.Destroy(iconGo);
                var label = Label(rt, NodeGlyph((NodeType)node.Type), 16, TextAnchor.MiddleCenter,
                                  Vector2.zero, new Vector2(size, size));
                label.raycastTarget = false;
            }

            // Node khả dụng nhấp nháy (scale + sáng viền) — người chơi mới không phải đoán
            // node nào bấm được, trước đây chỉ khác nhau 1 chút độ sáng màu tĩnh, dễ bị bỏ qua.
            if (state == NodeState.Available)
                _pulseNodes.Add(new PulseNode(rt, ring, Color.Lerp(baseColor, Color.white, 0.35f)));
        }

        private static readonly Dictionary<NodeType, Sprite> _nodeIconCache = new();

        private static readonly Dictionary<NodeType, string> NODE_ICON_KEY = new()
        {
            [NodeType.Battle] = "battle", [NodeType.Elite] = "elite", [NodeType.Boss] = "boss",
            [NodeType.Treasure] = "treasure", [NodeType.Event] = "event", [NodeType.Shop] = "shop",
            [NodeType.Rest] = "rest", [NodeType.Mystery] = "mystery",
        };

        private static Sprite LoadNodeIcon(NodeType type)
        {
            if (_nodeIconCache.TryGetValue(type, out var cached)) return cached;
            var sprite = NODE_ICON_KEY.TryGetValue(type, out var key)
                ? Resources.Load<Sprite>($"Art/UI/Icons/Nodes/icon_node_{key}")
                : null;
            _nodeIconCache[type] = sprite;
            return sprite;
        }

        // Chữ ASCII thuần — LegacyRuntime.ttf (font mặc định UI.Text) không có glyph Unicode đặc biệt.
        // Dùng làm fallback nếu thiếu icon cho 1 loại node nào đó.
        private static string NodeGlyph(NodeType t) => t switch
        {
            NodeType.Battle => "B",
            NodeType.Elite => "E",
            NodeType.Boss => "K",
            NodeType.Treasure => "$",
            NodeType.Event => "?",
            NodeType.Shop => "S",
            NodeType.Rest => "+",
            NodeType.Mystery => "...",
            _ => ""
        };

        private void Toast(string message)
        {
            if (_toastPanel != null) _toastPanel.SetActive(!string.IsNullOrEmpty(message));
            if (_toastLabel != null) _toastLabel.text = message;
            Debug.Log("[Meta] " + message);
        }

        // task-phase-5-gaps.md Phần D — HeroDisplayUtil vẫn là fallback cuối khi service (hiếm khi)
        // chưa đăng ký, không đổi hành vi cũ trong trường hợp đó.
        private string HeroName(string defId) =>
            _loc != null ? _loc.GetName(defId, LocalizedNameKind.Hero) : HeroDisplayUtil.FormatName(defId);

        // ---------- Helper dựng UI ----------

        private static Sprite _circleSprite;

        private static Sprite CircleSprite()
        {
            if (_circleSprite != null) return _circleSprite;

            const int size = 64;
            const float aa = 1.5f;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var center = new Vector2(size / 2f, size / 2f);
            float r = size / 2f - 1f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    float alpha = Mathf.Clamp01((r - dist) / aa + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            _circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return _circleSprite;
        }

        private static Sprite _triangleSprite;

        /// <summary>Tam giác trỏ sang phải (0°), dùng làm mũi tên chỉ hướng trên đường nối map.</summary>
        private static Sprite TriangleSprite()
        {
            if (_triangleSprite != null) return _triangleSprite;

            const int size = 32;
            const float aa = 1.2f;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Vector2 p0 = new(size * 0.92f, size * 0.5f);
            Vector2 p1 = new(size * 0.12f, size * 0.08f);
            Vector2 p2 = new(size * 0.12f, size * 0.92f);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var pt = new Vector2(x + 0.5f, y + 0.5f);
                    float d = SignedDistToTriangle(pt, p0, p1, p2);
                    float alpha = Mathf.Clamp01(0.5f - d / aa);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            _triangleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return _triangleSprite;
        }

        /// <summary>Khoảng cách có dấu tới biên tam giác (âm = trong, dương = ngoài) —
        /// dùng để vẽ cạnh mượt (anti-alias) thay vì cạnh răng cưa cứng.</summary>
        private static float SignedDistToTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            // Xấp xỉ: âm sâu bên trong (xa biên), dương ở ngoài — đủ tốt để anti-alias cạnh,
            // không cần SDF chính xác tuyệt đối cho icon 32px. Đã kiểm bằng script Python
            // trước khi đưa vào: max(d1,d2,d3) <= 0 đúng là vùng trong tam giác.
            float d1 = EdgeDist(p, a, b);
            float d2 = EdgeDist(p, b, c);
            float d3 = EdgeDist(p, c, a);
            return Mathf.Max(d1, Mathf.Max(d2, d3));

            static float EdgeDist(Vector2 pt, Vector2 e0, Vector2 e1)
            {
                Vector2 edge = e1 - e0;
                Vector2 n = new(-edge.y, edge.x);
                n.Normalize();
                return Vector2.Dot(pt - e0, n);
            }
        }

        private static Text Label(Transform parent, string text, int size, TextAnchor align,
                                  Vector2 pos, Vector2 dim)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = dim;

            var t = go.AddComponent<Text>();
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = TEXT;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.raycastTarget = false;
            return t;
        }
    }
}

using System.Collections.Generic;
using Game.Data;
using Game.Data.Dto;

namespace Game.Meta.Dungeon
{
    /// <summary>
    /// Cầu nối Meta ↔ Battle scene — cùng cách GameBootstrap giữ Profile tĩnh
    /// (object-map.md chưa có Addressables/DI cho scene loading ở P2 nên dùng static bridge).
    /// Meta scene ghi <see cref="Pending"/> trước khi LoadScene("Battle");
    /// Battle scene ghi <see cref="LastResult"/> trước khi LoadScene("Meta").
    /// </summary>
    public static class RunContext
    {
        public static RunStateDto Run { get; set; }
        public static PendingBattle Pending { get; private set; }
        public static BattleOutcome LastResult { get; private set; }

        public static void QueueBattle(int nodeId, IReadOnlyList<string> heroDefIds,
                                        IReadOnlyList<string> enemyDefIds, long seed,
                                        IReadOnlyDictionary<ItemType, int> itemLoadout = null,
                                        string formation = null, bool isTutorial = false)
            => Pending = new PendingBattle(nodeId, heroDefIds, enemyDefIds, seed,
                                            itemLoadout: itemLoadout, formation: formation,
                                            isTutorial: isTutorial);

        /// <summary>Trận Dungeon/Trial Boss (task-endgame.md) — KHÔNG gắn với node map (nodeId
        /// luôn -1, không khớp bất kỳ <c>MapNodeDto</c> nào nên nhánh xử lý kết quả theo node map
        /// ở <c>MetaSceneInstaller</c> tự bỏ qua an toàn). <paramref name="specialMode"/> đi kèm
        /// để Battle scene/Meta scene biết đường xử lý kết quả khác (xem
        /// <see cref="BattleOutcome"/>).</summary>
        public static void QueueSpecialBattle(DungeonKind specialMode, int specialFloor,
                                               IReadOnlyList<string> heroDefIds,
                                               IReadOnlyList<string> enemyDefIds, long seed,
                                               IReadOnlyDictionary<ItemType, int> itemLoadout = null,
                                               string formation = null)
            => Pending = new PendingBattle(-1, heroDefIds, enemyDefIds, seed, specialMode, specialFloor,
                                            itemLoadout, formation);

        public static void ClearPending() => Pending = null;

        public static void ReportResult(int nodeId, bool victory, long gold, long exp,
                                         IReadOnlyList<string> heroDefIds = null,
                                         DungeonKind? specialMode = null, int specialFloor = 0,
                                         long totalPlayerDamage = 0,
                                         IReadOnlyDictionary<ItemType, int> itemsUsed = null)
            => LastResult = new BattleOutcome(nodeId, victory, gold, exp, heroDefIds,
                                               specialMode, specialFloor, totalPlayerDamage, itemsUsed);

        public static void ClearResult() => LastResult = null;
    }

    public sealed class PendingBattle
    {
        public readonly int NodeId;
        public readonly IReadOnlyList<string> HeroDefIds;
        public readonly IReadOnlyList<string> EnemyDefIds;
        public readonly long Seed;
        /// <summary>null = trận node map bình thường. Có giá trị = trận Dungeon/Trial Boss
        /// (task-endgame.md) — <see cref="DungeonKind.TrialBoss"/> dùng chung enum, phân biệt
        /// bằng chính giá trị này, không cần field riêng.</summary>
        public readonly DungeonKind? SpecialMode;
        public readonly int SpecialFloor;
        /// <summary>task-consumable-items.md — số vật phẩm MANG vào trận (khoá = ItemType). Null/
        /// rỗng = trận không mang item nào (mọi trận cũ không đổi hành vi).</summary>
        public readonly IReadOnlyDictionary<ItemType, int> ItemLoadout;
        /// <summary>task-formation-synergy.md — preset đội hình chọn từ TeamSelectScreen.
        /// null/rỗng = mặc định "formation_balanced" (mọi trận cũ không đổi hành vi).</summary>
        public readonly string Formation;
        /// <summary>task-phase-5-gaps.md Phần B — true = trận battle-node ĐẦU TIÊN của 1 profile
        /// mới (<c>!ProgressDto.TutorialCompleted</c>), <c>BattleSceneInstaller</c> dựng
        /// <c>TutorialController</c> dạy 5 bước. Mặc định false = mọi trận khác không đổi hành vi
        /// (kể cả Dungeon/Trial Boss qua <see cref="RunContext.QueueSpecialBattle"/>, luôn false).</summary>
        public readonly bool IsTutorial;

        public PendingBattle(int nodeId, IReadOnlyList<string> heroDefIds,
                              IReadOnlyList<string> enemyDefIds, long seed,
                              DungeonKind? specialMode = null, int specialFloor = 0,
                              IReadOnlyDictionary<ItemType, int> itemLoadout = null,
                              string formation = null, bool isTutorial = false)
        {
            NodeId = nodeId; HeroDefIds = heroDefIds; EnemyDefIds = enemyDefIds; Seed = seed;
            SpecialMode = specialMode; SpecialFloor = specialFloor; ItemLoadout = itemLoadout;
            Formation = string.IsNullOrEmpty(formation) ? Meta.Battle.FormationSystem.Default : formation;
            IsTutorial = isTutorial;
        }
    }

    public sealed class BattleOutcome
    {
        public readonly int NodeId;
        public readonly bool Victory;
        public readonly long Gold;
        public readonly long Exp;
        /// <summary>Đội hình ra trận — dùng để phát thưởng theo-hero (VD mảnh Ascend khi hạ Boss).
        /// Có thể null nếu battle chạy độc lập không qua RunContext.Pending.</summary>
        public readonly IReadOnlyList<string> HeroDefIds;
        public readonly DungeonKind? SpecialMode;
        public readonly int SpecialFloor;
        /// <summary>Tổng damage phe Player gây ra trong trận — CHỈ có ý nghĩa khi
        /// <c>SpecialMode == DungeonKind.TrialBoss</c> (Damage Meter, plan.md §8.3), luôn 0 cho
        /// mọi trận khác.</summary>
        public readonly long TotalPlayerDamage;
        /// <summary>task-consumable-items.md — số vật phẩm ĐÃ DÙNG THẬT trong trận (khác đã
        /// mang) — Meta layer trừ vĩnh viễn khỏi <c>profile.Inventory.Items</c> theo đúng số này,
        /// không phải số mang vào.</summary>
        public readonly IReadOnlyDictionary<ItemType, int> ItemsUsed;

        public BattleOutcome(int nodeId, bool victory, long gold, long exp,
                             IReadOnlyList<string> heroDefIds = null,
                             DungeonKind? specialMode = null, int specialFloor = 0,
                             long totalPlayerDamage = 0,
                             IReadOnlyDictionary<ItemType, int> itemsUsed = null)
        {
            NodeId = nodeId; Victory = victory; Gold = gold; Exp = exp; HeroDefIds = heroDefIds;
            SpecialMode = specialMode; SpecialFloor = specialFloor; TotalPlayerDamage = totalPlayerDamage;
            ItemsUsed = itemsUsed;
        }
    }
}

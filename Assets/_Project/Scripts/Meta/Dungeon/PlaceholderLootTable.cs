using Game.Core.Random;

namespace Game.Meta.Dungeon
{
    /// <summary>
    /// Nguồn thu thập vật liệu Ascend TỐI THIỂU (task-ascend.md §2/§7 mục D.1) — KHÔNG phải
    /// LootTableDefinition/gacha thật, chỉ là chỗ cắm tạm cho tới khi có hệ loot đầy đủ
    /// (plan.md §7.2/§8.1). Tách khỏi <c>MetaSceneInstaller</c> và dùng <see cref="IRandomSource"/>
    /// (thay vì UnityEngine.Random trực tiếp) để Balance Harness gọi lại được với seed cố định.
    /// </summary>
    public static class PlaceholderLootTable
    {
        /// <summary>Thắng Boss: cấp cho cả đội (không nhân theo số hero).</summary>
        public const int BOSS_REWARD_ESSENCE_II = 3;

        /// <summary>
        /// task-ascend.md §7 mục D (Balance Harness) phát hiện: Essence III/Core KHÔNG rơi ở đâu
        /// cả trong bản gốc (Treasure chỉ ra Essence I, Boss chỉ ra Essence II) → ★4→★5/★5→★6
        /// (chính bậc mở Awakening) KHÔNG THỂ đạt qua chơi thường, chỉ có đường Shop nhưng Shop
        /// cần Gem mà Gem chưa có faucet nào trong game. Vá tối thiểu: thêm Core + Essence III cố
        /// định vào thưởng Boss. Số liệu: 5 Core/15 Core cần cho ★4→5/★5→6 → 5/15 lượt Boss; 40
        /// Essence III cần cho ★5→6 → 40 lượt Boss (endgame chậm nhưng HỮU HẠN, chấp nhận được
        /// cho bậc tối thượng). Vẫn là placeholder — chờ LootTableDefinition thật.
        /// </summary>
        public const int BOSS_REWARD_CORE = 1;
        public const int BOSS_REWARD_ESSENCE_III = 1;

        // BOSS_REWARD_GEM (100/Boss, task-ascend.md §9) đã bị XOÁ — thay bằng QuestSystem
        // (task-quest.md): Daily quest + Achievement thật, không còn tuyến tính theo số Boss nữa.

        public readonly struct TreasureRoll
        {
            public readonly long Gold;
            /// <summary>&gt; 0 nếu trúng nhánh Essence I.</summary>
            public readonly int EssenceIAmount;
            /// <summary>Index trong danh sách hero đang sở hữu nếu trúng nhánh mảnh, -1 nếu không.</summary>
            public readonly int ShardHeroIndex;

            public TreasureRoll(long gold, int essenceIAmount, int shardHeroIndex)
            {
                Gold = gold; EssenceIAmount = essenceIAmount; ShardHeroIndex = shardHeroIndex;
            }
        }

        /// <summary>80-160 gold luôn có; nếu có hero sở hữu thì roll thêm 50/50 giữa 2-4 Essence I
        /// hoặc 1 mảnh cho hero ngẫu nhiên trong số đang sở hữu.</summary>
        public static TreasureRoll RollTreasure(IRandomSource rng, int ownedHeroCount)
        {
            long gold = rng.NextInt(80, 161);

            if (ownedHeroCount <= 0) return new TreasureRoll(gold, 0, -1);

            if (rng.Chance(0.5f))
                return new TreasureRoll(gold, rng.NextInt(2, 5), -1);

            return new TreasureRoll(gold, 0, rng.NextInt(0, ownedHeroCount));
        }
    }
}

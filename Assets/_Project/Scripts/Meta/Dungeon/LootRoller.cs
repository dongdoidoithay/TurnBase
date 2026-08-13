using System.Collections.Generic;
using System.Linq;
using Game.Core.Random;
using Game.Data;
using Game.Data.Dto;
using Game.Meta.Content;
using Game.Meta.Equipment;
using UnityEngine;

namespace Game.Meta.Dungeon
{
    /// <summary>
    /// Roll loot từ <see cref="LootTableDefinitionSO"/> — task-loottable.md, thay
    /// <c>PlaceholderLootTable</c>. Dùng <see cref="IRandomSource"/> (không phải
    /// UnityEngine.Random) để test/harness gọi lại được với seed cố định, đúng kỷ luật đã dùng
    /// cho GachaSystem/PlaceholderLootTable.
    /// </summary>
    public static class LootRoller
    {
        public readonly struct MaterialGrant
        {
            public readonly CurrencyType Type;
            public readonly int Amount;
            public MaterialGrant(CurrencyType type, int amount) { Type = type; Amount = amount; }
        }

        public readonly struct LootRollResult
        {
            public readonly long Gold;
            public readonly IReadOnlyList<MaterialGrant> Materials;
            /// <summary>Index hero ngẫu nhiên trong danh sách sở hữu nếu trúng mảnh, -1 nếu không.</summary>
            public readonly int ShardHeroIndex;
            /// <summary>Trang bị ngẫu nhiên nếu trúng (task-equipment.md), null nếu không.</summary>
            public readonly EquipmentInstanceDto Equipment;

            public LootRollResult(long gold, IReadOnlyList<MaterialGrant> materials, int shardHeroIndex,
                EquipmentInstanceDto equipment = null)
            {
                Gold = gold; Materials = materials; ShardHeroIndex = shardHeroIndex; Equipment = equipment;
            }
        }

        private static List<LootTableDefinitionSO> _catalog;

        private static List<LootTableDefinitionSO> Catalog
            => _catalog ??= Resources.LoadAll<LootTableDefinitionSO>("Data/LootTables").ToList();

        /// <summary>Chỉ dùng trong test — buộc nạp lại catalog sau khi Resources thay đổi.</summary>
        public static void ClearCache() => _catalog = null;

        /// <summary>Ưu tiên bảng khớp đúng Chapter, fallback Chapter=0 (wildcard). Trả null nếu
        /// chưa author asset nào cho NodeType đó — caller phải tự fallback (không throw).</summary>
        public static LootTableDefinitionSO Resolve(int chapter, NodeType nodeType)
        {
            LootTableDefinitionSO wildcard = null;
            foreach (var table in Catalog)
            {
                if (table.NodeType != nodeType) continue;
                if (table.Chapter == chapter) return table;
                if (table.Chapter == 0) wildcard = table;
            }
            return wildcard;
        }

        /// <summary>Mỗi MaterialDrop roll ĐỘC LẬP theo Chance riêng — có thể trúng nhiều dòng
        /// cùng lúc (khác PlaceholderLootTable cũ chỉ có 1 nhánh loại trừ).</summary>
        public static LootRollResult Roll(LootTableDefinitionSO table, IRandomSource rng, int ownedHeroCount)
        {
            long gold = rng.NextInt(table.GoldMin, table.GoldMax + 1);

            var materials = new List<MaterialGrant>();
            foreach (var drop in table.Materials)
            {
                if (!rng.Chance(drop.Chance)) continue;
                int amount = drop.MinAmount >= drop.MaxAmount
                    ? drop.MinAmount
                    : rng.NextInt(drop.MinAmount, drop.MaxAmount + 1);
                materials.Add(new MaterialGrant(drop.Type, amount));
            }

            int shardHeroIndex = -1;
            if (ownedHeroCount > 0 && rng.Chance(table.HeroShardChance))
                shardHeroIndex = rng.NextInt(ownedHeroCount);

            EquipmentInstanceDto equipment = null;
            if (rng.Chance(table.EquipmentChance))
            {
                var rarity = RollEquipmentRarity(table.EquipmentMinRarity, rng);
                EquipSlot? slot = table.EquipmentAnySlot ? null : table.EquipmentSlot;
                equipment = EquipmentGenerator.Roll(slot, rarity, rng);
            }

            return new LootRollResult(gold, materials, shardHeroIndex, equipment);
        }

        /// <summary>Rarity của trang bị rơi ra, từ <paramref name="minRarity"/> trở lên — plan.md
        /// không cho công thức cụ thể (chỉ nói "đảm bảo ≥ Rare"), nên đây là phân phối TỰ THIẾT
        /// KẾ (giống cách LootTableDefinitionSO tự thiết kế schema — task-loottable.md §0):
        /// mỗi bậc cao hơn có trọng số bằng 40% bậc liền trước. Placeholder chờ Balance Harness
        /// tinh chỉnh nếu cần.</summary>
        private static Rarity RollEquipmentRarity(Rarity minRarity, IRandomSource rng)
        {
            int minIdx = (int)minRarity;
            int tierCount = (int)Rarity.Mythic - minIdx + 1;

            float totalWeight = 0f;
            float w = 1f;
            for (int i = 0; i < tierCount; i++) { totalWeight += w; w *= 0.4f; }

            float roll = rng.NextFloat() * totalWeight;
            float cumulative = 0f;
            w = 1f;
            for (int i = 0; i < tierCount; i++)
            {
                cumulative += w;
                if (roll < cumulative) return (Rarity)(minIdx + i);
                w *= 0.4f;
            }
            return Rarity.Mythic;
        }
    }
}

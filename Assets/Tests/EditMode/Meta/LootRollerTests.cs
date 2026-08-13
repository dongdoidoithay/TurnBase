using Game.Core.Random;
using Game.Data;
using Game.Data.Dto;
using Game.Meta.Content;
using Game.Meta.Dungeon;
using Game.Meta.Hero;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Meta
{
    /// <summary>LootRoller/LootTableDefinitionSO — task-loottable.md, thay PlaceholderLootTable.
    /// Tên class theo đúng object-map.md §8 (T-META-LOOT).</summary>
    public class LootRollerTests
    {
        private static LootTableDefinitionSO MakeTable(int chapter, NodeType nodeType,
            int goldMin = 0, int goldMax = 0, LootTableDefinitionSO.MaterialDrop[] materials = null,
            float shardChance = 0f, float equipmentChance = 0f, Rarity equipmentMinRarity = Rarity.Rare)
        {
            var t = ScriptableObject.CreateInstance<LootTableDefinitionSO>();
            t.Chapter = chapter;
            t.NodeType = nodeType;
            t.GoldMin = goldMin;
            t.GoldMax = goldMax;
            t.Materials = materials ?? System.Array.Empty<LootTableDefinitionSO.MaterialDrop>();
            t.HeroShardChance = shardChance;
            t.HeroShardMin = 1;
            t.HeroShardMax = 1;
            t.EquipmentChance = equipmentChance;
            t.EquipmentMinRarity = equipmentMinRarity;
            t.EquipmentAnySlot = true;
            return t;
        }

        [Test]
        public void Roll_Gold_AlwaysInRange()
        {
            var table = MakeTable(0, NodeType.Treasure, goldMin: 80, goldMax: 160);
            var rng = new XorShiftRandom(1UL);

            for (int i = 0; i < 500; i++)
            {
                var result = LootRoller.Roll(table, rng, ownedHeroCount: 0);
                Assert.GreaterOrEqual(result.Gold, 80);
                Assert.LessOrEqual(result.Gold, 160);
            }
        }

        [Test]
        public void Roll_MaterialDrop_AppliesWhenChanceIsOne_NeverWhenZero()
        {
            var materials = new[]
            {
                new LootTableDefinitionSO.MaterialDrop { Type = CurrencyType.EssenceI, MinAmount = 5, MaxAmount = 5, Chance = 1f },
                new LootTableDefinitionSO.MaterialDrop { Type = CurrencyType.Core, MinAmount = 1, MaxAmount = 1, Chance = 0f },
            };
            var table = MakeTable(0, NodeType.Treasure, materials: materials);
            var rng = new XorShiftRandom(2UL);

            var result = LootRoller.Roll(table, rng, ownedHeroCount: 0);

            Assert.AreEqual(1, result.Materials.Count, "Chance=1 phải luôn trúng, Chance=0 không bao giờ trúng");
            Assert.AreEqual(CurrencyType.EssenceI, result.Materials[0].Type);
            Assert.AreEqual(5, result.Materials[0].Amount);
        }

        [Test]
        public void Roll_MultipleMaterialDrops_AreIndependent_CanBothHit()
        {
            var materials = new[]
            {
                new LootTableDefinitionSO.MaterialDrop { Type = CurrencyType.EssenceI, MinAmount = 1, MaxAmount = 1, Chance = 1f },
                new LootTableDefinitionSO.MaterialDrop { Type = CurrencyType.EssenceII, MinAmount = 1, MaxAmount = 1, Chance = 1f },
            };
            var table = MakeTable(0, NodeType.Boss, materials: materials);
            var rng = new XorShiftRandom(3UL);

            var result = LootRoller.Roll(table, rng, ownedHeroCount: 0);

            Assert.AreEqual(2, result.Materials.Count,
                "Khác PlaceholderLootTable cũ (1 nhánh loại trừ) — mỗi dòng roll độc lập, cả 2 cùng trúng được nếu Chance=1 cả 2");
        }

        [Test]
        public void Roll_HeroShard_ChanceGatesIt_IndexWithinOwnedRange()
        {
            var table = MakeTable(0, NodeType.Treasure, shardChance: 1f);
            var rng = new XorShiftRandom(4UL);

            for (int i = 0; i < 100; i++)
            {
                var result = LootRoller.Roll(table, rng, ownedHeroCount: 6);
                Assert.GreaterOrEqual(result.ShardHeroIndex, 0);
                Assert.Less(result.ShardHeroIndex, 6);
            }
        }

        [Test]
        public void Roll_HeroShard_NeverGrantedWhenNoHeroesOwned()
        {
            var table = MakeTable(0, NodeType.Treasure, shardChance: 1f);
            var rng = new XorShiftRandom(5UL);

            var result = LootRoller.Roll(table, rng, ownedHeroCount: 0);

            Assert.AreEqual(-1, result.ShardHeroIndex);
        }

        [Test]
        public void Roll_Equipment_ChanceOne_AlwaysGranted_ChanceZero_Never()
        {
            var granted = MakeTable(0, NodeType.Treasure, equipmentChance: 1f);
            var never = MakeTable(0, NodeType.Treasure, equipmentChance: 0f);
            var rng = new XorShiftRandom(6UL);

            for (int i = 0; i < 50; i++)
            {
                Assert.IsNotNull(LootRoller.Roll(granted, rng, ownedHeroCount: 0).Equipment,
                    "EquipmentChance=1 phải luôn rơi trang bị (catalog thật có sẵn 14 def)");
                Assert.IsNull(LootRoller.Roll(never, rng, ownedHeroCount: 0).Equipment,
                    "EquipmentChance=0 không bao giờ rơi trang bị");
            }
        }

        [Test]
        public void Roll_Equipment_RarityAlwaysAtOrAboveMinRarity()
        {
            var table = MakeTable(0, NodeType.Treasure, equipmentChance: 1f, equipmentMinRarity: Rarity.Epic);
            var rng = new XorShiftRandom(7UL);

            for (int i = 0; i < 200; i++)
            {
                var equipment = LootRoller.Roll(table, rng, ownedHeroCount: 0).Equipment;
                Assert.IsNotNull(equipment);
                Assert.GreaterOrEqual(equipment.Rarity, (int)Rarity.Epic,
                    "Rarity rơi ra không bao giờ được thấp hơn EquipmentMinRarity đã cấu hình");
            }
        }

        [Test]
        public void RealTreasureAsset_GuaranteesEquipmentAtOrAboveRare()
        {
            LootRoller.ClearCache();
            var table = LootRoller.Resolve(chapter: 1, NodeType.Treasure);
            Assert.IsNotNull(table);
            Assert.AreEqual(1f, table.EquipmentChance, 0.0001f,
                "plan.md §8.1: Treasure phải ĐẢM BẢO ≥1 trang bị — EquipmentChance phải = 1 trên asset thật");
            Assert.GreaterOrEqual((int)table.EquipmentMinRarity, (int)Rarity.Rare,
                "plan.md §8.1: trang bị Treasure phải ≥ Rare");
        }

        [Test]
        public void Resolve_PrefersExactChapterMatch_OverWildcard()
        {
            LootRoller.ClearCache();
            // task-loottable-chapters.md — trước đây chỉ có bảng wildcard (Chapter=0) nên test
            // này (dù đặt tên đúng ý định) thực ra chỉ verify được nhánh fallback. Nay đã có
            // loottable_treasure_ch{1..5}.asset thật — verify đúng ý nghĩa tên test: chương cụ
            // thể phải THẮNG wildcard, không rơi về Chapter=0 nữa.
            var table = LootRoller.Resolve(chapter: 1, NodeType.Treasure);
            Assert.IsNotNull(table);
            Assert.AreEqual(1, table.Chapter, "Đã có loottable_treasure_ch1.asset — phải ưu tiên bảng này, không rơi về wildcard");
            Assert.AreEqual("loottable_treasure_ch1", table.DefId);
        }

        [Test]
        public void Resolve_AllFiveChapters_ReturnDedicatedTable_ForTreasureAndBoss()
        {
            LootRoller.ClearCache();
            for (int chapter = 1; chapter <= 5; chapter++)
            {
                var treasure = LootRoller.Resolve(chapter, NodeType.Treasure);
                Assert.IsNotNull(treasure, $"Thiếu loottable_treasure_ch{chapter}.asset");
                Assert.AreEqual(chapter, treasure.Chapter);

                var boss = LootRoller.Resolve(chapter, NodeType.Boss);
                Assert.IsNotNull(boss, $"Thiếu loottable_boss_ch{chapter}.asset");
                Assert.AreEqual(chapter, boss.Chapter);
            }
        }

        [Test]
        public void Resolve_ReturnsNull_WhenNoTableMatchesNodeType()
        {
            LootRoller.ClearCache();
            var table = LootRoller.Resolve(chapter: 1, NodeType.Event);
            Assert.IsNull(table, "Chưa author bảng nào cho Event — phải trả null để caller tự fallback, không throw");
        }

        /// <summary>Regression quan trọng — copy tinh thần
        /// BossReward_CoversEveryMaterialType_AscendSystemEverRequires cũ (task-ascend.md §8).
        /// task-loottable-chapters.md **đổi ý nghĩa test này**: trước đây chỉ có 1 bảng Boss
        /// wildcard nên bảng đó MỘT MÌNH phải cấp đủ mọi vật liệu. Nay vật liệu CỐ Ý chia theo
        /// chương (chương 1 chỉ có EssenceI, Core chỉ xuất hiện từ chương 3...) — đúng tinh thần
        /// "tiến trình" (đi qua chương mới mở khoá vật liệu bậc cao), 1 chương riêng lẻ không cần
        /// tự cấp đủ mọi thứ nữa. Verify đúng theo thiết kế mới: HỘI của cả 5 bảng Boss (chương
        /// 1-5) cộng lại phải phủ đủ mọi loại vật liệu AscendSystem từng yêu cầu.</summary>
        [Test]
        public void RealBossAssets_UnionAcrossAllChapters_CoversEveryMaterialType_AscendSystemEverRequires()
        {
            LootRoller.ClearCache();
            var coveredTypes = new System.Collections.Generic.HashSet<CurrencyType>();
            for (int chapter = 1; chapter <= 5; chapter++)
            {
                var bossTable = LootRoller.Resolve(chapter, NodeType.Boss);
                Assert.IsNotNull(bossTable, $"Chưa author loottable_boss_ch{chapter}.asset");
                foreach (var drop in bossTable.Materials)
                    if (drop.Chance > 0f) coveredTypes.Add(drop.Type);
            }

            var hero = new HeroInstanceDto { DefId = "probe", Star = 1 };
            while (hero.Star < AscendSystem.MAX_STAR)
            {
                var cost = AscendSystem.CostForNextStar(hero);
                Assert.IsNotNull(cost);
                foreach (var m in cost.Value.Materials)
                {
                    if (m.Type == CurrencyType.EssenceI) continue; // Essence I đến từ Treasure, không phải Boss
                    Assert.IsTrue(coveredTypes.Contains(m.Type),
                        $"★{hero.Star}→★{hero.Star + 1} cần {m.Type} nhưng KHÔNG bảng Boss chương nào (1-5) cấp — bậc này vĩnh viễn không đạt được dù đã đi hết 5 chương");
                }
                hero.Star++;
            }
        }
    }
}

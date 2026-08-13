using Game.Core.Random;
using Game.Data;
using Game.Data.Dto;
using Game.Meta.Dungeon;
using Game.Meta.Hero;
using NUnit.Framework;

namespace Game.Tests.Meta
{
    /// <summary>Regression cho refactor task-ascend.md §7 mục D.1 (tách RNG ra để test/harness
    /// được, hành vi RollTreasure giữ nguyên) + fix mục D (Balance Harness phát hiện Core/Essence
    /// III không rơi ở đâu cả → ★4+ không đạt được qua chơi thường, đã vá bằng thưởng Boss).</summary>
    public class PlaceholderLootTableTests
    {
        [Test]
        public void RollTreasure_NoOwnedHeroes_OnlyGold_NoMaterial()
        {
            var rng = new XorShiftRandom(1UL);
            var roll = PlaceholderLootTable.RollTreasure(rng, ownedHeroCount: 0);

            Assert.GreaterOrEqual(roll.Gold, 80);
            Assert.Less(roll.Gold, 161);
            Assert.AreEqual(0, roll.EssenceIAmount);
            Assert.AreEqual(-1, roll.ShardHeroIndex);
        }

        [Test]
        public void RollTreasure_WithOwnedHeroes_AlwaysExactlyOneBranch()
        {
            var rng = new XorShiftRandom(7UL);
            for (int i = 0; i < 200; i++)
            {
                var roll = PlaceholderLootTable.RollTreasure(rng, ownedHeroCount: 6);
                bool gotEssence = roll.EssenceIAmount > 0;
                bool gotShard = roll.ShardHeroIndex >= 0;
                Assert.AreNotEqual(gotEssence, gotShard, "Đúng 1 trong 2 nhánh trúng, không cả 2 không cả 0");
                if (gotEssence)
                {
                    Assert.GreaterOrEqual(roll.EssenceIAmount, 2);
                    Assert.Less(roll.EssenceIAmount, 5);
                }
                else
                {
                    Assert.GreaterOrEqual(roll.ShardHeroIndex, 0);
                    Assert.Less(roll.ShardHeroIndex, 6);
                }
            }
        }

        [Test]
        public void RollTreasure_Branches_ApproximatelyFiftyFifty_Over10000Rolls()
        {
            var rng = new XorShiftRandom(42UL);
            int essence = 0, shard = 0;
            for (int i = 0; i < 10_000; i++)
            {
                var roll = PlaceholderLootTable.RollTreasure(rng, ownedHeroCount: 6);
                if (roll.EssenceIAmount > 0) essence++; else shard++;
            }
            Assert.That(essence / 10000f, Is.EqualTo(0.5f).Within(0.03f));
            Assert.That(shard / 10000f, Is.EqualTo(0.5f).Within(0.03f));
        }

        /// <summary>Regression cho phát hiện của Balance Harness (task-ascend.md §7 mục D): trước
        /// bản vá này, Essence III/Core = 0 ở mọi nguồn placeholder → ★4→★5/★5→★6 không đạt được
        /// dù cày bao nhiêu Boss. Mỗi loại vật liệu AscendSystem yêu cầu phải có ÍT NHẤT 1 nguồn
        /// cấp > 0 (Treasure hoặc Boss), nếu không bậc đó vĩnh viễn không đạt được.</summary>
        [Test]
        public void BossReward_CoversEveryMaterialType_AscendSystemEverRequires()
        {
            var hero = new HeroInstanceDto { DefId = "probe", Star = 1 };
            while (hero.Star < AscendSystem.MAX_STAR)
            {
                var cost = AscendSystem.CostForNextStar(hero);
                Assert.IsNotNull(cost);
                foreach (var m in cost.Value.Materials)
                {
                    long supply = m.Type switch
                    {
                        CurrencyType.EssenceI => 4, // Treasure roll trung bình (2-4)/lần trúng nhánh essence
                        CurrencyType.EssenceII => PlaceholderLootTable.BOSS_REWARD_ESSENCE_II,
                        CurrencyType.EssenceIII => PlaceholderLootTable.BOSS_REWARD_ESSENCE_III,
                        CurrencyType.Core => PlaceholderLootTable.BOSS_REWARD_CORE,
                        _ => 0
                    };
                    Assert.Greater(supply, 0,
                        $"★{hero.Star}→★{hero.Star + 1} cần {m.Type} nhưng không nguồn placeholder nào cấp > 0 — bậc này vĩnh viễn không đạt được");
                }
                hero.Star++;
            }
        }

        // BossReward_GrantsGem_SoShopAndGachaStayUsable đã XOÁ — Gem không còn đến từ Boss
        // (task-quest.md thay bằng QuestSystem). Regression tương đương giờ nằm ở
        // QuestSystemTests.AtLeastOneQuestOrAchievement_GrantsGem_SoShopAndGachaStayUsable.
    }
}

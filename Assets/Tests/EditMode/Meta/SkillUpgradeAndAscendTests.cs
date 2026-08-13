using Game.Core;
using Game.Data;
using Game.Data.Dto;
using Game.Meta.Hero;
using Game.Services.Economy;
using NUnit.Framework;

namespace Game.Tests.Meta
{
    public class SkillUpgradeAndAscendTests
    {
        private IEconomyService _economy;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            _economy = new EconomyService();
            ServiceLocator.Register(_economy);
        }

        [TearDown]
        public void TearDown() => ServiceLocator.Clear();

        private static PlayerProfileDto Profile(long gold)
        {
            var p = new PlayerProfileDto();
            p.Wallet.Gold = gold;
            return p;
        }

        private static HeroInstanceDto Hero(int star = 1)
            => new() { Uid = "u", DefId = "hero_test", Level = 1, Star = star };

        /// <summary>Cấp đủ Gold + Mảnh + mọi Vật liệu cho MỌI bậc Ascend — dùng cho test chỉ
        /// quan tâm hệ quả của việc Ascend thành công, không quan tâm chi phí chính xác.</summary>
        private PlayerProfileDto RichProfile(HeroInstanceDto hero)
        {
            var p = Profile(gold: 10_000_000);
            _economy.GrantShards(p.Wallet, hero.DefId, 10_000);
            foreach (CurrencyType t in new[] { CurrencyType.EssenceI, CurrencyType.EssenceII, CurrencyType.EssenceIII, CurrencyType.Core })
                _economy.Grant(p.Wallet, t, 10_000);
            return p;
        }

        // ---------- SkillUpgradeSystem ----------

        [Test]
        public void SkillUpgrade_NotEnoughGold_FailsAndDoesNotChangeLevel()
        {
            var profile = Profile(gold: 0);
            var hero = Hero();

            bool ok = SkillUpgradeSystem.TryUpgrade(profile, hero, 0);

            Assert.IsFalse(ok);
            Assert.AreEqual(1, hero.SkillLevels[0]);
            Assert.AreEqual(0, profile.Wallet.Gold);
        }

        [Test]
        public void SkillUpgrade_EnoughGold_LevelsUpAndDeductsCost()
        {
            long cost = SkillUpgradeSystem.UpgradeCost(1);
            var profile = Profile(gold: cost);
            var hero = Hero();

            bool ok = SkillUpgradeSystem.TryUpgrade(profile, hero, 0);

            Assert.IsTrue(ok);
            Assert.AreEqual(2, hero.SkillLevels[0]);
            Assert.AreEqual(0, profile.Wallet.Gold);
        }

        [Test]
        public void SkillUpgrade_AtMaxLevel_CannotUpgradeFurther()
        {
            var profile = Profile(gold: 1_000_000);
            var hero = Hero();
            hero.SkillLevels[0] = SkillUpgradeSystem.MAX_SKILL_LEVEL;

            Assert.IsFalse(SkillUpgradeSystem.CanUpgrade(hero, 0));
            Assert.IsFalse(SkillUpgradeSystem.TryUpgrade(profile, hero, 0));
        }

        [Test]
        public void SkillUpgrade_CostIncreasesWithLevel()
        {
            long costAt1 = SkillUpgradeSystem.UpgradeCost(1);
            long costAt7 = SkillUpgradeSystem.UpgradeCost(7);
            Assert.Greater(costAt7, costAt1);
        }

        [Test]
        public void SkillUpgrade_InvalidSlot_ReturnsFalse()
        {
            var profile = Profile(gold: 1_000_000);
            var hero = Hero();

            Assert.IsFalse(SkillUpgradeSystem.TryUpgrade(profile, hero, -1));
            Assert.IsFalse(SkillUpgradeSystem.TryUpgrade(profile, hero, 99));
        }

        [Test]
        public void SkillUpgrade_LockedSlot_CannotUpgradeEvenWithGold()
        {
            var hero = Hero(star: 1); // ★1 -> slot 3 (skill C) chưa mở khoá
            var profile = Profile(gold: 1_000_000);

            Assert.IsFalse(SkillUpgradeSystem.CanUpgrade(hero, 3));
            Assert.IsFalse(SkillUpgradeSystem.TryUpgrade(profile, hero, 3));
        }

        // ---------- AscendSystem ----------

        [Test]
        public void Ascend_NoGoldNoShardsNoMaterials_Fails()
        {
            var profile = Profile(gold: 0);
            var hero = Hero(star: 1);

            Assert.IsFalse(AscendSystem.TryAscend(profile, hero));
            Assert.AreEqual(1, hero.Star);
        }

        [Test]
        public void Ascend_EnoughOfEverything_IncreasesStarAndDeductsAll()
        {
            var hero = Hero(star: 1);
            var cost = AscendSystem.CostForNextStar(hero)!.Value;
            var profile = Profile(gold: cost.Gold);
            _economy.GrantShards(profile.Wallet, hero.DefId, cost.Shards);
            foreach (var m in cost.Materials) _economy.Grant(profile.Wallet, m.Type, m.Amount);

            bool ok = AscendSystem.TryAscend(profile, hero);

            Assert.IsTrue(ok);
            Assert.AreEqual(2, hero.Star);
            Assert.AreEqual(0, profile.Wallet.Gold);
            Assert.AreEqual(0, _economy.GetShards(profile.Wallet, hero.DefId));
            foreach (var m in cost.Materials)
                Assert.AreEqual(0, _economy.Get(profile.Wallet, m.Type));
        }

        [Test]
        public void Ascend_EnoughGoldAndShards_ButMissingMaterial_FailsAtomically()
        {
            var hero = Hero(star: 1);
            var cost = AscendSystem.CostForNextStar(hero)!.Value;
            var profile = Profile(gold: cost.Gold);
            _economy.GrantShards(profile.Wallet, hero.DefId, cost.Shards);
            // KHÔNG cấp Vật liệu (EssenceI) — phải fail toàn bộ, không được trừ Gold/Mảnh dở dang.

            bool ok = AscendSystem.TryAscend(profile, hero);

            Assert.IsFalse(ok, "Thiếu 1 trong 3 loại chi phí thì phải fail toàn bộ giao dịch");
            Assert.AreEqual(1, hero.Star);
            Assert.AreEqual(cost.Gold, profile.Wallet.Gold, "Gold không được trừ khi giao dịch fail");
            Assert.AreEqual(cost.Shards, _economy.GetShards(profile.Wallet, hero.DefId),
                            "Mảnh không được trừ khi giao dịch fail");
        }

        [Test]
        public void Ascend_AtMaxStar_CannotAscendFurther()
        {
            var hero = Hero(star: AscendSystem.MAX_STAR);
            var profile = RichProfile(hero);

            Assert.IsFalse(AscendSystem.CanAscend(hero));
            Assert.IsFalse(AscendSystem.TryAscend(profile, hero));
            Assert.IsNull(AscendSystem.CostForNextStar(hero));
        }

        [Test]
        public void Ascend_RaisesHeroLevelSystemCap_AutomaticallyViaStar()
        {
            var hero = Hero(star: 1);
            int capBefore = HeroLevelSystem.LevelCap(hero.Star);
            var profile = RichProfile(hero);

            for (int i = 0; i < 3; i++) // ★1 -> ★4
                Assert.IsTrue(AscendSystem.TryAscend(profile, hero), $"Ascend lần {i + 1} phải thành công");

            int capAfter = HeroLevelSystem.LevelCap(hero.Star);
            Assert.Greater(capAfter, capBefore, "Ascend phải tự nâng trần cấp qua HeroLevelSystem.LevelCap(star)");
        }

        [Test]
        public void Ascend_CostRisesEachTier()
        {
            long cost1 = AscendSystem.CostForNextStar(Hero(star: 1))!.Value.Gold;
            long cost4 = AscendSystem.CostForNextStar(Hero(star: 4))!.Value.Gold;
            Assert.Greater(cost4, cost1);
        }

        [Test]
        public void Ascend_ReachingMaxStar_SetsAwakenedFlag()
        {
            var hero = Hero(star: AscendSystem.MAX_STAR - 1);
            var profile = RichProfile(hero);

            Assert.IsFalse(hero.Awakened);
            AscendSystem.TryAscend(profile, hero);

            Assert.AreEqual(AscendSystem.MAX_STAR, hero.Star);
            Assert.IsTrue(hero.Awakened, "Đạt ★MAX phải bật cờ Awakened (chưa áp hiệu ứng passive thật)");
        }

        // ---------- Skill slot unlock theo sao ----------

        [TestCase(1, 0, true)] [TestCase(1, 1, true)] [TestCase(1, 2, true)]
        [TestCase(1, 3, false)] [TestCase(1, 4, false)]
        [TestCase(3, 3, false)] [TestCase(4, 3, true)] [TestCase(4, 4, false)]
        [TestCase(5, 4, true)] [TestCase(6, 4, true)]
        public void IsSkillSlotUnlocked_MatchesStarThreshold(int star, int slot, bool expected)
        {
            Assert.AreEqual(expected, AscendSystem.IsSkillSlotUnlocked(star, slot));
        }
    }
}
